using System.Threading.Channels;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

using PitWall.Domain;
using PitWall.Jev;
using PitWall.Jev.Decisions;
using PitWall.Jev.Requests;
using PitWall.Jev.Serialization;
using PitWall.Jev.Triage;
using PitWall.Jev.Triggers;
using PitWall.Replay;
using PitWall.Strategy.MonteCarlo;
using PitWall.Web.Hubs;

namespace PitWall.Web.Services;

/// <summary>
/// Decide cuándo consultar a Jev (docs/architecture.md §3, docs/jev.md §3 y §7): triage ante mensajes nuevos de
/// Race Control o eventos inyectados y estrategia según <c>Jev:Mode</c>, el botón "Consultar Jev" o un triage
/// <c>ACT_NOW</c>. Cada respuesta se mapea a <see cref="JevDecision"/>, se guarda en <c>JevDecisionLog</c> y se
/// publica por <see cref="PitWallHub"/>. Lee la configuración en cada pedido, así que los cambios en caliente
/// (<c>Jev:Enabled</c>, modo, vueltas, pilotos) aplican al siguiente.
/// </summary>
public sealed partial class JevWorker(
    ReplayEngine engine,
    PitWallSession session,
    StrategyStore strategies,
    JevTriggers triggers,
    JevDecisionStore decisions,
    IJevClient client,
    IJevDecisionLog decisionLog,
    IHubContext<PitWallHub, IPitWallClient> hub,
    IOptionsMonitor<JevOptions> options,
    IOptions<MonteCarloOptions> monteCarlo,
    TimeProvider timeProvider,
    ILogger<JevWorker> logger)
    : BackgroundService
{
    // Mensajes de Race Control recientes que viajan en el state de estrategia.
    private const int RaceControlInStrategyState = 5;

    private RaceState? _latest;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        strategies.Published += triggers.OnStrategyPublished;
        try
        {
            using var subscription = engine.Subscribe();
            await Task.WhenAll(WatchReplayAsync(subscription.Reader, stoppingToken), ConsumeAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            strategies.Published -= triggers.OnStrategyPublished;
        }
    }

    // Solo encola cuando hay mensajes nuevos: el resto de los estados nada más actualiza el último conocido.
    private async Task WatchReplayAsync(ChannelReader<RaceState> reader, CancellationToken cancellationToken)
    {
        await foreach (var state in reader.ReadAllAsync(cancellationToken))
        {
            var previous = Interlocked.Exchange(ref _latest, state);
            if (previous?.SessionKey != state.SessionKey)
            {
                decisions.Reset(state.SessionKey);
            }

            if (state.NewMessages.Count > 0)
            {
                triggers.OnRaceControl(state);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        await foreach (var item in triggers.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await ProcessAsync(item, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogProcessingFailed(exception, item.GetType().Name);
            }
        }
    }

    private Task ProcessAsync(JevWorkItem item, CancellationToken cancellationToken) => item switch
    {
        JevWorkItem.StrategyCalculated calculated => OnLapCompletedAsync(calculated.Publication, cancellationToken),
        JevWorkItem.RaceControl raceControl => TriageRaceControlAsync(raceControl.State, cancellationToken),
        JevWorkItem.Manual manual => ManualAsync(manual.DriverNumber, cancellationToken),
        JevWorkItem.Injected injected => TriageInjectedAsync(injected, cancellationToken),
        _ => Task.CompletedTask,
    };

    private Task OnLapCompletedAsync(StrategyPublication publication, CancellationToken cancellationToken)
    {
        var snapshot = publication.Result.Snapshot;
        return JevTriggerPolicy.OnLapCompleted(options.CurrentValue, snapshot.DriverNumber, snapshot.Lap) is { } trigger
            ? StrategyAsync(publication, trigger, null, [], cancellationToken)
            : Task.CompletedTask;
    }

    private async Task ManualAsync(int driverNumber, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _latest) is not { } state
            || strategies.GetPublication(state.SessionKey, driverNumber) is not { } publication)
        {
            LogNoStrategy(driverNumber);
            return;
        }

        await StrategyAsync(publication, JevTrigger.Manual, null, [], cancellationToken);
    }

    private async Task TriageRaceControlAsync(RaceState state, CancellationToken cancellationToken)
    {
        foreach (var driverNumber in options.CurrentValue.Drivers)
        {
            var messages = state.NewMessages
                .Where(message => RaceControlRelevance.IsRelevant(message, driverNumber))
                .Select(message => message.Message)
                .ToList();
            if (messages.Count > 0)
            {
                await TriageAsync(state, driverNumber, messages, [], null, cancellationToken);
            }
        }
    }

    private async Task TriageInjectedAsync(JevWorkItem.Injected injected, CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _latest) is not { } state)
        {
            return;
        }

        foreach (var driverNumber in options.CurrentValue.Drivers)
        {
            await TriageAsync(state, driverNumber, [], [injected.Event], injected.ScenarioId, cancellationToken);
        }
    }

    private async Task TriageAsync(
        RaceState state,
        int driverNumber,
        IReadOnlyList<string> raceControl,
        IReadOnlyList<string> injected,
        Guid? scenarioId,
        CancellationToken cancellationToken)
    {
        var current = options.CurrentValue;
        var car = state.Cars.FirstOrDefault(candidate => candidate.DriverNumber == driverNumber);
        if (!JevTriggerPolicy.ShouldTriage(current, driverNumber) || car is null)
        {
            return;
        }

        var publication = strategies.GetPublication(state.SessionKey, driverNumber);
        var request = JevRequestFactory.Triage(
            current.Model,
            new JevTriageInput(state.SessionKey, state, driverNumber, raceControl, injected));
        var input = new JevDecisionInput(
            NewId(),
            state.SessionKey,
            scenarioId,
            driverNumber,
            car.Lap,
            JevTrigger.Triage,
            publication?.Result.Snapshot.Reasons ?? []);
        if (await DecideAsync(input, request, cancellationToken) is not { } triage
            || !JevTriggerPolicy.TriageTriggersStrategy(options.CurrentValue, triage))
        {
            return;
        }

        if (publication is null)
        {
            LogNoStrategy(driverNumber);
            return;
        }

        await StrategyAsync(publication, JevTrigger.Triage, scenarioId, injected, cancellationToken);
    }

    private async Task StrategyAsync(
        StrategyPublication publication,
        JevTrigger trigger,
        Guid? scenarioId,
        IReadOnlyList<string> injected,
        CancellationToken cancellationToken)
    {
        var current = options.CurrentValue;
        var state = publication.State;
        var snapshot = publication.Result.Snapshot;
        if (!JevTriggerPolicy.IsDriverEnabled(current, snapshot.DriverNumber))
        {
            return;
        }

        var view = session.View;
        var sameSession = view.Playback.SessionKey == state.SessionKey;
        var request = JevRequestFactory.Strategy(
            current.Model,
            new JevStrategyInput(
                state.SessionKey,
                state,
                snapshot,
                sameSession && view.Playback.LapCount > 0 ? view.Playback.LapCount : snapshot.Lap,
                sameSession ? RecentRaceControl(view.RaceControl, state.Clock, snapshot.DriverNumber) : [],
                injected,
                monteCarlo.Value.TopOutcomesToJev));
        var input = new JevDecisionInput(
            NewId(),
            state.SessionKey,
            scenarioId,
            snapshot.DriverNumber,
            snapshot.Lap,
            trigger,
            snapshot.Reasons);
        await DecideAsync(input, request, cancellationToken);
    }

    private async Task<JevDecision?> DecideAsync(JevDecisionInput input, JevRequest request, CancellationToken cancellationToken)
    {
        var response = await client.SystemOneAsync(request, cancellationToken);
        if (JevDecisionMapper.Map(input, request, response) is not { } decision)
        {
            LogNotAnswered(request.Kind, input.DriverNumber, input.Lap, response.Status);
            return null;
        }

        decisions.Add(decision);
        await decisionLog.AppendAsync(
            new JevDecisionLogEntry(decision, JevSerializer.ComputeStateHash(request), JevSerializer.SerializeRequest(request)),
            cancellationToken);
        await hub.Clients.All.OnJevDecision(decision);
        LogDecision(decision.Kind, decision.Trigger, decision.DriverNumber, decision.Lap, decision.Confidence, decision.FromCache, decision.LatencyMs);
        return decision;
    }

    // La vista trae Race Control del más nuevo al más viejo; el state los lleva en orden cronológico.
    private static IReadOnlyList<string> RecentRaceControl(IReadOnlyList<RaceControlMessage> raceControl, DateTimeOffset clock, int driverNumber) =>
        [.. raceControl
            .Where(message => message.Date <= clock && RaceControlRelevance.IsRelevant(message, driverNumber))
            .Take(RaceControlInStrategyState)
            .Reverse()
            .Select(message => message.Message)];

    private Guid NewId() => Guid.CreateVersion7(timeProvider.GetUtcNow());

    [LoggerMessage(Level = LogLevel.Information, Message = "Jev {Kind} ({Trigger}) for driver {DriverNumber} on lap {Lap}: confidence {Confidence}, cache {FromCache}, {LatencyMs} ms")]
    private partial void LogDecision(JevKind kind, JevTrigger trigger, int driverNumber, int lap, double confidence, bool fromCache, int latencyMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Jev {Kind} for driver {DriverNumber} on lap {Lap} not answered: {Status}")]
    private partial void LogNotAnswered(JevKind kind, int driverNumber, int lap, JevResponseStatus status);

    [LoggerMessage(Level = LogLevel.Information, Message = "No strategy calculated yet for driver {DriverNumber}; Jev strategy skipped")]
    private partial void LogNoStrategy(int driverNumber);

    [LoggerMessage(Level = LogLevel.Error, Message = "Jev work item {Item} failed")]
    private partial void LogProcessingFailed(Exception exception, string item);
}

using Microsoft.EntityFrameworkCore;

using PitWall.Data;
using PitWall.Domain;
using PitWall.Strategy.Energy;

namespace PitWall.Web.Components.Panels.Energy;

/// <summary>
/// Lo que necesita el panel Energía de una sesión: traza de energía integrada por piloto (desde <c>car_data</c>),
/// inicio de cada vuelta y mensajes de Race Control en orden cronológico. Se lee de SQLite una vez por sesión.
/// </summary>
internal sealed class EnergySessionData
{
    private readonly IReadOnlyDictionary<int, EnergyTrace> _traces;
    private readonly IReadOnlyDictionary<int, (int Lap, DateTimeOffset Start)[]> _lapStarts;
    private readonly RaceControlMessage[] _messages;

    private EnergySessionData(
        int sessionKey,
        IReadOnlyDictionary<int, EnergyTrace> traces,
        IReadOnlyDictionary<int, (int Lap, DateTimeOffset Start)[]> lapStarts,
        RaceControlMessage[] messages)
    {
        SessionKey = sessionKey;
        _traces = traces;
        _lapStarts = lapStarts;
        _messages = messages;
    }

    public int SessionKey { get; }

    public static EnergySessionData Empty(int sessionKey) =>
        new(sessionKey, new Dictionary<int, EnergyTrace>(), new Dictionary<int, (int, DateTimeOffset)[]>(), []);

    /// <summary>Traza del piloto; <c>null</c> si no se ingirió su telemetría.</summary>
    public EnergyTrace? Trace(int driverNumber) =>
        _traces.TryGetValue(driverNumber, out var trace) && trace.Count > 0 ? trace : null;

    /// <summary>Vueltas del piloto iniciadas en o antes de <paramref name="clock"/>, en orden.</summary>
    public IReadOnlyList<(int Lap, DateTimeOffset Start)> LapStartsUpTo(int driverNumber, DateTimeOffset clock) =>
        _lapStarts.TryGetValue(driverNumber, out var starts) ? [.. starts.TakeWhile(lap => lap.Start <= clock)] : [];

    /// <summary>Si "OVERTAKE DISABLED" está vigente en <paramref name="clock"/>.</summary>
    public bool IsOvertakeDisabled(DateTimeOffset clock) =>
        OvertakeSignals.IsOvertakeDisabled(_messages.TakeWhile(message => message.Date <= clock));

    public static async Task<EnergySessionData> LoadAsync(PitWallDbContext db, EnergyModel model, int sessionKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(model);

        var samples = await db.CarData.AsNoTracking()
            .Where(e => e.SessionKey == sessionKey)
            .Select(e => new { e.DriverNumber, Sample = new TelemetrySample(e.Date, e.Speed, e.Throttle, e.Brake) })
            .ToListAsync(cancellationToken);
        var laps = await db.Laps.AsNoTracking()
            .Where(e => e.SessionKey == sessionKey && e.DateStart != null)
            .Select(e => new { e.DriverNumber, e.LapNumber, e.DateStart })
            .ToListAsync(cancellationToken);
        var raceControl = await db.RaceControl.AsNoTracking().Where(e => e.SessionKey == sessionKey).ToListAsync(cancellationToken);

        var traces = samples
            .GroupBy(e => e.DriverNumber)
            .ToDictionary(group => group.Key, group => model.Integrate(group.Select(e => e.Sample)));
        var lapStarts = laps
            .GroupBy(e => e.DriverNumber)
            .ToDictionary(
                group => group.Key,
                group => group.Select(e => (Lap: e.LapNumber, Start: e.DateStart!.Value)).OrderBy(lap => lap.Start).ToArray());
        RaceControlMessage[] messages =
        [
            .. raceControl
                .Select(e => new RaceControlMessage(e.Date, e.LapNumber, e.Category, e.Flag, e.Scope, e.DriverNumber, e.Message))
                .OrderBy(message => message.Date),
        ];

        return new EnergySessionData(sessionKey, traces, lapStarts, messages);
    }
}

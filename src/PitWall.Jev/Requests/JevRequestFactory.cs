using PitWall.Domain;
using PitWall.Jev.Serialization;
using PitWall.Jev.State;

namespace PitWall.Jev.Requests;

/// <summary>
/// Requests de triage (impact, urgency) y estrategia (pit_call, compound, pace_mode, energy_mode) de docs/jev.md §3.
/// Los números del <c>state</c> se redondean: menos tokens y el mismo estado da el mismo hash de caché.
/// </summary>
public static class JevRequestFactory
{
    // TyreDegradationModel.NoCliff: la goma no degrada.
    private const int NoCliff = int.MaxValue;

    public static JevRequest Triage(string model, JevTriageInput input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(input);

        var car = FindCar(input.State, input.DriverNumber);
        var state = new JevTriageState(
            car.Lap,
            car.Position,
            new JevTyreState(car.Compound, car.TyreAge),
            Weather(input.State.Weather),
            Track(input.State.Track),
            input.RaceControl,
            input.Injected);

        var questions = new Dictionary<string, JevQuestion>(StringComparer.Ordinal)
        {
            [JevQuestionKeys.Impact] = Choice(
                "Classify the impact on the focus car's strategy of the new race control messages and injected events.",
                JevContractNames.Of(Enum.GetValues<Impact>())),
            [JevQuestionKeys.Urgency] = new(JevQuestionType.Score, "Rate the urgency from 1 to 5."),
        };

        return new JevRequest(model, JevSerializer.SerializeState(state), questions)
        {
            Kind = JevKind.Triage,
            SessionKey = input.SessionKey,
        };
    }

    public static JevRequest Strategy(string model, JevStrategyInput input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentOutOfRangeException.ThrowIfNegative(input.TopOutcomes);

        var snapshot = input.Snapshot;
        var car = FindCar(input.State, snapshot.DriverNumber);
        var behind = input.State.Cars.FirstOrDefault(other => car.Position > 0 && other.Position == car.Position + 1);
        var tyre = snapshot.Tyre;
        var state = new JevStrategyState(
            snapshot.Lap,
            input.LapsTotal,
            car.Position,
            Round(car.Interval, 1),
            Round(behind?.Interval, 1),
            new JevTyreState(
                tyre.Compound,
                tyre.TyreAge,
                Math.Round(tyre.DegPerLap, 2),
                tyre.LapsToCliff == NoCliff ? null : tyre.LapsToCliff),
            new JevEnergyState(Math.Round(snapshot.Energy.SocPct), snapshot.Energy.OvertakeAvailable),
            Weather(input.State.Weather),
            Track(input.State.Track),
            input.RaceControl,
            input.Injected,
            [.. snapshot.Outcomes.Take(input.TopOutcomes).Select(JevMonteCarloEntry.From)]);

        var questions = new Dictionary<string, JevQuestion>(StringComparer.Ordinal);
        AddChoice(questions, JevQuestionKeys.PitCall, "Decide the pit call for the focus car this lap.", JevContractNames.Of(JevAllowedOptions.PitCalls(snapshot.Constraints)));
        AddChoice(questions, JevQuestionKeys.Compound, "Choose the compound to fit if the focus car pits.", JevContractNames.Of(JevAllowedOptions.Compounds(snapshot.Constraints)));
        AddChoice(questions, JevQuestionKeys.PaceMode, "Choose the pace mode for the focus car this lap.", JevContractNames.Of(Enum.GetValues<PaceMode>()));
        AddChoice(questions, JevQuestionKeys.EnergyMode, "Choose the energy mode for the focus car this lap.", JevContractNames.Of(JevAllowedOptions.EnergyModes(snapshot.Constraints)));

        return new JevRequest(model, JevSerializer.SerializeState(state), questions)
        {
            Kind = JevKind.Strategy,
            SessionKey = input.SessionKey,
        };
    }

    private static CarState FindCar(RaceState state, int driverNumber) =>
        state.Cars.FirstOrDefault(car => car.DriverNumber == driverNumber)
            ?? throw new ArgumentException($"Driver {driverNumber} is not in the race state.", nameof(state));

    private static JevWeatherState Weather(WeatherState weather) =>
        new(weather.Rainfall, Math.Round(weather.TrackTemp, 1), Math.Round(weather.Humidity));

    private static JevTrackState Track(TrackStatus track) => new(track.Flag, track.SafetyCar, track.VirtualSafetyCar);

    private static double? Round(double? value, int decimals) => value is { } v ? Math.Round(v, decimals) : null;

    // Sin opciones permitidas la pregunta no se envía.
    private static void AddChoice(Dictionary<string, JevQuestion> questions, string key, string instructions, IReadOnlyList<string> criteria)
    {
        if (criteria.Count > 0)
        {
            questions[key] = Choice(instructions, criteria);
        }
    }

    private static JevQuestion Choice(string instructions, IReadOnlyList<string> criteria) =>
        new(JevQuestionType.Choice, instructions, criteria);
}

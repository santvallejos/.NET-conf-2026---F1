using PitWall.Domain;
using PitWall.Strategy.MonteCarlo;
using PitWall.Strategy.Rules;

namespace PitWall.Strategy.Scenarios;

/// <summary>
/// Efecto de los eventos de un escenario en una vuelta de carrera: se fijan como ciertos en reglas y Monte Carlo
/// (docs/strategy-engine.md §4.2, docs/scenarios.md §3). El replay no cambia (ADR-010): <see cref="Apply"/> devuelve
/// una copia hipotética del estado. Ventanas de cada evento (vuelta del líder):
/// <list type="bullet">
/// <item>SC, VSC y lluvia: <c>[lap, lap + durationLaps)</c>.</item>
/// <item>Bandera roja, incidente del choque y su probabilidad de SC: solo en <c>lap</c>.</item>
/// <item>Rival que choca: fuera de carrera desde <c>lap</c>.</item>
/// <item>Pinchazo y daño de alerón: hasta que el auto para (empieza un stint después de <c>lap</c>).</item>
/// <item>Falla de ERS: hasta el final.</item>
/// </list>
/// </summary>
public sealed record ScenarioEffects
{
    public const string RedFlagName = "RED";

    private static readonly IReadOnlySet<int> NoDrivers = new HashSet<int>();
    private static readonly IReadOnlyDictionary<int, DamageSeverity> NoDamage = new Dictionary<int, DamageSeverity>();
    private static readonly IReadOnlyDictionary<int, double> NoPenalties = new Dictionary<int, double>();

    /// <summary>Carrera real: sin efectos y no hipotético.</summary>
    public static ScenarioEffects None { get; } = new();

    /// <summary>Escenario hipotético; null en la carrera real.</summary>
    public Guid? ScenarioId { get; init; }

    public bool Hypothetical => ScenarioId is not null;

    /// <summary>Eventos con algún efecto en la vuelta, en orden de vuelta.</summary>
    public IReadOnlyList<InjectedEvent> ActiveEvents { get; init; } = [];

    public Neutralisation Neutralisation { get; init; }

    /// <summary>Vueltas de SC/VSC que quedan contando la actual.</summary>
    public int NeutralisationLapsLeft { get; init; }

    /// <summary>Fracción del pit loss bajo el SC/VSC inyectado (<c>Scenarios:*PitLossFactor</c>).</summary>
    public double NeutralisationPitLossFactor { get; init; } = 1.0;

    public bool RedFlag { get; init; }

    public RainIntensity Rain { get; init; }

    /// <summary>Lluvia inyectada en la vuelta anterior.</summary>
    public RainIntensity PreviousRain { get; init; }

    /// <summary>Vueltas de lluvia cierta que quedan contando la actual.</summary>
    public int RainLapsLeft { get; init; }

    /// <summary>Rivales que chocaron: fuera de la parrilla.</summary>
    public IReadOnlySet<int> RetiredDrivers { get; init; } = NoDrivers;

    /// <summary>Autos con un rival inmediato que choca en esta vuelta.</summary>
    public IReadOnlySet<int> RivalIncidentDrivers { get; init; } = NoDrivers;

    /// <summary>Probabilidad de SC en esta vuelta por el choque de un rival; null = el prior de Monte Carlo.</summary>
    public double? SafetyCarProbability { get; init; }

    public IReadOnlySet<int> PuncturedDrivers { get; init; } = NoDrivers;

    public double? PunctureLossMinSeconds { get; init; }

    public double? PunctureLossMaxSeconds { get; init; }

    public IReadOnlyDictionary<int, DamageSeverity> WingDamage { get; init; } = NoDamage;

    public IReadOnlyDictionary<int, DamageSeverity> ErsFailures { get; init; } = NoDamage;

    /// <summary>Segundos por vuelta que se pierden hasta parar (alerón dañado).</summary>
    public IReadOnlyDictionary<int, double> RepairablePacePenalties { get; init; } = NoPenalties;

    /// <summary>Segundos por vuelta que se pierden hasta el final (falla de ERS).</summary>
    public IReadOnlyDictionary<int, double> PermanentPacePenalties { get; init; } = NoPenalties;

    /// <summary>Efectos de <paramref name="events"/> en la vuelta <paramref name="raceLap"/>; sin escenario, <see cref="None"/>.</summary>
    /// <param name="stintStartLaps">Vuelta en la que empezó el stint actual de cada auto (para saber si ya paró).</param>
    public static ScenarioEffects Resolve(
        Guid? scenarioId,
        IEnumerable<InjectedEvent> events,
        int raceLap,
        IReadOnlyDictionary<int, int> stintStartLaps,
        ScenarioOptions options)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(stintStartLaps);
        ArgumentNullException.ThrowIfNull(options);

        if (scenarioId is null)
        {
            return None;
        }

        var active = new List<InjectedEvent>();
        var neutralisation = Neutralisation.None;
        var neutralisedLaps = 0;
        var redFlag = false;
        var rain = RainIntensity.None;
        var previousRain = RainIntensity.None;
        var rainLaps = 0;
        var retired = new HashSet<int>();
        var incident = new HashSet<int>();
        double? safetyCarProbability = null;
        var punctured = new HashSet<int>();
        var wing = new Dictionary<int, DamageSeverity>();
        var ers = new Dictionary<int, DamageSeverity>();

        foreach (var injected in events.Where(e => e.Lap <= raceLap).OrderBy(e => e.Lap).ThenBy(e => e.Id))
        {
            var parameters = injected.Parameters;
            var elapsed = raceLap - injected.Lap;
            var driver = parameters.Driver;
            var applies = false;
            switch (injected.Type)
            {
                case InjectedEventType.SafetyCar:
                    var safetyCarLaps = parameters.DurationLaps ?? options.SafetyCarDefaultLaps;
                    if (elapsed < safetyCarLaps)
                    {
                        neutralisedLaps = neutralisation == Neutralisation.SafetyCar ? Math.Max(neutralisedLaps, safetyCarLaps - elapsed) : safetyCarLaps - elapsed;
                        neutralisation = Neutralisation.SafetyCar;
                        applies = true;
                    }

                    break;
                case InjectedEventType.VirtualSafetyCar:
                    var vscLaps = parameters.DurationLaps ?? options.VscDefaultLaps;
                    if (elapsed < vscLaps && neutralisation != Neutralisation.SafetyCar)
                    {
                        neutralisedLaps = Math.Max(neutralisedLaps, vscLaps - elapsed);
                        neutralisation = Neutralisation.VirtualSafetyCar;
                        applies = true;
                    }

                    break;
                case InjectedEventType.RedFlag:
                    applies = redFlag = elapsed == 0;
                    break;
                case InjectedEventType.Rain:
                    var rainDuration = parameters.DurationLaps ?? options.RainDefaultLaps;
                    var intensity = parameters.Intensity ?? RainIntensity.Light;
                    if (elapsed < rainDuration)
                    {
                        rain = Max(rain, intensity);
                        rainLaps = Math.Max(rainLaps, rainDuration - elapsed);
                        applies = true;
                    }

                    if (elapsed >= 1 && elapsed - 1 < rainDuration)
                    {
                        previousRain = Max(previousRain, intensity);
                    }

                    break;
                case InjectedEventType.Puncture when driver is { } car && NotPittedSince(stintStartLaps, car, injected.Lap):
                    punctured.Add(car);
                    applies = true;
                    break;
                case InjectedEventType.WingDamage when driver is { } car && NotPittedSince(stintStartLaps, car, injected.Lap):
                    wing[car] = Max(wing.GetValueOrDefault(car), parameters.Severity ?? DamageSeverity.Minor);
                    applies = true;
                    break;
                case InjectedEventType.ErsFailure when driver is { } car:
                    ers[car] = Max(ers.GetValueOrDefault(car), parameters.Severity ?? DamageSeverity.Minor);
                    applies = true;
                    break;
                case InjectedEventType.RivalCrash when parameters.Rival is { } rival:
                    retired.Add(rival);
                    if (elapsed == 0)
                    {
                        if (driver is { } reference)
                        {
                            incident.Add(reference);
                        }

                        safetyCarProbability = Math.Max(safetyCarProbability ?? 0, options.RivalCrashSafetyCarProbability);
                    }

                    applies = true;
                    break;
            }

            if (applies)
            {
                active.Add(injected);
            }
        }

        return new ScenarioEffects
        {
            ScenarioId = scenarioId,
            ActiveEvents = active,
            Neutralisation = neutralisation,
            NeutralisationLapsLeft = neutralisedLaps,
            NeutralisationPitLossFactor = neutralisation switch
            {
                Neutralisation.SafetyCar => options.SafetyCarPitLossFactor,
                Neutralisation.VirtualSafetyCar => options.VscPitLossFactor,
                _ => 1.0,
            },
            RedFlag = redFlag,
            Rain = rain,
            PreviousRain = previousRain,
            RainLapsLeft = rainLaps,
            RetiredDrivers = retired,
            RivalIncidentDrivers = incident,
            SafetyCarProbability = safetyCarProbability,
            PuncturedDrivers = punctured,
            PunctureLossMinSeconds = options.PunctureLossMinSeconds,
            PunctureLossMaxSeconds = options.PunctureLossMaxSeconds,
            WingDamage = wing,
            ErsFailures = ers,
            RepairablePacePenalties = wing.ToDictionary(
                pair => pair.Key,
                pair => pair.Value == DamageSeverity.Major ? options.WingDamageMajorSecPerLap : options.WingDamageMinorSecPerLap),
            PermanentPacePenalties = ers.ToDictionary(
                pair => pair.Key,
                pair => pair.Value == DamageSeverity.Major ? options.ErsFailureMajorSecPerLap : options.ErsFailureMinorSecPerLap),
        };
    }

    /// <summary>
    /// Estado hipotético: bandera roja, SC/VSC y lluvia inyectados, y la parrilla sin los rivales que chocaron (con
    /// posiciones e intervalos recalculados). Idempotente; sin efectos devuelve el mismo estado.
    /// </summary>
    public RaceState Apply(RaceState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var track = state.Track;
        if (RedFlag)
        {
            track = track with { Flag = RedFlagName };
        }

        if (Neutralisation == Neutralisation.SafetyCar)
        {
            track = track with { SafetyCar = true, VirtualSafetyCar = false };
        }
        else if (Neutralisation == Neutralisation.VirtualSafetyCar && !track.SafetyCar)
        {
            track = track with { VirtualSafetyCar = true };
        }

        var weather = Rain != RainIntensity.None ? state.Weather with { Rainfall = true } : state.Weather;
        var cars = WithoutRetired(state.Cars);
        return track == state.Track && weather == state.Weather && ReferenceEquals(cars, state.Cars)
            ? state
            : state with { Track = track, Weather = weather, Cars = cars };
    }

    // Quita los rivales que chocaron: los de atrás suben de posición y el siguiente suma el intervalo del que salió.
    private IReadOnlyList<CarState> WithoutRetired(IReadOnlyList<CarState> cars)
    {
        if (RetiredDrivers.Count == 0 || !cars.Any(car => RetiredDrivers.Contains(car.DriverNumber)))
        {
            return cars;
        }

        var result = new List<CarState>(cars.Count);
        var removedAhead = 0;
        double? pendingInterval = null;
        var leaderRemoved = false;
        double? leaderOffset = null;
        foreach (var car in cars.OrderBy(car => car.Position > 0 ? car.Position : int.MaxValue))
        {
            if (RetiredDrivers.Contains(car.DriverNumber))
            {
                removedAhead++;
                leaderRemoved |= result.Count == 0;
                pendingInterval = (pendingInterval ?? 0) + (car.Interval ?? 0);
                continue;
            }

            var updated = car.Position > 0 ? car with { Position = car.Position - removedAhead } : car;
            if (leaderRemoved && result.Count == 0)
            {
                leaderOffset = car.GapToLeader;
                updated = updated with { GapToLeader = null, Interval = null };
            }
            else
            {
                if (leaderOffset is { } offset && car.GapToLeader is { } gap)
                {
                    updated = updated with { GapToLeader = gap - offset };
                }

                if (pendingInterval is { } extra && car.Interval is { } interval)
                {
                    updated = updated with { Interval = interval + extra };
                }
            }

            pendingInterval = null;
            result.Add(updated);
        }

        return result;
    }

    private static bool NotPittedSince(IReadOnlyDictionary<int, int> stintStartLaps, int driver, int lap) =>
        !stintStartLaps.TryGetValue(driver, out var start) || start <= lap;

    private static TEnum Max<TEnum>(TEnum left, TEnum right)
        where TEnum : struct, Enum =>
        Comparer<TEnum>.Default.Compare(left, right) >= 0 ? left : right;
}

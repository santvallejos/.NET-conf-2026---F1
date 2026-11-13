using PitWall.Domain;
using PitWall.Strategy.Degradation;

namespace PitWall.Strategy.MonteCarlo;

/// <summary>
/// Proyecta el resto de la carrera del piloto foco por estrategia candidata (docs/strategy-engine.md §4). Cada
/// iteración simula vuelta a vuelta a toda la parrilla con estas variables estocásticas:
/// <list type="bullet">
/// <item>Ritmo: <c>base + deg · edad + fuel · restantes + Normal(0, σ residual)</c> del modelo de degradación.</item>
/// <item>Pit loss: <c>Normal(media, σ)</c> de los <c>lane_duration</c> de la sesión; reducido bajo SC/VSC.</item>
/// <item>SC y VSC: Bernoulli por vuelta (<c>p_sc</c>, <c>p_vsc</c>) con duración fija; el SC agrupa la parrilla.</item>
/// <item>Lluvia: Markov seco/lluvia; el compuesto que no corresponde al estado pierde tiempo por vuelta.</item>
/// <item>Adelantamiento: Bernoulli(f(Δritmo, overtake mode)) cuando el ritmo alcanza al de adelante; si falla,
/// queda detrás.</item>
/// <item>Rivales: misma dinámica con su propio modelo; paran en su cliff (o por la regla de compuestos) y ante
/// un cambio de clima.</item>
/// </list>
/// El replay no se modifica. Determinista: la candidata <c>i</c> deriva sus semillas de <c>Random(Seed ^ i)</c>, así
/// que el resultado no depende del orden en que <see cref="Parallel"/> ejecute el trabajo.
/// </summary>
public sealed class MonteCarloSimulator
{
    // Vueltas seguidas con el compuesto equivocado para el clima tras las que un rival para a cambiarlo.
    private const int RivalWeatherReactionLaps = 2;

    // Cada candidata se reparte en bloques de iteraciones para que Parallel.For use todos los núcleos aunque haya
    // menos candidatas que núcleos o no sean múltiplo (RNF-03). Fijo: el resultado no depende de la máquina.
    private const int ChunksPerCandidate = 8;

    private readonly MonteCarloOptions _options;

    public MonteCarloSimulator(MonteCarloOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Iterations);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.PunctureLossMinSeconds, options.PunctureLossMaxSeconds);

        _options = options;
    }

    public int Iterations => _options.Iterations;

    /// <summary>Un <see cref="StrategyOutcome"/> por candidata, en el mismo orden.</summary>
    public IReadOnlyList<StrategyOutcome> Run(MonteCarloInput input, IReadOnlyList<StrategyCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(candidates);

        foreach (var candidate in candidates)
        {
            if (candidate.StopCompound is { } compound && !input.FocusCompounds.ContainsKey(compound))
            {
                throw new ArgumentException($"Candidate {candidate.Id} fits {compound}, which has no pace model.", nameof(candidates));
            }
        }

        var field = new Field(input, _options);

        // RNG de la candidata con semilla seed ^ candidateIndex; de él salen, en orden, las semillas de sus bloques.
        var chunkSeeds = new int[candidates.Count * ChunksPerCandidate];
        for (var index = 0; index < candidates.Count; index++)
        {
            var random = new Random(input.Seed ^ index);
            for (var chunk = 0; chunk < ChunksPerCandidate; chunk++)
            {
                chunkSeeds[(index * ChunksPerCandidate) + chunk] = random.Next();
            }
        }

        var tallies = new Tally[chunkSeeds.Length];
        Parallel.For(0, chunkSeeds.Length, item =>
        {
            var chunk = item % ChunksPerCandidate;
            var iterations = (_options.Iterations / ChunksPerCandidate) + (chunk < _options.Iterations % ChunksPerCandidate ? 1 : 0);
            tallies[item] = Simulate(field, input, candidates[item / ChunksPerCandidate], chunkSeeds[item], iterations);
        });

        var results = new CandidateResult[candidates.Count];
        for (var index = 0; index < candidates.Count; index++)
        {
            results[index] = Merge(tallies.AsSpan(index * ChunksPerCandidate, ChunksPerCandidate), field.Start.Length);
        }

        var bestTime = results.Length == 0 ? 0 : results.Min(result => result.MeanRaceTime);
        return [.. candidates.Select((candidate, index) =>
        {
            var result = results[index];
            return new StrategyOutcome(
                candidate.Id,
                candidate.Call,
                candidate.BoxLap,
                candidate.Compound,
                result.ExpectedPosition,
                result.Distribution,
                result.PGain,
                result.PLose,
                result.MeanRaceTime - bestTime);
        })];
    }

    // Bucle caliente (RNF-03: < 1 s por piloto con N = 10.000): parámetros copiados a locales y estado de cada auto
    // en un struct contiguo.
    private Tally Simulate(Field field, MonteCarloInput input, StrategyCandidate candidate, int seed, int iterations)
    {
        var options = _options;
        var random = new Random(seed);
        var quantiles = StandardNormal.Quantiles;
        var spare = -1;
        var focus = field.FocusIndex;
        var focusAfterStop = candidate.StopCompound is { } stopCompound ? input.FocusCompounds[stopCompound] : null;

        var firstLap = input.Lap;
        var totalLaps = input.TotalLaps;
        var fuelCoef = input.FuelCoefSecondsPerLap;
        var pitLossMean = input.PitLossMean;
        var pitLossSigma = input.PitLossSigma;
        var probSafetyCar = options.ProbSafetyCarPerLap;
        var probNeutralised = options.ProbSafetyCarPerLap + options.ProbVscPerLap;
        var safetyCarLaps = options.SafetyCarLaps;
        var vscLaps = options.VscLaps;
        var safetyCarGap = options.SafetyCarGapSeconds;
        var safetyCarLap = field.SafetyCarLap;
        var vscLap = field.VscLap;
        var safetyCarPitLoss = options.SafetyCarPitLossFactor;
        var vscPitLoss = options.VscPitLossFactor;
        var punctureMin = options.PunctureLossMinSeconds;
        var punctureRange = options.PunctureLossMaxSeconds - options.PunctureLossMinSeconds;
        var dryInRain = options.DryCompoundInRainPenaltySeconds;
        var wetInDry = options.WetCompoundInDryPenaltySeconds;
        var rainStop = options.RainStopProbPerLap;
        var rainStart = field.RainStartProbPerLap;
        var startPosition = field.FocusStartPosition;
        var overtakes = new OvertakeModel(
            field.OvertakeBase + (input.OvertakeModeEnabled ? options.OvertakeModeBonus : 0),
            options.OvertakePaceSlopePerSecond,
            options.CertainOvertakeDeltaSeconds,
            options.MaxOvertakeProbability,
            options.FollowGapSeconds);

        var start = field.Start;
        var startOrder = field.StartOrder;
        var n = start.Length;
        var cars = new SimulatedCar[n];
        var order = new int[n];
        var counts = new long[n + 1];
        double positionSum = 0;
        double timeSum = 0;
        long gains = 0;
        long losses = 0;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            Array.Copy(start, cars, n);
            Array.Copy(startOrder, order, n);
            cars[focus].StopLap = candidate.StopLap ?? -1;

            var punctured = input.Puncture;
            var raining = input.Raining;
            var neutralisation = input.ActiveNeutralisation;
            var neutralisedLaps = neutralisation switch
            {
                Neutralisation.SafetyCar => safetyCarLaps,
                Neutralisation.VirtualSafetyCar => vscLaps,
                _ => 0,
            };

            for (var lap = firstLap; lap <= totalLaps; lap++)
            {
                if (neutralisedLaps == 0)
                {
                    neutralisation = Neutralisation.None;
                    var draw = random.NextDouble();
                    if (draw < probSafetyCar)
                    {
                        neutralisation = Neutralisation.SafetyCar;
                        neutralisedLaps = safetyCarLaps;
                        BunchUp(cars, order, safetyCarGap);
                    }
                    else if (draw < probNeutralised)
                    {
                        neutralisation = Neutralisation.VirtualSafetyCar;
                        neutralisedLaps = vscLaps;
                    }
                }

                if (neutralisedLaps > 0)
                {
                    neutralisedLaps--;
                }

                var neutralised = neutralisation != Neutralisation.None;
                var neutralLap = neutralisation == Neutralisation.SafetyCar ? safetyCarLap : vscLap;
                var pitLossFactor = neutralisation switch
                {
                    Neutralisation.SafetyCar => safetyCarPitLoss,
                    Neutralisation.VirtualSafetyCar => vscPitLoss,
                    _ => 1.0,
                };
                var fuel = fuelCoef * (totalLaps - lap);
                // Compuesto que no corresponde al clima: seco con lluvia o de lluvia con pista seca.
                var dryPenalty = raining ? dryInRain : 0;
                var wetPenalty = raining ? 0 : wetInDry;

                for (var i = 0; i < n; i++)
                {
                    ref var car = ref cars[i];
                    var wrongTyre = car.Dry == raining;
                    var stop = car.StopLap == lap;
                    var reactsToWeather = false;
                    if (wrongTyre && i != focus && ++car.Mismatch >= RivalWeatherReactionLaps)
                    {
                        stop = true;
                        reactsToWeather = true;
                    }

                    double time;
                    if (neutralised)
                    {
                        time = neutralLap;
                    }
                    else
                    {
                        // Normal(0, 1): dos índices de 12 bits por Random.Next() sobre la tabla de cuantiles.
                        double noise;
                        if (spare >= 0)
                        {
                            noise = quantiles[spare];
                            spare = -1;
                        }
                        else
                        {
                            var bits = random.Next();
                            spare = (bits >> StandardNormal.Bits) & StandardNormal.Mask;
                            noise = quantiles[bits & StandardNormal.Mask];
                        }

                        time = car.Pace + fuel + (car.Sigma * noise) + (car.Dry ? dryPenalty : wetPenalty);
                    }

                    if (punctured && i == focus)
                    {
                        time += punctureMin + (punctureRange * random.NextDouble());
                    }

                    if (stop)
                    {
                        time += Math.Max(0, pitLossMean + (pitLossSigma * StandardNormal.BoxMuller(random))) * pitLossFactor;
                        if (i == focus)
                        {
                            punctured = false;
                            if (focusAfterStop is not null)
                            {
                                car.Base = focusAfterStop.BaseLapTime;
                                car.Deg = focusAfterStop.DegPerLap;
                                car.Sigma = focusAfterStop.ResidualSigma;
                                car.Dry = IsDry(focusAfterStop.Compound);
                            }
                        }
                        else if (reactsToWeather)
                        {
                            // El rival conserva su ritmo base y deg; solo pasa a la goma que pide el clima.
                            car.Dry = !raining;
                            car.Mismatch = 0;
                            car.StopLap = -1;
                        }

                        car.Pace = car.Base;
                    }
                    else
                    {
                        car.Pace += car.Deg;
                        if (!wrongTyre)
                        {
                            car.Mismatch = 0;
                        }
                    }

                    car.LapTime = time;
                    car.Stopped = stop;
                    car.Cumulative += time;
                }

                overtakes.Resolve(cars, order, random, neutralised);

                var weatherDraw = random.NextDouble();
                raining = raining ? weatherDraw >= rainStop : weatherDraw < rainStart;
            }

            var position = Array.IndexOf(order, focus) + 1;
            counts[position]++;
            positionSum += position;
            timeSum += cars[focus].Cumulative;
            if (position < startPosition)
            {
                gains++;
            }
            else if (position > startPosition)
            {
                losses++;
            }
        }

        return new Tally(iterations, counts, positionSum, timeSum, gains, losses);
    }

    private static CandidateResult Merge(ReadOnlySpan<Tally> tallies, int cars)
    {
        var counts = new long[cars + 1];
        double iterations = 0;
        double positionSum = 0;
        double timeSum = 0;
        double gains = 0;
        double losses = 0;
        foreach (var tally in tallies)
        {
            for (var position = 1; position <= cars; position++)
            {
                counts[position] += tally.Counts[position];
            }

            iterations += tally.Iterations;
            positionSum += tally.PositionSum;
            timeSum += tally.TimeSum;
            gains += tally.Gains;
            losses += tally.Losses;
        }

        var distribution = new Dictionary<int, double>();
        for (var position = 1; position <= cars; position++)
        {
            if (counts[position] > 0)
            {
                distribution[position] = counts[position] / iterations;
            }
        }

        return new CandidateResult(positionSum / iterations, distribution, gains / iterations, losses / iterations, timeSum / iterations);
    }

    // Al desplegarse el SC la parrilla se agrupa: nadie queda a más de gapSeconds del auto de adelante.
    private static void BunchUp(SimulatedCar[] cars, int[] order, double gapSeconds)
    {
        for (var p = 1; p < order.Length; p++)
        {
            var limit = cars[order[p - 1]].Cumulative + gapSeconds;
            if (cars[order[p]].Cumulative > limit)
            {
                cars[order[p]].Cumulative = limit;
            }
        }
    }

    private static bool IsDry(Compound compound) => compound is Compound.Soft or Compound.Medium or Compound.Hard;

    /// <summary>Estado de un auto durante una iteración.</summary>
    private struct SimulatedCar
    {
        /// <summary>Tiempo de carrera acumulado desde el inicio de la proyección (incluye el gap inicial al líder).</summary>
        public double Cumulative;

        /// <summary><c>base + deg · edad</c> de la vuelta en curso.</summary>
        public double Pace;

        public double Base;

        public double Deg;

        public double Sigma;

        public double LapTime;

        /// <summary>Vuelta en la que para, o -1.</summary>
        public int StopLap;

        /// <summary>Vueltas seguidas con el compuesto equivocado para el clima.</summary>
        public int Mismatch;

        public bool Dry;

        public bool Stopped;
    }

    private sealed record Tally(int Iterations, long[] Counts, double PositionSum, double TimeSum, long Gains, long Losses);

    private sealed record CandidateResult(
        double ExpectedPosition,
        IReadOnlyDictionary<int, double> Distribution,
        double PGain,
        double PLose,
        double MeanRaceTime);

    /// <summary>
    /// Ordena la parrilla por tiempo acumulado tras la vuelta. Un auto que alcanza al de adelante lo pasa seguro si
    /// el de adelante paró, si él salió de boxes delante o si es <c>CertainOvertakeDeltaSeconds</c> más rápido; si no,
    /// con probabilidad <c>base (+ overtake mode) + pendiente · Δritmo</c>. Si falla, queda a <c>FollowGapSeconds</c>
    /// del de adelante. Bajo SC/VSC no hay adelantamientos en pista.
    /// </summary>
    private sealed class OvertakeModel(double baseProbability, double paceSlope, double certainDelta, double maxProbability, double followGap)
    {
        public void Resolve(SimulatedCar[] cars, int[] order, Random random, bool neutralised)
        {
            for (var p = 1; p < order.Length; p++)
            {
                var car = order[p];
                var j = p;
                while (j > 0)
                {
                    var ahead = order[j - 1];
                    if (cars[car].Cumulative >= cars[ahead].Cumulative)
                    {
                        break;
                    }

                    var paceDelta = cars[ahead].LapTime - cars[car].LapTime;
                    var passes = cars[ahead].Stopped || cars[car].Stopped || paceDelta >= certainDelta
                        || (!neutralised && random.NextDouble() < Math.Clamp(baseProbability + (paceSlope * paceDelta), 0, maxProbability));
                    if (!passes)
                    {
                        var held = cars[ahead].Cumulative + followGap;
                        cars[car].Cumulative = j < p ? Math.Min(held, cars[order[j + 1]].Cumulative) : held;
                        break;
                    }

                    order[j] = ahead;
                    j--;
                }

                order[j] = car;
            }
        }
    }

    /// <summary>
    /// Normal(0, 1). El ruido de ritmo (la llamada más frecuente) usa una tabla de 4.096 cuantiles equiespaciados:
    /// cada <see cref="Random.Next()"/> da dos índices de 12 bits. Es una normal discretizada (colas cortadas en
    /// ±3,5σ) que entra en la caché L1 y es varias veces más barata que Box-Muller (RNF-03). El pit loss, poco
    /// frecuente, usa Box-Muller.
    /// </summary>
    private static class StandardNormal
    {
        public const int Bits = 12;
        public const int Mask = (1 << Bits) - 1;

        public static readonly double[] Quantiles = BuildQuantiles();

        public static double BoxMuller(Random random)
        {
            var u1 = 1.0 - random.NextDouble();
            var u2 = random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        private static double[] BuildQuantiles()
        {
            var quantiles = new double[1 << Bits];
            for (var i = 0; i < quantiles.Length; i++)
            {
                quantiles[i] = InverseNormalCdf((i + 0.5) / quantiles.Length);
            }

            return quantiles;
        }

        // Aproximación racional de Acklam a la inversa de la CDF normal (error relativo < 1,15e-9).
        private static double InverseNormalCdf(double p)
        {
            const double Low = 0.02425;

            if (p < Low)
            {
                return Tail(Math.Sqrt(-2 * Math.Log(p)));
            }

            if (p > 1 - Low)
            {
                return -Tail(Math.Sqrt(-2 * Math.Log(1 - p)));
            }

            var s = p - 0.5;
            var r = s * s;
            var numerator = ((((((-3.969683028665376e+01 * r) + 2.209460984245205e+02) * r) - 2.759285104469687e+02) * r) + 1.383577518672690e+02) * r;
            numerator = ((numerator - 3.066479806614716e+01) * r) + 2.506628277459239e+00;
            var denominator = ((((((-5.447609879822406e+01 * r) + 1.615858368580409e+02) * r) - 1.556989798598866e+02) * r) + 6.680131188771972e+01) * r;
            denominator = ((denominator - 1.328068155288572e+01) * r) + 1.0;
            return numerator * s / denominator;

            static double Tail(double q)
            {
                var numerator = ((((((-7.784894002430293e-03 * q) - 3.223964580411365e-01) * q) - 2.400758277161838e+00) * q) - 2.549732539343734e+00) * q;
                numerator = ((numerator + 4.374664141464968e+00) * q) + 2.938163982698783e+00;
                var denominator = ((((((7.784695709041462e-03 * q) + 3.224671290700398e-01) * q) + 2.445134137142996e+00) * q) + 3.754408661907416e+00) * q;
                denominator += 1.0;
                return numerator / denominator;
            }
        }
    }

    /// <summary>Estado inicial de la parrilla, compartido (solo lectura) por todas las candidatas.</summary>
    private sealed class Field
    {
        public Field(MonteCarloInput input, MonteCarloOptions options)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(input.Lap);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(input.TotalLaps);
            if (input.Cars.Count == 0)
            {
                throw new ArgumentException("The projection needs at least the focus car.", nameof(input));
            }

            var count = input.Cars.Count;
            Start = new SimulatedCar[count];
            FocusIndex = -1;
            for (var i = 0; i < count; i++)
            {
                var car = input.Cars[i];
                if (car.DriverNumber == input.FocusDriver)
                {
                    FocusIndex = i;
                }

                var tyre = car.Tyre;
                Start[i] = new SimulatedCar
                {
                    Cumulative = car.GapToLeader,
                    Pace = tyre.BaseLapTime + (tyre.DegPerLap * tyre.TyreAge),
                    Base = tyre.BaseLapTime,
                    Deg = tyre.DegPerLap,
                    Sigma = tyre.ResidualSigma,
                    StopLap = RivalStopLap(car, input),
                    Dry = IsDry(tyre.Compound),
                };
            }

            if (FocusIndex < 0)
            {
                throw new ArgumentException($"Driver {input.FocusDriver} is not in the field.", nameof(input));
            }

            StartOrder = [.. Enumerable.Range(0, count).OrderBy(i => Start[i].Cumulative)];
            FocusStartPosition = Array.IndexOf(StartOrder, FocusIndex) + 1;
            var fastest = input.Cars.Min(car => car.Tyre.BaseLapTime);
            SafetyCarLap = fastest * options.SafetyCarLapFactor;
            VscLap = fastest * options.VscLapFactor;
            RainStartProbPerLap = Math.Clamp(input.RainRisk, 0, 1) / options.RainRiskHorizonLaps;
            OvertakeBase = Math.Clamp(input.OvertakeBaseProbability, 0, 1);
        }

        public SimulatedCar[] Start { get; }

        public int FocusIndex { get; }

        public int FocusStartPosition { get; }

        public int[] StartOrder { get; }

        public double SafetyCarLap { get; }

        public double VscLap { get; }

        public double RainStartProbPerLap { get; }

        public double OvertakeBase { get; }

        // Un rival para en su cliff si llega antes del final; si debe el segundo compuesto seco y no llega al
        // cliff, en la penúltima vuelta. Sin parada: -1.
        private static int RivalStopLap(MonteCarloCar car, MonteCarloInput input)
        {
            var lapsToCliff = car.Tyre.LapsToCliff;
            if (lapsToCliff != TyreDegradationModel.NoCliff && input.Lap + lapsToCliff < input.TotalLaps)
            {
                return input.Lap + lapsToCliff;
            }

            return car.MandatoryStopPending && input.Lap < input.TotalLaps ? input.TotalLaps - 1 : -1;
        }
    }
}

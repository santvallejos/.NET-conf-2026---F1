using System.Diagnostics;
using System.Reflection;

using PitWall.Strategy.MonteCarlo;

using Xunit.Abstractions;

namespace PitWall.Tests.Strategy.MonteCarlo;

/// <summary>
/// Medición de RNF-03. Corre sola (sin otros tests en paralelo): con N = 10.000 usa todos los núcleos y la
/// competencia con otras clases de test falsearía el tiempo.
/// </summary>
[Collection(PerformanceCollection.Name)]
public sealed class MonteCarloPerformanceTests(ITestOutputHelper output)
{
    private const int Lap = 20;

    // En Debug el JIT no optimiza PitWall.Strategy (unas 2 veces más lento): ahí el test solo vigila regresiones
    // groseras; el objetivo de 1 s se exige con build optimizado (dotnet test -c Release).
    private static readonly bool Optimized =
        typeof(MonteCarloSimulator).Assembly.GetCustomAttribute<DebuggableAttribute>()?.IsJITOptimizerDisabled != true;

    [Fact]
    public void Calculate_FullFieldWith10000Iterations_UnderOneSecondPerDriver()
    {
        // RNF-03: Monte Carlo de una vuelta (todas las candidatas, un piloto) < 1 s con N = 10.000. Se toma la mejor
        // de 5 corridas para filtrar la carga ajena de la máquina; todas quedan en la salida del test.
        var calculator = MonteCarloFixtures.Calculator(iterations: 10_000);
        var context = MonteCarloFixtures.Race(Lap);
        calculator.Calculate(context, MonteCarloFixtures.Focus);

        var runs = Enumerable.Range(0, 5).Select(_ => calculator.Calculate(context, MonteCarloFixtures.Focus)!).ToList();

        var durations = runs.Select(run => run.Duration).Order().ToList();
        var limit = TimeSpan.FromSeconds(Optimized ? 1 : 3);
        output.WriteLine(
            $"RNF-03 ({(Optimized ? "optimizado" : "Debug")}): {runs[0].Snapshot.Outcomes.Count} candidatas × {runs[0].Iterations} iteraciones, " +
            $"20 autos, {MonteCarloFixtures.TotalLaps - Lap + 1} vueltas, {Environment.ProcessorCount} núcleos: " +
            $"mín {durations[0].TotalMilliseconds:0} ms, mediana {durations[2].TotalMilliseconds:0} ms, máx {durations[^1].TotalMilliseconds:0} ms " +
            $"(Monte Carlo {string.Join(" / ", runs.Select(run => $"{run.MonteCarloDuration.TotalMilliseconds:0}"))} ms)");
        Assert.True(durations[0] < limit, $"Calculation took {durations[0].TotalMilliseconds:0} ms (limit {limit.TotalMilliseconds:0} ms)");
    }
}

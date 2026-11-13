namespace PitWall.Tests.Strategy.MonteCarlo;

/// <summary>Colección sin paralelismo: xUnit la ejecuta después del resto, sin otros tests compitiendo por CPU.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PerformanceCollection
{
    public const string Name = "Performance";
}

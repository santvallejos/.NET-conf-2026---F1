namespace PitWall.Strategy.Energy;

/// <summary>Qué hace el ERS durante una muestra según el modelo estimado (docs/strategy-engine.md §2).</summary>
public enum EnergyPhase
{
    None,
    HarvestBrake,
    HarvestLift,
    Deploy,
}

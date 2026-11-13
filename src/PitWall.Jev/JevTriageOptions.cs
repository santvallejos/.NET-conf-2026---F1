namespace PitWall.Jev;

/// <summary>Sección <c>Jev:Triage</c>.</summary>
public sealed class JevTriageOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>Un triage <c>ACT_NOW</c> dispara la estrategia aunque el modo no la pida.</summary>
    public bool CanTriggerStrategy { get; set; } = true;
}

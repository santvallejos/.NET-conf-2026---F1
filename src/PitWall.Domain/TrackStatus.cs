namespace PitWall.Domain;

/// <summary>Estado de pista; mismos campos que <c>track</c> del state de Jev (docs/jev.md §3.3).</summary>
public sealed record TrackStatus(
    string Flag,
    bool SafetyCar,
    bool VirtualSafetyCar);

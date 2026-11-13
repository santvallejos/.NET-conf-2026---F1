namespace PitWall.Domain;

/// <summary>Estado de pista: bandera vigente y si hay safety car o VSC.</summary>
public sealed record TrackStatus(
    string Flag,
    bool SafetyCar,
    bool VirtualSafetyCar);

using System.Text.Json.Serialization;

using PitWall.Domain.Serialization;

namespace PitWall.Strategy.Scenarios;

/// <summary>Catálogo de eventos hipotéticos inyectables (docs/scenarios.md §3).</summary>
[JsonConverter(typeof(UpperSnakeEnumConverter<InjectedEventType>))]
public enum InjectedEventType
{
    SafetyCar,
    VirtualSafetyCar,
    RedFlag,
    Rain,
    Puncture,
    WingDamage,
    RivalCrash,
    ErsFailure,
}

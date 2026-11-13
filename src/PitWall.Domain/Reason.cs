using System.Text.Json.Serialization;

using PitWall.Domain.Serialization;

namespace PitWall.Domain;

[JsonConverter(typeof(UpperSnakeEnumConverter<Reason>))]
public enum Reason
{
    TyreDegHigh,
    TyreCliffNear,
    UndercutThreat,
    OvercutWindow,
    ScDeployed,
    VscDeployed,
    RedFlag,
    RainStarting,
    RainStopping,
    TrackDrying,
    LowGrip,
    TrafficOnExit,
    ClearAirOnExit,
    EnergyLow,
    OvertakeAvailable,
    Puncture,
    WingDamage,
    RivalIncident,
    ErsFailure,
    MandatoryCompoundPending,
}

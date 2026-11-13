using PitWall.Data.Entities;
using PitWall.Domain;

namespace PitWall.Ingestion.OpenF1;

/// <summary>Filas de OpenF1 → entidades de <c>PitWall.Data</c>; todas quedan con la <c>SessionKey</c> de la sesión ingerida.</summary>
internal static class OpenF1Mapper
{
    public static Driver ToEntity(this OpenF1Driver row, int sessionKey) => new()
    {
        SessionKey = sessionKey,
        DriverNumber = row.DriverNumber,
        FullName = row.FullName ?? string.Empty,
        Acronym = row.NameAcronym ?? string.Empty,
        Team = row.TeamName ?? string.Empty,
        TeamColour = row.TeamColour ?? string.Empty,
    };

    public static Lap ToEntity(this OpenF1Lap row, int sessionKey) => new()
    {
        SessionKey = sessionKey,
        DriverNumber = row.DriverNumber,
        LapNumber = row.LapNumber,
        DateStart = row.DateStart,
        Duration = row.LapDuration,
        S1 = row.DurationSector1,
        S2 = row.DurationSector2,
        S3 = row.DurationSector3,
        I1Speed = row.I1Speed,
        I2Speed = row.I2Speed,
        IsPitOutLap = row.IsPitOutLap ?? false,
    };

    /// <summary><c>null</c> si el compuesto no es uno de <see cref="Compound"/> (OpenF1 publica a veces <c>UNKNOWN</c>).</summary>
    public static Stint? ToEntity(this OpenF1Stint row, int sessionKey) =>
        Enum.TryParse<Compound>(row.Compound, true, out var compound) && Enum.IsDefined(compound)
            ? new Stint
            {
                SessionKey = sessionKey,
                DriverNumber = row.DriverNumber,
                StintNumber = row.StintNumber,
                LapStart = row.LapStart,
                LapEnd = row.LapEnd ?? row.LapStart,
                Compound = compound,
                TyreAgeAtStart = row.TyreAgeAtStart ?? 0,
            }
            : null;

    public static PitStop ToEntity(this OpenF1Pit row, int sessionKey) => new()
    {
        SessionKey = sessionKey,
        DriverNumber = row.DriverNumber,
        LapNumber = row.LapNumber,
        Date = row.Date,
        PitDuration = row.PitDuration,
        LaneDuration = row.LaneDuration,
        StopDuration = row.StopDuration,
    };

    public static RaceControl ToEntity(this OpenF1RaceControl row, int sessionKey) => new()
    {
        SessionKey = sessionKey,
        Date = row.Date,
        LapNumber = row.LapNumber,
        Category = row.Category ?? string.Empty,
        Flag = row.Flag,
        Scope = row.Scope,
        DriverNumber = row.DriverNumber,
        Message = row.Message ?? string.Empty,
    };

    public static Weather ToEntity(this OpenF1Weather row, int sessionKey) => new()
    {
        SessionKey = sessionKey,
        Date = row.Date,
        AirTemp = row.AirTemperature,
        TrackTemp = row.TrackTemperature,
        Humidity = row.Humidity,
        Rainfall = row.Rainfall != 0,
        WindSpeed = row.WindSpeed,
        WindDirection = row.WindDirection,
        Pressure = row.Pressure,
    };

    public static IntervalSample ToEntity(this OpenF1Interval row, int sessionKey) => new()
    {
        SessionKey = sessionKey,
        DriverNumber = row.DriverNumber,
        Date = row.Date,
        GapToLeader = row.GapToLeader,
        Interval = row.Interval,
    };

    public static PositionSample ToEntity(this OpenF1Position row, int sessionKey) => new()
    {
        SessionKey = sessionKey,
        DriverNumber = row.DriverNumber,
        Date = row.Date,
        Position = row.Position,
    };

    public static Overtake ToEntity(this OpenF1Overtake row, int sessionKey) => new()
    {
        SessionKey = sessionKey,
        Date = row.Date,
        OvertakingDriver = row.OvertakingDriverNumber,
        OvertakenDriver = row.OvertakenDriverNumber,
        Position = row.Position,
    };

    public static SessionResult ToEntity(this OpenF1SessionResult row, int sessionKey) => new()
    {
        SessionKey = sessionKey,
        DriverNumber = row.DriverNumber,
        Position = row.Position,
        Laps = row.NumberOfLaps ?? 0,
        Dnf = row.Dnf ?? false,
        Dns = row.Dns ?? false,
        Dsq = row.Dsq ?? false,
        Duration = row.Duration,
        GapToLeader = row.GapToLeader,
    };

    public static StartingGrid ToEntity(this OpenF1StartingGrid row, int sessionKey) => new()
    {
        SessionKey = sessionKey,
        DriverNumber = row.DriverNumber,
        Position = row.Position,
        LapDuration = row.LapDuration,
    };

    public static TeamRadio ToEntity(this OpenF1TeamRadio row, int sessionKey, string recordingUrl, string localPath) => new()
    {
        SessionKey = sessionKey,
        DriverNumber = row.DriverNumber,
        Date = row.Date,
        RecordingUrl = recordingUrl,
        LocalPath = localPath,
    };

    public static CarData ToEntity(this OpenF1CarData row, int sessionKey) => new()
    {
        SessionKey = sessionKey,
        DriverNumber = row.DriverNumber,
        Date = row.Date,
        Speed = row.Speed ?? 0,
        Rpm = row.Rpm ?? 0,
        Gear = row.NGear ?? 0,
        Throttle = row.Throttle ?? 0,
        Brake = row.Brake ?? 0,
    };

    public static Location ToEntity(this OpenF1Location row, int sessionKey) => new()
    {
        SessionKey = sessionKey,
        DriverNumber = row.DriverNumber,
        Date = row.Date,
        X = row.X,
        Y = row.Y,
        Z = row.Z,
    };
}

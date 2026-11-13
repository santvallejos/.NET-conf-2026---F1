using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitWall.Data.Migrations;

/// <inheritdoc />
public partial class _20261113025521_InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CarData",
            columns: table => new
            {
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                DriverNumber = table.Column<int>(type: "INTEGER", nullable: false),
                Date = table.Column<string>(type: "TEXT", nullable: false),
                Speed = table.Column<int>(type: "INTEGER", nullable: false),
                Rpm = table.Column<int>(type: "INTEGER", nullable: false),
                Gear = table.Column<int>(type: "INTEGER", nullable: false),
                Throttle = table.Column<int>(type: "INTEGER", nullable: false),
                Brake = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CarData", x => new { x.SessionKey, x.DriverNumber, x.Date });
            });

        migrationBuilder.CreateTable(
            name: "Driver",
            columns: table => new
            {
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                DriverNumber = table.Column<int>(type: "INTEGER", nullable: false),
                FullName = table.Column<string>(type: "TEXT", nullable: false),
                Acronym = table.Column<string>(type: "TEXT", nullable: false),
                Team = table.Column<string>(type: "TEXT", nullable: false),
                TeamColour = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Driver", x => new { x.SessionKey, x.DriverNumber });
            });

        migrationBuilder.CreateTable(
            name: "IngestedSession",
            columns: table => new
            {
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                MeetingKey = table.Column<int>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Circuit = table.Column<string>(type: "TEXT", nullable: false),
                DateStart = table.Column<string>(type: "TEXT", nullable: false),
                DateEnd = table.Column<string>(type: "TEXT", nullable: false),
                QualiSessionKey = table.Column<int>(type: "INTEGER", nullable: true),
                IngestedAt = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IngestedSession", x => x.SessionKey);
            });

        migrationBuilder.CreateTable(
            name: "Interval",
            columns: table => new
            {
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                DriverNumber = table.Column<int>(type: "INTEGER", nullable: false),
                Date = table.Column<string>(type: "TEXT", nullable: false),
                GapToLeader = table.Column<double>(type: "REAL", nullable: true),
                Interval = table.Column<double>(type: "REAL", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Interval", x => new { x.SessionKey, x.DriverNumber, x.Date });
            });

        migrationBuilder.CreateTable(
            name: "JevCache",
            columns: table => new
            {
                StateHash = table.Column<string>(type: "TEXT", nullable: false),
                Kind = table.Column<string>(type: "TEXT", nullable: false),
                ResponseJson = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JevCache", x => x.StateHash);
            });

        migrationBuilder.CreateTable(
            name: "Lap",
            columns: table => new
            {
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                DriverNumber = table.Column<int>(type: "INTEGER", nullable: false),
                LapNumber = table.Column<int>(type: "INTEGER", nullable: false),
                DateStart = table.Column<string>(type: "TEXT", nullable: true),
                Duration = table.Column<double>(type: "REAL", nullable: true),
                S1 = table.Column<double>(type: "REAL", nullable: true),
                S2 = table.Column<double>(type: "REAL", nullable: true),
                S3 = table.Column<double>(type: "REAL", nullable: true),
                I1Speed = table.Column<int>(type: "INTEGER", nullable: true),
                I2Speed = table.Column<int>(type: "INTEGER", nullable: true),
                IsPitOutLap = table.Column<bool>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Lap", x => new { x.SessionKey, x.DriverNumber, x.LapNumber });
            });

        migrationBuilder.CreateTable(
            name: "Location",
            columns: table => new
            {
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                DriverNumber = table.Column<int>(type: "INTEGER", nullable: false),
                Date = table.Column<string>(type: "TEXT", nullable: false),
                X = table.Column<int>(type: "INTEGER", nullable: false),
                Y = table.Column<int>(type: "INTEGER", nullable: false),
                Z = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Location", x => new { x.SessionKey, x.DriverNumber, x.Date });
            });

        migrationBuilder.CreateTable(
            name: "Overtake",
            columns: table => new
            {
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                Date = table.Column<string>(type: "TEXT", nullable: false),
                OvertakingDriver = table.Column<int>(type: "INTEGER", nullable: false),
                OvertakenDriver = table.Column<int>(type: "INTEGER", nullable: false),
                Position = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Overtake", x => new { x.SessionKey, x.Date, x.OvertakingDriver, x.OvertakenDriver });
            });

        migrationBuilder.CreateTable(
            name: "PitStop",
            columns: table => new
            {
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                DriverNumber = table.Column<int>(type: "INTEGER", nullable: false),
                LapNumber = table.Column<int>(type: "INTEGER", nullable: false),
                Date = table.Column<string>(type: "TEXT", nullable: false),
                PitDuration = table.Column<double>(type: "REAL", nullable: true),
                LaneDuration = table.Column<double>(type: "REAL", nullable: true),
                StopDuration = table.Column<double>(type: "REAL", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PitStop", x => new { x.SessionKey, x.DriverNumber, x.LapNumber });
            });

        migrationBuilder.CreateTable(
            name: "Position",
            columns: table => new
            {
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                DriverNumber = table.Column<int>(type: "INTEGER", nullable: false),
                Date = table.Column<string>(type: "TEXT", nullable: false),
                Position = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Position", x => new { x.SessionKey, x.DriverNumber, x.Date });
            });

        migrationBuilder.CreateTable(
            name: "RaceControl",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                Date = table.Column<string>(type: "TEXT", nullable: false),
                LapNumber = table.Column<int>(type: "INTEGER", nullable: true),
                Category = table.Column<string>(type: "TEXT", nullable: false),
                Flag = table.Column<string>(type: "TEXT", nullable: true),
                Scope = table.Column<string>(type: "TEXT", nullable: true),
                DriverNumber = table.Column<int>(type: "INTEGER", nullable: true),
                Message = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RaceControl", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Scenario",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Scenario", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "SessionResult",
            columns: table => new
            {
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                DriverNumber = table.Column<int>(type: "INTEGER", nullable: false),
                Position = table.Column<int>(type: "INTEGER", nullable: true),
                Laps = table.Column<int>(type: "INTEGER", nullable: false),
                Dnf = table.Column<bool>(type: "INTEGER", nullable: false),
                Dns = table.Column<bool>(type: "INTEGER", nullable: false),
                Dsq = table.Column<bool>(type: "INTEGER", nullable: false),
                Duration = table.Column<double>(type: "REAL", nullable: true),
                GapToLeader = table.Column<double>(type: "REAL", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SessionResult", x => new { x.SessionKey, x.DriverNumber });
            });

        migrationBuilder.CreateTable(
            name: "StartingGrid",
            columns: table => new
            {
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                DriverNumber = table.Column<int>(type: "INTEGER", nullable: false),
                Position = table.Column<int>(type: "INTEGER", nullable: false),
                LapDuration = table.Column<double>(type: "REAL", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StartingGrid", x => new { x.SessionKey, x.DriverNumber });
            });

        migrationBuilder.CreateTable(
            name: "Stint",
            columns: table => new
            {
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                DriverNumber = table.Column<int>(type: "INTEGER", nullable: false),
                StintNumber = table.Column<int>(type: "INTEGER", nullable: false),
                LapStart = table.Column<int>(type: "INTEGER", nullable: false),
                LapEnd = table.Column<int>(type: "INTEGER", nullable: false),
                Compound = table.Column<string>(type: "TEXT", nullable: false),
                TyreAgeAtStart = table.Column<int>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Stint", x => new { x.SessionKey, x.DriverNumber, x.StintNumber });
            });

        migrationBuilder.CreateTable(
            name: "TeamRadio",
            columns: table => new
            {
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                DriverNumber = table.Column<int>(type: "INTEGER", nullable: false),
                Date = table.Column<string>(type: "TEXT", nullable: false),
                RecordingUrl = table.Column<string>(type: "TEXT", nullable: false),
                LocalPath = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TeamRadio", x => new { x.SessionKey, x.DriverNumber, x.Date });
            });

        migrationBuilder.CreateTable(
            name: "Weather",
            columns: table => new
            {
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                Date = table.Column<string>(type: "TEXT", nullable: false),
                AirTemp = table.Column<double>(type: "REAL", nullable: false),
                TrackTemp = table.Column<double>(type: "REAL", nullable: false),
                Humidity = table.Column<double>(type: "REAL", nullable: false),
                Rainfall = table.Column<bool>(type: "INTEGER", nullable: false),
                WindSpeed = table.Column<double>(type: "REAL", nullable: false),
                WindDirection = table.Column<int>(type: "INTEGER", nullable: false),
                Pressure = table.Column<double>(type: "REAL", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Weather", x => new { x.SessionKey, x.Date });
            });

        migrationBuilder.CreateTable(
            name: "InjectedEvent",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                ScenarioId = table.Column<int>(type: "INTEGER", nullable: false),
                Lap = table.Column<int>(type: "INTEGER", nullable: false),
                Type = table.Column<string>(type: "TEXT", nullable: false),
                Params = table.Column<string>(type: "TEXT", nullable: false),
                CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InjectedEvent", x => x.Id);
                table.ForeignKey(
                    name: "FK_InjectedEvent_Scenario_ScenarioId",
                    column: x => x.ScenarioId,
                    principalTable: "Scenario",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "JevDecisionLog",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                SessionKey = table.Column<int>(type: "INTEGER", nullable: false),
                ScenarioId = table.Column<int>(type: "INTEGER", nullable: true),
                DriverNumber = table.Column<int>(type: "INTEGER", nullable: false),
                Lap = table.Column<int>(type: "INTEGER", nullable: false),
                Kind = table.Column<string>(type: "TEXT", nullable: false),
                StateHash = table.Column<string>(type: "TEXT", nullable: false),
                RequestJson = table.Column<string>(type: "TEXT", nullable: false),
                ResponseJson = table.Column<string>(type: "TEXT", nullable: false),
                LatencyMs = table.Column<int>(type: "INTEGER", nullable: false),
                Tokens = table.Column<int>(type: "INTEGER", nullable: false),
                CostUsd = table.Column<double>(type: "REAL", nullable: false),
                CreatedAt = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_JevDecisionLog", x => x.Id);
                table.ForeignKey(
                    name: "FK_JevDecisionLog_Scenario_ScenarioId",
                    column: x => x.ScenarioId,
                    principalTable: "Scenario",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(
            name: "IX_InjectedEvent_ScenarioId",
            table: "InjectedEvent",
            column: "ScenarioId");

        migrationBuilder.CreateIndex(
            name: "IX_JevDecisionLog_ScenarioId",
            table: "JevDecisionLog",
            column: "ScenarioId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "CarData");

        migrationBuilder.DropTable(
            name: "Driver");

        migrationBuilder.DropTable(
            name: "IngestedSession");

        migrationBuilder.DropTable(
            name: "InjectedEvent");

        migrationBuilder.DropTable(
            name: "Interval");

        migrationBuilder.DropTable(
            name: "JevCache");

        migrationBuilder.DropTable(
            name: "JevDecisionLog");

        migrationBuilder.DropTable(
            name: "Lap");

        migrationBuilder.DropTable(
            name: "Location");

        migrationBuilder.DropTable(
            name: "Overtake");

        migrationBuilder.DropTable(
            name: "PitStop");

        migrationBuilder.DropTable(
            name: "Position");

        migrationBuilder.DropTable(
            name: "RaceControl");

        migrationBuilder.DropTable(
            name: "SessionResult");

        migrationBuilder.DropTable(
            name: "StartingGrid");

        migrationBuilder.DropTable(
            name: "Stint");

        migrationBuilder.DropTable(
            name: "TeamRadio");

        migrationBuilder.DropTable(
            name: "Weather");

        migrationBuilder.DropTable(
            name: "Scenario");
    }
}

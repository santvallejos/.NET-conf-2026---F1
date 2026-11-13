using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitWall.Data.Migrations;

/// <summary>Borra las tablas de Jev (<c>JevDecisionLog</c> y <c>JevCache</c>): Jev se quitó del proyecto (ADR-019).</summary>
public partial class _20261118120000_RemoveJev : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "JevCache");

        migrationBuilder.DropTable(
            name: "JevDecisionLog");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
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
            name: "IX_JevDecisionLog_ScenarioId",
            table: "JevDecisionLog",
            column: "ScenarioId");
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitWall.Data.Migrations;

/// <summary>Borra las tablas de escenarios (<c>InjectedEvent</c> y <c>Scenario</c>): Escenarios se quitó del proyecto (ADR-021).</summary>
public partial class _20261118130000_RemoveScenarios : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "InjectedEvent");

        migrationBuilder.DropTable(
            name: "Scenario");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
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

        migrationBuilder.CreateIndex(
            name: "IX_InjectedEvent_ScenarioId",
            table: "InjectedEvent",
            column: "ScenarioId");
    }
}

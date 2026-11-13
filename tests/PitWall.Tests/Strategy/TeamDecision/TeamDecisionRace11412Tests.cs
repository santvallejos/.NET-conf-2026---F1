using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using PitWall.Domain;
using PitWall.Ingestion.OpenF1;
using PitWall.Strategy.TeamDecision;
using PitWall.Tests.Ingestion;

namespace PitWall.Tests.Strategy.TeamDecision;

/// <summary>Decisión real derivada de los fixtures grabados de la carrera 11412 (São Paulo 2026).</summary>
public sealed class TeamDecisionRace11412Tests
{
    private const int Alpine = 43;

    [Fact]
    public void Derive_Race11412Alpine_BoxesOnLap61WithIntermediate()
    {
        var decisions = DeriveFromFixtures(Alpine);

        var lastBox = decisions.Last(decision => decision.PitCall == PitCall.Box);
        Assert.Equal(new TeamLapDecision(Alpine, 61, PitCall.Box, Compound.Intermediate), lastBox);
        Assert.All(
            decisions.Where(decision => decision.Lap is 62 or 63),
            decision => Assert.Equal(PitCall.StayOut, decision.PitCall));
    }

    [Fact]
    public void Derive_Race11412Alpine_BoxesOnEveryPitLapAndStaysOutOnTheRest()
    {
        var decisions = DeriveFromFixtures(Alpine);

        Assert.Equal(Enumerable.Range(1, 70), decisions.Select(decision => decision.Lap));
        Assert.Equal(
            [
                new TeamLapDecision(Alpine, 17, PitCall.Box, Compound.Soft),
                new TeamLapDecision(Alpine, 53, PitCall.Box, Compound.Soft),
                new TeamLapDecision(Alpine, 61, PitCall.Box, Compound.Intermediate),
            ],
            decisions.Where(decision => decision.PitCall == PitCall.Box));
        Assert.All(
            decisions.Where(decision => decision.PitCall != PitCall.Box),
            decision => Assert.Null(decision.Compound));
    }

    private static IReadOnlyList<TeamLapDecision> DeriveFromFixtures(int driverNumber)
    {
        var stints = Read(OpenF1JsonContext.Default.ListOpenF1Stint, "stints_11412.json")
            .Where(row => Enum.TryParse<Compound>(row.Compound, true, out _))
            .Select(row => new TeamStint(
                row.DriverNumber,
                row.StintNumber,
                row.LapStart,
                row.LapEnd ?? row.LapStart,
                Enum.Parse<Compound>(row.Compound!, true)));
        var pitStops = Read(OpenF1JsonContext.Default.ListOpenF1Pit, "pit_11412.json")
            .Select(row => new TeamPitStop(row.DriverNumber, row.LapNumber));

        return TeamDecisionDeriver.Derive(driverNumber, stints, pitStops);
    }

    private static List<T> Read<T>(JsonTypeInfo<List<T>> typeInfo, string fileName)
    {
        var json = File.ReadAllText(Path.Combine(OpenF1Fixtures.Directory, fileName));
        return JsonSerializer.Deserialize(json, typeInfo) ?? [];
    }
}

using System.Text.Json;

using PitWall.Domain;

namespace PitWall.Tests.Domain;

public sealed class DomainSerializationTests
{
    public static TheoryData<Type, string[]> DocumentedEnumValues => new()
    {
        { typeof(Compound), ["SOFT", "MEDIUM", "HARD", "INTERMEDIATE", "WET"] },
        { typeof(PitCall), ["STAY_OUT", "BOX", "BOX_NEXT_LAP"] },
        { typeof(PaceMode), ["PUSH", "MANAGE", "SAVE_TYRES"] },
        { typeof(EnergyMode), ["HARVEST", "BALANCED", "DEPLOY", "OVERTAKE"] },
        { typeof(Impact), ["NONE", "MONITOR", "ACT_NOW"] },
        {
            typeof(Reason),
            [
                "TYRE_DEG_HIGH", "TYRE_CLIFF_NEAR", "UNDERCUT_THREAT", "OVERCUT_WINDOW",
                "SC_DEPLOYED", "VSC_DEPLOYED", "RED_FLAG", "RAIN_STARTING", "RAIN_STOPPING",
                "TRACK_DRYING", "LOW_GRIP", "TRAFFIC_ON_EXIT", "CLEAR_AIR_ON_EXIT",
                "ENERGY_LOW", "OVERTAKE_AVAILABLE", "PUNCTURE", "WING_DAMAGE",
                "RIVAL_INCIDENT", "ERS_FAILURE", "MANDATORY_COMPOUND_PENDING",
            ]
        },
        { typeof(JevKind), ["Triage", "Strategy"] },
        { typeof(JevTrigger), ["EveryLap", "SpecificLap", "Manual", "Triage"] },
    };

    [Theory]
    [MemberData(nameof(DocumentedEnumValues))]
    public void Serialize_AllEnumValues_MatchDocumentedNames(Type enumType, string[] expected)
    {
        var actual = Enum.GetValues(enumType)
            .Cast<object>()
            .Select(value => JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(value, enumType))!)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(DocumentedEnumValues))]
    public void Deserialize_DocumentedNames_RoundTripsToSameValues(Type enumType, string[] names)
    {
        var expected = Enum.GetValues(enumType).Cast<object>().ToArray();

        var actual = names
            .Select(name => JsonSerializer.Deserialize(JsonSerializer.Serialize(name), enumType))
            .ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Serialize_JevDecision_WritesEnumsWithContractNames()
    {
        var decision = new JevDecision(
            Guid.Empty, 11412, null, 43, 60, JevKind.Strategy, JevTrigger.SpecificLap,
            PitCall.BoxNextLap, Compound.Intermediate, PaceMode.SaveTyres, EnergyMode.Balanced,
            Impact.ActNow, 4, 0.84,
            new Dictionary<string, IReadOnlyDictionary<string, double>>(),
            [Reason.RainStarting, Reason.TyreCliffNear, Reason.LowGrip],
            false, false, 212, 640, 0.0000269m);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(decision, JsonSerializerOptions.Web));
        var root = json.RootElement;

        Assert.Equal("Strategy", root.GetProperty("kind").GetString());
        Assert.Equal("SpecificLap", root.GetProperty("trigger").GetString());
        Assert.Equal("BOX_NEXT_LAP", root.GetProperty("pitCall").GetString());
        Assert.Equal("INTERMEDIATE", root.GetProperty("compound").GetString());
        Assert.Equal("SAVE_TYRES", root.GetProperty("paceMode").GetString());
        Assert.Equal("BALANCED", root.GetProperty("energyMode").GetString());
        Assert.Equal("ACT_NOW", root.GetProperty("impact").GetString());
        Assert.Equal(
            ["RAIN_STARTING", "TYRE_CLIFF_NEAR", "LOW_GRIP"],
            root.GetProperty("reasons").EnumerateArray().Select(reason => reason.GetString()!).ToArray());
    }

    [Fact]
    public void GetReferencedAssemblies_DomainAssembly_ReferencesNoPitWallAssembly()
    {
        var references = typeof(RaceState).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => reference.Name!.StartsWith("PitWall", StringComparison.Ordinal));
    }
}

using System.Text.Json;

using PitWall.Domain;

namespace PitWall.Tests.Domain;

public sealed class DomainSerializationTests
{
    public static TheoryData<Type, string[]> DocumentedEnumValues => new()
    {
        { typeof(Compound), ["SOFT", "MEDIUM", "HARD", "INTERMEDIATE", "WET"] },
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
    public void GetReferencedAssemblies_DomainAssembly_ReferencesNoPitWallAssembly()
    {
        var references = typeof(RaceState).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => reference.Name!.StartsWith("PitWall", StringComparison.Ordinal));
    }
}

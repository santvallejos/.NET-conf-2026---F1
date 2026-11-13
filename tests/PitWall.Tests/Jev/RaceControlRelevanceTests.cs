using PitWall.Domain;
using PitWall.Jev.Triage;

namespace PitWall.Tests.Jev;

public sealed class RaceControlRelevanceTests
{
    [Theory]
    [InlineData("SafetyCar", null, "Track", null, "SAFETY CAR DEPLOYED", true)]
    [InlineData("Other", null, null, null, "LOW GRIP CONDITIONS", true)]
    [InlineData("Flag", "YELLOW", "Sector", null, "YELLOW IN TRACK SECTOR 5", true)]
    [InlineData("Flag", "CLEAR", "Sector", null, "CLEAR IN TRACK SECTOR 5", false)]
    [InlineData("Flag", "BLUE", "Driver", 43, "WAVED BLUE FLAG FOR CAR 43 (COL)", false)]
    [InlineData("Other", null, "Driver", 43, "CAR 43 (COL) TIME DELETED - TRACK LIMITS", true)]
    [InlineData("Other", null, "Driver", 44, "CAR 44 (HAM) TIME DELETED - TRACK LIMITS", false)]
    public void IsRelevant_ForFocusDriver(string category, string? flag, string? scope, int? driverNumber, string text, bool expected)
    {
        var message = new RaceControlMessage(JevTestRace.RaceStart, 10, category, flag, scope, driverNumber, text);

        Assert.Equal(expected, RaceControlRelevance.IsRelevant(message, 43));
    }
}

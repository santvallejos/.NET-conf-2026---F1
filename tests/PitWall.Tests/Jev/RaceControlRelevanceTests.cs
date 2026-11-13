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
    [InlineData("Other", null, null, null, "CAR 43 (COL) TIME 1:17.853 DELETED - TRACK LIMITS AT TURN 1 LAP 52 15:12:59", true)]
    [InlineData("Other", null, null, null, "CAR 51 (CAM) TIME 1:23.728 DELETED - TRACK LIMITS AT TURN 1 LAP 18 14:29:33", false)]
    [InlineData("Other", null, null, null, "CAR 3 (VER) LAP DELETED - TRACK LIMITS AT TURN 1 LAP 62 15:24:33 (PIT)", false)]
    [InlineData("Other", null, null, null, "FIA STEWARDS: 5 SECOND TIME PENALTY FOR CAR 23 (ALB) - REJOINING UNSAFELY (14:50:05)", false)]
    [InlineData("Other", null, null, null, "INCIDENT INVOLVING CAR 43 (COL) NOTED - FALSE START - OUT OF POSITION (14:03:17)", true)]
    [InlineData("Other", null, null, null, "TURN 1 INCIDENT INVOLVING CARS 23 (ALB) AND 10 (GAS) NOTED (14:50:05)", false)]
    [InlineData("Other", null, null, null, "TURN 6 INCIDENT INVOLVING CARS 87 (BEA) AND 43 (COL) NOTED (14:44:23)", true)]
    [InlineData("Other", null, null, null, "TURN 4 INCIDENT INVOLVING CARS 1 (VER), 43 (COL) AND 16 (LEC) NOTED", true)]
    [InlineData("SafetyCar", null, "Track", null, "VIRTUAL SAFETY CAR DEPLOYED", true)]
    [InlineData("Flag", "RED", "Track", null, "RED FLAG", true)]
    [InlineData("Other", null, null, null, "RISK OF RAIN FOR THE F1 RACE IS 80%", true)]
    [InlineData("Other", null, null, null, "OVERTAKE DISABLED", true)]
    public void IsRelevant_ForFocusDriver(string category, string? flag, string? scope, int? driverNumber, string text, bool expected)
    {
        var message = new RaceControlMessage(JevTestRace.RaceStart, 10, category, flag, scope, driverNumber, text);

        Assert.Equal(expected, RaceControlRelevance.IsRelevant(message, 43));
    }
}

using PitWall.Domain;
using PitWall.Jev;

namespace PitWall.Tests.Jev;

internal static class JevTestRequests
{
    public const int SessionKey = 11412;

    // Estado de ejemplo de docs/jev.md §3.3 (vuelta 60, lluvia, gomas al límite).
    public const string StrategyStateLap60 =
        """{"lap":60,"laps_total":71,"pos":13,"gap_ahead":1.8,"gap_behind":0.9,"tyre":{"compound":"SOFT","age":42,"deg_s_per_lap":0.11,"laps_to_cliff":3},"energy_est":{"soc_pct":54,"overtake_available":false},"weather":{"rain":true,"track_c":29.1,"humidity":81},"track":{"flag":"GREEN","sc":false,"vsc":false},"race_control":["LOW GRIP CONDITIONS","OVERTAKE DISABLED"],"injected":[],"mc":[{"id":"STAY_OUT","exp_pos":14.6,"p_gain":0.08},{"id":"BOX_L_INTERMEDIATE","exp_pos":11.9,"p_gain":0.61},{"id":"BOX_L+1_INTERMEDIATE","exp_pos":12.4,"p_gain":0.49}]}""";

    // Preguntas en orden distinto del ordinal para ejercitar la normalización.
    public static JevRequest StrategyLap60() => Strategy(StrategyStateLap60);

    public static JevRequest Strategy(string state) => new(
        "jev-latest",
        state,
        new Dictionary<string, JevQuestion>
        {
            ["pit_call"] = Choice("Decide the pit call for the focus car this lap.", "STAY_OUT", "BOX", "BOX_NEXT_LAP"),
            ["compound"] = Choice("Choose the compound to fit if the focus car pits.", "SOFT", "MEDIUM", "HARD", "INTERMEDIATE", "WET"),
            ["pace_mode"] = Choice("Choose the pace mode for the focus car this lap.", "PUSH", "MANAGE", "SAVE_TYRES"),
            ["energy_mode"] = Choice("Choose the energy mode for the focus car this lap.", "HARVEST", "BALANCED", "DEPLOY", "OVERTAKE"),
        })
    {
        Kind = JevKind.Strategy,
        SessionKey = SessionKey,
    };

    /// <summary>Request de estrategia distinto por vuelta: cada uno tiene su propio hash.</summary>
    public static JevRequest StrategyForLap(int lap) =>
        Strategy(StrategyStateLap60.Replace("\"lap\":60", $"\"lap\":{lap}", StringComparison.Ordinal));

    public static JevRequest Triage(string message) => new(
        "jev-latest",
        message,
        new Dictionary<string, JevQuestion>
        {
            ["impact"] = Choice("Classify the impact of the new race control message.", "NONE", "MONITOR", "ACT_NOW"),
            ["urgency"] = new(JevQuestionType.Score, "Rate the urgency from 1 to 5."),
        })
    {
        Kind = JevKind.Triage,
        SessionKey = SessionKey,
    };

    private static JevQuestion Choice(string instructions, params string[] criteria) =>
        new(JevQuestionType.Choice, instructions, criteria);
}

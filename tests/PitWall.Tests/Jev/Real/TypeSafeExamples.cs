namespace PitWall.Tests.Jev.Real;

/// <summary>
/// Respuestas de <c>POST /v1/systemone</c> con la forma de la referencia oficial (https://docs.typesafe.ai/api/system-one):
/// <c>model</c> versionado, <c>answers</c> por nombre de pregunta con su <c>type</c> y <c>usage</c>.
/// </summary>
internal static class TypeSafeExamples
{
    public const string TestApiKey = "test-key-not-real";

    // Ejemplo "Choice" de la referencia, literal.
    public const string ToneChoiceResponse =
        """{"model":"jev-latest","answers":{"tone":{"type":"choice","choice":"calm","confidence":0.7,"probabilities":{"angry":0.15,"calm":0.8,"excited":0.05}}},"usage":{"input_tokens":120,"output_tokens":12}}""";

    // Ejemplo "Score" de la referencia, literal.
    public const string UrgencyScoreResponse =
        """{"model":"jev-latest","answers":{"urgency":{"type":"score","score":1.7,"confidence":0.55,"legend":{"0":"Can wait","1":"Needs attention this week","2":"Needs attention today"},"probabilities":{"0":0.1,"1":0.1,"2":0.8}}},"usage":{"input_tokens":120,"output_tokens":12}}""";

    // Ejemplo "Noul" de la referencia, literal.
    public const string BillingNoulResponse =
        """{"model":"jev-latest","answers":{"billing":{"type":"noul","noul":0.98}},"usage":{"input_tokens":120,"output_tokens":12}}""";

    public const string StrategyLap60Response =
        """{"model":"jev-1.14.0","answers":{"pit_call":{"type":"choice","choice":"BOX","confidence":0.76,"probabilities":{"STAY_OUT":0.09,"BOX":0.84,"BOX_NEXT_LAP":0.07}},"compound":{"type":"choice","choice":"INTERMEDIATE","confidence":0.9,"probabilities":{"SOFT":0.01,"MEDIUM":0.01,"HARD":0.01,"INTERMEDIATE":0.92,"WET":0.05}},"pace_mode":{"type":"choice","choice":"MANAGE","confidence":0.6,"probabilities":{"PUSH":0.1,"MANAGE":0.73,"SAVE_TYRES":0.17}},"energy_mode":{"type":"choice","choice":"BALANCED","confidence":0.5,"probabilities":{"HARVEST":0.2,"BALANCED":0.62,"DEPLOY":0.1,"OVERTAKE":0.08}}},"usage":{"input_tokens":640,"output_tokens":48}}""";

    // Triage con la escala 1–5 por defecto: niveles 0..4 de la API, score 3.2 → urgencia 4.2.
    public const string TriageResponse =
        """{"model":"jev-1.14.0","answers":{"impact":{"type":"choice","choice":"ACT_NOW","confidence":0.8,"probabilities":{"NONE":0.02,"MONITOR":0.11,"ACT_NOW":0.87}},"urgency":{"type":"score","score":3.2,"confidence":0.6,"legend":{"0":"1","1":"2","2":"3","3":"4","4":"5"},"probabilities":{"0":0.0,"1":0.05,"2":0.1,"3":0.45,"4":0.4}}},"usage":{"input_tokens":210,"output_tokens":20}}""";
}

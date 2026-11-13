namespace PitWall.Jev.Serialization;

/// <summary>
/// ESTIMADO: ~4 caracteres por token de entrada. Sirve para métricas y costo cuando la API no informa
/// el consumo real.
/// </summary>
public static class JevTokenEstimator
{
    private const int CharsPerToken = 4;

    public static int Estimate(string requestJson)
    {
        ArgumentNullException.ThrowIfNull(requestJson);

        return (requestJson.Length + CharsPerToken - 1) / CharsPerToken;
    }
}

namespace PitWall.Ingestion;

/// <summary>Fallo de la ingesta: OpenF1 no responde tras los reintentos, la sesión no existe o los argumentos no cuadran con los datos.</summary>
public sealed class OpenF1IngestionException : Exception
{
    public OpenF1IngestionException()
    {
    }

    public OpenF1IngestionException(string message)
        : base(message)
    {
    }

    public OpenF1IngestionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

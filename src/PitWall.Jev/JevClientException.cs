namespace PitWall.Jev;

/// <summary>Fallo de un cliente Jev (red, key ausente, respuesta inválida); nunca tumba la app.</summary>
public sealed class JevClientException : Exception
{
    public JevClientException()
    {
    }

    public JevClientException(string message)
        : base(message)
    {
    }

    public JevClientException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

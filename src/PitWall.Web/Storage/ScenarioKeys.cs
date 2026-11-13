namespace PitWall.Web.Storage;

/// <summary>
/// La tabla <c>Scenario</c> usa id entero y el contrato (<c>JevDecision.ScenarioId</c>) usa Guid: el Guid se deriva
/// del id (primeros 4 bytes) con una marca fija en el resto, así la conversión es estable en ambos sentidos.
/// </summary>
internal static class ScenarioKeys
{
    private static readonly byte[] Marker = [0x50, 0x57, 0x53, 0x43, 0x45, 0x4E, 0x41, 0x52];

    public static Guid ToGuid(int id) => new(id, 0, 0, Marker);

    /// <summary>Id de la tabla <c>Scenario</c>, o null si el Guid es null o no salió de <see cref="ToGuid"/>.</summary>
    public static int? ToDatabaseId(Guid? id)
    {
        if (id is not { } guid)
        {
            return null;
        }

        Span<byte> bytes = stackalloc byte[16];
        guid.TryWriteBytes(bytes);
        return bytes[4..8].IndexOfAnyExcept((byte)0) < 0 && bytes[8..].SequenceEqual(Marker)
            ? BitConverter.ToInt32(bytes[..4])
            : null;
    }
}

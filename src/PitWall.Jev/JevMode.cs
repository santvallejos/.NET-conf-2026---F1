using System.Text.Json.Serialization;

namespace PitWall.Jev;

/// <summary>Modo de disparo de la estrategia, <c>Jev:Mode</c> (docs/jev.md §7).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JevMode>))]
public enum JevMode
{
    /// <summary>Al cerrar cada vuelta, para cada piloto de <c>Jev:Drivers</c>.</summary>
    EveryLap,

    /// <summary>Solo en las vueltas de <c>Jev:Laps</c>.</summary>
    SpecificLaps,

    /// <summary>Solo a pedido (botón "Consultar Jev").</summary>
    Manual,
}

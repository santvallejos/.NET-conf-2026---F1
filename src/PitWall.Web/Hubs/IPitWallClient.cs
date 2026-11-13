using PitWall.Domain;
using PitWall.Web.Services;

namespace PitWall.Web.Hubs;

/// <summary>Mensajes que <see cref="PitWallHub"/> empuja a sus clientes; el nombre del método es el del evento SignalR.</summary>
public interface IPitWallClient
{
    /// <summary>Cambios del estado de carrera; el primero que recibe cada conexión es un snapshot.</summary>
    Task OnRaceStateDelta(RaceStateDelta delta);

    Task OnPlaybackChanged(PlaybackState playback);

    /// <summary>Piloto foco nuevo; el estado de los 4 pilotos ya está calculado, no hay recálculo.</summary>
    Task OnFocusChanged(int driverNumber);

    /// <summary>Estrategia recién calculada de un piloto al cerrar una vuelta.</summary>
    Task OnStrategySnapshot(StrategySnapshot snapshot);
}

using PitWall.Domain;
using PitWall.Strategy.Workers;

namespace PitWall.Web.Services;

/// <summary>Estrategia calculada de un piloto junto con el estado de carrera del instante en que se calculó.</summary>
public sealed record StrategyPublication(RaceState State, StrategyResult Result);

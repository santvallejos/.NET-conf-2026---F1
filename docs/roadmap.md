# Roadmap de desarrollo

Fases incrementales. Cada fase termina con algo ejecutable y tests verdes.

## Fase 0 — Base
- [ ] Solución `PitWall.sln` con los 8 proyectos ([architecture.md](architecture.md)).
- [ ] .NET 11, ServiceDefaults (Aspire AppHost eliminado: ADR-018).
- [ ] `.gitignore` (`data/`, `*.db`, secrets).
- [ ] `Directory.Build.props`: nullable, warnings as errors, LangVersion latest.

**Listo cuando:** `dotnet build` y `dotnet test` pasan; Web levanta vacía.

## Fase 1 — Ingesta
- [ ] `PitWall.Domain`: enums (`Compound`, ...).
- [ ] `PitWall.Data`: DbContext + entidades + migración inicial.
- [ ] `PitWall.Ingestion`: cliente OpenF1, ventanas de 10 min para `car_data`/`location`, mp3, idempotencia.
- [ ] Fixtures grabados + tests de ingesta.

**Listo cuando:** `data/pitwall.db` con sesión 11412 completa y conteos verificados.

## Fase 2 — Replay + UI mínima
- [ ] `ReplayClock` (play/pausa/velocidad/saltar) con `TimeProvider`.
- [ ] `RaceStateBuilder` + caché en memoria.
- [ ] `PitWallHub` SignalR.
- [ ] Paneles: Controles (11), Torre (1), Race Control (7), Clima (6).

**Listo cuando:** se reproduce la carrera completa offline, saltando vueltas.

## Fase 3 — Telemetría y mapa
- [ ] Paneles Telemetría (3) y Mapa (2).
- [ ] Selector de piloto + cálculo en segundo plano de los 4.
- [ ] Reproductor de radio.

## Fase 4 — Motor de estrategia
- [ ] Degradación + vida restante + panel Neumáticos (4).
- [ ] Energía estimada + panel Energía (8).
- [ ] Pit loss + panel Llamada a boxes (14).

**Listo cuando:** estrategia de los 4 pilotos al cerrar cada vuelta y tests deterministas verdes.

## Fase 5 — Pulido para la charla
- [ ] Layout charla (proyector) + layout ensayo (multi-monitor).
- [ ] Guion de demo ([spec.md](spec.md) §7.1) ensayado.
- [ ] Validación manual ([testing.md](testing.md) §5).
- [ ] Segundo GP opcional (ingesta por `session_key`).

## Pendientes externos
- [ ] Definir duración de la charla → ajustar guion.
- [ ] Inicializar git y primer commit (cuando se decida).
- [ ] Elegir GPs adicionales.

# Roadmap de desarrollo

Fases incrementales. Cada fase termina con algo ejecutable y tests verdes.

## Fase 0 — Base
- [ ] Solución `PitWall.sln` con los 10 proyectos ([architecture.md](architecture.md)).
- [ ] .NET 11, Aspire AppHost + ServiceDefaults.
- [ ] `.gitignore` (`data/`, `*.db`, secrets).
- [ ] `Directory.Build.props`: nullable, warnings as errors, LangVersion latest.

**Listo cuando:** `dotnet build` y `dotnet test` pasan; AppHost levanta Web vacía.

## Fase 1 — Ingesta
- [ ] `PitWall.Domain`: enums (`Compound`, `PitCall`, `PaceMode`, `EnergyMode`, `Impact`, `Reason`, ...).
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
- [ ] Motor de reglas + reasons.
- [ ] Monte Carlo + panel Monte Carlo (5).
- [ ] Decisión real del equipo.

**Listo cuando:** Monte Carlo < 1 s/piloto/vuelta y tests deterministas verdes.

## Fase 5 — Jev
- [ ] `IJevClient`, `FakeJevClient`, `GuardedJevClient` (caché, límites, métricas).
- [ ] Triage + estrategia; modos de disparo.
- [ ] Paneles Jev (9) y Jev vs equipo (10).
- [ ] Métricas en Aspire.
- [ ] `JevClient` real: validar request/response contra docs oficiales al obtener API key.

**Listo cuando:** carrera completa con Fake; luego con Real < US$0,05.

## Fase 6 — Escenarios
- [ ] `Scenario` + `InjectedEvent`.
- [ ] 8 eventos en MC + reglas.
- [ ] Panel de inyección + timeline + etiqueta HIPOTÉTICO.

## Fase 7 — Pulido para la charla
- [ ] Layout charla (proyector) + layout ensayo (multi-monitor).
- [ ] Guion de demo ([scenarios.md](scenarios.md) §5) ensayado.
- [ ] Validación manual ([testing.md](testing.md) §5).
- [ ] Segundo GP opcional (ingesta por `session_key`).

## Pendientes externos
- [ ] Obtener `TYPESAFE_API_KEY` (waitlist).
- [ ] Definir duración de la charla → ajustar guion.
- [ ] Inicializar git y primer commit (cuando se decida).
- [ ] Elegir GPs adicionales.

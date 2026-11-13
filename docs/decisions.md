# Registro de decisiones (ADR)

Formato corto: contexto → decisión → consecuencia. Fecha: 2026-11-10 salvo indicación.

### ADR-001 — .NET 11
Charla de .NET Conf 2026. → Usar .NET 11 (última versión). → Mostrar features nuevas; riesgo de librerías aún sin soporte.

### ADR-002 — Datos reales en replay, sin simular la carrera
OpenF1 no simula; solo da datos. → El replay reproduce la carrera real tal cual. → Credibilidad; los what-if no alteran el replay (ver ADR-010).

### ADR-003 — Offline con SQLite
Wifi del evento no confiable. → CLI de ingesta descarga una vez a SQLite + mp3. → Todo funciona sin red excepto Jev real.

### ADR-004 — Blazor
Mantener todo en C#; parecerse a sistemas de equipos. → Blazor Interactive Server + SignalR. → Un solo lenguaje en la charla.

### ADR-005 — .NET Aspire (reemplazada por ADR-018)
Mostrar observabilidad en escenario. → Aspire AppHost + dashboard (latencia y costo de Jev). → Dependencia de Aspire en dev.

### ADR-006 — Pilotos: foco + segundo plano
Colapinto principal; cambio a Hamilton/Antonelli/Verstappen. → Los 4 se calculan siempre en segundo plano; cambio instantáneo sin recálculo. Resto de la parrilla solo timing/posiciones/stints. → Más CPU, UI inmediata.

### ADR-007 — Jev vía HTTP con cliente propio (reemplazada por ADR-019)
Mantener todo en C#; paquetes NuGet de Jev son comunitarios (no oficiales). → Cliente propio `IJevClient` (`JevClient` real + `FakeJevClient`). → Control total; hay que validar la API contra la doc oficial.

### ADR-008 — Jev como "System One", Monte Carlo como "System Two" (reemplazada por ADR-019 y ADR-020)
Jev es un clasificador; Monte Carlo ya calcula números. → Reglas (C#) = restricciones; Monte Carlo = probabilidades; Jev = decisión rápida fusionando números + texto libre de Race Control. Dos usos: triage y estrategia (4 preguntas). → Jev aporta donde un modelo numérico no llega (texto). Se presenta como propuesta, no práctica real de F1.

### ADR-009 — `reasons` del motor de reglas (reemplazada por ADR-021)
Jev no devuelve razones. → Enum cerrado de `Reason` calculado en C#. → Contrato sin texto libre y explicable.

### ADR-010 — Eventos inyectados: solo decisión (reemplazada por ADR-020 y ADR-021)
Carrera real sin SC/VSC/bandera roja. → 8 eventos hipotéticos afectan solo Monte Carlo + Jev + UI; replay sigue real; sin fork simulado. → Simple y honesto; las posiciones posteriores no reflejan el evento.

### ADR-011 — Jev con internet en la charla (reemplazada por ADR-019)
Se confirmó conectividad. → Sin modo "grabado". `Fake` para desarrollo, `Real` para charla. → Si cae la red, Jev se deshabilita con alerta.

### ADR-012 — Minimizar costo de Jev (reemplazada por ADR-019)
Requisito del usuario: lo más barato posible. → `Fake` en desarrollo, caché por hash de estado, `MaxRequestsPerSession`, on/off global y por piloto, modos de disparo, triage antes de estrategia, `state` compacto, métrica de costo `pitwall.jev.cost_usd`. → Carrera completa < US$0,02 estimado; re-ensayos $0.

### ADR-013 — Energía 2026 estimada
OpenF1 no expone ERS. → Modelo simple desde `car_data`, marcado ESTIMADO. → Habilita `energy_mode` y overtake mode; no es dato real.

### ADR-014 — Degradación por regresión sobre datos reales
→ `lap_time ~ tyre_age` con corrección de combustible por piloto/compuesto. → Sin parámetros inventados.

### ADR-015 — Multi-GP extensible
→ Ingesta por `session_key`; la UI lista sesiones ingeridas. Brasil 2026 primero; otros GPs a definir.

### ADR-016 — Tests sin UI
→ xUnit sobre lógica determinista + ingesta con fixtures. Ningún test llama APIs reales.

### ADR-017 — Git diferido
→ Versionado y primer commit se harán cuando el usuario lo decida.

### ADR-018 — Sin Aspire
Pedido del usuario (2026-11-17): arranque simple y la charla enfocada en la app, sin mostrar otra herramienta. → Se elimina `PitWall.AppHost`; Web arranca sola con `dotnet run --project src/PitWall.Web` y la API key va por User Secrets de Web. `PitWall.ServiceDefaults` se mantiene (resiliencia HTTP, health checks, OpenTelemetry con export OTLP solo si hay `OTEL_EXPORTER_OTLP_ENDPOINT`). → Sin dashboard en escenario; se elimina el criterio de demo "costo visible en Aspire". Reemplaza ADR-005.

### ADR-019 — Quitar Jev (reemplazada en parte por ADR-020 y ADR-021)
Decisión del usuario (2026-11-18): simplificar el pit wall para la exposición. No hay nada confirmado de que un pit wall real use un agente tipo Jev, y la charla trata sobre cómo deciden los equipos. → Se quita Jev entero: proyecto `PitWall.Jev`, ventana `/jev`, paneles Jev y Jev vs equipo, "Jev por evento", "Consultar Jev", sección `Jev` de configuración, User Secrets de Web, métricas `pitwall.jev.*`, enums `JevKind`, `JevTrigger`, `Impact` y `PaceMode`, y las tablas `JevDecisionLog`/`JevCache` (migración `RemoveJev`). La decisión visible es la de reglas + Monte Carlo; los escenarios recalculan la estrategia hipotética sin triage. → Demo 100% offline y sin costo por consulta; se retiran los requisitos F10, F12–F14, RF-05, RF-08, RF-09, RNF-04 y RNF-05. Reemplaza ADR-007, ADR-008, ADR-011 y ADR-012; ADR-009 y ADR-010 siguen vigentes sin la parte de Jev.

### ADR-020 — Quitar Monte Carlo (reemplazada en parte por ADR-021)
Decisión del usuario (2026-11-18): simplificar el pit wall para la exposición. En el pit wall real no hay una pantalla de estrategia Monte Carlo: la estrategia probabilística la lleva el estratega de cada piloto. → Se quita el simulador y lo que existía solo para él: `PitWall.Strategy/MonteCarlo` (simulador, candidatas, `RainSignals`), el panel Monte Carlo (5, `/panel/montecarlo`), la recomendación "SEGUIR AFUERA"/"BOX V{n}" de la llamada a boxes, `StrategyOutcome`, las restricciones duras del motor de reglas (`Constraint`, `ConstraintRules`, `RaceControlSignals`, enums `PitCall` y `EnergyMode`), la decisión real del equipo (`TeamDecision`, sin uso desde ADR-019), la sección `Strategy:MonteCarlo`, los efectos de escenario que solo alimentaban la simulación (pit loss bajo neutralización, vuelta lenta con pinchazo, pérdidas de ritmo, probabilidad de SC del choque) con sus claves de `Scenarios`, y la métrica `pitwall.montecarlo.duration`. Quedan degradación, energía (ESTIMADO), pit loss (ahora en `Strategy:PitCall`), las `reasons` (ADR-009), la llamada a boxes determinista (posición al salir y posiciones perdidas normal/SC/VSC) y los escenarios, que modifican el estado hipotético y las reglas. → La vista Estrategia queda en Llamada a boxes + Neumáticos; se retiran F9, RNF-03 y RNF-06. Reemplaza ADR-008; de ADR-010 sigue vigente que los eventos solo afectan la decisión y no el replay; de ADR-019, que la decisión visible ya no incluye Monte Carlo.

### ADR-021 — Quitar Escenarios (inyección de eventos)
Decisión del usuario (2026-11-18): simplificar el pit wall para la exposición. El pit wall muestra solo el replay real de la carrera. → Se quitan los escenarios y el modo HIPOTÉTICO: `PitWall.Strategy/Scenarios` (`ScenarioEffects`, `InjectedEvent`, `ScenarioOptions`, `Neutralisation`, `RivalRelative`, `IScenarioStore`), el panel de Escenarios (`/panel/scenarios`, `ScenarioInjector`, `ScenarioTimeline`) y el desplegable `ScenarioDrawer` de la barra común, `ScenarioService` y la estrategia hipotética (`ScenarioStrategyStore`, recálculo en `StrategyWorker`), los métodos del hub `GetScenarios`, `CreateScenario`, `SelectScenario`, `InjectEvent`, `GetHypotheticalStrategy`, `OnScenarioChanged` y `OnHypotheticalStrategySnapshot`, la sección `Scenarios` de configuración, la etiqueta HIPOTÉTICO de la llamada a boxes, `StrategySnapshot.Hypothetical` y las tablas `Scenario`/`InjectedEvent` (migración `RemoveScenarios`). El motor de reglas (`RulesEngine`, `RuleFeatures`, `RulesOptions`, `Reason`, `DamageSeverity`, `RainIntensity`) y `StrategySnapshot.Reasons` se quitan también: tras ADR-020 sus razones solo se mostraban en la timeline de Escenarios, y sin ella no las mostraba ninguna pantalla. Con él se va `Strategy:Rules`. → Quedan degradación, energía (ESTIMADO), pit loss y la llamada a boxes determinista sobre el estado real (posición al salir y posiciones perdidas normal/SC/VSC). El guion de demo pasa a `spec.md` §7.1 y usa solo el replay real; se retiran F8, F11 y RF-06. Reemplaza ADR-009 y ADR-010; de ADR-019 y ADR-020, que siguen las `reasons` y los escenarios.

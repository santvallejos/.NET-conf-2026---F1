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

### ADR-005 — .NET Aspire
Mostrar observabilidad en escenario. → Aspire AppHost + dashboard (latencia y costo de Jev). → Dependencia de Aspire en dev.

### ADR-006 — Pilotos: foco + segundo plano
Colapinto principal; cambio a Hamilton/Antonelli/Verstappen. → Los 4 se calculan siempre en segundo plano; cambio instantáneo sin recálculo. Resto de la parrilla solo timing/posiciones/stints. → Más CPU, UI inmediata.

### ADR-007 — Jev vía HTTP con cliente propio
Mantener todo en C#; paquetes NuGet de Jev son comunitarios (no oficiales). → Cliente propio `IJevClient` (`JevClient` real + `FakeJevClient`). → Control total; hay que validar la API contra la doc oficial.

### ADR-008 — Jev como "System One", Monte Carlo como "System Two"
Jev es un clasificador; Monte Carlo ya calcula números. → Reglas (C#) = restricciones; Monte Carlo = probabilidades; Jev = decisión rápida fusionando números + texto libre de Race Control. Dos usos: triage y estrategia (4 preguntas). → Jev aporta donde un modelo numérico no llega (texto). Se presenta como propuesta, no práctica real de F1.

### ADR-009 — `reasons` del motor de reglas
Jev no devuelve razones. → Enum cerrado de `Reason` calculado en C#. → Contrato sin texto libre y explicable.

### ADR-010 — Eventos inyectados: solo decisión
Carrera real sin SC/VSC/bandera roja. → 8 eventos hipotéticos afectan solo Monte Carlo + Jev + UI; replay sigue real; sin fork simulado. → Simple y honesto; las posiciones posteriores no reflejan el evento.

### ADR-011 — Jev con internet en la charla
Se confirmó conectividad. → Sin modo "grabado". `Fake` para desarrollo, `Real` para charla. → Si cae la red, Jev se deshabilita con alerta.

### ADR-012 — Minimizar costo de Jev
Requisito del usuario: lo más barato posible. → `Fake` en desarrollo, caché por hash de estado, `MaxRequestsPerSession`, on/off global y por piloto, modos de disparo, triage antes de estrategia, `state` compacto, contador de costo en Aspire. → Carrera completa < US$0,02 estimado; re-ensayos $0.

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

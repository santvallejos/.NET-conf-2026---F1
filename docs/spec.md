# Spec — PitWall .NET (F1 + Jev)

> Estado: **aprobado para desarrollo** · Última actualización: 2026-11-10

## 1. Propósito

Proyecto demo para una charla de .NET Conf 2026. Tesis: **cómo los equipos de Fórmula 1 toman decisiones en tiempo real y cómo la tecnología (.NET + IA) las asiste**.

Se construye un *pit wall* (muro de boxes) que reproduce una carrera real con datos de OpenF1, calcula estrategia (degradación, energía, Monte Carlo) y consulta a **Jev**, un clasificador de IA que devuelve decisiones tipadas para que las consuma código, no un humano.

> Jev **no** tiene uso oficial documentado en la F1. En la charla se presenta como **propuesta**, no como práctica real de los equipos.

## 2. Alcance

### 2.1 Incluido (v1)

| # | Funcionalidad | Detalle |
|---|---|---|
| F1 | Ingesta OpenF1 | CLI que descarga una sesión por `session_key` a SQLite + mp3 de radio. Extensible a cualquier GP. |
| F2 | Replay | Reproducción de la carrera **real**: play/pausa, velocidad x1/x5/x20, saltar a vuelta N. Sin internet. |
| F3 | Pilotos | Principal: **Colapinto (43)**. Segundo plano: Hamilton (44), Antonelli (12), Verstappen (3). Cambio instantáneo, sin recalcular. |
| F4 | Telemetría | Velocidad, throttle, freno, marcha, RPM, posición en mapa. |
| F5 | Neumáticos | Stints, degradación por regresión sobre vueltas reales, vida restante. |
| F6 | Energía 2026 | Modelo **estimado** (OpenF1 no expone ERS). Marcado "estimado" en UI. |
| F7 | Pista y clima | Temperaturas, lluvia, humedad, viento; mensajes de Race Control y banderas. |
| F8 | Motor de reglas | Restricciones duras + generación de `reasons`. |
| F9 | Monte Carlo | Proyección del resto de carrera por estrategia candidata. Input de Jev. |
| F10 | Jev | Triage de eventos + llamada de estrategia. Disparo configurable. |
| F11 | Eventos inyectables | 8 eventos hipotéticos. Solo afectan decisión, no el replay. |
| F12 | Panel Jev | Historial propio de decisiones de Jev. |
| F13 | Jev vs equipo | Comparación decisión Jev vs decisión real del equipo. |
| F14 | Guardas de costo | Caché, límite de requests, contador de tokens, on/off. |

### 2.2 Fuera de alcance (v1)

- Datos en vivo (OpenF1 real-time es pago).
- Fork simulado de carrera (generar vueltas sintéticas tras un evento).
- Cálculo completo para los 18 pilotos restantes (solo timing/posiciones/stints como tráfico).
- Tests de UI.
- Autenticación de usuarios.

## 3. Datos de la carrera

| Campo | Valor |
|---|---|
| GP | São Paulo Grand Prix 2026 — Interlagos (`circuit_key=14`) |
| `meeting_key` | 1299 |
| Carrera `session_key` | **11412** (2026-11-08, 17:00–19:00 UTC, 71 vueltas) |
| Clasificación `session_key` | 11408 (fuente de `starting_grid`) |
| Sprint | No hubo |

| Piloto | Nº | Equipo | `team_colour` | Resultado |
|---|---|---|---|---|
| Franco Colapinto | 43 | Alpine | `00A1E8` | P13 (+1 vuelta) |
| Lewis Hamilton | 44 | Ferrari | `ED1131` | P9 (+1 vuelta) |
| Kimi Antonelli | 12 | Mercedes | `00D7B6` | P5 |
| Max Verstappen | 3 | Red Bull Racing | `4781D7` | P2 |

Hechos relevantes de la carrera real:
- Largada con **INTERMEDIATE**; paso a **SOFT** en vueltas 15–18; vuelta a **INTERMEDIATE** en 62–63 (vueltas del líder; Alpine (43), a una vuelta, para en su vuelta 61).
- Lluvia al final (18:26–18:43 UTC). "LOW GRIP" + "OVERTAKE DISABLED" al inicio y desde vuelta 62.
- **No hubo SC, VSC ni bandera roja** → motivo de los eventos inyectables.

Detalle en [data-openf1.md](data-openf1.md).

## 4. Requisitos funcionales

### RF-01 Ingesta
- `pitwall-ingest --session 11412 [--quali 11408] [--drivers 43,44,12,3]`.
- Descarga completa: `laps`, `stints`, `pit`, `race_control`, `weather`, `intervals`, `position`, `overtakes`, `session_result`, `team_radio`, `drivers`, `starting_grid` (desde quali).
- `car_data` y `location` **solo** para los 4 pilotos, filtrado por `driver_number` y rango de `date` de la carrera.
- Descarga mp3 de `team_radio` a disco.
- Idempotente: re-ejecutar no duplica.
- Agregar otro GP = otra ejecución con otro `session_key`.

### RF-02 Replay
- Reloj de carrera virtual desacoplado del reloj real.
- Controles: play, pausa, velocidad (x1, x5, x20), saltar a vuelta N.
- Emite un `RaceState` por tick a los suscriptores (UI, estrategia, Jev).
- Funciona 100% offline.

### RF-03 Cálculo por piloto
- Los 4 pilotos se calculan en segundo plano en paralelo.
- Cambiar piloto en UI **no** dispara recálculo: muestra el estado ya calculado.

### RF-04 Estrategia
- Degradación: regresión `lap_time ~ tyre_age` por compuesto/piloto con corrección por combustible. Ver [strategy-engine.md](strategy-engine.md).
- Energía estimada desde throttle/freno/velocidad.
- Motor de reglas: restricciones duras y `reasons`.
- Monte Carlo por vuelta: N=10.000 iteraciones por estrategia candidata.

### RF-05 Jev
- Dos usos: **triage** (eventos/mensajes) y **estrategia** (4 preguntas).
- Modos de disparo: `EveryLap`, `SpecificLaps`, `Manual` (botón).
- On/off global y por piloto.
- Salida: contrato JSON cerrado. Ver [jev.md](jev.md).

### RF-06 Eventos inyectables
- 8 eventos: SC, VSC, bandera roja, lluvia (leve/fuerte), pinchazo propio, daño de alerón, choque de rival (adelante/atrás), falla de energía/ERS.
- Inyectables varias veces, en cualquier vuelta.
- Solo afectan Monte Carlo + Jev + UI. El replay real sigue igual.
- UI marca todo como **HIPOTÉTICO**. Timeline de inyecciones con decisión de Jev.

### RF-07 Paneles
11 paneles. Ver [ui.md](ui.md).

### RF-08 Jev vs equipo
- Decisión real del equipo derivada de `pit` + `stints` (vuelta de parada, compuesto montado).
- Comparación por vuelta: coincide / difiere / Jev adelantado N vueltas / Jev atrasado N vueltas.

### RF-09 Guardas de costo
- Caché por hash de estado → mismo estado = 0 llamadas.
- `MaxRequestsPerSession` (default 500) → corte duro.
- Contador de requests y tokens en dashboard Aspire.
- `FakeJevClient` para desarrollo (costo $0).

## 5. Requisitos no funcionales

| ID | Requisito |
|---|---|
| RNF-01 | .NET 11, C# exclusivamente (sin otros lenguajes en el backend). |
| RNF-02 | Demo offline excepto Jev real. |
| RNF-03 | Monte Carlo de una vuelta (todas las estrategias, 1 piloto) < 1 s. |
| RNF-04 | Costo Jev por carrera completa (4 pilotos, cada vuelta) < US$0.05. |
| RNF-05 | API key de Jev nunca en el repo: User Secrets / parámetro Aspire. |
| RNF-06 | Determinismo en tests: Monte Carlo con semilla fija. |
| RNF-07 | UI oscura, densa, legible en proyector. |

## 6. Stack

| Capa | Tecnología |
|---|---|
| Runtime | .NET 11 |
| Orquestación | .NET Aspire |
| UI | Blazor (Interactive Server) + SignalR |
| Datos | SQLite + EF Core |
| IA | Jev (TypeSafe AI) vía HTTP, cliente propio |
| Tests | xUnit |

## 7. Criterios de aceptación de la demo

1. Arranca con `dotnet run --project src/PitWall.AppHost` sin internet y muestra Brasil 2026.
2. Replay salta a vuelta 60 y los paneles se actualizan coherentes.
3. Cambiar a Verstappen es instantáneo.
4. Inyectar SC en vuelta 40 → Jev devuelve decisión en < 1 s, marcada HIPOTÉTICO.
5. Panel Jev vs equipo muestra la parada real de Alpine (BOX en su vuelta 61, INTERMEDIATE) vs la llamada de Jev.
6. Contador de costo visible en Aspire y < US$0.05 tras una carrera completa.

## 8. Documentos relacionados

- [architecture.md](architecture.md) · [data-openf1.md](data-openf1.md) · [strategy-engine.md](strategy-engine.md)
- [jev.md](jev.md) · [scenarios.md](scenarios.md) · [ui.md](ui.md)
- [configuration.md](configuration.md) · [testing.md](testing.md) · [roadmap.md](roadmap.md)
- [decisions.md](decisions.md) · [glossary.md](glossary.md)

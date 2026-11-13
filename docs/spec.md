# Spec — PitWall .NET

> Estado: **aprobado para desarrollo** · Última actualización: 2026-11-18

## 1. Propósito

Proyecto demo para una charla de .NET Conf 2026. Tesis: **cómo los equipos de Fórmula 1 toman decisiones en tiempo real y cómo la tecnología (.NET) las asiste**.

Se construye un *pit wall* (muro de boxes) que reproduce una carrera real con datos de OpenF1 y calcula estrategia (degradación, energía y pit loss) para cada piloto seguido: qué pasa si entra a boxes ahora (posición al salir y posiciones perdidas, normal y bajo SC/VSC). La estrategia probabilística la lleva el estratega de cada piloto, no el muro (ADR-020). El muro muestra solo el replay real de la carrera, sin eventos hipotéticos (ADR-021).

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

### 2.2 Fuera de alcance (v1)

- Datos en vivo (OpenF1 real-time es pago).
- Fork simulado de carrera (generar vueltas sintéticas tras un evento).
- Eventos hipotéticos sobre la carrera (what-if): SC, lluvia, pinchazo… inyectados a mano (ADR-021).
- Cálculo completo para los 18 pilotos restantes (solo timing/posiciones/stints como tráfico).
- Tests de UI.
- Autenticación de usuarios.

> Los IDs retirados con ADR-019 (F10, F12–F14, RF-05, RF-08, RF-09, RNF-04, RNF-05) y ADR-020 (F9, RNF-03, RNF-06) y ADR-021 (F8, F11, RF-06) no se reutilizan.

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
- **No hubo SC, VSC ni bandera roja**. La llamada a boxes igual muestra cuánto perdería el foco parando bajo SC o VSC.

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
- Emite un `RaceState` por tick a los suscriptores (UI, estrategia).
- Funciona 100% offline.

### RF-03 Cálculo por piloto
- Los 4 pilotos se calculan en segundo plano en paralelo.
- Cambiar piloto en UI **no** dispara recálculo: muestra el estado ya calculado.

### RF-04 Estrategia
- Degradación: regresión `lap_time ~ tyre_age` por compuesto/piloto con corrección por combustible. Ver [strategy-engine.md](strategy-engine.md).
- Energía estimada desde throttle/freno/velocidad.
- Pit loss: media de `pit.lane_duration` de la sesión; llamada a boxes determinista (posición al salir y posiciones perdidas normal/SC/VSC).

### RF-07 Paneles
11 paneles. Ver [ui.md](ui.md).

## 5. Requisitos no funcionales

| ID | Requisito |
|---|---|
| RNF-01 | .NET 11, C# exclusivamente (sin otros lenguajes en el backend). |
| RNF-02 | Demo 100% offline. |
| RNF-07 | UI oscura, densa, legible en proyector. |

## 6. Stack

| Capa | Tecnología |
|---|---|
| Runtime | .NET 11 |
| UI | Blazor (Interactive Server) + SignalR |
| Datos | SQLite + EF Core |
| Tests | xUnit |

## 7. Criterios de aceptación de la demo

1. Arranca con `dotnet run --project src/PitWall.Web` sin internet y muestra Brasil 2026.
2. Replay salta a vuelta 60 y los paneles se actualizan coherentes.
3. Cambiar a Verstappen es instantáneo.
4. En Estrategia, la llamada a boxes del foco muestra pit loss, posición al salir y posiciones perdidas normal/SC/VSC, con la etiqueta ESTIMADO.

### 7.1 Guion sugerido

Todo sobre el replay real de la carrera.

1. Vueltas 15–18: cambio de intermedio a blando. Timing muestra las paradas y Neumáticos el stint nuevo y su degradación.
2. Saltar a la vuelta 40, vista Estrategia: llamada a boxes de Colapinto con el pit loss medio de la sesión. Comparar
   cuántas posiciones pierde parando normal y cuántas bajo SC o VSC.
3. Cambiar el foco a Verstappen: instantáneo, sin recalcular.
4. Vueltas 60–63, vista Condiciones: vuelve la lluvia, Race Control marca "LOW GRIP" y "OVERTAKE DISABLED", y se
   escucha la radio. Alpine para en su vuelta 61 a intermedio.

## 8. Documentos relacionados

- [architecture.md](architecture.md) · [data-openf1.md](data-openf1.md) · [strategy-engine.md](strategy-engine.md)
- [ui.md](ui.md)
- [configuration.md](configuration.md) · [testing.md](testing.md) · [roadmap.md](roadmap.md)
- [decisions.md](decisions.md) · [glossary.md](glossary.md)

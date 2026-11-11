# UI — Pit wall

Blazor Interactive Server. Estado empujado por SignalR (`PitWallHub`). Inspiración: pantallas de estrategia reales (dashboards por rol, densos, oscuros). No hay fuentes primarias del layout exacto de un equipo; es una interpretación.

## 1. Principios visuales

- Tema oscuro, alto contraste, legible en proyector (tipografía ≥ 14 px, números monoespaciados).
- Densidad alta: mucha info por pantalla, sin decoración.
- Color de equipo (`team_colour`) como acento del piloto foco.
- Etiquetas fijas: **ESTIMADO** (energía), **HIPOTÉTICO** (escenarios), **CACHÉ** (Jev desde caché), **FAKE** (cliente fake).
- Colores de compuesto: SOFT rojo, MEDIUM amarillo, HARD blanco, INTERMEDIATE verde, WET azul.
- Banderas: verde, amarilla, doble amarilla, SC/VSC, roja, azul — barra de estado superior.

## 2. Paneles (11)

| # | Panel | Contenido | Fuente |
|---|---|---|---|
| 1 | **Torre de tiempos** | Pos, piloto, gap líder, intervalo, compuesto + edad, paradas | `position`, `intervals`, `stints` |
| 2 | **Mapa del circuito** | Trazado Interlagos + autos en vivo, foco resaltado | `location` |
| 3 | **Telemetría** | Velocidad, throttle, freno, marcha, RPM vs distancia/tiempo; comparar vuelta actual vs mejor | `car_data` |
| 4 | **Neumáticos** | Stints (barra por compuesto), curva de degradación, vida restante, cliff | Strategy §1 |
| 5 | **Monte Carlo** | Por estrategia: posición esperada, distribución (histograma), P(ganar/perder) | Strategy §4 |
| 6 | **Clima** | Temp pista/aire, lluvia, humedad, viento; serie temporal | `weather` |
| 7 | **Race Control** | Feed de mensajes y banderas, con triage de Jev por mensaje | `race_control` + Jev triage |
| 8 | **Energía (ESTIMADO)** | SoC, recuperado/desplegado por vuelta, overtake disponible | Strategy §2 |
| 9 | **Jev** | Última decisión grande + historial (vuelta, decisión, confianza, reasons, latencia, caché) + botón "Consultar Jev" | `JevDecisionLog` |
| 10 | **Jev vs equipo** | Por vuelta: decisión Jev vs real (pit + compuesto), coincide/difiere, adelanto/atraso | Strategy §5 + Jev |
| 11 | **Controles** | Sesión, escenario, play/pausa, velocidad, saltar a vuelta, selector piloto, panel de inyección de eventos, toggle Jev | — |

Extra: **reproductor de radio**: marcadores en la timeline del replay; click reproduce el mp3 local.

## 3. Layouts

### Charla (1 pantalla, proyector)
- Barra superior: banderas + vuelta + reloj + controles (11).
- Grilla 3×2 configurable con pestañas por zona:
  - Izq: Torre (1) · Mapa (2)
  - Centro: Monte Carlo (5) · Neumáticos (4) / Telemetría (3)
  - Der: Jev (9) · Jev vs equipo (10) / Race Control (7)
- Clima (6) y Energía (8) como widgets compactos.

### Ensayo (multi-monitor)
- Rutas independientes por panel (`/panel/tower`, `/panel/jev`, …) para abrir en ventanas separadas.
- Todas comparten el mismo estado de sesión vía SignalR.

## 4. Interacción

| Acción | Efecto |
|---|---|
| Cambiar piloto | Instantáneo (estado ya calculado en segundo plano) |
| Saltar a vuelta N | Replay reposiciona; paneles se recalculan desde caché/estado |
| Inyectar evento | Ver [scenarios.md](scenarios.md) |
| "Consultar Jev" | Fuerza estrategia ahora (modo `Manual` o cualquiera) |
| Toggle Jev | Activa/desactiva `Jev:Enabled` en caliente |

## 5. Componentes Blazor

- `PitWallLayout`, `FlagBar`, `TimingTower`, `TrackMap` (SVG), `TelemetryChart`, `TyrePanel`, `MonteCarloPanel`, `WeatherWidget`, `RaceControlFeed`, `EnergyWidget`, `JevPanel`, `JevVsTeamPanel`, `ReplayControls`, `ScenarioInjector`, `RadioPlayer`.
- Gráficos: SVG propio para mapa; librería de charts Blazor a definir en fase UI (requisito: actualización frecuente sin re-render completo).

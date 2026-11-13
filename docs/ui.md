# UI — Pit wall

Blazor Interactive Server. Estado empujado por SignalR (`PitWallHub`). Inspiración: pantallas de estrategia reales (dashboards por rol, densos, oscuros). No hay fuentes primarias del layout exacto de un equipo; es una interpretación.

## 1. Principios visuales

- Tema oscuro, alto contraste, legible en proyector (tipografía ≥ 14 px, números monoespaciados).
- Densidad alta: mucha info por pantalla, sin decoración.
- Color de equipo (`team_colour`) como acento del piloto foco.
- Etiqueta fija: **ESTIMADO** (energía, llamada a boxes).
- Colores de compuesto: SOFT rojo, MEDIUM amarillo, HARD blanco, INTERMEDIATE verde, WET azul.
- Colores de timing (tiempos de sector y de vuelta): **púrpura** = mejor de la sesión, **verde** = mejor personal, **amarillo** = sin mejora.
- Banderas: verde, amarilla, doble amarilla, SC/VSC, roja, azul — barra de estado superior.

## 2. Paneles (11)

Los números 9 y 10 (ADR-019) y 5 (ADR-020) quedaron libres; el resto se conserva para no romper referencias.

| # | Panel | Contenido | Fuente |
|---|---|---|---|
| 1 | **Torre de tiempos** | Pos, piloto, gap líder, intervalo, compuesto + edad, paradas, S1/S2/S3 y última vuelta (colores de timing §1) | `position`, `intervals`, `stints`, `laps` |
| 2 | **Mapa del circuito** | Trazado Interlagos + autos en vivo, foco resaltado | `location` |
| 3 | **Telemetría** | Velocidad, throttle, freno, marcha, RPM vs distancia/tiempo; comparar vuelta actual vs mejor | `car_data` |
| 4 | **Neumáticos** | Stints (barra por compuesto), curva de degradación, vida restante, cliff | Strategy §1 |
| 6 | **Clima** | Temp pista/aire, lluvia, humedad, viento, presión. Ampliado en Condiciones: gráfico por variable + delta vs inicio de sesión; compacto solo en `/panel/weather` | `weather` |
| 7 | **Race Control** | Feed de mensajes y banderas | `race_control` |
| 8 | **Energía (ESTIMADO)** | SoC, recuperado/desplegado por vuelta, overtake disponible | Strategy §2 |
| 11 | **Controles** | Sesión, play/pausa, velocidad, saltar a vuelta, selector piloto (en la barra común, §3) | — |
| 12 | **Mejores sectores** | Ranking de la parrilla por mejor S1, mejor S2, mejor S3, mejor vuelta y vuelta ideal (= suma de los mejores sectores propios); foco resaltado | `laps` |
| 13 | **Ritmo de carrera** | Historial vuelta a vuelta del foco y de un rival elegible: tiempo, S1/S2/S3, compuesto, colores de timing §1; marcas de in lap, out lap y vueltas neutralizadas (SC/VSC/amarilla/roja) | `laps`, `stints`, `race_control` |
| 14 | **Llamada a boxes (ESTIMADO)** | Foco: pit loss, posición al salir y posiciones perdidas si entra ahora, normal y bajo SC/VSC, sobre el estado real del replay. Solo lectura | Strategy §4 |

Extras:

- **Reproductor de radio**: marcadores en la timeline del replay; click reproduce el mp3 local.

## 3. Layouts

El layout se inspira en cómo un equipo reparte la info entre los monitores del pit wall (timing + GPS; clima + Race Control; monitor "confidencial" con datos del auto y ventana de llamada a boxes). La charla usa un solo proyector: cada monitor es una **vista** que se cambia con pestañas o teclado.

### Barra común (todas las vistas)
- Banderas + vuelta + reloj, controles de replay, selector de piloto, radio y pestañas de vista.
- Atajos **1–4** cambian de vista; no actúan con el foco dentro de un campo de texto (`input`, `textarea`, `select`).

### Vistas (charla, 1 pantalla, proyector)

| Tecla | Ruta | Vista | Paneles |
|---|---|---|---|
| 1 | `/timing` | **Timing** | Torre (1), Mejores sectores (12), Ritmo de carrera (13) |
| 2 | `/gps` | **GPS** | Mapa (2) a pantalla completa |
| 3 | `/car` | **Auto** | Telemetría (3), Energía (8), Neumáticos (4) |
| 4 | `/conditions` | **Condiciones** | Clima ampliado (6), Race Control (7), Radio |

- No hay vista de resumen: `/` redirige a `/timing`.
- Cada vista es un componente propio sobre el mismo `PitWallSession`: cambiar de vista no recalcula y conserva foco y posición del replay.
- Ancho < 1100 px: los paneles de la vista se apilan en una columna.

### Ensayo (multi-monitor)
- Cada vista abre en su propia ventana (una por monitor) desde su ruta.
- Además, rutas independientes por panel (`/panel/{slug}`: `/panel/tower`, `/panel/map`, `/panel/weather`, …); el índice `/panel` lista también las vistas.
- Todas comparten el mismo estado de sesión vía SignalR.

## 4. Interacción

| Acción | Efecto |
|---|---|
| Cambiar piloto | Instantáneo (estado ya calculado en segundo plano) |
| Saltar a vuelta N | Replay reposiciona; paneles se recalculan desde caché/estado |

## 5. Componentes Blazor

- `PitWallLayout`, `FlagBar`, `TimingTower`, `TrackMap` (SVG), `TelemetryChart`, `TyrePanel`, `WeatherWidget`, `RaceControlFeed`, `EnergyWidget`, `ReplayControls`, `RadioPlayer`.
- Paneles nuevos: `BestSectorsPanel` (12), `RacePacePanel` (13), `PitCallPanel` (14). Clima ampliado de Condiciones: `WeatherDetail` (`/panel/weather-detail`).
- Vistas: `TimingView`, `GpsView`, `CarView`, `ConditionsView`; barra común con `ViewTabs` (pestañas + atajos 1–4).
- Gráficos: SVG propio para mapa; librería de charts Blazor a definir en fase UI (requisito: actualización frecuente sin re-render completo).

# Datos — OpenF1

Base: `https://api.openf1.org/v1`. Datos históricos (2023+) **gratis y sin autenticación**. Tiempo real requiere suscripción (no se usa).

Rate limit: no documentado. Ingesta con pausa entre requests (default 500 ms) y reintentos con backoff ante 429/5xx.

## 1. Sesiones Brasil 2026 (`meeting_key=1299`)

| Sesión | `session_key` |
|---|---|
| FP1 | 11405 |
| FP2 | 11406 |
| FP3 | 11407 |
| Qualifying | 11408 |
| **Race** | **11412** |

## 2. Endpoints usados

| Endpoint | Filtro | Filas aprox. (carrera) | Campos clave | Uso |
|---|---|---|---|---|
| `drivers` | `session_key` | 22 | `driver_number`, `full_name`, `team_name`, `team_colour` | catálogo |
| `laps` | `session_key` | 1.437 | `lap_number`, `lap_duration`, `duration_sector_1/2/3`, `i1_speed`, `i2_speed`, `is_pit_out_lap`, `date_start` | degradación, cierre de vuelta |
| `stints` | `session_key` | 80 | `stint_number`, `lap_start`, `lap_end`, `compound`, `tyre_age_at_start` | neumáticos, decisión real |
| `pit` | `session_key` | 58 | `lap_number`, `pit_duration`, `lane_duration`, `stop_duration` | pit loss, decisión real |
| `race_control` | `session_key` | 212 | `date`, `category`, `flag`, `scope`, `message`, `lap_number` | banderas, triage Jev |
| `weather` | `session_key` | 160 (1/min) | `rainfall`, `track_temperature`, `air_temperature`, `humidity`, `wind_speed`, `wind_direction`, `pressure` | clima |
| `intervals` | `session_key` | 29.368 | `gap_to_leader`, `interval` | torre de tiempos |
| `position` | `session_key` | 576 | `position`, `date` | torre de tiempos |
| `overtakes` | `session_key` | 297 | `overtaking_driver_number`, `overtaken_driver_number`, `position` | prob. de adelantamiento |
| `session_result` | `session_key` | 22 | `position`, `number_of_laps`, `dnf`, `dns`, `dsq`, `gap_to_leader` | validación |
| `team_radio` | `session_key` | 30 | `date`, `driver_number`, `recording_url` | radio (mp3) |
| `starting_grid` | **`session_key=11408`** | 22 | `position`, `driver_number`, `lap_duration` | grilla (404 con key de carrera) |
| `car_data` | `session_key`, `driver_number`, rango `date` | ~23.000 / piloto | `date`, `speed`, `rpm`, `n_gear`, `throttle`, `brake`, `drs` | telemetría, energía |
| `location` | `session_key`, `driver_number`, rango `date` | ~23.000 / piloto | `date`, `x`, `y`, `z` | mapa |

Notas:
- Filtro por fecha en URL: `date%3E=2026-11-08T17:00:00&date%3C=2026-11-08T19:00:00`.
- Sin filtro de fecha, `car_data` de un piloto trae ~6 MB incluyendo datos del sábado.
- `lap_duration` de la vuelta 1 es `null`.
- `drs` viene `null` (2026 sin DRS).
- `car_data`/`location` a ~3,7 Hz. Se descargan en ventanas de 10 min para evitar respuestas grandes.

## 3. CLI de ingesta

```bash
dotnet run --project src/PitWall.Ingestion -- \
  --session 11412 --quali 11408 --drivers 43,44,12,3 \
  --db data/pitwall.db --radio data/radio
```

Pasos:
1. `drivers` → resolver números.
2. Endpoints de sesión completos (tabla anterior).
3. `starting_grid` desde `--quali`.
4. `car_data` + `location` por piloto y ventana de 10 min.
5. Descargar mp3 de `team_radio` a `data/radio/{session_key}/`.
6. Marcar `IngestedSession` con fecha y conteos.

Idempotencia: `upsert` por clave natural (p. ej. `session_key + driver_number + date` para `car_data`). Re-ejecutar una sesión reemplaza sus datos.

Agregar un GP: correr de nuevo con otro `--session`/`--quali`. La UI lista las sesiones disponibles en `IngestedSession`.

## 4. Esquema SQLite (EF Core)

```
IngestedSession   (SessionKey PK, MeetingKey, Name, Circuit, DateStart, DateEnd, QualiSessionKey, IngestedAt)
Driver            (SessionKey, DriverNumber, FullName, Acronym, Team, TeamColour)            PK(SessionKey, DriverNumber)
Lap               (SessionKey, DriverNumber, LapNumber, DateStart, Duration, S1, S2, S3, I1Speed, I2Speed, IsPitOutLap)
Stint             (SessionKey, DriverNumber, StintNumber, LapStart, LapEnd, Compound, TyreAgeAtStart)
PitStop           (SessionKey, DriverNumber, LapNumber, Date, PitDuration, LaneDuration, StopDuration)
RaceControl       (Id PK, SessionKey, Date, LapNumber, Category, Flag, Scope, DriverNumber?, Message)
Weather           (SessionKey, Date, AirTemp, TrackTemp, Humidity, Rainfall, WindSpeed, WindDirection, Pressure)
Interval          (SessionKey, DriverNumber, Date, GapToLeader?, Interval?)
Position          (SessionKey, DriverNumber, Date, Position)
Overtake          (SessionKey, Date, OvertakingDriver, OvertakenDriver, Position)
SessionResult     (SessionKey, DriverNumber, Position?, Laps, Dnf, Dns, Dsq, Duration?, GapToLeader?)
StartingGrid      (SessionKey, DriverNumber, Position, LapDuration?)
TeamRadio         (SessionKey, DriverNumber, Date, RecordingUrl, LocalPath)
CarData           (SessionKey, DriverNumber, Date, Speed, Rpm, Gear, Throttle, Brake)        índice (SessionKey, DriverNumber, Date)
Location          (SessionKey, DriverNumber, Date, X, Y, Z)                                   índice (SessionKey, DriverNumber, Date)

-- Generadas por la app
Scenario          (Id PK, SessionKey, Name, CreatedAt)
InjectedEvent     (Id PK, ScenarioId, Lap, Type, Params JSON, CreatedAt)
JevDecisionLog    (Id PK, SessionKey, ScenarioId?, DriverNumber, Lap, Kind, StateHash, RequestJson, ResponseJson, LatencyMs, Tokens, CostUsd, CreatedAt)
JevCache          (StateHash PK, Kind, ResponseJson, CreatedAt)
```

`Compound` se guarda como texto (`SOFT`, `MEDIUM`, `HARD`, `INTERMEDIATE`, `WET`).

Ubicación: `data/pitwall.db`. **`data/` va en `.gitignore`**; se regenera con la CLI.

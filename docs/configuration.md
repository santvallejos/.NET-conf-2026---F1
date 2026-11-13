# Configuración

## 1. `appsettings.json` (PitWall.Web)

```jsonc
{
  "PitWall": {
    "DatabasePath": "data/pitwall.db",
    "RadioPath": "data/radio",
    "DefaultSessionKey": 11412,
    "FocusDriver": 43,                      // Colapinto
    "BackgroundDrivers": [44, 12, 3]        // Hamilton, Antonelli, Verstappen
  },
  "Replay": {
    "Speeds": [1, 5, 20],
    "TickMs": 250
  },
  "Strategy": {
    "FuelCoefSecondsPerLap": 0.03,
    "CliffDeltaSeconds": 1.5,
    "MinCleanLaps": 4,
    "Energy": {                              // ESTIMADO: no son cifras oficiales
      "BatteryCapacityMj": 4.0,
      "HarvestBrakeKw": 350,
      "HarvestLiftKw": 120,
      "DeployKw": 350,
      "SpeedDeployMin": 120
    }
  }
}
```

Las claves que no aparecen en `appsettings.json` toman el default del código. Las secciones de opciones se validan al
arrancar (`ValidateDataAnnotations` + `ValidateOnStart`): un valor fuera de rango impide iniciar la app. **ESTIMADO** =
supuesto de la demo, ni cifra oficial ni medido en la sesión.

### 1.1 `Strategy:PitCall`

Pit loss de la llamada a boxes ([strategy-engine.md §4](strategy-engine.md#4-llamada-a-boxes-panel-14))
(`PitCallOptions`). No está en `appsettings.json`: rige el default del código. **ESTIMADO**.

| Clave | Tipo | Default | Rango | Efecto |
|---|---|---|---|---|
| `DefaultPitLossSeconds` | `double` | `22.0` | 5–60 | Pit loss (s) si la sesión aún no tiene `MinPitSamples` paradas con `lane_duration`. |
| `MinPitSamples` | `int` | `3` | 1–100 | Paradas mínimas con `lane_duration` (entre 0 y 60 s) para usar el pit loss medio de la sesión. |
| `ScPitLossFactor` | `double` | `0.5` | 0–1 | Fracción del pit loss normal que se pierde parando bajo Safety Car. |
| `VscPitLossFactor` | `double` | `0.65` | 0–1 | Fracción del pit loss normal que se pierde parando bajo VSC. |

## 2. Variables de entorno

| Variable | Equivale a |
|---|---|
| `PitWall__DatabasePath` | `PitWall:DatabasePath` |

## 3. `.gitignore` relevante

```
data/
*.db
*.db-shm
*.db-wal
secrets.json
```

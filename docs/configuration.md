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
    "MonteCarlo": {
      "Iterations": 10000,
      "Seed": 1412,
      "ProbSafetyCarPerLap": 0.02,
      "ProbVscPerLap": 0.02,
      "TopOutcomesToJev": 5
    },
    "Energy": {                              // ESTIMADO: no son cifras oficiales
      "BatteryCapacityMj": 4.0,
      "HarvestBrakeKw": 350,
      "HarvestLiftKw": 120,
      "DeployKw": 350,
      "SpeedDeployMin": 120
    }
  },
  "Jev": {
    "Enabled": true,
    "Client": "Fake",                        // Fake | Real
    "BaseUrl": "https://api.typesafe.ai/v1/",
    "Model": "jev-latest",
    "Mode": "EveryLap",                      // EveryLap | SpecificLaps | Manual
    "Laps": [18, 40, 60, 62],                // solo si Mode = SpecificLaps
    "Drivers": [43, 44, 12, 3],
    "Triage": { "Enabled": true, "CanTriggerStrategy": true },
    "Cache": { "Enabled": true },
    "MaxRequestsPerSession": 500,
    "PricePerMillionInputTokensUsd": 0.042,
    "TimeoutMs": 2000
  },
  "Scenarios": {
    "SafetyCarDefaultLaps": 4,
    "VscDefaultLaps": 2,
    "SafetyCarPitLossFactor": 0.5,
    "VscPitLossFactor": 0.65,
    "PunctureLossSeconds": [20, 40],
    "WingDamageMinorSecPerLap": 0.3,
    "WingDamageMajorSecPerLap": 1.5
  }
}
```

## 2. Secretos

La API key de Jev **nunca** va en el repo ni en `appsettings*.json`.

Desarrollo (User Secrets en AppHost):

```bash
dotnet user-secrets set "Parameters:typesafe-api-key" "<tu-key>" --project src/PitWall.AppHost
```

AppHost:

```csharp
// Parámetro secreto de Aspire: se inyecta como variable de entorno en Web
var jevKey = builder.AddParameter("typesafe-api-key", secret: true);

builder.AddProject<Projects.PitWall_Web>("web")
       .WithEnvironment("Jev__ApiKey", jevKey);
```

Sin key → `Jev:Client` debe ser `Fake`. Si es `Real` sin key, la app arranca con Jev deshabilitado y muestra alerta.

## 3. Variables de entorno

| Variable | Equivale a |
|---|---|
| `Jev__ApiKey` | `Jev:ApiKey` (inyectada por Aspire) |
| `Jev__Client` | `Jev:Client` |
| `Jev__Enabled` | `Jev:Enabled` |
| `PitWall__DatabasePath` | `PitWall:DatabasePath` |

## 4. Cambio en caliente

Vía UI (panel Controles) sin reiniciar: `Jev:Enabled`, `Jev:Mode`, `Jev:Laps`, `Jev:Drivers`. Implementado con `IOptionsMonitor<JevOptions>` + override en memoria.

## 5. `.gitignore` relevante

```
data/
*.db
*.db-shm
*.db-wal
secrets.json
```

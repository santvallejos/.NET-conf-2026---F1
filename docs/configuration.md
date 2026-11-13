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

Las claves que no aparecen en `appsettings.json` toman el default del código. Las secciones de opciones se validan al
arrancar (`ValidateDataAnnotations` + `ValidateOnStart`): un valor fuera de rango impide iniciar la app. **ESTIMADO** =
supuesto de la demo, ni cifra oficial ni medido en la sesión.

### 1.1 `Strategy:MonteCarlo`

Parámetros de la simulación de [strategy-engine.md §4](strategy-engine.md#4-monte-carlo) (`MonteCarloOptions`).

| Clave | Tipo | Default | Rango | Efecto |
|---|---|---|---|---|
| `Iterations` | `int` | `10000` | 100–1.000.000 | Iteraciones por estrategia candidata. Si no se cumple < 1 s por piloto y vuelta, bajar este valor primero. |
| `Seed` | `int` | `1412` | — | Semilla base; cada candidata usa `seed ^ candidateIndex`, así el resultado es determinista. |
| `ProbSafetyCarPerLap` | `double` | `0.02` | 0–1 | Prior: probabilidad por vuelta de un SC aleatorio en cada iteración. |
| `ProbVscPerLap` | `double` | `0.02` | 0–1 | Prior: probabilidad por vuelta de un VSC aleatorio en cada iteración. |
| `TopOutcomesToJev` | `int` | `5` | 1–50 | Mejores estrategias que viajan a Jev en el `state`. |
| `SafetyCarLaps` | `int` | `4` | 1–20 | Vueltas de un SC simulado (o en curso sin duración conocida). Un SC inyectado usa `Scenarios:SafetyCarDefaultLaps`. |
| `VscLaps` | `int` | `2` | 1–20 | Ídem para el VSC (inyectado: `Scenarios:VscDefaultLaps`). |
| `SafetyCarPitLossFactor` | `double` | `0.5` | 0–1 | Fracción del pit loss que se pierde parando bajo SC. |
| `VscPitLossFactor` | `double` | `0.65` | 0–1 | Fracción del pit loss que se pierde parando bajo VSC. |
| `SafetyCarLapFactor` | `double` | `1.4` | 1–3 | **ESTIMADO.** Vuelta bajo SC = ritmo base más rápido de la parrilla × factor. |
| `VscLapFactor` | `double` | `1.3` | 1–3 | **ESTIMADO.** Vuelta bajo VSC = ritmo base más rápido de la parrilla × factor. |
| `SafetyCarGapSeconds` | `double` | `0.8` | 0,1–5 | **ESTIMADO.** Separación máxima (s) entre autos consecutivos al agruparse detrás del SC. |
| `PitLossMeanSeconds` | `double` | `22.0` | 5–60 | Pit loss medio (s) si la sesión aún no tiene suficientes paradas con `lane_duration` (ver `MinPitSamples`). |
| `PitLossSigmaSeconds` | `double` | `1.5` | 0–10 | σ (s) de ese pit loss de respaldo. |
| `MinPitSamples` | `int` | `3` | 1–100 | Paradas mínimas con `lane_duration` para usar el pit loss de la sesión (nunca menos de 2). |
| `RainStopProbPerLap` | `double` | `0.1` | 0–1 | **ESTIMADO.** Probabilidad por vuelta de que pare de llover (Markov seco/lluvia). |
| `RainRiskHorizonLaps` | `int` | `10` | 1–100 | **ESTIMADO.** Vueltas sobre las que se reparte el "RISK OF RAIN x%" de Race Control: probabilidad de empezar a llover por vuelta = x% / horizonte. |
| `DryCompoundInRainPenaltySeconds` | `double` | `6.0` | 0–60 | **ESTIMADO.** Pérdida por vuelta (s) de un compuesto seco con lluvia. |
| `WetCompoundInDryPenaltySeconds` | `double` | `3.0` | 0–60 | **ESTIMADO.** Pérdida por vuelta (s) de intermedio o lluvia con pista seca. |
| `HeavyRainPenaltyFactor` | `double` | `2.0` | 1–10 | **ESTIMADO.** Multiplicador de la pérdida de un compuesto seco con lluvia fuerte inyectada. |
| `OvertakeBaseProbability` | `double` | `0.3` | 0–1 | **ESTIMADO.** Probabilidad base de completar un adelantamiento cuando el ritmo alcanza al de adelante. Con datos suficientes se escala por la tasa de `overtakes` de la sesión, con tope `MaxOvertakeProbability`. |
| `OvertakeReferenceRatePerCarLap` | `double` | `0.03` | 0,001–1 | **ESTIMADO.** Adelantamientos por auto y vuelta de una carrera "normal"; base efectiva = base × tasa de la sesión / referencia. |
| `MinCarLapsForOvertakeRate` | `int` | `100` | 1–10.000 | Vueltas cerradas (sumando autos) mínimas para usar la tasa de adelantamientos de la sesión; por debajo se usa la base sin escalar. |
| `OvertakePaceSlopePerSecond` | `double` | `0.25` | 0–5 | **ESTIMADO.** Probabilidad extra de adelantar por cada segundo de ritmo a favor en la vuelta. |
| `OvertakeModeBonus` | `double` | `0.15` | 0–1 | **ESTIMADO.** Probabilidad extra con overtake mode disponible (reglamento 2026). |
| `CertainOvertakeDeltaSeconds` | `double` | `2.0` | 0,1–60 | Con más ritmo a favor que esto (s), o si el de adelante paró, el adelantamiento es seguro. |
| `MaxOvertakeProbability` | `double` | `0.9` | 0–1 | Tope de la probabilidad de adelantar. |
| `FollowGapSeconds` | `double` | `0.3` | 0–5 | Distancia (s) a la que queda un auto que no consigue adelantar. |
| `PunctureLossMinSeconds` | `double` | `20.0` | 0–300 | Mínimo (s) de la vuelta lenta con pinchazo; la pérdida se sortea uniforme entre mínimo y máximo. En un escenario inyectado manda `Scenarios:PunctureLossSeconds`. |
| `PunctureLossMaxSeconds` | `double` | `40.0` | 0–300 | Máximo (s) de esa vuelta lenta. Debe ser ≥ `PunctureLossMinSeconds` (se valida al arrancar). |
| `FallbackLapSigmaSeconds` | `double` | `0.5` | 0–10 | σ de ritmo (s) de un auto sin modelo de degradación propio ni de parrilla. |

### 1.2 `Strategy:Rules`

Umbrales con los que el motor de reglas genera razones ([strategy-engine.md §3.2](strategy-engine.md#32-razones-reason))
(`RulesOptions`). No está en `appsettings.json`: rige el default del código.

| Clave | Tipo | Default | Rango | Efecto |
|---|---|---|---|---|
| `DegHighSecondsPerLap` | `double` | `0.10` | 0–10 | `TYRE_DEG_HIGH` si la degradación (s/vuelta) es ≥ este valor. |
| `CliffNearLaps` | `int` | `3` | 0–100 | `TYRE_CLIFF_NEAR` si faltan ≤ este número de vueltas al cliff. `OVERCUT_WINDOW` exige que el de adelante haya parado y que falten más vueltas que esto. |
| `UndercutGapSeconds` | `double` | `3.0` | 0–60 | `UNDERCUT_THREAT` si el gap al auto de atrás (s) es ≤ este valor. |
| `TrafficOnExitGapSeconds` | `double` | `2.0` | 0–60 | Gap (s) al auto más cercano al salir de boxes si para ahora: menor → `TRAFFIC_ON_EXIT`; mayor o igual → `CLEAR_AIR_ON_EXIT`. |
| `EnergyLowSocPct` | `double` | `20.0` | 0–100 | `ENERGY_LOW` si el estado de carga estimado (%) es ≤ este valor. |

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

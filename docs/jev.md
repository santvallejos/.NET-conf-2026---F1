# Jev — integración

## 1. Qué es

Jev es el modelo "System One" de **TypeSafe AI** (lanzado 2026-09-15; modelo estable `jev-1.13.0`, alias `jev-latest`). Es un **clasificador**: recibe un estado y preguntas con opciones cerradas, y devuelve la opción elegida con probabilidades. No genera texto libre ni conversa.

Rol en el proyecto (ver [decisions.md](decisions.md), ADR-008):

| Capa | Qué hace |
|---|---|
| Reglas (C#) | Restricciones duras, `reasons` |
| Monte Carlo (C#) | Números: probabilidades por estrategia ("System Two") |
| **Jev** | Decisión rápida fusionando señales numéricas + **texto libre** de Race Control ("System One") |

> Jev no tiene uso documentado en la F1. Se presenta como propuesta.

## 2. API

> ⚠️ Fuente: wiki comunitaria (https://github.com/cobusgreyling/Jev/wiki) y docs.typesafe.ai (parcial). **Verificar contra la doc oficial al obtener la API key** antes de cerrar `JevClient`.

- `POST https://api.typesafe.ai/v1/systemone`
- Header: `Authorization: Bearer {TYPESAFE_API_KEY}`
- Tipos de pregunta: `choice` (opción), `score` (escala), `noul` (0–1), `box` (imágenes, no se usa).
- Varias preguntas por request, evaluadas en paralelo sobre el mismo `state`.
- `state`: texto o JSON (no hay entrada numérica nativa → se serializa JSON compacto).
- Contexto: 64k tokens/request (32k para state + pregunta más larga).
- Latencia: 70–500 ms. Solo hosted (sin offline).

Request (forma esperada):

```json
{
  "model": "jev-latest",
  "state": "{...JSON compacto del estado...}",
  "questions": {
    "pit_call": {
      "type": "choice",
      "instructions": "Decide the pit call for the focus car this lap.",
      "criteria": ["STAY_OUT", "BOX", "BOX_NEXT_LAP"]
    }
  }
}
```

Response (forma esperada):

```json
{
  "pit_call": { "choice": "BOX", "probabilities": { "STAY_OUT": 0.09, "BOX": 0.84, "BOX_NEXT_LAP": 0.07 }, "confidence": 0.84 }
}
```

## 3. Usos

### 3.1 Triage (barato, frecuente)
Disparo: nuevo mensaje de Race Control relevante o evento inyectado.

| Pregunta | Tipo | Opciones |
|---|---|---|
| `impact` | choice | `NONE`, `MONITOR`, `ACT_NOW` |
| `urgency` | score | 1–5 |

`state`: mensaje(s) nuevos + resumen mínimo (vuelta, posición, compuesto, edad, clima).

Si `impact = ACT_NOW` → dispara **Estrategia** inmediatamente (aunque el modo sea `Manual`, ver `Jev:TriageCanTriggerStrategy`).

### 3.2 Estrategia
Disparo: según `Jev:Mode` o por triage.

| Pregunta | Tipo | Opciones |
|---|---|---|
| `pit_call` | choice | `STAY_OUT`, `BOX`, `BOX_NEXT_LAP` |
| `compound` | choice | `SOFT`, `MEDIUM`, `HARD`, `INTERMEDIATE`, `WET` (filtradas por restricciones) |
| `pace_mode` | choice | `PUSH`, `MANAGE`, `SAVE_TYRES` |
| `energy_mode` | choice | `HARVEST`, `BALANCED`, `DEPLOY`, `OVERTAKE` |

Las opciones prohibidas por el motor de reglas **no se envían**.

### 3.3 `state` de estrategia (ejemplo)

```json
{
  "lap": 60, "laps_total": 71, "pos": 13, "gap_ahead": 1.8, "gap_behind": 0.9,
  "tyre": { "compound": "SOFT", "age": 42, "deg_s_per_lap": 0.11, "laps_to_cliff": 3 },
  "energy_est": { "soc_pct": 54, "overtake_available": false },
  "weather": { "rain": true, "track_c": 29.1, "humidity": 81 },
  "track": { "flag": "GREEN", "sc": false, "vsc": false },
  "race_control": ["LOW GRIP CONDITIONS", "OVERTAKE DISABLED"],
  "injected": [],
  "mc": [
    { "id": "STAY_OUT", "exp_pos": 14.6, "p_gain": 0.08 },
    { "id": "BOX_L_INTERMEDIATE", "exp_pos": 11.9, "p_gain": 0.61 },
    { "id": "BOX_L+1_INTERMEDIATE", "exp_pos": 12.4, "p_gain": 0.49 }
  ]
}
```

Mantener compacto (claves cortas, top 5 de Monte Carlo) → menos tokens.

## 4. Contrato de salida (público)

Lo consumen la UI y cualquier automatización. **Sin texto libre.**

```csharp
public sealed record JevDecision(
    Guid Id,
    int SessionKey,
    Guid? ScenarioId,          // null = carrera real; != null = hipotético
    int DriverNumber,
    int Lap,
    JevKind Kind,              // Triage | Strategy
    JevTrigger Trigger,        // EveryLap | SpecificLap | Manual | Triage
    PitCall? PitCall,
    Compound? Compound,
    PaceMode? PaceMode,
    EnergyMode? EnergyMode,
    Impact? Impact,
    int? Urgency,
    double Confidence,         // mínimo de las confidences de las preguntas
    IReadOnlyDictionary<string, IReadOnlyDictionary<string,double>> Probabilities,
    IReadOnlyList<Reason> Reasons,   // del motor de reglas
    bool Hypothetical,
    bool FromCache,
    int LatencyMs,
    int Tokens,
    decimal CostUsd);
```

JSON serializado (ejemplo):

```json
{ "lap": 60, "driver": 43, "kind": "Strategy", "pitCall": "BOX", "compound": "INTERMEDIATE",
  "paceMode": "MANAGE", "energyMode": "BALANCED", "confidence": 0.84,
  "reasons": ["RAIN_STARTING","TYRE_CLIFF_NEAR","LOW_GRIP"],
  "hypothetical": false, "fromCache": false, "latencyMs": 212, "tokens": 640, "costUsd": 0.0000269 }
```

Publicado por SignalR (`PitWallHub.JevDecision`) y persistido en `JevDecisionLog`.

## 5. Cliente .NET

Cliente **propio** (no se usan los paquetes comunitarios `Jev.Net`/`JevNet`).

```csharp
public interface IJevClient
{
    Task<JevResponse> SystemOneAsync(JevRequest request, CancellationToken ct);
}
```

| Implementación | Uso |
|---|---|
| `JevClient` | API real. `HttpClient` tipado + `System.Text.Json` source-gen + resiliencia de `ServiceDefaults`. |
| `FakeJevClient` | Desarrollo y tests. Determinista: elige la opción de mejor `exp_pos` de Monte Carlo y probabilidades derivadas. Costo $0, sin red. |

Decorador `GuardedJevClient` (envuelve cualquiera de los dos):
1. `Jev:Enabled == false` → no llama.
2. Calcula `StateHash` (SHA-256 del request normalizado) → si existe en `JevCache`, devuelve con `FromCache = true`.
3. Si `requests >= Jev:MaxRequestsPerSession` → no llama, emite alerta en UI.
4. Llama, mide latencia/tokens/costo, guarda caché y métricas.

Selección: `Jev:Client = Fake | Real`.

## 6. Costo

- Precio publicado por terceros: US$0,042 / millón de tokens de entrada; salida gratis. **Verificar** al obtener la key.
- Estimación: ~1.000 tokens/request → ~US$0,00004.
- Carrera completa, 4 pilotos, cada vuelta: ~284 requests estrategia + triage ≈ **< US$0,02**.
- Re-ensayos con el mismo estado: **$0** por caché.
- Free tier: no claro; hay waitlist para keys de producción. Desarrollar con `Fake` hasta tener key.

## 7. Modos de disparo

| `Jev:Mode` | Comportamiento |
|---|---|
| `EveryLap` | Estrategia al cerrar cada vuelta, para cada piloto en `Jev:Drivers` |
| `SpecificLaps` | Solo en vueltas de `Jev:Laps` (ej. `[18, 40, 60, 62]`) |
| `Manual` | Solo con botón "Consultar Jev" en la UI |

Triage corre en todos los modos si `Jev:Triage:Enabled = true`.

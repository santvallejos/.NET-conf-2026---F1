# Motor de estrategia

Proyecto `PitWall.Strategy`. Puro C#, sin I/O. Entrada: `RaceState` + histórico de vueltas. Salida: `StrategySnapshot` por piloto y vuelta.

Se ejecuta **al cerrar cada vuelta** de cada uno de los 4 pilotos, en paralelo.

## 1. Degradación de neumáticos

Datos reales, sin parámetros inventados.

**Modelo** por (piloto, compuesto), ajustado con vueltas limpias del stint:

```
lap_time = base + deg · tyre_age + fuel_coef · laps_remaining + ε
```

- `tyre_age = tyre_age_at_start + (lap − lap_start)`.
- `fuel_coef`: corrección por masa de combustible (default 0,03 s/vuelta, configurable). Se aplica antes de la regresión para aislar `deg`.
- Regresión lineal por mínimos cuadrados. `ε` = desvío residual → alimenta la varianza de Monte Carlo.

**Vueltas excluidas** (no limpias):
- vuelta 1, `is_pit_out_lap`, vuelta de entrada a boxes,
- vueltas con bandera amarilla/SC/VSC activa,
- outliers > 3σ (tráfico, errores).

**Fallback** si hay < 4 vueltas limpias del compuesto: usar la media del compuesto del resto de la parrilla en la misma sesión.

**Vida restante**: vueltas hasta que `deg · tyre_age` supere el umbral `CliffDeltaSeconds` (default 1,5 s) respecto a goma nueva.

## 2. Energía 2026 (estimada)

OpenF1 **no expone** estado de batería/ERS. Se estima desde `car_data`. Toda salida lleva flag `IsEstimated = true` y la UI lo muestra.

Modelo simple por muestra (~3,7 Hz):

| Condición | Efecto |
|---|---|
| `brake > 0` | recupera `HarvestBrakeKw · Δt` |
| `throttle < 20` y `speed > 150` | recupera parcial `HarvestLiftKw · Δt` (lift & coast) |
| `throttle > 95` y `speed > SpeedDeployMin` | despliega `DeployKw · Δt` |

- Estado de carga `SoC` acotado en `[0, BatteryCapacityMj]`.
- Todos los parámetros en config (`Strategy:Energy:*`). Valores iniciales orientativos; **no** son cifras oficiales.
- Overtake Mode (reglamento 2026: < 1 s del auto de adelante, energía extra): se marca como **disponible** cuando `interval < 1.0` y Race Control no reporta "OVERTAKE DISABLED".

Salida: `EnergyEstimate(SocPct, HarvestedLapMj, DeployedLapMj, OvertakeAvailable, IsEstimated)`.

## 3. Motor de reglas

Dos responsabilidades:

### 3.1 Restricciones duras (`Constraint`)
Eliminan estrategias candidatas antes de Monte Carlo y bloquean decisiones de Jev que las violen.

| Regla | Efecto |
|---|---|
| Bandera roja | no `BOX` (cambio de gomas en parrilla según reglamento) |
| Pit lane cerrado (Race Control) | no `BOX` |
| Carrera en seco sin uso de compuesto húmedo | obligatorio ≥ 2 compuestos secos distintos antes del final |
| Lluvia fuerte | descarta `SOFT`/`MEDIUM`/`HARD` |
| Pista seca y sin lluvia prevista | descarta `WET` |
| Pinchazo/daño de alerón inyectado | fuerza `BOX` |

### 3.2 Razones (`Reason`)
Jev **no** devuelve razones. Las genera este motor a partir de los features. Enum cerrado:

```
TYRE_DEG_HIGH, TYRE_CLIFF_NEAR, UNDERCUT_THREAT, OVERCUT_WINDOW,
SC_DEPLOYED, VSC_DEPLOYED, RED_FLAG, RAIN_STARTING, RAIN_STOPPING,
TRACK_DRYING, LOW_GRIP, TRAFFIC_ON_EXIT, CLEAR_AIR_ON_EXIT,
ENERGY_LOW, OVERTAKE_AVAILABLE, PUNCTURE, WING_DAMAGE,
RIVAL_INCIDENT, ERS_FAILURE, MANDATORY_COMPOUND_PENDING
```

## 4. Monte Carlo

Proyecta el **resto de la carrera** desde la vuelta actual, miles de veces, para cada estrategia candidata. No modifica el replay.

### 4.1 Estrategias candidatas
Para el piloto foco, en la vuelta `L`:
- `STAY_OUT` (hasta el próximo cliff o fin de carrera),
- `BOX` en `L`, `L+1`, `L+2`, `L+3`,
- × compuesto permitido por las restricciones.

Típicamente 8–15 candidatas.

### 4.2 Variables estocásticas por iteración

| Variable | Distribución | Fuente |
|---|---|---|
| Ritmo por vuelta | Normal(`modelo deg`, `σ residual`) | regresión §1 |
| Pit loss | Normal(media, σ) | `pit.lane_duration` reales de la sesión |
| SC por vuelta | Bernoulli(`p_sc`) | prior configurable (default 0,02) |
| VSC por vuelta | Bernoulli(`p_vsc`) | prior configurable (default 0,02) |
| Lluvia | Markov 2 estados (seco/lluvia) | `weather` + mensajes "RISK OF RAIN x%" |
| Adelantamiento | Bernoulli(f(gap, Δritmo, overtake mode)) | `overtakes` de la sesión |
| Rivales | Misma dinámica con su propio modelo de deg | 18 pilotos restantes como tráfico |

Eventos inyectados (ver [scenarios.md](scenarios.md)) se fijan como **ciertos** en la vuelta indicada en todas las iteraciones.

### 4.3 Salida

```csharp
record StrategyOutcome(
    string StrategyId,            // ej. "BOX_L+1_INTERMEDIATE"
    PitCall Call, int? BoxLap, Compound? Compound,
    double ExpectedPosition,
    IReadOnlyDictionary<int,double> PositionDistribution, // P(posición final = k)
    double PGainPositions,        // P(terminar mejor que ahora)
    double PLosePositions,
    double ExpectedRaceTimeDelta  // vs. mejor estrategia
);
```

### 4.4 Rendimiento
- N = 10.000 iteraciones por candidata (config `Strategy:MonteCarlo:Iterations`).
- `Parallel.For` por candidata; RNG por hilo con semilla derivada (`seed ^ candidateIndex`) → determinista.
- Objetivo: < 1 s por piloto por vuelta (RNF-03). Si no se cumple, bajar N antes de optimizar.

## 5. Decisión real del equipo (para Jev vs equipo)

Derivada de `stints` + `pit`:
- `BOX` en la vuelta `lap_number` de cada `pit`.
- Compuesto = `compound` del stint siguiente.
- Resto de vueltas = `STAY_OUT`.

`pace_mode` y `energy_mode` reales **no** son observables → el panel solo compara pit + compuesto.

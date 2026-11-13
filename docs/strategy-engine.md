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
- Regresión lineal por mínimos cuadrados. `ε` = desvío residual (σ en el panel de neumáticos).

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

## 3. (libre)

El motor de reglas (`Reason`) se quitó con ADR-021: sus razones solo se mostraban en la timeline de Escenarios. El
número queda libre para no romper las referencias a §4. El muro no recomienda una parada: la estrategia
probabilística la lleva el estratega de cada piloto (ADR-020).

## 4. Llamada a boxes (panel 14)

Estimación determinista de qué pasa si el piloto foco entra **ahora** (`PitWall.Strategy/PitWindow`,
`PitCallCalculator`). Etiqueta **ESTIMADO**: usa los gaps del instante del replay y no simula el resto de la vuelta.

- **Pit loss normal** = media de `pit.lane_duration` de la sesión (paradas entre 0 y 60 s) o
  `Strategy:PitCall:DefaultPitLossSeconds` si todavía hay menos de `Strategy:PitCall:MinPitSamples` paradas. Lo
  calcula `StrategyCalculator` al cerrar cada vuelta (`StrategySnapshot.PitLossSeconds`).
- **Bajo SC** = pit loss × `Strategy:PitCall:ScPitLossFactor`; **bajo VSC** = pit loss × `Strategy:PitCall:VscPitLossFactor`.
- **Posiciones perdidas** = autos de atrás cuya diferencia acumulada con el foco (suma de `interval` desde el foco
  hacia atrás) es menor que el pit loss. Un auto sin `interval` (doblado o sin dato) corta la cuenta.
- **Posición al salir** = posición actual + posiciones perdidas. El último no pierde posiciones.
- Siempre sobre el estado real del replay.

Solo lectura: no cambia el replay ni la estrategia. Tests en `tests/PitWall.Tests/Strategy/PitWindow`.

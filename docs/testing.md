# Estrategia de tests

Proyecto `tests/PitWall.Tests` (xUnit). Foco en lógica determinista. **Sin tests de UI.**

## 1. Qué se testea

| Área | Tests | Tipo |
|---|---|---|
| Degradación | Regresión recupera `deg` conocido de datos sintéticos; exclusión de out-lap/vuelta 1/outliers; fallback con < 4 vueltas | unit |
| Vida restante | Cálculo de cliff con `CliffDeltaSeconds` | unit |
| Energía | SoC acotado `[0, cap]`; harvest en frenada; deploy a full throttle; `OvertakeAvailable` con interval < 1 s y sin "OVERTAKE DISABLED" | unit |
| Reglas | Cada restricción de [strategy-engine.md](strategy-engine.md) §3.1; generación de cada `Reason` | unit |
| Monte Carlo | Misma semilla → mismo resultado; distribución suma 1; estrategia obviamente mejor gana (ej. pinchazo → BOX) | unit |
| Escenarios | Cada uno de los 8 eventos modifica MC/reglas como se documenta; `Hypothetical = true` | unit |
| Jev — mapeo | `JevResponse` → `JevDecision`; opciones prohibidas no se envían; confidence = mínimo | unit |
| Jev — guardas | `Enabled=false` no llama; caché hit no llama; `MaxRequestsPerSession` corta; costo calculado | unit (con `FakeJevClient` + contador) |
| Jev — serialización | Request JSON coincide con fixture esperado (snapshot) | unit |
| Decisión real equipo | `stints` + `pit` → BOX en vuelta correcta + compuesto | unit |
| Ingesta | Respuestas OpenF1 grabadas (fixtures JSON) → filas esperadas en SQLite en memoria; idempotencia | integración |
| Replay | Saltar a vuelta N produce el `RaceState` esperado | integración (SQLite en memoria) |

## 2. Fixtures

```
tests/PitWall.Tests/Fixtures/openf1/
  drivers_11412.json
  laps_11412_43.json
  stints_11412.json
  pit_11412.json
  race_control_11412.json
  weather_11412.json
  car_data_11412_43_sample.json   (ventana corta)
tests/PitWall.Tests/Fixtures/jev/
  strategy_request_lap60.json
  strategy_response_lap60.json
  triage_response.json
```

Fixtures de OpenF1 se graban una vez con la CLI (`--record-fixtures`). Fixtures de Jev se escriben a mano según la forma documentada en [jev.md](jev.md) y se actualizan al validar contra la API real.

## 3. Reglas

- Ningún test llama a OpenF1 ni a Jev real.
- `HttpClient` falso vía `HttpMessageHandler` stub.
- Monte Carlo en tests: `Iterations = 1000`, semilla fija.
- `TimeProvider` (FakeTimeProvider) para el reloj del replay.

## 4. Ejecución

```bash
dotnet test
```

## 5. Validación manual previa a la charla

1. Ingesta completa de 11412 sin errores; conteos ≈ [data-openf1.md](data-openf1.md).
2. Replay completo x20 sin excepciones.
3. Carrera completa con `Jev:Client=Real`: costo en Aspire < US$0,05.
4. Segunda pasada idéntica: 100% caché, costo adicional $0.
5. Guion de [scenarios.md](scenarios.md) §5 de punta a punta.
6. Desconectar red: todo funciona salvo Jev real (alerta visible).

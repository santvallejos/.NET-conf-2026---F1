# Estrategia de tests

Proyecto `tests/PitWall.Tests` (xUnit). Foco en lógica determinista. **Sin tests de UI.**

## 1. Qué se testea

| Área | Tests | Tipo |
|---|---|---|
| Degradación | Regresión recupera `deg` conocido de datos sintéticos; exclusión de out-lap/vuelta 1/outliers; fallback con < 4 vueltas | unit |
| Vida restante | Cálculo de cliff con `CliffDeltaSeconds` | unit |
| Energía | SoC acotado `[0, cap]`; harvest en frenada; deploy a full throttle; `OvertakeAvailable` con interval < 1 s y sin "OVERTAKE DISABLED" | unit |
| Cálculo por piloto | Modelo de gomas del stint (propio o fallback), energía, pit loss de la sesión o de configuración | unit |
| Llamada a boxes | Posición al salir y posiciones perdidas normal/SC/VSC; auto sin intervalo corta la cuenta; pit loss de la sesión sobre el estado real | unit |
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
```

Fixtures de OpenF1 se graban una vez con la CLI (`--record-fixtures`).

## 3. Reglas

- Ningún test llama a OpenF1 real.
- `HttpClient` falso vía `HttpMessageHandler` stub.
- `TimeProvider` (FakeTimeProvider) para el reloj del replay.

## 4. Ejecución

```bash
dotnet test
```

## 5. Validación manual previa a la charla

1. Ingesta completa de 11412 sin errores; conteos ≈ [data-openf1.md](data-openf1.md).
2. Replay completo x20 sin excepciones.
3. Guion de [spec.md](spec.md) §7.1 de punta a punta.
4. Desconectar red: todo sigue funcionando (la demo es 100% offline).

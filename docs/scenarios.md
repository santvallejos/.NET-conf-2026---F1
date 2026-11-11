# Escenarios y eventos inyectables

## 1. Principio

La carrera de Brasil 2026 real **no tuvo SC, VSC ni bandera roja**. Para mostrar decisiones ante eventos repentinos se permiten **eventos hipotéticos** inyectados sobre el replay real.

**Solo afectan la decisión** (Monte Carlo + Jev + UI). El replay sigue siendo la carrera real: autos, tiempos y posiciones no cambian. No hay fork simulado (ADR-010).

## 2. Escenario

Un `Scenario` agrupa inyecciones sobre una sesión. `Real` = escenario sin inyecciones (siempre existe).

- Se pueden crear varios escenarios por sesión.
- Cada escenario guarda su timeline de inyecciones y las decisiones de Jev asociadas (`ScenarioId`).
- Todo lo producido en un escenario ≠ `Real` se marca `Hypothetical = true` y se muestra con etiqueta **HIPOTÉTICO**.

## 3. Catálogo (8 eventos)

| # | Tipo | Parámetros | Efecto en Monte Carlo | Efecto en reglas | Reason |
|---|---|---|---|---|---|
| 1 | `SafetyCar` | `lap`, `durationLaps` (default 4) | Ritmo de SC para todos, gaps comprimidos, pit loss reducido (~50%) | — | `SC_DEPLOYED` |
| 2 | `VirtualSafetyCar` | `lap`, `durationLaps` (default 2) | Ritmo delta para todos, pit loss reducido (~35%) | — | `VSC_DEPLOYED` |
| 3 | `RedFlag` | `lap` | Carrera detenida; cambio de gomas libre; gaps reseteados en reanudación | prohíbe `BOX` | `RED_FLAG` |
| 4 | `Rain` | `lap`, `intensity` (`Light`/`Heavy`), `durationLaps` | Ritmo seco penaliza; inter/wet ganan | `Heavy` descarta secos | `RAIN_STARTING` |
| 5 | `Puncture` | `lap`, `driver` (default foco) | Vuelta lenta a boxes (+20–40 s) | fuerza `BOX` | `PUNCTURE` |
| 6 | `WingDamage` | `lap`, `severity` (`Minor`/`Major`) | `Minor`: −0,3 s/vuelta; `Major`: −1,5 s/vuelta | `Major` fuerza `BOX` | `WING_DAMAGE` |
| 7 | `RivalCrash` | `lap`, `rival`, `relative` (`Ahead`/`Behind`) | Rival eliminado; prob. SC aumentada esa vuelta | — | `RIVAL_INCIDENT` |
| 8 | `ErsFailure` | `lap`, `severity` | Despliegue reducido → −0,5 a −2 s/vuelta; sin overtake mode | descarta `DEPLOY`/`OVERTAKE` | `ERS_FAILURE` |

Valores numéricos: defaults en `Scenarios:*` (configurables). Son supuestos para la demo, no datos oficiales.

## 4. Flujo de inyección

1. Usuario elige evento + parámetros + vuelta en el panel de escenarios.
2. Se crea/usa el `Scenario` activo y se persiste `InjectedEvent`.
3. Si `lap <= vuelta actual` → efecto inmediato: triage Jev → (si `ACT_NOW`) estrategia.
4. Si `lap > vuelta actual` → se programa; se activa cuando el replay llega.
5. Timeline del escenario muestra: evento → decisión Jev → confianza → reasons.

## 5. Guion sugerido para la demo

1. Replay real, vuelta 15–18: cambio de inter a soft. Jev vs equipo.
2. Escenario "SC vuelta 40": inyectar SC → Jev `BOX` a intermedio/blando, mostrar latencia en Aspire.
3. Vuelta 60–63 real: lluvia → Jev `INTERMEDIATE` vs parada real de Alpine.
4. Escenario "Pinchazo vuelta 50" o "ERS failure": decisión forzada por reglas + Jev ajusta `energy_mode`.

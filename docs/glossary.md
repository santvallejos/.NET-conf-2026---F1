# Glosario

## Fórmula 1

| Término | Significado |
|---|---|
| **Pit wall** | Muro de boxes donde ingenieros y estrategas monitorean la carrera y toman decisiones |
| **Box / pit stop** | Entrada a boxes para cambiar neumáticos |
| **Stint** | Tramo de carrera entre paradas con un mismo juego de neumáticos |
| **Compuesto** | Tipo de neumático: SOFT, MEDIUM, HARD (seco), INTERMEDIATE, WET (lluvia) |
| **Degradación** | Pérdida de rendimiento del neumático por vuelta (s/vuelta) |
| **Cliff** | Caída brusca de rendimiento al final de la vida útil del neumático |
| **Pit loss** | Tiempo perdido por pasar por boxes vs seguir en pista |
| **Undercut** | Parar antes que el rival para ganarle con goma nueva |
| **Overcut** | Parar después que el rival aprovechando aire limpio |
| **SC (Safety Car)** | Auto de seguridad: neutraliza la carrera; parar es más barato |
| **VSC (Virtual Safety Car)** | Neutralización virtual con delta de tiempo obligatorio |
| **Bandera roja** | Carrera detenida |
| **Bandera azul** | Aviso a un piloto doblado para dejar pasar |
| **Race Control** | Dirección de carrera; emite banderas, mensajes y sanciones |
| **Gap / intervalo** | Diferencia con el líder / con el auto de adelante |
| **ERS** | Sistema de recuperación de energía (híbrido) |
| **Harvest / deploy** | Recuperar / desplegar energía eléctrica |
| **Overtake Mode** (2026) | Energía extra disponible a < 1 s del auto de adelante |
| **Straight / Corner Mode** (2026) | Aerodinámica activa: baja resistencia en recta / alta carga en curva |
| **Lift & coast** | Levantar el acelerador antes de frenar para ahorrar/recuperar |

## Técnico

| Término | Significado |
|---|---|
| **OpenF1** | API pública con datos de F1 (históricos gratis) |
| **`session_key`** | Identificador de sesión en OpenF1 (carrera Brasil 2026 = 11412) |
| **Replay** | Reproducción de la carrera real con reloj virtual controlable |
| **Monte Carlo** | Simular el resto de carrera miles de veces con variables aleatorias para obtener probabilidades |
| **Jev** | Clasificador de IA de TypeSafe ("System One"): devuelve opciones tipadas con probabilidades |
| **System One / Two** | Decisión rápida e intuitiva (Jev) vs deliberada y calculada (Monte Carlo) |
| **Triage** | Clasificar si un evento requiere acción (`NONE`/`MONITOR`/`ACT_NOW`) |
| **`state`** | JSON compacto con la "foto" de la vuelta que se envía a Jev |
| **Escenario** | Conjunto de eventos hipotéticos inyectados sobre una sesión |
| **HIPOTÉTICO** | Etiqueta de todo lo producido en un escenario inyectado |
| **ESTIMADO** | Etiqueta de valores no medidos (energía) |
| **Aspire** | Stack .NET para orquestar apps distribuidas con dashboard de observabilidad |

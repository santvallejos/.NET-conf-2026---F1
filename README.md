# PitWall .NET — F1

Pit wall de Fórmula 1 hecho en .NET 11 para .NET Conf 2026. Reproduce el GP de São Paulo 2026 con datos reales de OpenF1, calcula estrategia (degradación, energía, pit loss) y muestra qué pasa si el piloto entra a boxes ahora (posición al salir y posiciones perdidas, normal y bajo SC/VSC). Muestra solo el replay real de la carrera. Funciona offline.

Tema de la charla: **toma de decisiones de los equipos + tecnología**.

## Inicio rápido

```bash
# 1. Descargar datos (una vez, requiere internet)
dotnet run --project src/PitWall.Ingestion -- --session 11412 --quali 11408 --drivers 43,44,12,3

# 2. Ejecutar
dotnet run --project src/PitWall.Web
```

## Documentación

Ver [docs/README.md](docs/README.md). Spec: [docs/spec.md](docs/spec.md).

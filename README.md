# PitWall .NET — F1 + Jev

Pit wall de Fórmula 1 hecho en .NET 11 para .NET Conf 2026. Reproduce el GP de São Paulo 2026 con datos reales de OpenF1, calcula estrategia (degradación, energía, Monte Carlo) y consulta a **Jev** (TypeSafe AI) como motor de decisiones tipadas.

Tema de la charla: **toma de decisiones de los equipos + tecnología**.

## Inicio rápido

```bash
# 1. Descargar datos (una vez, requiere internet)
dotnet run --project src/PitWall.Ingestion -- --session 11412 --quali 11408 --drivers 43,44,12,3

# 2. (Opcional) API key de Jev
dotnet user-secrets set "Parameters:typesafe-api-key" "<key>" --project src/PitWall.AppHost

# 3. Ejecutar
dotnet run --project src/PitWall.AppHost
```

## Documentación

Ver [docs/README.md](docs/README.md). Spec: [docs/spec.md](docs/spec.md).

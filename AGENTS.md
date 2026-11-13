# AGENTS.md — Mapa de navegación para agentes de IA

> Este archivo es el **punto de entrada** para cualquier agente que trabaje en este
> repositorio. NO es una biblia de reglas: es un **mapa**. Lee solo lo que
> necesites cuando lo necesites (divulgación progresiva).

---

## 1. Antes de empezar

1. El desarrollo lo coordina el plugin `sdd-team`: la sesión principal es el
   leader (`/sdd-team:run`) y el código lo escriben los subagentes
   `sdd-team:*`.
2. Si eres un subagente, tu briefing trae la feature (`id`, `name`,
   `acceptance`, `depends_on`, `files`, `refs`). Lee solo las secciones de
   `docs/` que lista `refs` y `features/docs/conventions.md`.

## 2. Mapa del repositorio

`features/` **no se versiona** (`.gitignore`): contiene solo artefactos del
harness. `docs/` sí se versiona y es la fuente de verdad del producto.

| Archivo / carpeta | Qué contiene | Cuándo leerlo |
|---|---|---|
| `features/feature_list.json` | Backlog: `status`, `assignee`, `depends_on`, `files` (perímetro), `refs` | Leader, siempre |
| `features/docs/specs.md` | Proceso: modo directo, SDD, paralelo, cierre | Antes de trabajar una feature |
| `features/docs/conventions.md` | Estilo C#, verificación (`dotnet build`/`dotnet test`), archivos de registro compartidos, CHANGELOG | Antes de escribir código |
| `features/docs/architecture.md` | Índice hacia la arquitectura de `docs/` | Si no sabes qué doc leer |
| `features/specs/<NNN>-<name>/` | Spec, QA y review de features `"sdd": true` | Si la feature es `"sdd": true` |
| `features/cambios/<NNN>-<name>/cambios.md` | Qué cambió en cada feature cerrada | Al cerrar; para entender lo ya hecho |
| `features/CHANGELOG.md` | Una línea por feature cerrada | Al cerrar |
| `docs/spec.md` | RF/RNF y criterios de aceptación de la demo | Según `refs` |
| `docs/architecture.md` | Proyectos, dependencias permitidas, flujo runtime | Antes de diseñar o implementar |
| `docs/testing.md` | Qué se testea, fixtures, reglas (sin red, semilla fija, sin tests de UI) | Antes de verificar |
| `docs/decisions.md` | ADRs: no contradecirlos sin aprobación humana | Si una decisión los roza |
| `docs/*.md` (resto) | Dominio por área | Solo los que liste `refs` |
| `src/`, `tests/`, `PitWall.sln` | Código del producto (.NET 11) | Para implementar |

## 3. Reglas duras

- **Varias features a la vez, sin solape.** Cada implementer solo toca los
  archivos de su perímetro (`files`). Si necesita otro, para y devuelve
  `blocked`. Los cambios sin commitear fuera de tu perímetro son de otra
  feature: no los toques.
- **Verificación de a una:** `dotnet build` / `dotnet test` con el lock
  `features/locks/verificacion/` tomado.
- **Sin `done` sin evidencia** de verificación documentada.
- **Al cerrar:** `done`, línea en `features/CHANGELOG.md` y
  `features/cambios/<NNN>-<name>/cambios.md`.
- `features/feature_list.json` y `features/CHANGELOG.md` se editan con `Edit`
  sobre la entrada propia, nunca reescritos enteros.
- **Rama actual**, sin branches ni worktrees; sin commits.
- **Si no sabes algo, busca en `docs/`** antes de inventarlo. El harness nunca
  se menciona en código ni en `docs/`.

## 4. Si te bloqueas

- Relee la sección relevante de `docs/`.
- No inventes un workaround: libera el lock si lo tienes, marca
  `status: "blocked"` y explica la razón en el archivo que devuelves al
  leader (`cambios.md` en modo directo, `tasks.md` en SDD).

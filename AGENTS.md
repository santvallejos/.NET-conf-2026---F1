# AGENTS.md — Mapa de navegación para agentes de IA

> Este archivo es el **punto de entrada** para cualquier agente que trabaje en este
> repositorio. NO es una biblia de reglas: es un **mapa**. Lee solo lo que
> necesites cuando lo necesites (divulgación progresiva).

---

## 1. Antes de empezar (obligatorio)

1. Lee `features/feature_list.json`. La feature objetivo es la que indique el
   humano; si no lo indica, la primera no-`done` / no-`blocked`. Toda
   feature nueva (`"sdd": true`) pasa por **Spec Driven Development** —
   ver `features/docs/specs.md` y §4 de este archivo. Las features con
   `"sdd": false` siguen el **modo directo** (sin spec ni puerta humana),
   ver §4.
2. Lee `features/docs/specs.md` antes de tocar cualquier spec o feature `sdd: true`.

## 2. Mapa del repositorio

`features/` **no se versiona** (`.gitignore`): contiene solo artefactos del
harness. `docs/` sí se versiona y es la fuente de verdad del producto.

| Archivo / carpeta            | Qué contiene                                                                | Cuándo leerlo |
|------------------------------|-----------------------------------------------------------------------------|---------------|
| `features/feature_list.json` | Backlog con estado (`pending`/`spec_ready`/`in_progress`/`done`/`blocked`), `assignee` y `refs` (secciones de `docs/` que aplican) | Siempre, al empezar |
| `features/specs/<feature>/`  | `requirements.md` + `design.md` + `tasks.md` (Kiro-style) + `review.md` (veredicto del gate estructural) | Antes de implementar cualquier feature con `"sdd": true`. En modo directo solo `tasks.md` + `review.md` |
| `features/CHANGELOG.md`      | Registro de features terminadas (Keep a Changelog)                          | Al cerrar una feature (`status: done`) |
| `features/docs/specs.md`     | Proceso SDD: EARS notation, los 3+1 archivos, puerta de aprobación humana   | Antes de redactar o leer un spec |
| `features/docs/conventions.md` | Estilo C#, nombres, errores y **cómo se verifica** (`dotnet build`/`dotnet test`) | Antes de escribir código |
| `docs/spec.md`               | Especificación del producto: RF/RNF, criterios de aceptación de la demo     | Al redactar specs |
| `docs/architecture.md`       | Proyectos de la solución, dependencias permitidas, flujo runtime            | Antes de diseñar o implementar |
| `docs/testing.md`            | Qué se testea, fixtures, reglas (sin red, semilla fija, sin tests de UI)    | Antes de verificar |
| `docs/decisions.md`          | ADRs: no contradecirlos sin aprobación humana                               | Si una decisión de diseño los roza |
| `docs/*.md` (resto)          | Dominio por área (`data-openf1`, `strategy-engine`, `jev`, `scenarios`, `ui`, `configuration`, `glossary`) | Solo los que liste `refs` de la feature |
| `.claude/agents/`            | Definiciones de subagentes (`leader`, `spec_author`, `implementer`, `reviewer`) | Si orquestas trabajo |
| `src/`, `tests/`, `PitWall.sln` | Código real del producto (.NET 11)                                       | Para implementar |

No existe carpeta `progress/` en este kit: cómo llevas tu propio avance
mientras trabajas una feature (notas, tu propio agente, herramientas
externas) es cosa tuya — no se prescribe ni se audita.

## 3. Reglas duras (no negociables)

- **Una sola feature `in_progress` en todo el repositorio.** El trabajo es
  estrictamente secuencial: se termina una antes de arrancar la siguiente.
- **No declares una tarea `done` sin evidencia de que funciona.** Documenta
  la verificación (test, comando ejecutado, smoke test manual...) inline
  en la task correspondiente de `features/specs/<feature>/tasks.md` (campo
  `Evidencia:`) antes de cerrar.
- **La conformidad del código con `docs/` la garantiza quien lo escribe.**
  El reviewer es un gate estructural y no lee código.
- **No saltes la fase de spec.** Toda feature con `"sdd": true` debe pasar
  por `spec_author` y obtener aprobación humana antes de tocar código.
  Las `"sdd": false` no tienen spec (modo directo).
- **No saltes la puerta de aprobación humana** en features `"sdd": true`.
  El leader detiene el flujo en `spec_ready` y espera.
- **Al cerrar una feature (`done`), añade una línea a `features/CHANGELOG.md`**
  bajo `## [Unreleased]` — es el único registro de cierre que exige el kit.
- **Trabaja sobre la rama actual.** No crees branches ni worktrees por
  feature.
- **Deja el repositorio limpio** antes de cerrar la sesión (ver §5).
- **Si no sabes algo, busca en `docs/`** antes de inventarlo.

## 4. Flujo de trabajo (SDD)

```
pending → [spec_author] → spec_ready → ⏸ HUMANO APRUEBA → in_progress → [implementer → reviewer] → done
```

1. El leader identifica la feature objetivo con status `pending` y
   `"sdd": true`.
2. El leader lanza `spec_author` con el briefing de la feature (`id`,
   `name`, `acceptance`, `refs`) en el prompt. El `spec_author` crea
   `features/specs/<name>/{requirements,design,tasks}.md`; el **leader** marca el
   status como `spec_ready`.
3. **Pausa.** El humano lee el spec en `features/specs/<name>/` y aprueba (o pide cambios).
4. Una vez aprobado, el leader cambia el status a `in_progress` y lanza `implementer`.
5. El implementer ejecuta `tasks.md` una a una, marcándolas `[x]` con su
   `Evidencia:` inline.
6. El reviewer (gate estructural) verifica trazabilidad `R<n>` ↔ evidencia
   y tasks completas leyendo solo `features/specs/<name>/{requirements,tasks}.md`;
   escribe veredicto en `features/specs/<name>/review.md`.
7. Si aprueba, el implementer marca `done` en `features/feature_list.json` y añade
   la entrada correspondiente a `features/CHANGELOG.md`.

### Modo directo (`"sdd": false`)

```
pending → [leader] → in_progress → [implementer → reviewer] → done
```

1. El leader marca `in_progress` y lanza `implementer` con el briefing
   (`id`, `name`, `acceptance`, `refs`) y la indicación "modo directo".
2. El implementer crea `features/specs/<name>/tasks.md` con `## Acceptance`
   (`A1..An`) y tasks `T<n>` con `Cubre: A<n>` + `Evidencia:`.
3. El reviewer valida `A<n>` ↔ evidencia leyendo solo ese `tasks.md`.
4. Pasos 7 y §5 iguales. Detalle: `features/docs/specs.md` §Modo directo.

## 5. Cierre de sesión (lifecycle)

Antes de terminar:

1. Si la tarea está acabada: marca `status: "done"` en `features/feature_list.json`.
2. Añade una línea a `features/CHANGELOG.md` bajo `## [Unreleased]`.
3. No dejes archivos temporales, ni código de debug, ni TODOs sin contexto.

## 6. Si te bloqueas

- Relee la sección relevante de `docs/`.
- Si la herramienta no hace lo que esperas, **no inventes un workaround**:
  marca `status: "blocked"` en `features/feature_list.json`, documenta el bloqueo en
  la sección `## Bloqueado` de `features/specs/<feature>/tasks.md` y para la sesión.

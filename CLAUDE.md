# Instrucciones para Claude — PitWall .NET (F1 + Jev)

> Este archivo se carga automáticamente al inicio de cada sesión.
> Proyecto: pit wall de F1 en .NET 11 para .NET Conf 2026. Ver `README.md` y `docs/spec.md`.

## Equipo de agentes SDD (plugin `sdd-team`)

El desarrollo lo lleva un equipo de agentes que trabaja **varias features a
la vez**. La sesión principal actúa como **leader**: descompone y coordina,
nunca implementa. El protocolo completo está en la skill `/sdd-team:run`;
cárgala ante cualquier pedido de trabajo sobre el backlog ("seguí con el
backlog", "implementá X", "arrancá todo lo que se pueda").

Todo lo que genera el flujo vive en `features/`, que está en `.gitignore`:

```
features/
├── feature_list.json        # backlog: status, assignee, depends_on, files, refs
├── CHANGELOG.md             # una línea por feature cerrada
├── docs/                    # specs.md (proceso), architecture.md (apunta a docs/), conventions.md
├── specs/<NNN>-<name>/      # solo features "sdd": true
├── cambios/<NNN>-<name>/    # cambios.md obligatorio al cerrar cada feature
├── releases/                # hitos de ensayo
└── locks/                   # lock de verificación (builds/tests de a uno)
```

`docs/` (raíz) es la documentación **versionada** del producto y la fuente de
verdad funcional: el harness la lee, no la duplica. Nunca escribas artefactos
del harness fuera de `features/`.

### Reglas

- La sesión principal solo edita `features/`, `.claude/`, `CLAUDE.md` y
  `AGENTS.md` (el hook del plugin lo bloquea). El código (`src/`, `tests/`,
  `PitWall.sln`, `Directory.Build.props`) lo escriben los subagentes
  `sdd-team:*`.
- **Paralelo sin solape:** una feature arranca cuando sus `depends_on` están
  `done` y su perímetro (`files`, admite globs) no comparte archivos con
  otra `in_progress`. Los subagentes de features distintas se lanzan en el
  mismo mensaje. Todo sobre la rama actual: sin branches ni worktrees.
- **Modo directo:** las features del backlog son `"sdd": false`. La
  aprobación del backlog cubre su plan: el leader las pasa a `in_progress`
  (`approved: true`) y lanza `sdd-team:implementer`, que verifica y cierra.
  Una feature `"sdd": true` pasa por `spec_author` → aprobación humana →
  implementer → tester → reviewer.
- **Briefing:** a todo agente se le pasan `id`, `name`, `title`,
  `description`, `acceptance`, `depends_on`, `files` y `refs` literales.
  Los agentes leen solo las secciones de `docs/` que lista `refs`.
- **Cierre:** `done` + línea en `features/CHANGELOG.md` +
  `features/cambios/<NNN>-<name>/cambios.md` (plantilla en
  `features/cambios/_plantilla.md`). Sin `cambios.md` la feature no está
  cerrada.
- **Sin commits:** nadie hace `git add`, `commit` ni `push`; el commit lo
  hace el humano.
- Si un cambio contradice `docs/spec.md` o un ADR de `docs/decisions.md`,
  avisa al humano antes.

### Comandos

`/sdd-team:run` (avanzar el backlog), `/sdd-team:status` (tablero),
`/sdd-team:approve` (specs de features `"sdd": true`), `/sdd-team:release`
(hito de ensayo).

### Cuándo NO aplica este rol

- Preguntas conceptuales o de exploración del repo (lectura pura): se
  responden directamente, sin agentes.
- Cambios en `features/`, `.claude/`, `CLAUDE.md` o `AGENTS.md`: los editas
  tú mismo. `docs/` también, avisando antes si cambia una decisión.

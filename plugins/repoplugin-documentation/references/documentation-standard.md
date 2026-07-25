# Repository documentation standard

Use this standard for repository-specific documentation without copying repository truth into a reusable skill.

## Roles

| Artifact | Purpose |
| --- | --- |
| Root `AGENTS.md` | Thin, durable route map: authority order, repository map, validation entry points, and pointers to local instructions. |
| Nested `AGENTS.md` | Local deltas only: boundary, owner, caller, commands, and local hazards. Do not restate root guidance. |
| `README.md` | Human-oriented purpose, local start, and supported operation. Keep it distinct from agent instructions. |
| ADR / decision record | A durable decision, its context, alternatives, consequences, status, and supersession. |
| Context map | Query-oriented routes from topic, task, or path to the minimum authority plus dynamic searches for owner, callers, configuration, tests, and commands. |
| Plan | Time-bounded intended work; link to the decision and evidence it relies on. |

## Bootstrap records

Write ordinary Markdown under `<task>/documentation/`:

- `inventory.md`: all included material, source path, type, owner/status if known, and exclusions.
- `claim-map.md`: each unique claim, its source(s), authority/status, proposed destination or retained source, and uncertainty.
- `context-map.md`: the query-oriented map.
- `open-questions.md`, `contradictions.md`, and `awareness.md`: uncertainty the user or a later route must see.
- `bootstrap-plan.md` or `maintenance-plan.md`: intended edits and zero-loss mapping.
- `audit.md`: scope, checks, findings, exclusions, and evidence.

Keep source files intact until their claims have a recorded destination or explicit retained status. A reorganisation is zero-loss only when every unique source claim is retained, deliberately superseded, or retained in its original location with a visible route.

## Context-map format

For each route provide: query/topic/path pattern, minimum authority to read first, then the dynamic repository searches that find policy owner, real callers, configuration, tests, and executable validation commands. Prefer links and commands that can be checked over narrative copies of mutable facts.

## Contradictions and questions

Record each contradiction as `DOC-CON-NNN` with both verbatim-safe summaries, source paths, authority/status, impact, and the exact user decision needed. Never resolve a contradiction by inference or deleting one side. The user alone chooses the resolution. A question marked Answered remains blocking until its answer is incorporated and a full applicable rescan finds no unresolved contradiction or missing route.

Ask one question at a time when a missing truth would materially change structure, authority, commands, ownership, or a claim. Record an assumption and its impact when work may safely continue.

## Maturity horizons

- `0.x`: pre-alpha
- `1.x`: alpha
- `2.x`: beta
- `3.x+`: release

Document future features in horizons, seams, migration notes, and decisions where they constrain present choices. Do not add dormant code, projects, services, tables, routes, dependencies, or release gates merely to represent a future horizon.

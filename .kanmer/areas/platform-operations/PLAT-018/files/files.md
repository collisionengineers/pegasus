# Files — PLAT-018

## Where the change lands

| Path | Why |
|---|---|
| `docs/design/README.md` | Remove `queue` from the banned operator-copy words and clarify that only the closed approved necessary-copy list permits a consequence sentence. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/design/README.md` | Contains both contradictions and the controlling nearby rules: `queue mechanics` remains prohibited; the approved-copy list is the sole source of permitted guidance. |
| `.kanmer/areas/platform-operations/PLAT-018/PLAT-018.md` | Records the operator direction, origin/dev baseline, exact intended scope, and the separation from [[MAIL-006]] and [[PLAT-019]]. |
| `src/Pegasus.Web/Pages/Shared/_Layout.cshtml` | Demonstrates the mandated operator-visible `Queues` shell label that makes the blanket word ban self-contradictory. |
| `.kanmer/areas/platform-operations/PLAT-019/PLAT-019.md` | Explains the separate shared-dialog removal that applies the closed approved-copy list; do not duplicate its markup changes here. |

## Ripple effects

No runtime caller, test, build artifact, or deployment changes follow. Review compares the documentation-only diff against the two intended textual corrections and confirms no unrelated file changed.

## Out of scope

- Changing current UI labels, including the Inbox page’s separate `Queue` label owned by [[MAIL-006]].
- Removing guidance copy or changing shared dialogs, owned by [[PLAT-019]].
- Changing the existing ban on implementation-facing “queue mechanics”.
- Revising PRDs, FRDs, ADRs, source code, styles, tests, or the approved-copy entries themselves.

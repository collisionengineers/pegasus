# Files — DELIV-043

## Where the change lands

| Path | Why |
|---|---|
| `.github/workflows/ci.yml` | gains a workflow-level `concurrency` block; `infrastructure`, `unit`, `sql-integration`, `browser` and `test-ui` gain `needs` on the three cheap jobs; `sql-integration-coverage` skips when the shards were skipped. Any change here forces `build=true` and `infrastructure=true` (`scripts/Get-CiChangeFlags.ps1:11-12`), so the PR itself runs every lane. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `scripts/Get-CiChangeFlags.ps1` | the two flags the heavy lanes key on; unchanged here. |
| `docs/engineering.md` "Branches and delivery" (`:44-52`) | "Green means every `repository-check` job for the PR's head revision succeeded or was path-skipped" — a lane skipped because preflight failed is not green, which is the intended reading. |
| `.github/workflows/ci.yml` comments on `sql-integration-coverage` | `always()` exists so a failing shard still reports; it must not fire when the shards never ran. |
| Kanmer `info-pack` audits (2026-09-03) | the measurements: 89 runner-minutes downstream of a 1.2-minute documentation failure; overlapping runs on the same branch. |

## Ripple effects

- Green runs gain the cheap jobs' duration (about one to two minutes, parallel) before heavy lanes start.
- No job is renamed; branch-rule and documentation references stay valid.

## Out of scope

- `push: branches: [dev]` CI (cost decision for the operator), the duplicate Azure-plan invocation in `infrastructure`, build-once fan-out, Test UI capture reuse, shard balancing, SDK pinning: lane-content work that waits for Kanmer's generated workflow.

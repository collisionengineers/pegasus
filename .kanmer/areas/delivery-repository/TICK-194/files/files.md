# Files — TICK-194

## Where the change lands

| Path | Why |
|---|---|
| `.github/workflows/ci.yml` | Invoke the main-history guard in the existing always-running, full-history `changes` job for `push` events on `main`; failure must stop the workflow before path-based lanes proceed. |
| `scripts/Test-MainBranchHistory.ps1` (new) | Validate an explicit before/head range, fail closed for unavailable or rewritten history, and reject every single-parent commit on the new first-parent segment. |
| `tests/Pegasus.ArchitectureTests/MainBranchHistoryGuardTests.cs` (new) | Exercise the script against isolated temporary Git repositories for permitted and rejected history shapes without depending on mutable Pegasus commits. |
| `docs/engineering.md` (conditional) | Record the executable guard only if this adds useful current guidance and the active `KANMER-002` documentation claim explicitly releases this file; the existing merge-only rule already owns the policy. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `EPIC-001/context.md` | Excludes Web presentation, UI browser/snapshot tests, design assets, and Stitch assets; requires a fresh overlap check before implementation. |
| `docs/engineering.md` | Defines the authoritative merge-only, append-only `dev` to `main` rule and distinguishes repository-check behavior from product evidence. |
| `.github/workflows/ci.yml` | Supplies the existing main-push trigger, full-depth checkout, event before/head revisions, and the always-running changes job where the guard belongs. |
| `AGENTS.md` | Requires task worktrees/PRs and separately authorised `dev` to `main` merges; it does not authorise a cloud or GitHub settings change. |
| `tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs` | Shows the repository precedent for invoking PowerShell subprocesses from architecture tests and asserting exit/output behavior. |
| `.github/workflows/workspaces.yml` | Confirms a second main-push workflow exists, but it is path-filtered to imported workspaces and is not the canonical repository-wide enforcement point. |

## Ripple effects

- A violating push has already changed `main`; the new job makes
  `repository-check` red and provides diagnostics but does not roll back or
  prevent the push.
- The `changes` job becomes a required dependency for this policy, so guard
  errors prevent all downstream build/test lanes.
- Adding the script and architecture test is build-relevant under the current
  path detector and therefore exercises the normal validation lanes in its PR.
- Workflow comments or engineering guidance should state that branch protection
  is a separate preventive control and is not proved by this CI check.

## Out of scope

- All `src/Pegasus.Web/**`, UI-focused browser/snapshot tests, `design/**`,
  and `.stitch/**`.
- GitHub branch-protection/ruleset changes, repository settings, credentials,
  automatic rollback, force-push recovery, deployment, and other external
  writes.
- Retargeting the ticket's retired the retired pre-Kanmer tracker (historical evidence) citation, owned by `KANMER-001`.
- Broad documentation cleanup owned by `KANMER-002`.
- Changes to `.github/workflows/workspaces.yml`; repository-wide enforcement
  belongs in `repository-check`.

# Files — DELIV-003

## Where the change lands

| Path | Why |
|---|---|
| The DELIV-003 task branch and PR to `dev` | Carries the one-time, non-rewriting merge of the current `main` history into `dev`; it must be created only after DELIV-002 has supplied the explicit allowance. |
| `docs/operations.md` | Potential current-state release evidence. Refresh only from the observed promotion and CI result, not a predicted SHA. |
| `docs/current-architecture.md` | Potential as-built release state refresh required by the repository safety rail; do not change product-architecture facts unless the actual release changed them. |
| DELIV-003 Kanmer `proof` document | Records the exact preflight SHAs, ancestry checks, non-force promotion, equal-head read-back, CI result, and documentation-refresh determination. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `AGENTS.md` | The single allowed/banned Git-operation list, ticket/worktree rule, proof timing, exact release authorization, and the requirement to refresh current-state docs after a release. Read the version merged by DELIV-002, not the stale baseline. |
| `docs/engineering.md` | The canonical detailed fast-forward release procedure that DELIV-002 will establish. It must be followed verbatim for the exact-SHA promotion. |
| `scripts/Test-MainBranchHistory.ps1` | The DELIV-002 revision should test append-only `main` history and containment in `dev`; inspect it before relying on the push CI result. |
| `.github/workflows/ci.yml` | Shows the post-push guard, its fetch of `origin/dev`, and the CI run whose result is release evidence. |
| `docs/operations.md` and `docs/current-architecture.md` | The authoritative current-state documents that must be reread after the release so any update states observed facts only. |
| [[DELIV-002]] research and plan | Establish the no-protection decision, the target invariant, and the implementation dependency. |
| `.codex/config.toml` | Currently modified by another user; it is explicitly excluded from this ticket. |

## Ripple effects

- The ordinary task PR must first land the convergence merge on `dev`; the
  later `main` promotion contains that merge as an existing `dev` commit
  and should not create another release merge.
- The main-push CI guard proves the resulting `main` head is in the then
  fetched `dev` history, but the operator's immediate equal-head check proves
  the exact release point.
- DELIV-002's policy must be checked in the branch actually used. If it lacks
  the one-time `origin/main` convergence allowance, this ticket is blocked
  and must return the gap to DELIV-002.
- Refreshing current-state documentation may add a small documentation commit
  to the DELIV-003 PR before its merge to `dev`; never attempt to amend
  shared refs after the exact-SHA promotion to “fix” documentation.

## Out of scope

- Implementing or altering DELIV-002's policy, history guard, workflow, or
  architecture tests.
- GitHub branch protection, rulesets, merge-method configuration, credentials,
  or any other GitHub setting.
- Rebasing, resetting, force-pushing, deleting, or otherwise rewriting
  `dev` or `main`.
- Product, deployment, cloud, or application-code changes.
- The unrelated root-worktree `.codex/config.toml` modification.

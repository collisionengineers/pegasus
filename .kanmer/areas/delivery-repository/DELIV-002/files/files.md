# Files — DELIV-002

## Where the change lands

| Path | Why |
|---|---|
| `docs/engineering.md` | Replace the merge-commit release rule with the agreed linear release invariant and exact promotion procedure. |
| `AGENTS.md` | Align the task workflow’s `dev` → `main` authorization and allowed-operation language with an approved fast-forward promotion; retain the no-rewrite rule. |
| `scripts/Test-MainBranchHistory.ps1` | Replace the two-parent-merge predicate with the linear-release checks while retaining append-only-history rejection. |
| `.github/workflows/ci.yml` | Keep the `main`-push guard wired to the revised script and pass any additional release evidence the chosen guard needs. |
| `tests/Pegasus.ArchitectureTests/MainBranchHistoryGuardTests.cs` | Replace merge-only test expectations with accepted fast-forward promotion, rejected non-fast-forward rewrite, and rejected unauthorized shape/identity cases supported by the selected enforcement design. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/index.md` | `docs/engineering.md` owns working delivery guidance and `AGENTS.md` owns task claims and Git safety; neither a PRD nor an ADR should be invented for this process change. |
| `docs/operations.md` | Release entries are current-state evidence, not the source of repository workflow. Do not rewrite historical release facts; update this only if a later task actually deploys. |
| `scripts/Test-MainBranchHistory.ps1` | The existing `Before`/`Head` contract validates only the pushed `main` range. It has no reliable source-branch input or pre-push control. |
| `.github/workflows/ci.yml` | The guard is a post-push CI check on `main`, while PR checks run independently. The `changes` job is shared with path classification, so preserve its outputs and checkout history. |
| `tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj` | The guard tests run within the architecture-test project, which also references the application projects; keep the focused test run usable through the canonical test workflow. |
| GitHub repository settings for `collisionengineers/pegasus` | There are currently no `main` protection rules or rulesets, so remote restriction must be configured outside the repository if prevention—not post-push detection—is required. |
| [GitHub merge methods](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/about-merge-methods-on-github) | GitHub rebase merge rewrites SHAs; do not choose it when equality with the existing `dev` ref is an acceptance criterion. |

## Ripple effects

- The source workflow and architecture tests must change together; changing only the script will leave CI assertions inconsistent.
- The review/release instructions must name the same authorization boundary in both `AGENTS.md` and `docs/engineering.md`.
- Remote rules or a restricted release actor are GitHub configuration, not a repository-file change, and require exact-target approval before they are changed.
- The first release under the new policy needs a one-time non-rewriting convergence of `dev` with the existing `main` release commit before the fast-forward promotion.

## Out of scope

- Rewriting existing `main` or `dev` history.
- Altering product behavior, deployment topology, or release evidence already recorded in `docs/operations.md`.
- Treating GitHub rebase or squash merge as an exact fast-forward.
- Applying any GitHub protection, ruleset, credential, or release-ref update without explicit approval for the exact external target.

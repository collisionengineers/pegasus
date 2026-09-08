# Files — DELIV-059

## Where the change lands

| Path | Why |
|---|---|
| `docs/operations.md` | The single canonical owner for the missing dated release-39 operational record. Add only qualified historical deployment evidence and its limitations; do not turn it into a current-state claim. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/index.md` | Operations owns last observed deployment/support facts; architecture owns source structure. This prevents placing release identifiers in the wrong document. |
| `docs/engineering.md` | Evidence tiers distinguish source identity, artifacts, deployment and accepted behaviour. Historical PR narrative cannot be upgraded to fresh environment proof. |
| `docs/current-architecture.md` | Source/composition boundary only. It is deliberately not edited for release-39 operational facts. |
| `docs/runbook.md` | Current release and recovery procedure, including the portable workstation boundary. Do not restore PR #676's superseded Linux-only instructions. |
| `.agents/skills/pegasus-release/SKILL.md` | Canonical current release route. It remains unchanged; the historical record grants no deployment authority. |
| [PR #676 operations record](https://github.com/collisionengineers/pegasus/blob/93255e5c188865e5b0dab95d6c628ab49a902d99/docs/operations.md) | The retained, unmerged source of the release-39 source/image/manifest, rejected/replacement ZIP, migration/reset and deviation narrative. It is historical provenance, not a fresh Azure receipt. |
| [CI run 34132950893](https://github.com/collisionengineers/pegasus/actions/runs/34132950893) | Confirms the release source SHA and the failed `test-ui` job, while contradicting the PR narrative that browser was cancelled. It does not prove an operator waiver. |
| `DELIV-054/proof/proof.md` | Exact merged PR #703 proof for the later portable ZIP source correction. It explicitly excludes real artifact/deployment evidence. |
| `DELIV-048/proof/proof.md` | Portable workstation evidence and its still-inconclusive coordinated artifact obligation; it is not release-39 Azure proof. |
| `DELIV-047/proof/proof.md` | Retained historical Linux release lane and its distinct unfinished live proof; it must not be reopened here. |

## Ripple effects

- Documentation-only verification: relevant link/placement checks, a literal
  comparison with PR #676's retained record, and independent semantic review.
  No restore, build, test, package, Azure inventory, migration, deployment or
  smoke run is appropriate.
- Root needs the exact future documentation merge SHA alongside
  `6509746913eda16d2c4440add20e7f6793500f0b` to make an explicit,
  evidence-backed #676 superseded disposition.
- The current release-39 artifact directory named by PR #676 was absent on this
  workstation during research. Its hashes and deployment result remain
  qualified historical claims unless an authorised later task supplies the
  retained artifacts/receipts.

## Out of scope

- Every file other than `docs/operations.md`, including current architecture,
  runbook, release skill, ADRs, scripts, tests and build artifacts.
- PR #676 merge, closure, comments, review-state changes, DELIV-047/048/054
  claims, Kanmer status beyond this research transition, and all worktree
  actions.
- Fresh Azure/database/artifact verification, production status claims,
  migration/reset execution, credentials or operational commands.
- Reintroducing Linux-only guidance, duplicate release procedures, or a
  preservation/compatibility mechanism for disposable development data.

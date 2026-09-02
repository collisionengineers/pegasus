# Post-implementation report — UIIMP-013

## Result

The Test UI gate still runs on every build-relevant pull request and retains the
same 415 capture tests plus the single snapshot verifier. Capture is now split
into a two-thread browser phase and a non-browser phase using the repository's
four-thread project default. The second phase and verify reuse the first build
and both capture phases share one once-wiped directory.

The three exact-SHA performance executions of
`fa7d82ed95c7dc8a0b90f9d22db74118603def75` passed in 22:42, 21:32, and
20:50. The median was 21:32 and the maximum 22:42. Applying the plan's formula
to the 22:42 slowest sample produced a 35-minute step budget and 40-minute job
budget. Final SHA `35667cb176baf31eceaa3eefa77ddb7ec3111ac8` passed the full
repository check under those budgets; its snapshot step took 25:04.

## Changed files

- `scripts/Update-TestUiSnapshots.ps1` — owns the disjoint browser/non-browser
  split, build reuse, shared capture directory, and phase timing/failures.
- `.github/workflows/ci.yml` — retains the build-affecting trigger, corrects
  the gate commentary and incomplete-run diagnostic, and applies measured
  35/40-minute budgets.
- `docs/runbook.md` — records the browser/non-browser concurrency split beside
  the existing Test UI command guidance.

No file under `docs/design/test-ui/**`, `src/**`, `tests/**`, or
`AGENTS.md` changed.

## Governing documents

- `docs/engineering.md`: reused the existing filter, project concurrency cap,
  browser cap, and script boundary; introduced no dependency or public
  abstraction.
- `docs/design/README.md`: unchanged; snapshots remain derived from fresh
  routed Razor responses and the existing verifier remains authoritative.
- `docs/runbook.md`: updated only where the canonical Test UI execution
  behavior is described.
- No ADR was required because no architecture, runtime, dependency, or product
  contract changed.

## Verification

All commands below ran from
`../pegasus-worktrees/uiimp-013-test-ui-cost`.

- `dotnet restore ./Pegasus.slnx --locked-mode` — PASS, exit 0.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — PASS,
  exit 0, 0 warnings, 0 errors.
- Core tests — PASS, 1185/1185.
- Architecture tests — PASS, 100/100.
- Canonical non-Corpus integration attempt — INCONCLUSIVE, exit 1: this
  workstation has no LocalDB runtime (SQL Network Interfaces error 52). The
  failure was retained; repository-check runs 33633170699 and 33641477638
  supplied green integration evidence.
- Filter enumeration — PASS, exit 0: original 415; browser 119; non-browser
  296; partition sum 415.
- `pwsh ./scripts/Test-UiCatalogue.ps1` — PASS, exit 0: 54 routed sources,
  58 prototypes, 0 broken references.
- `pwsh ./scripts/Test-DocumentationLinks.ps1` — PASS, exit 0: 87 files.
- PowerShell parse and `git diff --check` — PASS, exit 0.
- Repository-check run 33633170699, attempts 1–3 — PASS on the exact
  performance SHA.
- Repository-check run 33641477638 — PASS on the final timeout SHA.

The stale-file and orphan assertions were not weakened or changed. Their
negative injection proof remains the linked UIIMP-005 evidence; a new local
injection could not be executed without LocalDB and no temporary snapshot
mutation was pushed to CI.

## Risks and follow-ups

- Hosted-runner variance remains visible: the final safety run took 25:04
  although the three acceptance samples topped out at 22:42. The measured
  35-minute step cap retains at least 50% headroom over the acceptance sample
  and passed that slower safety run.
- No follow-up implementation is required. Review should reject any proposal
  to reuse broader CI captures or schedule the gate only for UI paths unless a
  separate ticket proves the curated capture and stale/orphan guarantees remain
  complete.

## Traceability

- Commits:
  `fa7d82ed95c7dc8a0b90f9d22db74118603def75`,
  `35667cb176baf31eceaa3eefa77ddb7ec3111ac8`.
- Pull request: https://github.com/collisionengineers/pegasus/pull/644
  targeting `dev`.
- Kanmer: UIIMP-013.

## Verification handoff

After merge, `kanmer-verify` should check the exact merge SHA with locked
restore, Release build, the canonical non-Corpus test gate, fresh
`Update-TestUiSnapshots.ps1 -Verify`, and `Test-UiCatalogue.ps1`. Confirm the
two capture counts remain 119 and 296, verify runs once over their shared
capture, and the workflow retains the 35-minute step / 40-minute job budgets.

## Remediation round 1

- F-001: changed the incomplete-run diagnostic from a 40-minute step budget
  to the configured 35-minute step budget in `.github/workflows/ci.yml`.
- Remediation commit:
  `8116ac7b5545149670eb318708a2a4181bdba786`.
- Scope: one diagnostic string; no capture behavior, timeout, test, snapshot,
  trigger, or other workflow lane changed.
- Verification: `git diff --check` PASS; exact workflow text confirms the
  configured timeout and diagnostic both name 35 minutes. The updated PR CI
  run supplies final-head workflow validation.

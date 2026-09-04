# Plan — PLAT-073: Provision Linux-native WSL toolchain

## Objective

Provision the exact Linux-native WSL tools, initialize repository-owned payloads, reconcile Kanmer v0.4.1, and correct only execution-proven cross-platform defects.

## Governing docs

`docs/runbook.md` owns workstation requirements; `AGENTS.md` owns workflow; `EPIC-013/context.md` binds Linux storage, no Windows PATH, no cloud write and sequencing. `scripts/Invoke-Doctor.ps1` remains the executable prerequisite authority and `scripts/PegasusPlatform.ps1` remains the one platform owner.

## Ordered steps

### Step 1 — Provision exact host tools

Select Node 24 and install the exact Offline and Cloud prerequisite versions. Retain Windows interop while disabling Windows PATH import. Do not authenticate or write to a vendor service.

### Step 2 — Initialize repository-owned payload

Use `scripts/Initialize-LocalDevelopment.ps1`, locked restore and Release build. Run non-Corpus verification in the documented Core, Architecture, SQL-backed integration and Browser lanes so the 8 GiB host does not overlap SQL and browser-heavy workloads.

### Step 3 — Reconcile Kanmer and guidance

Refresh the v0.4.1 managed block and skill projections. Correct only the observed sqlcmd version format and PowerShell diagnostic formatting, then run repository and Kanmer checks.

### Step 4 — Deliver for independent review

Commit, push, open a PR targeting `dev`, report all evidence and move to Review. Do not self-review or self-merge.

## Acceptance checks

- Both Doctor profiles pass without authentication and required executables resolve outside `/mnt`.
- Locked restore, Release build, Core, Architecture, SQL-backed non-browser integration and Browser lanes pass.
- Kanmer build/headless smoke, documentation links, Markdown placement and diff checks pass.

## Failure and deviation record

An unconfigured solution test failed because Linux has no LocalDB and SQL variables were absent; it also exposed PowerShell-formatted wrapping in two architecture assertions. A configured combined SQL/browser run was interrupted after about 30 minutes by memory/swap thrashing. Both attempts remain evidence. The documented split lanes subsequently passed and their container and secret cleanup completed.

## Stop condition

Stop with the PR open in Review. Do not self-review, merge, or start dependent tickets.

## Simplification pass — 2026-09-04

- Reused the existing Doctor, platform, initialization and test-lane owners.
- Limited sqlcmd parsing to the vendor's optional `v` prefix.
- Used unformatted stderr for the existing history diagnostic and one test-local whitespace normalizer for all affected exact assertions.
- Split only verification workloads; production parallelism is unchanged.
- No further behavior-preserving simplification was identified. Kanmer changes are pinned mechanical reconciliation.

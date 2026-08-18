# Plan — DELIV-007

## Premises (verified read-only, 2026-08-18)

- `.github/workflows/qdos-pressure.yml` runs nightly (`0 3 * * *`) + dispatch;
  its only run (32096873724, main `2b0df78c`) failed on
  `FailureInjectionTests.ConcurrentReplayPressureProducesOneDurableReceipt`
  asserting `/Received` while SIMPLI-008 redirects manual uploads to
  `/Upload/Status/{id}` (later replays `?duplicate=true`) — stale assertion, not
  a regression; all eight replays land on one receipt.
- `docs/runbook.md:679-700` and `docs/operations.md:58,67-73,83` describe the
  lane as a diagnostic, not a gate. `docs/capabilities.md`, FRDs, PRDs, ADRs do
  not reference `CiPressure` or `QdosPressure`.
- `scripts/Invoke-QdosAlphaAcceptance.ps1` runs the pressure staging for **both**
  profiles (lines 657-689); `OfflineCandidate` additionally runs the
  `Category=QdosAlphaAcceptance` lane. `PEGASUS_QDOS_PRESSURE_PROFILE` has no
  consumer outside `tests/Pegasus.PerformanceTests/`.
- `scripts/Get-CiChangeFlags.ps1:11` lists `Invoke-QdosAlphaAcceptance` in the
  build pattern; `Test-CiChangeFlags.ps1` has no case for it.

## Steps

1. Delete `.github/workflows/qdos-pressure.yml`. (Reuses nothing; removal.)
2. Delete `tests/Pegasus.PerformanceTests/CapacitySoakTests.cs` and
   `FailureInjectionTests.cs` (source-only, no csproj; only the lane compiled them).
3. `scripts/Invoke-QdosAlphaAcceptance.ps1`: drop the `CiPressure` value and make
   `-Profile OfflineCandidate` the only value; remove `$pressureSourceRoot`,
   `$stagingRoot`, the staging/copy/`Category=QdosPressure` run, the
   `PEGASUS_QDOS_PRESSURE_PROFILE` save/restore, `pressureSourceSha256`,
   `testResultSha256` (pressure TRX), and the CiPressure `limitation`/output
   branches. The `OfflineCandidate` prerequisites, coverage check, acceptance
   lane, and evidence file keep their behaviour and fail-closed messages.
4. `scripts/Get-CiChangeFlags.ps1`: remove `Invoke-QdosAlphaAcceptance` from
   `$buildPattern` (the script no longer runs in any workflow).
5. Docs: `docs/operations.md` — `Performance` profile row → "no lane; planned
   trait unused", delete the `CiPressure` paragraph, drop `QdosPressure` from
   traits-in-use, note the OfflineCandidate profile no longer includes the
   in-process pressure probe; `docs/runbook.md` — rename §"QDOS pressure
   profiles" to the OfflineCandidate runner and remove the CiPressure text.
   No FRD/PRD/ADR/capabilities change.
6. Local checks: `pwsh ./scripts/Test-CiChangeFlags.ps1`,
   `pwsh ./scripts/Test-DocumentationLinks.ps1`, `pwsh -NoProfile -Command
   "& ./scripts/Invoke-QdosAlphaAcceptance.ps1 -SourceRevision <head>"` expecting
   the same fail-closed `OfflineCandidate is blocked:` error, then PR to `dev`.

## CI cost

Deleting under `tests/` and editing `Get-CiChangeFlags.ps1` matches the build
and infrastructure patterns, so this PR runs the full `repository-check` once
(~9 min). Unavoidable under the classifier; no further CI cost afterwards, and
no scheduled workflow remains in the repository.

## Simplification pass — 2026-08-18

n/a — deletion of a lane and its dead branches; the only edited logic is the
removal of the pressure path from one script.

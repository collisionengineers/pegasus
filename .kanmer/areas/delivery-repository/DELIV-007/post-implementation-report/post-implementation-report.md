# Post-implementation report — DELIV-007

## Summary

The nightly `qdos-pressure` lane and its `CiPressure` profile are removed;
`Invoke-QdosAlphaAcceptance.ps1` keeps only the fail-closed `OfflineCandidate`
profile. No application source, infrastructure, or deployment content changed.
Commit `1d20a556` on `task/deliv-007-retire-qdos-pressure`, PR #402 to `dev`.

## Changes

| File | Change | Why |
| --- | --- | --- |
| `.github/workflows/qdos-pressure.yml` | deleted | nightly diagnostic that gated nothing; operator direction |
| `tests/Pegasus.PerformanceTests/*.cs` (2) | deleted | source-only fixtures compiled only by that lane |
| `scripts/Invoke-QdosAlphaAcceptance.ps1` | −131/+30: single `OfflineCandidate` profile; pressure staging, run, env var and evidence fields removed | remove the dead path with its fixtures |
| `scripts/Get-CiChangeFlags.ps1` | build pattern no longer names the script | it runs in no workflow |
| `docs/operations.md`, `docs/runbook.md` | lane text updated; runbook section renamed `QDOS offline candidate runner` | keep current-state docs true |

## Governing docs

Chore; no PRD/FRD/ADR/capabilities change. `docs/capabilities.md` and the
FRDs never referenced the pressure lane (verified by grep).

## Risks / follow-ups

- `OfflineCandidate` evidence no longer carries `pressureSourceSha256` /
  `testResultSha256`; the profile has never run (fail-closed, no approved
  dataset), so no consumer exists. Whether OfflineCandidate itself should be
  retired is a separate product decision, not taken here.
- This PR classifies as build + infrastructure (deletes under `tests/`) and so
  runs the full `repository-check` once (~9 min).

## Verification hand-off

On merged `main`: `gh workflow list` shows no `qdos-pressure`;
`grep -rn "CiPressure\|QdosPressure" .github scripts docs` returns only the
retirement note in `docs/runbook.md`; `pwsh ./scripts/Test-CiChangeFlags.ps1`
passes; `Invoke-QdosAlphaAcceptance.ps1 -SourceRevision <main-sha>` fails closed
with `OfflineCandidate is blocked: -CapacityDatasetManifest is required…`.

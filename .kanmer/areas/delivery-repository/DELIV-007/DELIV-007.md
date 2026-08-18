---
id: DELIV-007
type: ticket
title: Retire the qdos-pressure nightly CI lane
status: verifying
area: delivery-repository
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-18T10:39:23.666Z'
  review: '2026-08-18T10:43:49.729Z'
  verifying: '2026-08-18T11:22:23.433Z'
taken_at: '2026-08-18T10:39:38.441Z'
branch: task/deliv-007-retire-qdos-pressure
worktree: ../pegasus-worktrees/deliv-007-retire-qdos-pressure
labels:
  - ci
  - source-now
links: []
archived: false
created: '2026-08-18T10:38:32.819Z'
updated: '2026-08-18T11:22:23.433Z'
---

## What

Remove the nightly `qdos-pressure` GitHub Actions lane and the `CiPressure`
profile it drives: `.github/workflows/qdos-pressure.yml`, the two source-only
pressure fixtures under `tests/Pegasus.PerformanceTests/`, the `CiPressure`
branch of `scripts/Invoke-QdosAlphaAcceptance.ps1`, its entry in
`scripts/Get-CiChangeFlags.ps1`, and the operations/runbook paragraphs that
describe the lane. The `OfflineCandidate` alpha-acceptance profile stays.

## Why

The lane rebuilds and runs the whole integration host nightly for three bounded
in-process concurrency tests that gate nothing (runbook: "recurring diagnostic
lane rather than a pull-request gate"; operations: "makes no alpha-capacity
claim"). Its only run (2026-08-18) failed on a stale `/Received` assertion that
SIMPLI-008 made obsolete. The operator directed its retirement as needless CI.

## Verification

- `gh workflow list` shows no `qdos-pressure`; no scheduled workflow remains.
- `pwsh ./scripts/Test-CiChangeFlags.ps1` and the `documentation` lane pass.
- `Invoke-QdosAlphaAcceptance.ps1 -Profile OfflineCandidate` still fails closed
  exactly as before (no behaviour change to that profile).

## Outcome

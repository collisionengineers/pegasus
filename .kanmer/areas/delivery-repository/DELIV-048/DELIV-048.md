---
id: DELIV-048
type: ticket
title: Restore Windows and Linux release workstation support
status: verifying
area: delivery-repository
assignee: principal_delivery_audit
profile: fix
stageEntered:
  preparing: '2026-09-07T20:56:46.944Z'
  review: '2026-09-07T21:22:41.584Z'
  verifying: '2026-09-07T21:29:07.175Z'
  implementing: '2026-09-08T06:46:29.631Z'
taken_at: '2026-09-07T20:58:55.907Z'
branch: DELIV-048-portable-release
worktree: .worktrees/deliv-048
claim_expires_at: '2026-09-08T07:56:24.234Z'
claim_controller: principal_delivery_audit
lease_id: 3c8db85f-4398-4b63-9133-6f8a1b49830a
lease_revision: 44
lease_controller_run: 20260907T200500Z-v1-remediation
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\deliv-048'
lease_phase: verifying
lease_heartbeat_at: '2026-09-08T07:26:24.234Z'
labels:
  - ci
  - tests
  - follow-up
groups:
  - EPIC-013
  - EPIC-014
links:
  - DELIV-047
refs:
  - docs/adr/0007-direct-terminal-azure-deployment.md
docs_todo: true
commits:
  - 1c1d7a0a45555604bafd3e732bd606bf083b804a
  - 26ba4ed408317cccdb354dc1e115b0297f15df94
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/681'
  - 'https://github.com/collisionengineers/pegasus/pull/701'
delivery_state: integrated
delivery_branch: dev
delivery_sha: 26ba4ed408317cccdb354dc1e115b0297f15df94
delivery_recorded_at: '2026-09-08T07:14:53.200Z'
archived: false
created: '2026-09-04T11:58:34.805Z'
updated: '2026-09-08T07:26:24.234Z'
---

## What

Audit and revamp CI only after the WSL, database, accessibility and release contracts are settled.

## Why

Changing CI now would encode unresolved platform assumptions and duplicate troubleshooting.

## Verification

- [ ] Every retained CI gate proves a named behavior against the final Linux toolchain and speculative gates are removed.

## Outcome

## Current scope — 7 September 2026

The operator explicitly requires Windows and Linux development and deployment.
This supersedes the prior Linux-only CI-cleanup premise in EPIC-013; that
original description is retained above as history, not the current outcome.
Under [[EPIC-014]], correct the existing release scripts to emit a migration
bundle matching the x64 release workstation (Windows or Linux), preserve
Linux Web/Worker/OCI deployment, and align current guidance and ADR authority.
No CI redesign, extra test lanes, packages, cloud writes or deployment here.

## Current acceptance

- [ ] Both supported x64 workstations use the same direct release route.
- [ ] Manifest validation rejects wrong bundle/runtime pairs and host mismatch;
  Linux owner-execute checks never run on Windows.
- [ ] Linux Web/Worker packages, OCI linux/amd64 and existing approval gates stay.
- [ ] ADR-0039 supersedes ADR-0037; current instructions agree.
- [ ] Focused script checks and root-owned release validation are recorded.

## Implementation handoff — 7 September 2026

Commit b6ffdeda1f8eee7f71e86ca033260de20483cb60 is pushed in PR #681 to dev. The current acceptance is implemented and the root-owned focused checks passed as recorded in post-implementation-report. Independent exact-head review and final integrated release packaging/required CI remain outstanding. This is not a deployment or native Linux execution claim; original Linux-only history above is superseded, not erased.

## Current follow-up — 2026-09-08

Exact-merge proof2462b18082379607 retains FAIL for the omitted Windows
ORAS hint. Root resumed the same workspace and corrected only mapped
PegasusPlatform/Test-PegasusPlatform guidance, with native script/hint PASS.
PR701 is the follow-up to independently reviewed/squash-integrated PR681;
new independent exact-head review and integrated artifact acceptance remain.
No cloud/deployment/Done claim; full reportf380ddd091eb9ccb retains history.

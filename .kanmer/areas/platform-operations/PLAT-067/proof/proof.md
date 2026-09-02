---
kind: proof-record
merged_sha: "1b705bd01d88109b21affddd014fbaa06c82b1ce"
environment: "Pegasus production and GitHub origin; Windows PowerShell 7"
verified_at: "2026-09-02T13:36:00Z"
result: PASS
failure_class: none
attempts:
  - attempted_at: "2026-09-02T12:07:00Z"
    command: "Invoke-IntakeDataWipe.ps1 -Execute"
    cwd: "PLAT-067 worktree"
    exit_code: 0
    result: PASS
    summary: "36 blobs and 147 rows across 70 non-preserved tables removed; zero targets remained; 354 preserved rows and sequences 31/7/1 retained."
  - attempted_at: "2026-09-02T12:50:04Z"
    command: "Release 38 deployment, production smoke, SQL and Application Insights read-back"
    cwd: "release-38 detached worktree and Pegasus production"
    exit_code: 0
    result: PASS
    summary: "Exact source and artifact identities deployed; Web and Worker healthy; Graph poll recovered, cursor advanced, and blocked emails arrived."
  - attempted_at: "2026-09-02T13:53:00Z"
    command: "Authenticated /Inbox preview persistence check"
    cwd: "Pegasus production Web UI"
    exit_code: 0
    result: PASS
    summary: "Selected message and preview remained after focus and pointer left the rows."
  - attempted_at: "2026-09-02T13:34:12Z"
    command: "Merge PR #645 into dev under explicit operator review/testing waiver"
    cwd: "GitHub"
    exit_code: 0
    result: PASS
    summary: "Documentation-only merge SHA 1b705bd01d88109b21affddd014fbaa06c82b1ce."
  - attempted_at: "2026-09-02T13:36:00Z"
    command: "Canonical atomic docs-only promotion and remote equality read-back"
    cwd: "PLAT-067 worktree"
    exit_code: 0
    result: PASS
    summary: "origin/main and origin/dev both equal 1b705bd01d88109b21affddd014fbaa06c82b1ce; no rebuild or redeployment."
---

# Proof — PLAT-067

The sixth approved intake-data wipe completed before promotion. It removed all
36 target blobs (3,932,690 bytes) and 147 rows from the 70 non-preserved SQL
tables. Post-checks found zero remaining target blobs and rows, 354 preserved
rows, and unchanged Case/Image/Unidentified sequences 31/7/1. The operator
confirmed the wiped test round was absent in the authenticated Web UI.

Release 38 deployed exact source
`0f0e90ae44ffda7339ca2a460310deeb98121afa`. Manifest SHA-256 is
`52E1A5AC23C2491594E79EA89740D9B5D826A3DD94258347DB91A16896F986AE`
and Web digest is
`sha256:b791d9587224d30d68fd6abcbd1e1d5f389f2baefc3702d9ec2d2f37398eef15`.
The Web revision is healthy and sole-active; Worker config-zip deployment
`01ed553a-b6cd-4652-b043-72c88b9ca2e6` succeeded. No migration or database
write occurred and the migration head remained
`20260829212237_GrantProviderSubmissionAcceptRecovery`.

Canonical production smoke passed. The real sparse Graph item that blocked
release 37 no longer wedged the poller: the 12:50 UTC execution succeeded,
cleared `LastFailureCode`, advanced the cursor, and released queued emails.
Authenticated production verification also proved that the Inbox selection
and preview survive focus and pointer leaving the rows.

PR #645 recorded the current production state in `docs/operations.md` and
`docs/current-architecture.md`. The operator explicitly waived independent
review and testing for that documentation-only change. After fresh
`MERGE AUTH GRANTED`, the canonical atomic promotion advanced both
`origin/main` and `origin/dev` to
`1b705bd01d88109b21affddd014fbaa06c82b1ce`; exact read-back passed. No
artifact was rebuilt and Azure was not redeployed for the documentation
promotion.

Result: **PASS**.

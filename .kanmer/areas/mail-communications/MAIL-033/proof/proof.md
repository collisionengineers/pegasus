---
kind: proof-record
merged_sha: "cc60cffc554ced423c97a86f014f577bc05d382b"
environment: "Pegasus production release 38; subscription e6076573-23a5-46a8-acef-7e22d264e5db; rg-pegasus-prod; Azure SQL and Application Insights read-backs"
verified_at: "2026-09-02T12:50:04Z"
result: PASS
failure_class: none
attempts:
  - attempted_at: "2026-09-02T12:50:04Z"
    command: "git merge-base ancestry; Invoke-ProductionSmoke.ps1; production SQL and Application Insights read-backs"
    cwd: "release-38 detached worktree and Pegasus production"
    exit_code: 0
    result: PASS
    summary: "PR #641 merge SHA is in release 38/main; the affected poll completed, cleared LastFailureCode, advanced the cursor, and blocked emails arrived."
---

# Proof — MAIL-033

PR #641's exact merge SHA is contained in production release 38 at
`0f0e90ae44ffda7339ca2a460310deeb98121afa` and in `origin/main`.
Release 38's immutable image and Worker package deployed successfully and the
canonical production smoke passed at that exact source SHA.

Before deployment, Application Insights showed the release-37 Worker failing
every five minutes with `Graph Inbox message omitted receivedDateTime`. The
mailbox state remained at a last completion of 2026-09-01 08:35 UTC with
`invalid_mailbox_source`. After release 38, the 2026-09-02 12:50 UTC timer
execution succeeded in 4.323 seconds; SQL read-back showed
`LastCompletedAtUtc=2026-09-02T12:50:04.2940973Z` and a null
`LastFailureCode`. The delta cursor advanced and the emails previously
blocked behind the sparse item arrived in the production Inbox.

This is the natural production canary named by the implementation report. No
malformed Graph item was fabricated and Outlook was not mutated for testing.
The pre-release exact-merge verification and green PR #641 CI remain
applicable.

Result: **PASS**.

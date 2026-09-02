---
kind: proof-record
merged_sha: "2a48be0456e42d22994193b35d6b4cc33bc90a59"
environment: "Pegasus production release 38; subscription e6076573-23a5-46a8-acef-7e22d264e5db; rg-pegasus-prod; authenticated Chrome UI verification"
verified_at: "2026-09-02T13:53:00Z"
result: PASS
failure_class: none
attempts:
  - attempted_at: "2026-09-02T13:50:00Z"
    command: "git merge-base --is-ancestor <merged_sha> <release-38-sha>; git merge-base --is-ancestor <merged_sha> origin/main"
    cwd: "C:\\Users\\Alex\\Documents\\GitHub\\pegasus"
    exit_code: 0
    result: PASS
    summary: "PR #640 merge SHA is an ancestor of deployed release 38 and origin/main."
  - attempted_at: "2026-09-02T13:53:00Z"
    command: "Authenticated production /Inbox: move focus to retained-mail search and pointer away from message rows"
    cwd: "Pegasus production Web UI"
    exit_code: 0
    result: PASS
    summary: "The same selected message remained aria-expanded and Message preview remained visible after both focus and pointer left the rows."
---

# Proof — MAIL-032

PR #640's exact merge SHA is contained in production release 38 at
`0f0e90ae44ffda7339ca2a460310deeb98121afa` and in `origin/main`.
The immutable release passed the canonical production smoke at that exact
source SHA.

An authenticated check of the deployed `/Inbox` route selected a retained
message, moved focus to retained-mail search, and moved the pointer away from
the message rows. The selected link remained expanded, its identity did not
change, and the Message preview remained visible. This satisfies the pending
production/UI condition.

The exact-merge verification remains applicable: locked restore, Release
build, Core and Architecture tests, UI catalogue, and all hosted PR checks
including browser, test-ui and every SQL integration lane passed. The
unrelated CSS-selector limitation remains owned by MAIL-034.

Result: **PASS**.

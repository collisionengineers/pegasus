---
kind: proof-record
merged_sha: "c804056deaa6d65aaba46754a89687f964609479"
environment: "detached Windows/PowerShell worktree .worktrees/verify-case-022-c804056deaa6d65aaba46754a89687f964609479"
verified_at: "2026-09-03T18:06:48.035Z"
result: WAIVED_BY_OPERATOR
attempts:
  - attempted_at: "2026-09-03T17:51:00Z"
    command: "gh pr view 650 --json state,mergeCommit,url,mergedAt,baseRefName,headRefOid"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "PR 650 is MERGED to dev; merge commit is c804056deaa6d65aaba46754a89687f964609479."
  - attempted_at: "2026-09-03T17:51:30Z"
    command: "git worktree add --detach .worktrees/verify-case-022-c804056deaa6d65aaba46754a89687f964609479 c804056deaa6d65aaba46754a89687f964609479"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "Clean detached worktree created at the exact GitHub merge SHA; origin/dev resolved to the same SHA."
  - attempted_at: "2026-09-03T17:52:40Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-case-022-c804056deaa6d65aaba46754a89687f964609479"
    exit_code: 0
    result: PASS
    summary: "Locked restore completed for all solution projects."
  - attempted_at: "2026-09-03T17:52:50.3901876Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-case-022-c804056deaa6d65aaba46754a89687f964609479"
    exit_code: 0
    result: PASS
    summary: "Release build succeeded with zero warnings and zero errors."
  - attempted_at: "2026-09-03T17:54:20.3380227Z"
    command: "dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --filter \"FullyQualifiedName~DocumentCustodyDurabilityTests|FullyQualifiedName~BoxDocumentContentStoreTests|FullyQualifiedName~PublicUploadTelemetryInitializerTests|FullyQualifiedName~ProductionCompositionTests\""
    cwd: ".worktrees/verify-case-022-c804056deaa6d65aaba46754a89687f964609479"
    exit_code: 0
    result: PASS
    summary: "All 32 focused exact-merge integration tests passed."
  - attempted_at: "2026-09-03T17:55:49.3229308Z"
    command: "dotnet test ./Pegasus.slnx --configuration Release --no-build --filter \"Category!=Corpus\""
    cwd: ".worktrees/verify-case-022-c804056deaa6d65aaba46754a89687f964609479"
    exit_code: 1
    result: FAIL
    summary: "Operator cancelled this duplicate full-suite run after Core 1185/1185 and Architecture 100/100 passed; no integration assertion failure had been emitted."
  - attempted_at: "2026-09-03T18:06:48.035Z"
    command: "Live production upload, Box, SQL receipt/counter, and telemetry verification"
    cwd: "production"
    exit_code: null
    result: NOT_APPLICABLE
    summary: "Operator explicitly assigned this testing to the next deployment, outside CASE-022 closeout scope."
---

# Verification — CASE-022

## Operator waiver

Operator identity: repository operator in this conversation.

Operator reason: the PR is already merged and the same full non-Corpus suite
was run successfully before merge. Repeating that long suite at the squash
merge SHA was explicitly cancelled as unnecessary ceremony. Exact-merge
locked restore, zero-warning Release build, and all 32 focused tests passed.
The operator directed CASE-022 to close and will test live uploads with the next
deployment, which is outside this ticket's scope.

This waiver does not claim deployment or production success. It accepts the
source-level merged result on `dev` and the retained pre-merge full-suite
evidence of 2,525 passing tests. The merged production caller remains:

`POST /Uploads/{token}` → `RequestModel` → `IUploadToRequest` →
`EfDocumentRequestStore` → `StoreVersionAsync` →
`BoxDocumentContentStore`.

PR: https://github.com/collisionengineers/pegasus/pull/650

Merged: 2026-09-03T17:30:39Z

## Outcome

PASS-equivalent closeout is authorized only by the operator waiver above.
Production validation remains intentionally unclaimed and will occur with the
next deployment outside CASE-022.

---
kind: proof-record
merged_sha: "7c8f6e50e7bf1297307dbd5a7dd2c1940a2f78f1"
environment: "Windows PowerShell 7, .NET 10 Release, existing SQL/browser fixtures; .worktrees/verify-intk-062-7c8f6e50e7bf1297307dbd5a7dd2c1940a2f78f1"
verified_at: "2026-09-07T23:15:00Z"
result: PASS
attempts:
  - attempted_at: "2026-09-07"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-intk-062-7c8f6e50e7bf1297307dbd5a7dd2c1940a2f78f1"
    exit_code: 0
    result: PASS
    summary: "Fresh locked restore at exact merge; date precision because exact start not captured."
  - attempted_at: "2026-09-07"
    command: "dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore"
    cwd: ".worktrees/verify-intk-062-7c8f6e50e7bf1297307dbd5a7dd2c1940a2f78f1"
    exit_code: 0
    result: PASS
    summary: "Fresh merged Release integration project build passed, zero warnings/errors, 69.83 seconds."
  - attempted_at: "2026-09-07T23:10:24.0551511Z"
    command: 'dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PublicUploadRetentionWebTests.PublicTransport|FullyQualifiedName~QdosCustodialWebTests.PublicRequestUploadUsesOneCoreCommandAndPrgWithGenericCompletion|FullyQualifiedName~PublicUploadRetentionWebTests.PublicPageAddsReplacesFinalizesAndRefusesLaterBytes|FullyQualifiedName~PublicUploadRetentionWebTests.AReplacementNamingAnotherLinksOccurrenceIsRefused|FullyQualifiedName~PublicUploadRetentionWebTests.ALinkFromAnotherLimitsVersionRendersTheTypedRefusalAndWritesNothing" --logger "trx;LogFileName=intk-062-merge.trx"'
    cwd: ".worktrees/verify-intk-062-7c8f6e50e7bf1297307dbd5a7dd2c1940a2f78f1"
    exit_code: 0
    result: PASS
    summary: "12 passed, zero failed/skipped/inconclusive, reported duration85 seconds; TRX finish2026-09-07T23:11:52.6400204Z."
---

# INTK-062 exact integrated proof

## Outcome

PASS at PR684 merge7c8f6e50e7bf1297307dbd5a7dd2c1940a2f78f1 on configured integration branch dev. Root performed every command above, with an immediate exit-code guard after restore/build and final test exit returned. This is fresh merged-source evidence, not relabelled premerge tests. Integration project builds actual Core/Infrastructure/Web/Worker callers. No full solution test rail or final release CI is claimed.

## Bound source and independent review

Root independent review6dddd562df2d048b/public5135612647 binds reviewed abf3657691a230bdc20e81d4744e6634a6d73f85, plan292c919799aad59d and report82ec9b83aad983b3. Exact detached workspace, common Git directory, clean HEAD and integration ancestry were validated in scratch/verifya91a0bee3b5612d0. All four PR paths match reviewed bytes; full trees do not match because68 other-lane paths differ. Shared DOCS-020 custody/source invalidation and PLAT-028 composition changes are why the same12 tests were rerun here. Final HEAD and clean status were read back after execution.

## Caller evidence

The actual public /Uploads/Request route runs RequestUploadTransportFilter before antiforgery/form reads. Existing GetRequestUpload policy resolves only the route token, rejecting unavailable tokens without reading content. Declared oversize is refused before reading; native request-body limit and framework bounded form buffering enforce unknown or understated lengths, including non-file form overhead. Valid maximum-size files succeed. A form field cannot substitute another route token. The existing Core upload command still verifies durable link/custody/replacement/finalization and limits-version rules.

The selected12 include eight public transport checks and four actual request/custody browser workflows. Their passing results prove this route/composition, not deployed host limits, load capacity or manual visual quality. No multipart parser, custom stream, separate validation policy or new dependency was introduced.

## Retained attempts and artifact

Premerge locked restore/build passed53.21s; the same12 focused cases passed77s, no skips, recorded in report82ec9b83aad983b3. Their TRX hash is A3DEBFD39C5E6F479D7AE4654625687664DEABE035AFC82D5053767C2D367AB4. Those attempts remain premerge evidence and are not erased by this new run.

Merged TRX tests/Pegasus.IntegrationTests/TestResults/intk-062-merge.trx has SHA25613B8A763D01C52922A273FAC12AFE323D863A2B35F994F9F495883B44FE4E1BC. Root read XML counters: total/executed/passed12, all failure/skip/inconclusive counters0. TRX local +01:00 instants were converted to UTC above. Test runner envelope includes overhead; console duration85s is not falsely equated with the full TRX envelope.

## Limits and closeout

No Azure/cloud/mailbox/email action, deployed upload check or manual viewport review occurred. Final integrated CI and release remain root-owned. Ordinary Done means accepted on dev. Preserve the reviewed head, exact merged SHA, failures if any and hash-verified TRXs during authorized closeout; delete only validated owned clean worktrees/branches and release the claim last.

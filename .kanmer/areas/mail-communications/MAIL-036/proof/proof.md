---
kind: proof-record
merged_sha: "1d972f05c0f10c2ecf804f271a4fd3155242f1ef"
environment: ".worktrees/verify-mail-036-1d972f05c0f10c2ecf804f271a4fd3155242f1ef; Windows x64, PowerShell 7, .NET 10, SQL LocalDB"
verified_at: "2026-09-07T21:05:05.419Z"
result: PASS
attempts: [{"attempted_at":"2026-09-07T21:05:05.419Z","command":"gh pr view 678 --json state,mergeCommit,url,mergedAt","cwd":".","exit_code":0,"result":"PASS","summary":"MERGED 2026-09-07T20:50:31Z; exact merge 1d972f05c0f10c2ecf804f271a4fd3155242f1ef."},{"attempted_at":"2026-09-07T21:05:05.419Z","command":"Assert exact HEAD, clean detached worktree; git diff --exit-code a0260a4e^{tree} HEAD^{tree}; git diff --check HEAD^ HEAD; Parser.ParseFile scripts/Invoke-IntakeDataWipe.ps1","cwd":".worktrees/verify-mail-036-1d972f05c0f10c2ecf804f271a4fd3155242f1ef","exit_code":0,"result":"PASS","summary":"Exact merge source tree is identical to author tree; no whitespace or PowerShell parse errors. No wipe invoked."},{"attempted_at":"2026-09-07T21:05:05.419Z","command":"dotnet test ../mail-036/tests/Pegasus.Core.Tests/bin/Release/net10.0/Pegasus.Core.Tests.dll --filter \"FullyQualifiedName~PollApprovedInboxTests\"","cwd":".worktrees/verify-mail-036-1d972f05c0f10c2ecf804f271a4fd3155242f1ef","exit_code":0,"result":"PASS","summary":"27 passed,0 failed/skipped,179ms; reused same-tree Release assembly without duplicate build."},{"attempted_at":"2026-09-07T21:05:05.419Z","command":"dotnet test ../mail-036/tests/Pegasus.IntegrationTests/bin/Release/net10.0/Pegasus.IntegrationTests.dll --filter \"FullyQualifiedName~ApprovedMailboxEstateIntegrationTests|FullyQualifiedName~ProductionGraphSourceTests\" -- xUnit.MaxParallelThreads=2","cwd":".worktrees/verify-mail-036-1d972f05c0f10c2ecf804f271a4fd3155242f1ef","exit_code":0,"result":"PASS","summary":"72 passed,0 failed/skipped,1m10s; real SQL reset/claim/completion and Graph protocol paths."}]
---

# MAIL-036 exact-merge verification

PR https://github.com/collisionengineers/pegasus/pull/678 merged to dev at
2026-09-07T20:50:31Z after independent PASS. All post-merge attempts above
passed; no failed or inconclusive test attempt is omitted.

## Source and artifact identity

The clean detached checkout is the exact GitHub merge, not mutable dev.
Its complete Git tree equals the reviewed author commit
a0260a4ef6fca856d86b4c44c5b0c863d93276b8. To respect the operator's explicit
no-duplicate-build constraint, the named focused tests ran from this detached
cwd using the already-built Release assemblies from the unchanged author
worktree. Tests that resolve fixture/source paths through their assembly
directory therefore read the identical author source tree, not a newly
compiled checkout. No assembly/source equivalence is inferred across a diff.

SHA-256:
- Core test assembly: 6B3AE4569C1C14327E326066A2DE7805D331AE3DA12AA062B58205E1651A839F.
- Integration test assembly: CC676E788B346309351CBE471B54C91D5B43A0C3B4F98E95DD3D8DA80EB91834.

The locked restore/build and earlier focused attempts remain in the
post-implementation report; they are not relabelled post-merge builds.

## Acceptance

The actual SQL reset body preserves approval/onboarding and advances/seeds
the effective cutoff atomically in the existing wipe transaction. Tests
exercise missing and existing poll states, scope rebinding, queued old/new
notifications, expired delta cursors, and lease completion without advancing
the recovery cursor. Exact notifications no longer perform a delta scan.
Existing timer/lifecycle recovery remains the catch-up mechanism.

## Limits and follow-up

This proves the integrated implementation, not deployment or live acceptance.
No live wipe, Worker stop, mailbox mutation or Azure configuration write was
performed. Actual notification delivery and deployed end-to-end intake remain
the EPIC-014 release acceptance obligation; the last live telemetry contained
only subscription-validation webhook calls. Required dev checks were absent
at independent merge review; final integrated release verification remains
with the controller. Ordinary Done does not claim production activation.

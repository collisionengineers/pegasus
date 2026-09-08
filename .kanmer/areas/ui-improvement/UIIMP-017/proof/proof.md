---
kind: proof-record
merged_sha: "cdaa02584c38ecc27d3bd24784f59da189138bc1"
environment: ".worktrees/verify-uiimp-017-cdaa02584c38ecc27d3bd24784f59da189138bc1; Windows x64; PowerShell 7"
verified_at: "2026-09-08T02:43:00Z"
result: PASS
attempts:
  - attempted_at: "2026-09-08"
    command: "dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore"
    cwd: ".worktrees/uiimp-017"
    exit_code: 1
    result: FAIL
    summary: "Pre-merge root61210;50.17s CA1859 test-field declaration error; locked restore had passed; no tests/captures. Exact build start not captured."
  - attempted_at: "2026-09-08"
    command: "dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore"
    cwd: ".worktrees/uiimp-017"
    exit_code: 0
    result: PASS
    summary: "Pre-merge root60924 after concrete Dictionary declaration correction;44.19s,0warnings/errors. Exact build start not captured."
  - attempted_at: "2026-09-08T02:18:56.5001682Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName=Pegasus.IntegrationTests.ApprovedMailboxAdministrationWebTests.ThePageShowsActivationAndSubscriptionHealthPerMailbox|FullyQualifiedName=Pegasus.IntegrationTests.TestUiSnapshotTests.HealthDefaultSnapshotRequiresTheRecordedMailboxFailureState' --logger 'trx;LogFileName=uiimp-017-health.trx' --results-directory ./artifacts/verification"
    cwd: ".worktrees/uiimp-017"
    exit_code: 1
    result: FAIL
    summary: "Pre-merge root60924:2executed/1PASS/1FAIL; exact service HTML assertion omitted existing space, while both actual London timestamps were correct. No snapshot update/verify ran."
  - attempted_at: "2026-09-08T02:27:26.9621279Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName=Pegasus.IntegrationTests.ApprovedMailboxAdministrationWebTests.ThePageShowsActivationAndSubscriptionHealthPerMailbox|FullyQualifiedName=Pegasus.IntegrationTests.TestUiSnapshotTests.HealthDefaultSnapshotRequiresTheRecordedMailboxFailureState' --logger 'trx;LogFileName=uiimp-017-health-corrected.trx' --results-directory ./artifacts/verification"
    cwd: ".worktrees/uiimp-017"
    exit_code: 0
    result: PASS
    summary: "Pre-merge root19696 after assertion-space-only correction:2PASS36s,0skip; incremental build20.79s0warnings/errors, scoped update3PASS/verify3PASS and catalogue60/67/0 also passed."
  - attempted_at: "2026-09-08T02:38:11Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-uiimp-017-cdaa02584c38ecc27d3bd24784f59da189138bc1"
    exit_code: 0
    result: PASS
    summary: "Root59242; script issue time observed immediately before invocation, not exact process start;7projects restored, each≤1.41s."
  - attempted_at: "2026-09-08T02:38:11Z"
    command: "dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore"
    cwd: ".worktrees/verify-uiimp-017-cdaa02584c38ecc27d3bd24784f59da189138bc1"
    exit_code: 0
    result: PASS
    summary: "Root59242; script issue time, not separate build start. Focused Integration project plus runtime dependencies,115.55s,0warnings/errors."
  - attempted_at: "2026-09-08T02:40:13.9050987Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName=Pegasus.IntegrationTests.ApprovedMailboxAdministrationWebTests.ThePageShowsActivationAndSubscriptionHealthPerMailbox|FullyQualifiedName=Pegasus.IntegrationTests.TestUiSnapshotTests.HealthDefaultSnapshotRequiresTheRecordedMailboxFailureState' --logger 'trx;LogFileName=uiimp-017-merged-health.trx' --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-uiimp-017-cdaa02584c38ecc27d3bd24784f59da189138bc1"
    exit_code: 0
    result: PASS
    summary: "Exact merged2/2PASS38s,0failed/skipped/error/inconclusive; fresh actual Health capture."
  - attempted_at: "2026-09-08T02:40:54Z"
    command: "pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope administration-health"
    cwd: ".worktrees/verify-uiimp-017-cdaa02584c38ecc27d3bd24784f59da189138bc1"
    exit_code: 0
    result: PASS
    summary: "Approximate post-test start; existing cohort3PASS2s, script5s; reuses preceding fresh capture, changes no expected snapshot."
  - attempted_at: "2026-09-08T02:41:00Z"
    command: "pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1"
    cwd: ".worktrees/verify-uiimp-017-cdaa02584c38ecc27d3bd24784f59da189138bc1"
    exit_code: 0
    result: PASS
    summary: "Approximate completion minute;60routed sources/67prototypes/0broken references."
---

# UIIMP-017 exact merged verification

PASS for [PR693](https://github.com/collisionengineers/pegasus/pull/693),
merged to configured integration branch dev at2026-09-08T02:37:21Z,
exact cdaa02584c38ecc27d3bd24784f59da189138bc1. Not deployed.

Root independently reviewed author principal_delivery_audit's complete six-file
diff; whole review906090e307646004 and exact-head public5136703197 bind
author3e585f6e0f43ed0d90773be967ead867bc5e76f1, plan0b651b4eb610041b,
reportda58cdf68cdd3897/checklist202dd0b19e6f604f. No source changes occurred
during this verification. Prior failed attempts remain above and below.

## Exact environment and production caller

GitHub confirmed MERGED/full merge SHA. Created only the deterministic
detached .worktrees/verify-uiimp-017-cdaa02584c38ecc27d3bd24784f59da189138bc1.
Pre-checks confirmed exact HEAD, detached branch, clean status and common
source .git directory, distinct from author/board/shared checkout. Root59242
ran the commands above sequentially with explicit exit guards. It returned0.

The actual routed /Administration/Health page reads existing populated SQL
mailbox health, not a fake rendered string. Persisted2031-05-06T10:20Z renders
06 May2031 11:20 in both metrics and service rows. All five metric instants
use the existing OperatorLabels.OfficeTime; no layout/query/global clock or
culture changes. The direct selector regression proves only the declared
populated failure cell qualifies, rejecting empty/automation/other-page/
outside-cell/denied responses.

The two named caller/selector tests freshly capture the actual route. The
following scoped verifier uses that fresh capture, not the old author capture;
no update/normalization or expected snapshot was edited on the merged source.
The existing TestUiSnapshotTests cohort now contains three tests because of
the new small predicate Fact. Catalogue validates60routed/67prototypes/0broken.
No broad browser/corpus/whole-solution test lane ran.

Root set PEGASUS_TEST_UI_CAPTURE_DIR to this exact verifier's
artifacts/test-ui-capture, PEGASUS_TEST_UI_SCOPE=administration-health and
removed PEGASUS_TEST_UI_MODE before capture. Tests run Release --no-build
against the preceding exact-source focused project build. It builds runtime
dependencies but is not labelled a full solution build.

Final git diff --check, git status --short --branch and git rev-parse HEAD
all exited0: clean detached cdaa02584c38ecc27d3bd24784f59da189138bc1.
The subsequent read-only status/HEAD remained identical.

## Actual retained TRX and timestamps

Root read actual result names/counters and SHA256, not merely process text:
artifacts/verification/uiimp-017-merged-health.trx,
CB1541AA91501F865AB59A74AD2FAEC0793765E6D7940D8D8D2A148F16D77CC0.
Two executed/passed; all failed/error/skipped/inconclusive counts0.
Exact interval2026-09-08T02:40:13.9050987Z to02:40:54.8343536Z
(TRX records corresponding+01:00 instants).

Script issue time02:38:11UTC was read immediately before invocation; it is not
a separately measured restore/build start. Later snapshot/catalogue times
are approximate post-test observations. No exact unrecorded clock value is
invented. Snapshot scripts retain console outcomes without a TRX logger.

## Earlier failures and correction evidence retained

The complete author report remains the original command/diagnosis record.
Initial root61210 locked restore passed; its build failed CA1859 after50.17s
before any tests or captures. New direct indexing exposed a concrete private
Dictionary-type analyzer requirement. Only the field declaration changed;
no suppression or policy/test weakening.

Root60924 build then passed44.19s. The actual route failed a newly exact
service-cell assertion because it omitted the existing rendered space after
span. The metrics assertion passed; captured response line216 proved the
service value was also correct. Author changed only the expected space after
root inspected the real response. No production markup changed to fit a test.

Preserve separate artifacts/verification/uiimp-017-health.trx, hash
EAB313FE2161D23459980A458EA55ABC2980841C6A2BEBE4020DA3EC4BB597CF,
2executed/1passed/1failed, interval02:18:56.5001682Z→02:19:33.9165457Z,
and its capture under
artifacts/test-ui-capture/5c7c63feae9391d9ef3ba092f7ea349ee659beeda9fbc7fd495c4892a144482e.
It was not overwritten.

Root19696 incremental build20.79s0warnings/errors then2/2PASS36s.
Its unique corrected TRX80431E38A5D8BE48469ABA2009547ACAE659CC9200F7E20F9669010C886B9AF5,
interval02:27:26.9621279Z→02:28:05.5418501Z, remains separate.
Author scoped update3PASS144ms/verify3PASS3s/script6s/catalogue60/67/0
and diffcheck0 followed. Root and author compared whole generated bytes:
only Health metrics timestamp and one Health scenario wording changed.
No claim that the failed attempts were actually passing or transient.

## Acceptance boundary and handoff

All bounded acceptance passes on exact merged source. No fresh manual visual,
non-UK request-culture harness, hosted CI, live mailbox/provider, Azure or
deployment PASS is claimed. Empty nonrequired check lists are not CI-green;
final converged release remains separately owed. Historical UIIMP-005 and
TICK-035 Settings files/claims were not taken over.

Read this entire proof and fresh gates before Done. Normal closeout must
retain/hash all three named author/merged TRXs and associated captures before
removing only the two owned roots and branch, then release the claim last.
No cleanup or release occurred while writing this proof.

## Closeout evidence retention

Root read whole proof b8a7ced80c521c62, accepted PASS and moved Done before
this authorized closeout. Verification results/failures above are unchanged.
All three named TRXs plus twelve associated capture files are now retained
under ignored `pegasus_pack/current/proofs/UIIMP-017/`, in separate `author/`
and `merged/` trees preserving their original relative paths. Source and copy
SHA256/length were checked for all 15 files; manifest.json records every path,
length and hash. Manifest SHA256:
`338698CD29A19C7CE645848B785B2CA02D06365AEE50497F0CCB90B263FE9554`.
This includes the original failing Health response and the independent exact
merged capture. No build/test/capture was rerun for closeout.

Fresh GitHub read-back confirms PR693 MERGED to dev at
2026-09-08T02:37:21Z, author head3e585f6e0f43ed0d90773be967ead867bc5e76f1,
merge cdaa02584c38ecc27d3bd24784f59da189138bc1 reachable from origin/dev.
All six delivered file blobs equal reviewed author inputs; no full-tree
identity claim is made. Both exact owned worktrees were clean, had the correct
heads/branch or detached state and common source Git directory; no other
ticket claims either path or branch. Only the authorized two roots and exact local/remote branch were removed
normally without force from the shared checkout. Local branch deletion used
`-d`; Git noted its upstream had the same head although the stale shared HEAD
is not its ancestor. Reachable squash merge and delivered six-file equality
were checked separately above. Fresh path, worktree-registration and local/
remote-ref checks confirm absence. No broad prune touched other registrations.
All 15 retained files were hash/length checked again after cleanup. Claim
release remains the final ownership action.

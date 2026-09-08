# UIIMP-017 post-implementation report

## Current result

Root focused runtime/snapshot evidence PASS; ready for independent review.
Not merged, deployed, live-tested or CI-verified.
Fresh isolated branch `UIIMP-017-health-display`, worktree
`.worktrees/uiimp-017`, accepted base
`cc441645b0a62a806e34367ad75e9eaff4df8b11`.
Published PR https://github.com/collisionengineers/pegasus/pull/693 targets dev;
exact pushed head `3e585f6e0f43ed0d90773be967ead867bc5e76f1`.
GitHub head/branch/base read-back matched; worktree clean.
Packet plan 0b651b4eb610041b/files cc4555712d217bf6 and EPIC014 context
apply. Root is the sole heavy verifier.

## Implementation and caller

- `src/Pegasus.Web/Pages/Administration/Health.cshtml`: five recorded metrics
  instants now use existing `OperatorLabels.OfficeTime(value, "")`, matching
  the service table. Counts, values, empty cells, query/actor and clocks stay
  unchanged. Production caller remains HealthModel.OnGetAsync through existing
  GetServiceHealth/GetAdministrationHealthMetrics.
- `tests/Pegasus.IntegrationTests/ApprovedMailboxAdministrationWebTests.cs`:
  existing populated mailbox route now asserts `06 May 2031 11:20` both in
  the metrics cell and last-successful-poll service cell from persisted
  `2031-05-06T10:20Z`, preserving all scenario assertions.
- `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`: only Health's
  default selector gains exact `<td>graph_unavailable</td>` requirement; one
  same-owner predicate Fact accepts that declared state and rejects empty,
  automation-only, marker-outside-cell, wrong-page and denied HTML fragments.
  No Case selector or normalization change.
- `docs/design/test-ui/catalogue.json`: names the actual populated mailbox
  failure scenario.
- `docs/design/test-ui/pages/administration-health--default.html`: root's
  fresh actual route capture changes only the formatted metrics timestamp.
- `docs/design/test-ui/index.html`: generated catalogue changes only the
  Health scenario wording. Both generated files were never hand-edited.

FRD12 and design/README's existing one-clock/Test UI rules govern this change.
The Razor skill led to the existing page/formatter, not a new component,
request-culture fixture or clock framework. No genuine request-culture harness
exists in this cohort; no culture test is claimed merely from caller-thread
culture. The actual persisted UTC input proves the intended BST display.

TICK035's precise index-only handoff is recorded in plan/files. Its Settings
outputs, historical UIIMP005 claim/worktree, shared checkout, other page
selectors, cloud/mailbox and corpus remain untouched.

## Checks and exact commands

Author read-only `git diff --check` and catalogue JSON parsing passed, exit 0.
Initial frozen source had four paths, +22/-4; after the analyzer correction
it has the same four paths, +23/-5. Git emitted only the existing LF/CRLF checkout
warnings. Final six-file source/generated diff is +25/-7. No build, test or
capture was run by the author; root supplied the evidence below. No manual
visual claim is made.

### Initial root attempt and bounded correction

Root session 61210: locked restore PASS, then the focused Integration Release
build FAILED after 50.17 seconds solely on CA1859 at
`TestUiSnapshotTests.cs(19,69)`. No tests or captures ran. This failed build is
retained; it is not evidence of a product-runtime failure.

The new direct selector regression indexes the private `StateMatches` field;
its actual and sole initializer is already `Dictionary<string, StateMatch>`.
Root authorized the analyzer's simplest concrete-type correction: change only
the field declaration from `IReadOnlyDictionary` to `Dictionary`. Dictionary
content, selectors, initializer and assertions stay unchanged. No suppression,
interface, factory or scope expansion. Author `git diff --check` passed again
with only the existing LF/CRLF warnings. No author build/test/capture.

### Second root attempt and exact whitespace correction

Root session 60924: corrected incremental Integration build PASS, 44.19s,
zero warnings/errors. The exact two-test cohort then executed 2, passed 1
(selector), failed 1 (populated route), with no skips. The metrics timestamp
assertion passed. The route's service assertion at line 409 expected no space
after `</span>`, but actual Razor and captured response line 216 contain:

```html
<td> <span>Last successful poll: </span> 06 May 2031 11:20</td>
```

Author inspected that exact retained response and changed only the assertion
by adding the one expected space. Production markup and displayed timestamp
remain untouched. Root authorized this exact test correction; no normalization
or weaker time assertion. `git diff --check` passed after correction.

Retained failure: `artifacts/verification/uiimp-017-health.trx`, SHA256
`EAB313FE2161D23459980A458EA55ABC2980841C6A2BEBE4020DA3EC4BB597CF`.
Author read/hash-checked actual counters 2 executed / 1 passed / 1 failed;
start 2026-09-08T03:18:56.5001682+01:00, finish
2026-09-08T03:19:33.9165457+01:00. Captured HTML remains under
`artifacts/test-ui-capture/5c7c63feae9391d9ef3ba092f7ea349ee659beeda9fbc7fd495c4892a144482e/response.html`.
Neither file was overwritten. Snapshot update/verify/catalogue had not run.
This failing attempt remains preserved separately from the corrected result.

### Final corrected route and generated evidence

Root session 19696, cwd `.worktrees/uiimp-017`: incremental Integration
Release build PASS in 20.79s, zero warnings/errors. Locked restore had already
passed in session 61210. The two actual route/selector tests PASS in 36s;
scoped snapshot update 3 PASS (144ms), verify 3 PASS (3s; script 6s), and
catalogue PASS: 60 routed pages, 67 prototypes, zero broken references.
Root `git diff --check` exited 0, six authorized paths only.

Build: `dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore`.
Test: `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build`
with `--logger "trx;LogFileName=uiimp-017-health-corrected.trx"`
and `--results-directory ./artifacts/verification`, exact filter:

```text
FullyQualifiedName=Pegasus.IntegrationTests.ApprovedMailboxAdministrationWebTests.ThePageShowsActivationAndSubscriptionHealthPerMailbox|FullyQualifiedName=Pegasus.IntegrationTests.TestUiSnapshotTests.HealthDefaultSnapshotRequiresTheRecordedMailboxFailureState
```

Root set `PEGASUS_TEST_UI_CAPTURE_DIR` to this worktree's absolute
`artifacts/test-ui-capture`, `PEGASUS_TEST_UI_SCOPE=administration-health`,
`PEGASUS_TEST_UI_MODE` unset. This is 2 tests, not the broad capture default.
Root then ran:

```powershell
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -SkipCapture -Scope administration-health
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope administration-health
pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1
```

The snapshot script's existing TestUiSnapshotTests cohort now also contains
this cheap selector Fact, explaining the three checks per update/verify.

Final retained TRX `artifacts/verification/uiimp-017-health-corrected.trx`
SHA256 `80431E38A5D8BE48469ABA2009547ACAE659CC9200F7E20F9669010C886B9AF5`.
Author independently read/hash-checked counters 2 executed, 2 passed, 0 failed,
0 skipped; start 2026-09-08T03:27:26.9621279+01:00, finish
2026-09-08T03:28:05.5418501+01:00. The original failing TRX remains unchanged.

Author compared complete newline-normalized generated bytes to accepted HEAD:
Health equals the original with only `Current (06/05/2031 10:20:00 &#x2B;00:00)`
replaced by `Current (06 May 2031 11:20)`; index equals the original with only
the declared Health scenario wording replaced. Both comparisons and final
`git diff --check` passed. No Settings, Case or other snapshot drift.

Root authorizes one scoped `[skip ci]` commit and dev-targeting PR, retaining
the final converged CI obligation and all prior failures. No manual visual,
non-UK request-culture, live/cloud/mail or deployment PASS is claimed. Root
independent review next; author stops without self-review/merge/claim release.

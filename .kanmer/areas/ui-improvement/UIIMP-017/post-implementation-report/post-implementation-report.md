# UIIMP-017 post-implementation report

## Current result

Source frozen for root verification; not runtime PASS, not Review or deployed.
Fresh isolated branch `UIIMP-017-health-display`, worktree
`.worktrees/uiimp-017`, base/HEAD
`cc441645b0a62a806e34367ad75e9eaff4df8b11`. No author commit or PR yet.
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
  failure scenario. Its generated Health output and conditional index remain
  owed from root's fresh focused capture; never hand-edited.

FRD12 and design/README's existing one-clock/Test UI rules govern this change.
The Razor skill led to the existing page/formatter, not a new component,
request-culture fixture or clock framework. No genuine request-culture harness
exists in this cohort; no culture test is claimed merely from caller-thread
culture. The actual persisted UTC input proves the intended BST display.

TICK035's precise index-only handoff is recorded in plan/files. Its Settings
outputs, historical UIIMP005 claim/worktree, shared checkout, other page
selectors, cloud/mailbox and corpus remain untouched.

## Checks and pending exact commands

Author read-only `git diff --check` and catalogue JSON parsing passed, exit 0.
Initial frozen source had four paths, +22/-4; after the analyzer correction
it has the same four paths, +23/-5. Git emitted only the existing LF/CRLF checkout
warnings. No build, test or capture was run by the author. No runtime PASS or
manual visual claim is made.

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
Root's incremental rebuild and the exact tests below remain pending.

Root cwd: `.worktrees/uiimp-017`. Locked restore already passed; root schedules
the incremental Integration Release rebuild,
then Integration `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build`
with the exact filter:

```text
FullyQualifiedName=Pegasus.IntegrationTests.ApprovedMailboxAdministrationWebTests.ThePageShowsActivationAndSubscriptionHealthPerMailbox|FullyQualifiedName=Pegasus.IntegrationTests.TestUiSnapshotTests.HealthDefaultSnapshotRequiresTheRecordedMailboxFailureState
```

Set `PEGASUS_TEST_UI_CAPTURE_DIR` to this worktree's absolute
`artifacts/test-ui-capture`, `PEGASUS_TEST_UI_SCOPE=administration-health`,
`PEGASUS_TEST_UI_MODE` unset. This is 2 tests, not the broad capture default.
Then root runs:

```powershell
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -SkipCapture -Scope administration-health
pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope administration-health
pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1
```

The snapshot script's existing TestUiSnapshotTests cohort now also contains
this cheap selector Fact. Keep actual outputs/failures in this report. Commit
only approved six-path output after root PASS; independent review next. No
self-review, merge, deployment or claim release.

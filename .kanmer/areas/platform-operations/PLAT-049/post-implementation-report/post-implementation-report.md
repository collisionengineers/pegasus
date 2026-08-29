# PLAT-049 post-implementation report

Branch `task/plat-049-operations-features`, based on `origin/dev` = `b92cb9a7`
(merged in first, clean, no conflicts).

## What shipped

**AI Job List panel** on `/operations`, first in the stack per EPIC-011 §1.11.
Meta "n jobs"; table `Job | Record | Started by | Created | State | Action`.
Membership is FRD-11's: every non-terminal job, plus the jobs that reached a
terminal state today, newest first. Non-terminal membership comes from the
unbounded `IAiJobQueries.ListOpenAsync`, so no live job can fall outside the
200-row recent window that bounds only the terminal tail.

Every drawn action reaches a real destination:

| Row | Action rendered | Goes to |
| --- | --- | --- |
| `Draft ready` Estimate | Review estimate, Cancel | `/Cases/{id}/Assessment`, `ICancelAiJob` |
| `Draft ready` Unidentified resolution | Review, Cancel | `/Unidentified/{id}`, `ICancelAiJob` |
| `Draft ready` Query response | Open query, Complete job, Cancel | `/Cases/{id}`, `IConfirmAiJob`, `ICancelAiJob` |
| `Draft ready` Unidentified-queue pass | Complete job, Cancel | `IConfirmAiJob`, `ICancelAiJob` |
| Any other non-terminal | Cancel | `ICancelAiJob` |
| Terminal (today's) | `—` | nothing |

`ICancelAiJob` and `IConfirmAiJob` were registered but had **no production
caller** before this change; they have one now.

**Send Unidentified to AI** — a dark control in the panel head that creates an
`UnidentifiedResolution` job for a chosen open U reference through the
existing `ICreateAiJob` seam (which itself consults `ISendToAiControl`). No
second dispatch path. Drawn only when an open Unidentified item exists.

**Service health** — the table was already real against `ServiceHealth.cs` from
PLAT-023; its Retry command is unchanged, and the action cell now renders `—`
where Core names no retry target instead of an empty cell.

**Labels** — one new nested static class `OperatorLabels.AiJobs`. Nothing
existing in that file was reordered or edited; the diff is 72 insertions and 0
deletions.

## Read this: I did not follow the lane brief on one point

The lane brief says the Send button "creates an Unidentified-queue pass job".
**It creates an `UnidentifiedResolution` job instead**, because:

- **FRD-11 § AI Job List** — a governing `refs` document of this ticket —
  says verbatim: "`Send Unidentified to AI` creates an
  Unidentified-resolution job for a chosen U reference", and its kinds table
  gives that kind the "Started from" value `Operations "Send Unidentified to
  AI" for one U reference`.
- **The PLAT-049 ticket body** says the same: "creating an
  UnidentifiedResolution job".
- **EPIC-011 D5** says scheduled queue passes "are created by external crons
  through the Automation Actor" — a Pegasus button creating one contradicts it.
- **`AiJobPolicy.RequireCreator`** encodes the same split in Core: the
  Automation Actor may create *only* `UnidentifiedQueuePass`.

If the orchestrator wants the queue pass instead, it is a one-line change to
the handler plus its test — but it needs an operator decision against FRD-11,
not a quiet edit.

## Verification — real numbers, run in this worktree

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release` | **Build succeeded. 0 Warning(s), 0 Error(s).** |
| `dotnet test ./tests/Pegasus.IntegrationTests --no-build --filter "FullyQualifiedName~OperationsWebTests"` | **Failed: 0, Passed: 19, Skipped: 0, Total: 19** (9 pre-existing + 10 new) |
| `dotnet test ./tests/Pegasus.ArchitectureTests --no-build` | **Failed: 0, Passed: 100, Skipped: 0, Total: 100** |

A baseline `dotnet build` was run before any edit and was also green, so the
0/0 above is this branch's own result and not an inherited one.

**Not run, and therefore not claimed:** the full suite, the `Browser` category
(`Browser/AccessibilityTests` and `Browser/OperatorJourneyTests` both visit
`/Operations` and will re-render it), and the snapshot capture script. The
lane brief forbids all three. `docs/design/test-ui/catalogue.json` carries two
`/Operations` states (`operations--default`, `operations--empty`) whose stored
markup is now stale; regeneration is the merging branch's job once per merge,
per the 2026-08-29 orchestration decisions.

## Assertions

No existing assertion was weakened, skipped, deleted or inverted. The two
existing negative assertions about this panel —
`DoesNotContain("AI operations")` and the "Requesting an AI job … are planned"
placeholder sentence — both remain true, because the panel is titled "AI Job
List" and carries no placeholder copy. They were left exactly as written.

## Defects outside this lane, all reported, none absorbed

Recorded with dispositions under a dated heading in the ticket's `plan`.

1. **EVA handoffs panel is not shipped.** §1.11 and the ticket body ask for it
   (Case, Route, Engineer, State, Result). No Core port lists EVA handoffs:
   `IEvaSubmissionQueries` offers only `GetLatestAsync(caseId)`,
   `GetRecentFailuresAsync` and `GetActivityAsync`, and
   `EvaSubmissionFailure` carries `CaseId` with no case reference, engineer or
   route. Building it needs a new `Core/Eva` read model and port plus an EF
   adapter and registration — outside this lane's boundary and large enough to
   need its own plan. **Nothing was rendered in its place**: an uncomposed
   capability is absent, never an empty panel.
2. **Service health `View` is not rendered.** §1.11 says "Retry/View";
   `ServiceHealthRow` carries `RetryTarget` and no view target. A guessed
   area→route map would be a second list of routes owned by nobody, and its
   plausible destinations (`/Administration/Mailboxes`,
   `/Administration/Automation`) are Administrator-only while `/Operations` is
   open to Engineer and User, so the link would 403 for most readers. `—` is
   rendered instead.
3. **Attention required has no `Item` column** (§1.11 lists Case, Work, Item,
   Attempts, Failure, Retry). `RequestOperationProjection` has no field naming
   the thing the work is about. PLAT-023's panel, now this lane's file, but the
   fix is in `Core/Operations` and the EF projection.
4. **Active upload links has no `Recipient` column** (§1.11 lists it). Same
   projection, same reason.
5. **`Open query` opens the Case, not the message.** A `QueryResponse` job's
   `SubjectKind` is `Case` and it carries no message identity, so FRD-11's
   "opens the message" is not reachable. Accepted risk: the link opens the
   record the job actually names rather than rendering an unresolvable control.

## Simplification pass

Ran over this branch's own diff before the PR; findings and dispositions are
recorded under "Simplification pass — 2026-08-29" in the `plan` document. Five
findings, four fixed, one rejected with a reason.

## Adversarial verifier remediation - 2026-08-29

This section corrects and supersedes earlier statements in this report wherever
they conflict.

### What changed after verification

- An effective `Expired` row from the persisted-open query now remains on the
  list when `ExpiresAtUtc` is on the current Europe/London office date. The
  regression fixture was created on the previous office day and expires today,
  matching the production failure the verifier reproduced.
- The Operations GET no longer calls the unbounded
  `IUnidentifiedStore.ListQueueAsync`. The send form accepts one canonical U
  reference; POST validates it and uses the existing unique-sequence
  `GetByReferenceAsync` lookup. The global rail filter remains the only queue
  enumeration on the request.
- The unbounded `<select>` was removed, so thousands of open items no longer
  become thousands of DOM options.
- The positive test fixture is `U412`, the shape Core can produce.
- `StateToneOverride` contains only Queued, Taken and Draft ready. The shared
  `_StatusChip` owns Completed, Failed, Cancelled and Expired.
- The stale baseline comment was corrected, and all AI type references in the
  shared labels file now stay inside the appended `AiJobs` class.

### Caller correction

The earlier claim that both `ICancelAiJob` and `IConfirmAiJob` had no production
caller was wrong. `SetCurrentEstimate` already called `IConfirmAiJob`; its own
`ISetCurrentEstimate` interface was registered but had no Web caller.
`ICancelAiJob` had no production caller. This PR gives Cancel its first reachable
caller and gives Confirm a reachable Operations caller. No credit is claimed
for creating the estimates-path call.

### Assertion disclosure

No assertion was weakened to obtain green. One existing PLAT-049 assertion was
intentionally inverted because the implementation behaviour changed: the send
control is now present without enumerating the queue on GET, and the action
refuses a closed or missing reference through the point lookup. The test was
renamed and now asserts that new behaviour. The two pre-existing negative
placeholder assertions remain unchanged and true.

### Finding dispositions

- **High - Expired row absent:** fixed in `ReadAiJobsAsync` and covered by the
  stale-queued regression row.
- **Medium - duplicate queue query:** fixed by removing the page query; a test
  pins one GET-time queue call from the global rail only.
- **Medium - unbounded options:** fixed by the canonical reference input.
- **Medium - fabricated positive fixture:** fixed to `U412` and a valid open
  `UnidentifiedItem`.
- **Low - duplicated tone mappings:** fixed by deferring known labels to
  `_StatusChip`.
- **Low - caller overclaim:** corrected above.
- **Low - missing EVA handoffs / Service health View:** risk accepted for this
  lane. They still require Core contracts and an authorised route outside the
  owned files, so the ticket remains Review and is not reported complete.
- **Low - plan drift:** corrected in the plan's verifier-remediation section.

### Verification and commits

| Command | Observed result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release` | Exit 0; Build succeeded; 0 warnings; 0 errors |
| `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "FullyQualifiedName~OperationsWebTests"` | Exit 0; 19 passed; 0 failed; 0 skipped |

Remediation commits pushed to the existing PR branch:

- `7df75798` - production remediation
- `3d5cdbb9` - regression and fixture coverage

The full suite, Browser category, snapshot capture and catalogue scripts were
not run and are not claimed.

## Remediation round — 2026-08-29 (cross-model)

An independent `gpt-5.6-terra` reviewer returned `REQUEST_CHANGES` with three
rule-14 blockers and two findings. The same model then remediated, in commits
`29d2bb06` and `f2ab0e4a`. The orchestrator re-ran the assertion-integrity
checks independently rather than accepting the report.

### The numbers this report originally got wrong

`OperatorLabels.cs` was reported as **72 insertions**. The real figure at that
time was **67 additions, 0 deletions**. Current full PR-range numbers
(`origin/dev...f2ab0e4a`):

| File | +/− |
| --- | --- |
| `Pages/Operations/Index.cshtml` | 152 / 12 |
| `Pages/Operations/Index.cshtml.cs` | 300 / 1 |
| `Presentation/OperatorLabels.cs` | 81 / 0 |
| `tests/…/OperationsWebTests.cs` | 673 / 5 |

### Finding 1 — empty-state panel · FIXED

`Index.cshtml:73` renders the jobs table only when `aiJobs.Count > 0`; the
`.empty` / "No AI jobs" block is deleted. This follows the sibling Service-health
panel's own absence-of-empty-body convention rather than inventing a new one.
Covered by `AiJobListOmitsTheEmptyStateAndTableWhenThereAreNoJobs`.

The design authority's page-economy rule forbids empty-state panels in a
read-only view; this was a defect, not a style preference.

### Finding 2 — EVA handoffs panel · BUILT

`Index.cshtml.cs:128` reads the already-registered `IEvaSubmissionQueries`
(`DependencyInjection.cs:166`); `Index.cshtml:190` renders recorded pending work,
latest activity, failure count and failure times.

It deliberately does **not** invent case labels, routes, engineer attribution,
actions, probes or a migration — the port's own doc comments say the health
surface shows only that failures exist and when, and that a person decides what
to do with each. Covered by `EvaHandoffsShowsOnlyRecordedHealthFacts`, which also
asserts the failure case id is **not** rendered.

### Finding 3 — Service health "View" · REMOVED, and not delivered

`ServiceHealthRow` carries only `RetryTarget`; there is no View target in the
Core projection. The entire unbacked action column is deleted
(`Index.cshtml:163`) rather than left rendering `—` or shipped as a disabled
control.

**This is the honest outcome under D21.** A column that always renders a dash is
dead UI, and a permanently inert control is never a delivered capability. This is
not a D7 integration seam (those are Experian, Glass's, Audatex and Cazana only),
so the disabled-seam allowance does not apply. **Service health View is not
delivered**; delivering it needs a routable target on the Core projection first.

Retry stays reachable from Attention required (`Index.cshtml:270`).

### Finding 4 — AI-job actions

- **Complete job** is now guarded at `Index.cshtml.cs:210`: it reads the live job
  and permits only Draft-ready `QueryResponse` or `UnidentifiedQueuePass` jobs.
  The estimate bypass is covered by
  `CompleteAiJobRefusesAJobWhoseDraftNeedsARecordAction`.
- **Open query** stays a Case route, **rejected with evidence**: `AiJobRecord`
  carries a single `SubjectId`, and `AiJobPolicy.SubjectKindFor(QueryResponse)`
  resolves it as `Case`. `/Inbox/{id}` needs a retained-message id, which cannot
  be constructed from the current contract. Routing to the retained message needs
  a Core contract change; the Case route is the only valid destination today.

### Finding 5 — report arithmetic · FIXED here

The remediating agent correctly refused to edit `.kanmer/` (the orchestrator
prohibits it) and reported the correction instead. Applied above.

### Assertion integrity — verified by the orchestrator, not taken on report

Across the whole branch diff `origin/dev...HEAD`:

- removed `Assert.` lines: **0**
- new `Skip =` / `[Ignore]` attributes: **0**
- deleted `[Fact]` / `[Theory]` methods: **0**

The agent disclosed that its first focused run was 21 passed / 1 failed because a
**new** test it had just written expected an unencoded `+` in an HTML datetime
attribute, and that it corrected that expectation to assert the visible office
time. Verified: the change is confined to its own new test in `f2ab0e4a`, all
additions, and no pre-existing assertion was touched. Fixing a wrong expectation
in a test you just wrote is not weakening an assertion.

### Verification

- `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false` — exit
  0, **0 `CS####` diagnostics**.
- `dotnet test … --filter "FullyQualifiedName~OperationsWebTests"` — **Passed 22,
  Failed 0, Skipped 0**.

### Rule 14 after remediation

| Capability | Caller |
| --- | --- |
| AI Job List | `/Operations` → `Index.cshtml.cs:110` `OnGetAsync` |
| Send Unidentified to AI | rendered POST form → `OnPostSendUnidentifiedToAiAsync` |
| Complete job | rendered POST form → guarded `OnPostCompleteAiJobAsync` |
| Cancel | rendered POST form → `OnPostCancelAiJobAsync` |
| Retry | Attention-required POST form → `OnPostRetryExternalAsync` |
| EVA handoffs | `/Operations` GET → the two existing EVA query methods |
| Open query | the existing Case subject route (rejected with evidence above) |
| Service health View | **no caller — removed, not delivered** |

Every capability PLAT-049 still names has a production caller. The one that did
not is gone from the UI rather than disclosed-and-shipped.

### Reused, not rebuilt

`IEvaSubmissionQueries` and its existing DI registration, `ServiceHealthPolicy`'s
existing EVA window and maximum, `CanCompleteByHand`, and the existing
Attention-required retry handler.

### Commits

- `29d2bb06` — fix(operations): close PLAT-049 UI findings
- `f2ab0e4a` — test(operations): cover PLAT-049 remediation

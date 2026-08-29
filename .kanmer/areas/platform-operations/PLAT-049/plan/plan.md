# PLAT-049 plan

Scope: the **AI Job List** panel, the **Send Unidentified to AI** action, and
the **Service health** table's action column, on `/operations`. Everything
else in EPIC-011 §1.11 already exists from PLAT-023 or is blocked by a Core
gap recorded in `research`.

## Step 1 — labels (`OperatorLabels.cs`, new nested class `AiJobs` only)

Reuses: `OperatorLabels.Humanise` as the fallback, exactly as every existing
map in the file does. There is no prior `AiJobKind`/`AiJobState` map anywhere
in Web (verified by grep), so this is the first and only copy.

- `Kind(AiJobKind)` → Estimate / Unidentified resolution / Query response /
  Unidentified-queue pass (FRD-11's own wording).
- `State(AiJobState)` → Queued / Taken / Draft ready / Completed / Failed /
  Cancelled / Expired (FRD-11's own wording).
- `StateTone(AiJobState)` → the `_StatusChip` tone for each, following
  `docs/design/README.md`: pending is amber (Queued, Draft ready), in-flight
  is navy (Taken), red is Failed, green is Completed, neutral is Cancelled and
  Expired.
- `Count(int)` → "1 job" / "n jobs" for the panel meta.
- `QueueRecord` → "Unidentified queue", because the Core subject reference for
  a queue pass is the internal token `unidentified-queue`.

Nothing existing in the file is reordered or edited.

## Step 2 — page model (`Index.cshtml.cs`)

Reuses: `StaffPageModel.TryGetActor` / `NewOperationKey`, the page's existing
`StatusMessage` + PRG shape, and its existing catch-by-type error handling.

1. Constructor gains `IAiJobQueries`, `ICreateAiJob`, `IConfirmAiJob`,
   `ICancelAiJob`, `IUnidentifiedStore` as required parameters (all four AI
   ports and the Unidentified store are registered unconditionally in
   `DependencyInjection.cs`, so no optional seam is warranted — unlike
   `GetServiceHealth`, which genuinely is conditional and stays optional).
2. `OnGetAsync` additionally loads:
   - `AiJobs`: `ListOpenAsync()` unioned by `JobId` with `ListRecentAsync(200)`
     filtered to jobs that are terminal *and* whose `ClosedAtUtc ?? CreatedAtUtc`
     falls on the office-local date of `LoadedAtUtc`. Union then order newest
     first. This is FRD-11's "every non-terminal job and the terminal jobs of
     the current day", and it is exact: `ListOpenAsync` is unbounded so no
     non-terminal job can fall off the end of the `ListRecentAsync` window.
     Filtering uses the *mapped* state, which `EfAiJobStore.Map` has already
     resolved through `AiJobPolicy.EffectiveState`.
   - `OpenUnidentified`: `ListQueueAsync(null, ct)` — the U references the
     Send action may name.
   - "Today" is decided by comparing `OperatorLabels.OfficeDate(...)` strings,
     reusing the single existing Europe/London conversion rather than adding a
     fifth `FindSystemTimeZoneById` (PLAT-060).
3. Three handlers, each PRG back to the page with a `StatusMessage`:
   - `OnPostSendUnidentifiedToAiAsync(Guid unidentifiedId, string operationKey)`
     → `ICreateAiJob` with `AiJobKind.UnidentifiedResolution`.
   - `OnPostCompleteAiJobAsync(Guid jobId, long expectedVersion, string operationKey)`
     → `IConfirmAiJob`.
   - `OnPostCancelAiJobAsync(Guid jobId, long expectedVersion, string reason, string operationKey)`
     → `ICancelAiJob`.
   Each catches `StaffAuthorizationException` → `Forbid()`, and
   `ArgumentException` / `InvalidOperationException` / `KeyNotFoundException` /
   `DbUpdateConcurrencyException` → a recorded `StatusMessage`. No catch-all.
4. Static helpers the markup needs, so route selection is one decision in one
   place: `RecordPage(AiJobRecord)`, `ReviewPage(AiJobRecord)`,
   `ReviewLabel(AiJobRecord)`, `CanCompleteByHand(AiJobRecord)`.

## Step 3 — markup (`Index.cshtml`)

Panel order becomes §1.11's: AI Job List, Service health, Attention required,
Active upload links.

- Panel head: `h2` "AI Job List", `span.muted` with the job count, and the
  **Send Unidentified to AI** control on the right — the file's own
  `<details><summary class="btn btn--dark">` + `form.row-confirm` shape, with a
  `<select>` of open U references. Rendered only when at least one open
  Unidentified item exists, so the control is never inert.
- Table `Job | Record | Started by | Created | State | Action`.
  - **Job** = kind label, with the job's instruction beneath as the detail.
  - **Record** = `SubjectReference` linked to `/Cases/{id}` or
    `/Unidentified/{id}` by subject kind; a queue-pass row prints
    "Unidentified queue" with no link, because there is no record behind it.
  - **State** = `_StatusChip` with the label and the tone override.
  - **Action** = for a `Draft ready` job, the review link its kind names
    (`Review estimate` → the Assessment page, `Review` → the Unidentified
    item, `Open query` → the Case the job names); plus **Complete job** for a
    `Draft ready` Query response or Unidentified-queue pass; plus **Cancel**
    (reason required) for any non-terminal job. When none of those applies the
    cell renders `—`. Every one of them posts to a real handler above.
- Service health's action cell renders `—` where `RetryTarget` is null, rather
  than an empty cell. The contract's `View` is not rendered: see the plan's
  dispositions below.

## Step 4 — tests (`OperationsWebTests.cs`)

Focused filter only: `--filter "FullyQualifiedName~OperationsWebTests"`.

1. `AiJobListShowsOpenJobsAndTodaysTerminalJobsWithTheirActions` — an open
   `Draft ready` Estimate, a `Queued` Unidentified resolution, a `Completed`
   job closed today, and a `Cancelled` job closed a week ago; asserts the
   first three render with the right labels, records and action controls and
   that the stale terminal job does not.
2. `AiJobRowWithNoAvailableActionRendersTheDash` — a `Taken` queue-pass row
   offers Cancel but no review link and no Complete job.
3. `SendUnidentifiedToAiCreatesAnUnidentifiedResolutionJobForTheChosenReference`
   — posts the handler and asserts the recorded `CreateAiJobCommand` kind,
   subject and staff actor.
4. `CompleteAiJobConfirmsThroughTheCanonicalCommand` — asserts the recorded
   `ConfirmAiJobCommand` job id, expected version and staff actor.
5. `CancelAiJobCarriesTheOperatorReason` — asserts the recorded
   `CancelAiJobCommand` reason.
6. `SendUnidentifiedToAiSurfacesTheAdministratorRefusal` — `ICreateAiJob`
   throwing `InvalidOperationException` leaves a recorded status message and
   a redirect, never a swallowed error.

The existing `OperationsPageIsStaffWorkspaceWithNoReceiptLedgerOrBoxSurface`
test asserts `DoesNotContain("AI operations")` and a placeholder sentence.
Both stay true — the new panel is "AI Job List" and carries no placeholder —
so **no existing assertion is weakened, inverted or deleted**.

## Step 5 — verify

`dotnet build ./Pegasus.slnx --configuration Release`, then
`dotnet test ... --filter "FullyQualifiedName~OperationsWebTests"`. Real counts
are recorded in the post-implementation report.

## Simplification pass — 2026-08-29

Run over this branch's own diff before the PR, four lenses.

| Lens | Finding | Disposition |
| --- | --- | --- |
| Reuse | Route selection was about to be written inline three times in the markup | **Fixed** — one `ReviewPage`/`ReviewLabel` pair on the page model |
| Reuse | A per-row reason dialog via `Shared/_ReasonDialog` would add N dialogs to the DOM | **Fixed** — reuse this file's existing `details` + `row-confirm` shape |
| One list | A second `AiJobState` → tone map inside the markup | **Fixed** — the only map is `OperatorLabels.AiJobs.StateTone` |
| Efficiency | `ListRecentAsync` was going to be called twice (once for the panel, once for a count) | **Fixed** — one call, the count is the rendered list's length |
| Altitude | A page-local `AiJobRow` view record wrapping `AiJobRecord` | **Rejected** — it would carry no field the record does not already have; the record plus four static helpers is smaller |

## Review dispositions — 2026-08-29

Every finding below is a defect against the binding contract that this lane
cannot fix inside its own boundary. AGENTS.md rule 22 and EPIC-011 D19 apply;
the preferred disposition (fix in lane) is impossible for all five, and each
needs Core work with its own plan rather than a one-line change.

| # | Finding | Disposition |
| --- | --- | --- |
| 1 | §1.11 requires an **EVA handoffs** panel (Case, Route, Engineer, State, Result). No Core port lists handoffs; `EvaSubmissionFailure` carries `CaseId` only | **Deferred, reported to the orchestrator.** Needs a new `Core/Eva` read model + port + EF adapter + registration. Not rendered: an uncomposed capability is absent, never an empty panel |
| 2 | §1.11 asks for `Retry/View` on Service health; `ServiceHealthRow` has no view target | **Rejected in lane, reported.** A guessed area→route map would be a second list of routes owned by nobody, and its plausible destinations are Administrator-only while `/Operations` is not. `—` is rendered instead |
| 3 | §1.11's **Item** column is missing from Attention required (PLAT-023's panel, now this lane's file) | **Deferred, reported.** `RequestOperationProjection` has no such field; adding one is `Core/Operations` + the EF projection |
| 4 | §1.11's **Recipient** column is missing from Active upload links | **Deferred, reported.** Same projection, same reason |
| 5 | FRD-11's `Open query` should open the message; a `QueryResponse` job's subject is the Case | **Accepted risk, reported.** The link opens the Case the job names — a real record and a real route. Naming the message needs a Core field on the job |
| 6 | The lane brief says the Send button creates a queue-pass job; FRD-11, the ticket body and D5 say Unidentified resolution | **Fixed to FRD-11.** Reasoning in `research`; called out loudly in the report |

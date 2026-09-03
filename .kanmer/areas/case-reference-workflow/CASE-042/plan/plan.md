# Plan — CASE-042 (2026-09-02, gpt-5.6-terra high)

Produced by gpt-5.6-terra (effort high) read-only in `.worktrees/research` at
origin/dev `897db953`; the wrapper (Claude) re-verified the cited lines and
made the adjustments marked **Wrapper**.

## Objective

Add `/Cases?tab=awaiting` as the **Awaiting instruction** Pre-case queue for
unmerged image-initiated cases (D38). It has its own count, removes those rows
from Not ready, and contributes once to the Cases shell rail total.

Today `GetCaseStageCountsAsync` folds unmerged AwaitingInstruction image
intakes into Not ready (`src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs:50-72`,
INTK-013), while `/Cases` also loads image rows into Not ready and calls
`ListImagesAsync` per row (`src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:372-415`).
The existing projection has no source, origin-received timestamp, or aggregate
image count (`src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs:100-109`).

## Governing docs and design rules

- FRD-12 places Awaiting instruction in Pre-Case work beside Triage, with
  formal Cases remaining in Not ready (`docs/frd/frd-12-operator-experience.md:145-175`).
- FRD-02 keeps an image-initiated case in Awaiting instruction until its
  retained evidence can associate with an eligible instructed Case
  (`docs/frd/frd-02-intake-and-source-identity.md:170-176`).
- No explanatory copy and no empty-state prose (follow CASE-025's empty list).
  An unavailable capability is absent, never disabled (D7/D21); every
  displayed control has a named, working handler.
- Labels come only from `Presentation/OperatorLabels.cs`. The tab label is
  **Awaiting instruction** (a CASE-038 entry, see Dependencies). **Wrapper:**
  the row lifecycle chip is omitted on this tab — every row shares the one
  state, so the chip carries no information (page economy); the existing
  `OperatorLabels.ImageIntakeLifecycleState` entry ("Awaiting definitive
  instruction", `OperatorLabels.cs:440-442`) is left untouched.
- Default pending the two recorded operator answers (`open-questions/`):
  keep `PreCaseGroup = "Pre-Case work"` (`Index.cshtml.cs:80`) unchanged and
  draw no Vehicle column or quick-detail fact. No em dash, no disabled
  control. Either later answer changes only a label entry (CASE-038) or opens
  a data-model ticket; neither changes the steps below.

## Dependencies

- **CASE-032 — blocking** (board: backlog, `blocks: CASE-042`). CASE-042 reads
  the Awaiting rows from CASE-032's extended `ImageIntakeSummary` through the
  existing `IImageIntakeQueries.ListAsync(false, …)` read — no per-row image
  query and no new query type. Fields CASE-042 needs and their status:
  - image reference, normalized registration, origin receipt id, lifecycle
    state — **already present**;
  - aggregate image count — **CASE-032's `files` half** of `files·custody`
    (its brief, item 1), rendered with the custody value as CASE-032 defines;
  - origin receipt received timestamp and source — **Wrapper:** these are
    *not* in CASE-032's brief as written. Either CASE-032's brief is amended
    to fold them into the same projection read, or CASE-042 ships without the
    Received and Source columns (absent, D21) and records that deviation in
    its post-implementation report. Vehicle and a registration-read result are
    not needed (open question 2).
- **CASE-038 — shared lock.** Provides the `OperatorLabels` entry for
  **Awaiting instruction** (and the group label if open question 1 changes
  it). CASE-042 does not edit `OperatorLabels.cs`, `Pages/Shared/*`,
  `Pages/Cases/Shared/*`, `wwwroot/css/site.css`, `wwwroot/js/site.js`,
  `Persistence/Migrations/**`, or governing docs. Until the entry exists the
  tab cannot be added — sequence after CASE-038's label handoff.
- **UIIMP-014 — dependent ticket** (CASE-042 `blocks` it). Adds
  `queues--awaiting` populated and empty states, catalogue rows, and the
  regenerated `/Cases` capture under `docs/design/test-ui/**`. The CI
  snapshot verify passes only once both PRs are on `dev`.
- **UIIMP-008 — coordination.** After the count split the Work Centre's
  Not ready metric becomes formal Not ready Cases only, because it reads
  `CaseStages.NotReady` (`src/Pegasus.Web/Pages/Index.cshtml:37-40`). Do not
  edit `Pages/Index.*`; note the side effect in the PR and the report.

## Count-split decision

**CASE-042 carries the move** in
`src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` and
`src/Pegasus.Core/Operations/DashboardCounts.cs`: it is inside CASE-042's
owned queue-projection scope, required by the queue split, and CASE-032 does
not own the dashboard aggregate. The addend is `EfDashboardQueries.cs:60-68`
(`CountAsync(item.MergedIntoCaseId == null && item.LifecycleState ==
awaitingInstruction)` added to `For(notReady)`). Leaving it while adding an
Awaiting count double-counts the same records.

**Wrapper:** `CaseStageCounts` (`DashboardCounts.cs:28`) gains a **required**
`AwaitingInstruction` field — no optional parameter or compatibility default
(rule 6, no abstraction to carry one call site). The only other constructor
is the test fake at
`tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs:355`, updated
in the same diff. `NotReady` becomes formal workflow rows only; the
`<remarks>` on the record and the comment at `EfDashboardQueries.cs:50-59`
are rewritten to say so.

## Ordered steps

### Step 1 — Split the dashboard aggregate

- Files: `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs`,
  `src/Pegasus.Core/Operations/DashboardCounts.cs`,
  `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs` (fake
  constructor only).
- Reuses: the existing `ImageIntakes` `CountAsync` filter and
  `EfImageIntakeStore.ToCode`; the existing `CaseStageCounts` record.
- Move the addend into the new `AwaitingInstruction` field; `NotReady` is
  `For(notReady)` alone.

### Step 2 — Move rows and counts to the Awaiting instruction tab

- Files: `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs`,
  `src/Pegasus.Web/Presentation/RailCountsPageFilter.cs`.
- Reuses: `Tabs`, `Count(Tab)` (`Index.cshtml.cs:88-97, 163-172`),
  `QueueRow`, `QuickDetail`, `ImageRow` (`543-558`), the existing
  `IDashboardQueries.GetCaseStageCountsAsync` call, and CASE-032's summary.
- Add `new("awaiting", OperatorLabels.<CASE-038 entry>, PreCaseGroup,
  "icon-image")` after `triage`; map `"awaiting" => StageCounts.AwaitingInstruction`.
- `RailCountsPageFilter.cs:69-75`: add `stages.AwaitingInstruction` to the
  Cases total exactly once.
- `LoadNotReadyAsync` (`379-415`) returns formal Not ready Case rows only;
  a new `LoadAwaitingAsync` calls `ListAsync(false, …)`, keeps
  `AwaitingInstruction`, and builds rows through `ImageRow` from the summary
  fields alone — no `ListImagesAsync` per row.
- Row/quick detail: reference·registration title, image count (CASE-032
  `files`), custody as CASE-032 renders it, registered date; Received and
  Source only if CASE-032 supplies them (see Dependencies). No Vehicle, no
  lifecycle chip. Row link stays `/VehicleImages/{id}`.

### Step 3 — Wire the quick-view actions

- Files: `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs`,
  `src/Pegasus.Web/Pages/Cases/Index.cshtml`.
- **Create Case:** a link to the existing route
  `/Cases/Create?receiptId={OriginReceiptId}`; its GET handler validates the
  receipt through `IAllocateIntake` and redirects an already-associated item
  (`src/Pegasus.Web/Pages/Cases/Create.cshtml.cs:210-263`). No new handler.
- **Add to an existing case:** `OnPostAttachAsync(Guid receiptId, string?
  reference, string reason)` in `Index.cshtml.cs`, the same shape as
  `UploadConfirmationPageModel.OnPostAttachAsync` (`:47-80`), calling
  `IUploadCaseDecision.AttachAsync(receiptId, caseId: null, reference,
  reason, actor, …)` (`src/Pegasus.Web/Presentation/UploadCaseDecision.cs:31-73`).
  That path is the leased `ILinkIntake` link whose success runs the single
  Core pairing owner `IImageIntakeCasePairing.SyncMergeAfterLinkAsync`; do
  not call the pairing port directly. **Wrapper:** the form is script-free —
  typed case reference plus reason, no `SearchAsync` autocomplete (that needs
  `site.js`, a CASE-038 lock, and a control with no caller is not added).
  Success/failure use the layout's existing `TempData["Confirmation"]` /
  error convention, redirecting back to `/Cases?tab=awaiting`.
- Reuse the existing quick-detail definition-list and button-row markup in
  `Index.cshtml`; no new partial, no generic action framework. If the attach
  handler cannot be completed with `IUploadCaseDecision`, the control is
  absent and the gap is recorded as a follow-up dependency — never inert.

### Step 4 — Prove the split and the actions

- File: `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`.
- Reuses: `RegisterImageIntakeAsync`, `SeedNotReadyCaseAsync`, the existing
  web client and regex rail-count assertions (`:96-183, 326-437`).
- Replace `NotReadyRailCountMatchesRowsAcrossBothOrigins` (INTK-013,
  `:95-130`) with assertions that one formal Not ready Case and one
  AwaitingInstruction image intake yield: Not ready count 1 with only the
  Case row; Awaiting instruction count 1 with only the image row; shell
  Cases total counting both exactly once; the image count value from the
  CASE-032 projection; a Create Case link to `/Cases/Create?receiptId=`;
  and a successful POST to `OnPostAttachAsync` after which the intake leaves
  the Awaiting tab.
- **Wrapper (verified by `git grep`):** no other test asserts the merged
  Not ready count; `QdosAllocationRecoveryTests.cs:1266-1267` reads
  `GetCaseStageCountsAsync` — re-run it, edit only if it asserts `NotReady`
  over image intakes.

## Acceptance checks

- `/Cases?tab=awaiting` is listed in the Pre-Case group with the central
  label; its count equals its rows.
- Not ready lists formal Cases only; no image intake appears there.
- The Cases shell rail total includes Awaiting instruction once.
- Work Centre Not ready equals the formal Not ready count (side effect
  recorded; UIIMP-008 notified).
- Rows and quick detail show only supported data; no Vehicle, no disabled
  control, no explanatory copy.
- Create Case and Add to an existing case reach named, working handlers.
- No `ListImagesAsync` call per Awaiting row; no migration.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

`./scripts/Test-MigrationGrants.ps1` is not applicable: no migration. If one
becomes necessary, stop and report new scope. The snapshot verify and
catalogue check pass only with UIIMP-014's states on `dev`; a local red on
those two alone is reported, not worked around.

## Simplification pass

_Executor fills in after implementation (dated heading): reuse,
simplification, efficiency, altitude findings with dispositions._

## Stop condition

PR targeting `dev` is open with the commands' outputs recorded and CASE-042 is
in **Review**. Do not merge; do not start another ticket.

## Resolutions (2026-09-03)

- Controller: keep the shipped `Pre-Case work` group label; no OperatorLabels change.
- Controller: no vehicle column until a vehicle is recorded (absent, not drawn); no data-model ticket.

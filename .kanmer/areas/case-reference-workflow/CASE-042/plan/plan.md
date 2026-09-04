# Plan — CASE-042 (2026-09-02, gpt-5.6-terra high; revised 2026-09-03)

Produced by gpt-5.6-terra (effort high) read-only in `.worktrees/research` at
origin/dev `897db953`; the wrapper (Claude) re-verified the cited lines and
made the adjustments marked **Wrapper**. Revised 2026-09-03 by Claude Opus
after gpt-5.6-sol's independent review (see **Plan review** at the end);
changes carry the marker **R-n** naming the finding they answer.

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

**R-5: the tab key is `awaiting`** (the mockup's `05-state.js` key). UIIMP-014's
plan currently writes `?tab=awaiting-instruction` and states that the value is
CASE-042's to settle; CASE-042 settles it as `awaiting` and the executor tells
UIIMP-014 in the PR description.

## Governing docs and design rules

- FRD-12 places Awaiting instruction in Pre-Case work beside Triage, with
  formal Cases remaining in Not ready (`docs/frd/frd-12-operator-experience.md:145-175`).
- FRD-02 keeps an image-initiated case in Awaiting instruction until its
  retained evidence can associate with an eligible instructed Case
  (`docs/frd/frd-02-intake-and-source-identity.md:170-176`), and makes the
  Image-initiated Case's own `RegisteredAtUtc` and its derived chase-due state
  the two facts its queue row already carries (the INT-32 paragraph,
  `frd-02-intake-and-source-identity.md:176`).
- No explanatory copy and no empty-state prose (follow CASE-025's empty list).
  An unavailable capability is absent, never disabled (D7/D21); every
  displayed control has a named, working handler.
- **R-4: the tab label is the inline literal `"Awaiting instruction"`**, not an
  `OperatorLabels` member. The existing convention in this very file is that a
  record kind's own settled name is a literal in `Tabs` — `new("triage",
  "Triage", …)` and `new("unidentified", "Unidentified", …)`
  (`Index.cshtml.cs:94-96`), documented by the comment above the list ("comes
  from `OperatorLabels.CaseStage` (D3) **or is the record kind's own settled
  name**", `:83-87`). The existing convention wins, the reason is recorded here,
  and CASE-042 therefore has **no CASE-038 dependency for the label** and does
  not edit `OperatorLabels.cs`. The conditional `OperatorLabels.cs` row in
  `files/files.md` is withdrawn by this revision.
  `OperatorLabels.ImageIntakeLifecycleState` ("Awaiting definitive
  instruction", `OperatorLabels.cs:440-442`) is left untouched. The action
  label is likewise a literal in `Index.cshtml`, beside the shipped `Open …`
  button text.
- **R-6 — FRD-12 is contradicted by this change and must move with it.**
  FRD-12 as written lists Pre-Case work = Triage only (`:150`), says "Not ready
  rows are either origin settled for the Image-initiated Case lifecycle"
  (`:169-171`), says "A row links to its detail and nothing else" and gives the
  non-Case quick detail "the definition list and the open action" (`:162-167`).
  All four statements change here. The PR must carry the FRD-12 edit for those
  four sentences and nothing else. Governing docs are a capacity-one lock held
  by CASE-038 this wave, so this is a **serialized narrow handoff**: take the
  docs lock after CASE-038 merges, make only that edit, and if the lock cannot
  be released, **stop and report** — do not ship behaviour that contradicts the
  FRD.
- **Wrapper:** the row lifecycle chip is omitted on this tab — every row shares
  the one state, so the chip carries no information (page economy).
- Resolved by the controller (`open-questions/`): keep `PreCaseGroup =
  "Pre-Case work"` (`Index.cshtml.cs:80`) unchanged; draw no Vehicle column or
  quick-detail fact. No em dash, no disabled control.

## Dependencies

- **CASE-032 — blocking** (board: backlog, `blocks: CASE-042`). CASE-042 reads
  the Awaiting rows from CASE-032's extended `ImageIntakeSummary` through the
  existing `IImageIntakeQueries.ListAsync(false, …)` read — no per-row image
  query and no new query type. **R-2:** CASE-032's body as written adds only the
  *custody* half of `files·custody` and leaves the file count where it is, so
  the earlier claim that it supplies an aggregate image count was wrong. The
  corrected prerequisite is:
  - image reference, normalized registration, origin receipt id, lifecycle
    state — **already present**;
  - **Received — resolved without a new field**: it is the intake's own
    `RegisteredAtUtc`, which FRD-02's INT-32 paragraph names as the
    image-initiated half's chronology and says is already on its queue row. No
    origin-receipt timestamp is introduced and no CASE-032 amendment is needed
    for this column;
  - **aggregate image count — a required CASE-032 amendment**, folded into the
    same projection read (CASE-032's own Verification bullet "Queue page query
    count is unchanged (no N+1 introduced)" already commits it to that shape);
  - **custody — CASE-032 as briefed**, rendered as CASE-032 defines;
  - **source — a required CASE-032 amendment** to the same projection.

  The earlier "ship without Received and Source and record the deviation"
  escape is **removed**: a ticket-required column is a prerequisite, not an
  optional deviation. If CASE-032 will not carry the count and the source,
  CASE-042 takes those exact two projection fields itself after CASE-032's
  handoff — they are inside its own "queue projection for AwaitingInstruction"
  ownership — and says so in the report. Vehicle and a registration-read result
  are not needed (open question 2).
- **CASE-038 — no longer a blocking dependency** (R-4), except for the
  governing-docs lock handoff in R-6. CASE-042 does not edit
  `OperatorLabels.cs`, `Pages/Shared/*`, `Pages/Cases/Shared/*`,
  `wwwroot/css/site.css`, `wwwroot/js/site.js`, or `Persistence/Migrations/**`.
- **UIIMP-014 — coordination, not a blocking dependency (R-5).** CASE-042's own
  page change invalidates the two *existing* `/Cases` captures, and AGENTS.md
  requires the snapshots to ship with the page change, so **CASE-042
  regenerates and commits `docs/design/test-ui/pages/queues--default.html` and
  `queues--empty.html` only** — a narrow serialized slice of UIIMP-014's
  `docs/design/test-ui/**` lock, agreed in the PR description. It adds no
  catalogue entry and no new scenario file: the new `queues--awaiting`
  populated and empty states, their `catalogue.json` rows and the
  `TestUiSnapshotTests.cs` scenario expectation stay UIIMP-014's. "Expected red
  until UIIMP-014 merges" is not an acceptable verification result and is
  removed.
- **UIIMP-008 — coordination.** After the count split the Work Centre's Not
  ready metric becomes formal Not ready Cases only, because it reads
  `CaseStages.NotReady` (`src/Pegasus.Web/Pages/Index.cshtml:37-40`). Do not
  edit `Pages/Index.*`; note the side effect in the PR and the report. **R-3:**
  no edit is needed there — see the constructor placement rule below.

## Count-split decision

**CASE-042 carries the move** in
`src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` and
`src/Pegasus.Core/Operations/DashboardCounts.cs`: it is inside CASE-042's owned
queue-projection scope, required by the queue split, and CASE-032 does not own
the dashboard aggregate. The addend is `EfDashboardQueries.cs:60-68`, moved out
of `For(notReady)`. Leaving it while adding an Awaiting count double-counts the
same records.

**R-7 — one membership authority for the count and the rows.** The shipped
count filters on `ImageIntakes.MergedIntoCaseId is null`
(`EfDashboardQueries.cs:60-68`) while `ListAsync(false, …)` filters on the
projected `AssociatedCaseId is null`, which is derived from
`IntakeManualAssociations` and `CaseIntakeLinks` on the *origin receipt*
(`EfImageIntakeStore.cs:654-668, 860-920`) — two different columns. A record
whose receipt is already linked but whose merge synchronisation has not yet run
is counted and not listed, so "count equals rows" fails. The split must not
inherit that: the awaiting **count and the row read use the same predicate**.
Take the row predicate as the authority — it is what the operator sees — and
count the same `AwaitingInstruction` records the projection returns with no
current association, expressed inside the same single `EfDashboardQueries` read
rather than as a second query. A test covers the linked-but-not-yet-merged
state (Step 4).

**R-3 — `CaseStageCounts` field placement.** `CaseStageCounts`
(`DashboardCounts.cs:28`) gains a **required** `AwaitingInstruction` field
inserted **before the optional `Complete`** — `(int NotReady, int Review, int
Held, int WithEngineer, int AwaitingInstruction, int Complete = 0)`. No
optional parameter and no compatibility default for the new field (rule 6), and
the placement keeps every existing four-argument initialiser compiling and
correct, so no lane boundary is crossed. The complete caller inventory,
verified by grep:

| Caller | Effect |
| --- | --- |
| `EfDashboardQueries.cs:68-73` (the construction) | edited — the split |
| `src/Pegasus.Web/Pages/Index.cshtml.cs:20` `new(0,0,0,0)` | unchanged, still compiles (UIIMP-008's file — not edited) |
| `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:156` `new(0,0,0,0)` | unchanged |
| `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs:355` | unchanged, still compiles |
| `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs:1272` `Assert.Equal(new(0,0,0,0), stages)` | unchanged; re-run to confirm it still passes — do not weaken it |
| `src/Pegasus.Core/Operations/OperationsSnapshot.cs:54` (member, not constructor) | unchanged |

`NotReady` becomes formal workflow rows only; the `<remarks>` on the record and
the comment at `EfDashboardQueries.cs:50-59` are rewritten to say so — that
comment also still names the pre-rename `Triage/Index.cshtml.cs`, corrected to
`Cases/Index.cshtml.cs`.

## Ordered steps

### Step 1 — Split the dashboard aggregate

- Files: `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs`,
  `src/Pegasus.Core/Operations/DashboardCounts.cs`.
- Reuses: the existing `ImageIntakes` read and `EfImageIntakeStore.ToCode`; the
  existing `CaseStageCounts` record.
- Move the addend into the new `AwaitingInstruction` field (placed per R-3);
  `NotReady` becomes `For(notReady)` alone; the predicate is the R-7 one.

### Step 2 — Move rows and counts to the Awaiting instruction tab

- Files: `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs`,
  `src/Pegasus.Web/Presentation/RailCountsPageFilter.cs`.
- Reuses: `Tabs`, `Count(Tab)` (`Index.cshtml.cs:88-97, 163-172`), `QueueRow`,
  `QuickDetail`, `RecordDetail` (`:510`), `ImageRow` (`543-558`), `Href`
  (`:256-278`), the existing `IDashboardQueries.GetCaseStageCountsAsync` call,
  and CASE-032's summary.
- Add `new("awaiting", "Awaiting instruction", PreCaseGroup, "icon-image")`
  after `triage` — the `icon-image` symbol already exists
  (`Pages/Shared/_LucideSprite.cshtml:25`), so no sprite edit. Map `"awaiting"
  => StageCounts.AwaitingInstruction` in `Count`, and add the `"awaiting" =>
  await LoadAwaitingAsync(…)` arm to the `Queue switch` at `:317` so it does not
  fall through to `LoadCasesAsync`.
- `RailCountsPageFilter.cs:69-75`: add `stages.AwaitingInstruction` to the
  Cases total exactly once.
- `LoadNotReadyAsync` (`379-415`) returns formal Not ready Case rows only.
  Delete the now-dead image branch with it: the `listImages` gate, the
  `ListAsync`/`ListImagesAsync` calls, the `AwaitingInstruction` filter, and the
  doc-comment sentence about an image row being "listed for All and
  Instructions only".
- `LoadAwaitingAsync` calls `ListAsync(false, …)`, keeps `AwaitingInstruction`,
  and builds rows through `ImageRow` from the summary fields alone — no
  `ListImagesAsync` per row.
- Row and quick detail: reference·registration title; image count (CASE-032)
  and custody as CASE-032 renders it; **Received = `RegisteredAtUtc`**; Source
  (CASE-032). **R-8:** keep the existing **Chase** fact
  (`OperatorLabels.ImageChaseState` over `ImageIntakeChaseSchedule.IsChaseDue`,
  `ImageRow:553-557`) — FRD-02's INT-32 paragraph makes the derived chase-due
  state a required queue-row fact, and dropping it would be an unauthorised
  regression of shipped behaviour. No Vehicle, no lifecycle chip.
- **R-1 — row selection must reach every row.** Rows on this tab link to
  `Model.Href(selected: row.Id)` rather than straight to `DetailHref`; the
  full-record link stays the quick detail's existing Open button
  (`RecordDetail`'s `DetailHref` / `OpenLabel`, rendered at
  `Index.cshtml:223-232`, target `/VehicleImages/{id}`). Without this no
  production control emits `?selected=`, the model always falls back to
  `Rows[0]` (`:329-331`), and the quick-view action below is unreachable for
  every row but the first — the `[data-select-href]` preview script in
  `site.js:1531+` is not wired to `.row-button`, and `site.js` is a CASE-038
  lock. This per-kind row behaviour is one of the FRD-12 sentences R-6 updates.

### Step 3 — Wire the quick-view action

- Files: `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs`,
  `src/Pegasus.Web/Pages/Cases/Index.cshtml`.
- **Add to an existing case.** Reuse the existing abstract
  `UploadConfirmationPageModel`
  (`src/Pegasus.Web/Pages/UploadConfirmationPageModel.cs:16-80`) rather than
  writing a third copy of the same handler: `Cases/IndexModel` derives from it
  and supplies only `RedirectToSurface(id) => RedirectToPage(new { tab =
  "awaiting", selected = id })`, exactly as `UploadStatus.cshtml.cs:15` and
  `UploadGroupStatus.cshtml.cs:19` do. That base already resolves the actor,
  requires a reason, calls `IUploadCaseDecision.AttachAsync(receiptId, caseId:
  null, reference, reason, actor, …)`
  (`Presentation/UploadCaseDecision.cs:31-73`) — the leased `ILinkIntake` path
  whose success runs the single Core pairing owner
  `IImageIntakeCasePairing.SyncMergeAfterLinkAsync` — and handles
  `StaffAuthorizationException`. Do not call the pairing port directly.
- The form is script-free: typed case reference plus reason, no `SearchAsync`
  autocomplete (that needs `site.js`, a CASE-038 lock). It posts the intake's
  **`OriginReceiptId`**, which must therefore be carried from
  `ImageIntakeSummary` onto the row or quick detail — `QueueRow.Id` is the
  intake id, not the receipt id.
- **R-9 — the failure must be visible.** The base writes success to
  `TempData["Confirmation"]` (rendered by `_Layout.cshtml:165`) and failure to
  `TempData["UploadConfirmationError"]`, which only the two upload pages render
  (`UploadStatus.cshtml:29`, `UploadGroupStatus.cshtml:26`); `/Cases` renders no
  such banner today. Add that one `@if (TempData["UploadConfirmationError"] is
  string …)` block to `Cases/Index.cshtml` in the same shape those two pages
  use, so a refused attach surfaces instead of being swallowed (rule 12).
- **R-10 — Create Case has no working route; it is an open question.**
  `/Cases/Create?receiptId={OriginReceiptId}` refuses exactly this receipt:
  `DescribeRefusal` (`Create.cshtml.cs:584-600`) returns
  `OperatorLabels.IntakeCannotBecomeCaseReason` whenever
  `IntakeDecisionPolicy.CanBecomeCase` is false, and that policy returns `false`
  for `IntakeDecision.ImageIntakeRegistered`
  (`src/Pegasus.Core/Intake/IntakeDecisionPolicy.cs:30-40`). FRD-02 agrees:
  image-only material merges into an eligible instructed Case and does not
  create a formal Case/PO by itself
  (`frd-02-intake-and-source-identity.md:172-174`). There is no blank Create
  route to fall back to either — `OnGetAsync` returns `NotFound` for an empty
  `receiptId` (`Create.cshtml.cs:210-220`). So the control cannot be wired to a
  named working handler, and D7/D21 forbid drawing it inert. **Until the
  operator answers open question 3, ship the tab with Add to an existing case
  only**; do not link to the refusing route and do not invent a Core creation
  flow (rule 1, rule 18).
- Reuse the existing quick-detail definition-list and button-row markup in
  `Index.cshtml` for the facts and the Open button; the attach form is the one
  piece of new markup and follows the two upload pages' existing form shape. No
  new partial, no generic action framework.

### Step 4 — Prove the split and the action

- Files: `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`,
  `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs`.
- Reuses: `RegisterImageIntakeAsync`, `SeedNotReadyCaseAsync`,
  `StoreMinimalReceiptAsync`, the existing web client and regex rail-count
  assertions (`:96-183, 326-437`), and `UploadConfirmationWebTests`'
  `PostAttachAsync` helper shape (`:526`) for the antiforgery-carrying POST.
- **R-11 — the complete inventory of tests this change breaks**, found by grep
  for `tab=not_ready` and `GetCaseStageCountsAsync`. All three assert the image
  row or its count on Not ready; all three are repurposed, none deleted or
  weakened:
  1. `NotReadyRailCountMatchesRowsAcrossBothOrigins` (`:104-152`) → the
     split-count test: one formal Not ready Case and one AwaitingInstruction
     intake give Not ready count 1 with only the Case row, Awaiting count 1 with
     only the image row, and a shell Cases total counting both exactly once.
     Keep its Work Centre assertion, retargeted: the Work Centre Not ready
     metric now equals the formal count.
  2. `NotReadyImageRowRendersRetainedImageCountAndChaseState` (`:163-183`) → the
     same assertions against `/Cases?tab=awaiting`, with the image count read
     from the CASE-032 projection and the chase state kept (R-8).
  3. `NotReadyRendersOneMergedRowListAcrossOrigins` (`:331-366`) → split: Not
     ready keeps the formal Case and the structural assertions; a new Awaiting
     assertion covers the image row, and its `Assert.Contains("Awaiting
     definitive instruction")` becomes a `DoesNotContain` on the awaiting tab
     (no lifecycle chip).
  4. `QdosAllocationRecoveryTests.cs:1272` — re-run; it must still pass
     unchanged under the R-3 placement. Do not edit it to go green.
- New assertions: selecting the **second** of two Awaiting rows without
  JavaScript shows that row's quick detail (R-1); a successful POST to the
  inherited `OnPostAttachAsync` after which the intake leaves the Awaiting tab;
  a refused attach (unknown reference, or a missing reason) rendering the error
  and leaving the row in place (R-9); and the R-7 state — an intake whose origin
  receipt is linked but whose merge has not yet synchronised — proving the
  Awaiting count still equals the rendered rows.
- `AccessibilityTests`' page list (`Browser/AccessibilityTests.cs:19-25`)
  enumerates the tab URLs; add `/Cases?tab=awaiting`.

## Acceptance checks

- `/Cases?tab=awaiting` is listed in the Pre-Case group; its count equals its
  rows, including the linked-but-unmerged state (R-7).
- Not ready lists formal Cases only; no image intake appears there.
- The Cases shell rail total includes Awaiting instruction once.
- Work Centre Not ready equals the formal Not ready count (side effect
  recorded; UIIMP-008 notified).
- Every row's quick detail is reachable without JavaScript (R-1).
- Rows and quick detail show only supported data — reference·registration,
  image count·custody, Received, Source, Chase; no Vehicle, no lifecycle chip,
  no disabled control, no explanatory copy.
- Add to an existing case reaches the inherited, named handler and its failure
  is rendered. Create Case is absent pending open question 3.
- FRD-12's four affected sentences are updated in the same PR (R-6).
- No `ListImagesAsync` call per Awaiting row; no migration; no new package.

## Expected files

- `src/Pegasus.Core/Operations/DashboardCounts.cs`
- `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs`
- `src/Pegasus.Web/Pages/Cases/Index.cshtml`
- `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs`
- `src/Pegasus.Web/Presentation/RailCountsPageFilter.cs`
- `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs`
- `docs/frd/frd-12-operator-experience.md` (only the four sentences in R-6,
  under the serialized docs-lock handoff)
- `docs/design/test-ui/pages/queues--default.html`
- `docs/design/test-ui/pages/queues--empty.html`

## Do not modify

- `src/Pegasus.Web/Presentation/OperatorLabels.cs`,
  `src/Pegasus.Web/Pages/Shared/*`, `src/Pegasus.Web/Pages/Cases/Shared/*`,
  `src/Pegasus.Web/Pages/Administration/Shared/*`,
  `src/Pegasus.Web/wwwroot/css/site.css`, `src/Pegasus.Web/wwwroot/js/site.js`
  — CASE-038.
- `src/Pegasus.Web/Pages/Index.*` (Work Centre) — UIIMP-008.
- `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` — CASE-032,
  unless it declines the count and source fields and hands them over (see
  Dependencies).
- `docs/design/test-ui/catalogue.json`,
  `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`, and any
  `queues--awaiting*` capture — UIIMP-014.
- `src/Pegasus.Infrastructure/Persistence/Migrations/**` — no migration here.
- `src/Pegasus.Web/Pages/Cases/Assessment/*`,
  `src/Pegasus.Web/Pages/Cases/Details.*`,
  `src/Pegasus.Web/Pages/Operations/**`,
  `src/Pegasus.Web/Pages/Administration/**`, `src/Pegasus.Core/AiWork/**`.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

**R-12:** the test command is the canonical AGENTS.md one (`Category!=Corpus`);
the earlier `&Category!=Browser` silently dropped the Browser half of the gate
on a change that alters a routed page. If the Browser lane cannot run on the
workstation, run the runbook's two complementary integration filters
(`docs/runbook.md:324-325`) and record both exit codes — never a single
filtered pass presented as the gate.

`./scripts/Test-MigrationGrants.ps1` is not applicable: no migration. If one
becomes necessary, stop and report new scope. The regenerate step writes only
the two existing `queues--*` captures this page change invalidates; if it wants
to write any other file under `docs/design/test-ui/**`, stop and report rather
than committing it. A red verify is resolved before the PR, not accepted.

## Simplification pass

_Executor fills in after implementation (dated heading): reuse,
simplification, efficiency, altitude findings with dispositions._

## Stop condition

PR targeting `dev` is open with the commands' outputs recorded and CASE-042 is
in **Review**. Do not merge; do not start another ticket.

## Resolutions (2026-09-03)

- Controller: keep the shipped `Pre-Case work` group label; no OperatorLabels change.
- Controller: no vehicle column until a vehicle is recorded (absent, not drawn); no data-model ticket.

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

Reviewer verdict: **REQUEST CHANGES** (9 findings). Every finding was
re-verified against `dev` `1e6ac077` before disposition; the wrapper added six
of its own. One finding becomes an operator question, which is why the ticket
is not yet ready to leave `preparing`.

| # | Severity | Plan step | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | Step 3 | "Create Case" links to `/Cases/Create?receiptId=`, which refuses `ImageIntakeRegistered` receipts (`IntakeDecisionPolicy.cs:30-40`, `Create.cshtml.cs:584-600`); FRD-02:172 forbids image-only material creating a formal Case/PO. There is no blank Create route either. | **Confirmed — operator question.** Both call paths verified. A ticket-mandated control with no lawful handler; D7/D21 forbid drawing it inert. Open question 3 added; the tab ships with Add to an existing case only until it is answered (R-10). |
| 2 | blocker | Dependencies / Step 2 | CASE-032 supplies only the custody half; image count, origin Received and Source are not in its brief, and the plan's "ship without Received and Source" escape drops ticket-required columns. | **Accepted, fixed with one correction.** Count and Source become required CASE-032 amendments (or CASE-042 takes those two projection fields after handoff); the deviation escape is deleted. **Received needs no new field**: FRD-02's INT-32 paragraph makes the intake's own `RegisteredAtUtc` the image half's queue-row chronology (R-2). |
| 3 | blocker | Steps 2-3 | No production control emits `?selected=`, so the model always falls back to `Rows[0]` and the quick-view actions are unreachable for every row but the first. | **Accepted, fixed.** Verified: rows are plain `href="@row.DetailHref"` links (`Index.cshtml:126`) and `site.js`'s preview binds `[data-select-href]`, which this page does not use. Awaiting rows now link through the existing `Href(selected:)`; the full-record link stays the quick detail's Open button (R-1). |
| 4 | blocker | Steps 2-3 | `OperatorLabels.<CASE-038 entry>` is not a real symbol, CASE-038's plan adds only Case-workspace vocabulary, and plan and `files.md` contradict each other on whether `OperatorLabels.cs` is edited. | **Accepted, fixed — but not as suggested.** The reviewer proposed taking the `OperatorLabels.cs` lock. Rejected in favour of the shipped convention: sibling pre-case tabs are literals (`Index.cshtml.cs:94-96`) under a comment that explicitly allows "the record kind's own settled name". CASE-042 uses the literal, edits no labels file, and drops the CASE-038 label dependency; the `files.md` row is withdrawn (R-4). |
| 5 | blocker | Commands / UIIMP-014 | Adding a tab invalidates the existing `/Cases` captures, which AGENTS.md requires in the same PR, while the plan disclaims `docs/design/test-ui/**` and accepts a red gate; UIIMP-014 also expects `?tab=awaiting-instruction`. | **Accepted, fixed.** CASE-042 regenerates only `queues--default.html` and `queues--empty.html` as a narrow serialized slice; the new scenario, catalogue row and `TestUiSnapshotTests` expectation stay UIIMP-014's. Expected-red removed. Tab key settled as `awaiting`; UIIMP-014's own plan already defers the key to CASE-042 (R-5). |
| 6 | blocker | Steps 1 and 4 | The constructor/test inventory is incomplete: other target-typed `new(0,0,0,0)` callers exist, and `TriageQueuesWebTests` lines 163 and 332 also require images on Not ready. | **Accepted, fixed; the suggested Work Centre edit rejected.** The six callers are tabulated. Placing the required field **before** the optional `Complete` keeps all four `new(0,0,0,0)` initialisers compiling and correct, so `Pages/Index.cshtml.cs` (UIIMP-008) needs no edit and no lane boundary is crossed (R-3). All three broken tests are repurposed, none weakened (R-11). |
| 7 | should-fix | Step 3 | The claimed "layout error convention" does not exist: `_Layout` renders only `TempData["Confirmation"]`; `UploadConfirmationError` is rendered by the two upload pages, and only success was to be tested. | **Accepted, fixed.** Both render sites verified. `/Cases/Index.cshtml` gains the same error block and a refused-attach test is added (R-9). |
| 8 | blocker | Steps 1-2 | Count and rows use different authorities — `MergedIntoCaseId is null` versus the projection's receipt-derived `AssociatedCaseId is null` — so a linked-but-unmerged record is counted and not listed. | **Accepted, fixed.** Both predicates verified at `EfDashboardQueries.cs:60-68` and `EfImageIntakeStore.cs:654-668, 860-920`. One predicate, taken from the row read, now governs both, and the state is tested (R-7). |
| 9 | should-fix | Whole plan | The packet has no `Expected files` / `Do not modify` sections, so the allowed-file set is not mechanically bounded. | **Accepted, fixed.** Both sections added with exact paths and no globs; Step 4 uses `Files:`. |
| 10 | blocker (wrapper) | Governing docs | FRD-12 as written contradicts this change in four places: Pre-Case work = Triage only (`:150`), "Not ready rows are either origin" (`:169-171`), "A row links to its detail and nothing else", and the non-Case quick detail being "the definition list and the open action" (`:162-167`). The plan named no FRD edit. | **Added and fixed.** The PR carries those four sentences under a serialized narrow handoff of the governing-docs lock; if the lock cannot be released, stop and report rather than shipping behaviour that contradicts the FRD (R-6). |
| 11 | should-fix (wrapper) | Step 2 | The plan silently dropped the image row's **Chase** fact, which FRD-02's INT-32 paragraph requires on the image-initiated queue row as a derived read. | **Added and fixed.** The Chase fact is retained on the Awaiting rows (R-8). |
| 12 | should-fix (wrapper) | Commands | The test filter added `&Category!=Browser`, dropping the Browser half of the canonical gate on a routed-page change. | **Added and fixed.** The canonical `Category!=Corpus` command is restored, with the runbook's two complementary filters as the only substitute (R-12). |
| 13 | should-fix (wrapper) | Step 3 | The plan wrote a fresh `OnPostAttachAsync` "the same shape as" the existing one — a third copy of a handler that already has an abstract base with two derivations. | **Fixed.** `Cases/IndexModel` derives from `UploadConfirmationPageModel` and supplies only `RedirectToSurface` (rule 7, rule 8). |
| 14 | should-fix (wrapper) | Step 3 | The attach and create actions both need the intake's `OriginReceiptId`, which is on `ImageIntakeSummary` but is not carried by `QueueRow`/`QuickDetail`; the plan never plumbed it. | **Fixed.** Step 3 names the plumbing explicitly. |
| 15 | nit (wrapper) | Step 2 | After the move, `LoadNotReadyAsync`'s `listImages` gate and its doc comment about image rows are dead code. | **Fixed.** Their deletion is named in Step 2. |
| 16 | nit (wrapper) | Step 4 | `Browser/AccessibilityTests` enumerates the tab URLs and would not cover the new one. | **Fixed.** Added to Step 4. |

## Resolutions (2026-09-03) — Create Case dropped

The operator answered the third open question with **option (a)**:

1. **Create Case is dropped** from the Awaiting instruction quick view. The
   tab ships with "Add to an existing case" only. Nothing is drawn inert
   (D7/D21), and no Core creation route is added.
2. **The ticket body's What and Verification lines are amended** to remove
   Create Case.
3. **The reverse direction is a separate ticket.** [[CASE-044]] "Add evidence
   to a case" gives an instructed case an upload route and an absorb-an-
   image-case route, reachable from the case action bar and the main rail.
   It is out of CASE-042's scope and does not block it; CASE-042 must not
   build any part of it.

## Simplification pass (2026-09-04)

Run over `git diff origin/dev...HEAD` by gpt-5.6-sol (low), read-only,
dispositioned by the wrapper.

| # | Lens | File:line | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | Simplification | `Pages/Cases/Index.cshtml.cs` `LoadNotReadyAsync` | `casesTask` was assigned and immediately awaited on the next line — dead task-shaped ceremony left over from the merged-source implementation, never run concurrently with anything. | **Applied.** Await `_searchCases.ExecuteAsync(...)` directly into `result`. |
| 2 | Simplification | `Pages/Cases/Index.cshtml.cs` `ImageRow` | The retained-image count label (with its pluralisation) was built twice for the same row — once for `Facts`, once for `Excerpt`. | **Applied.** Computed once into `imageCountLabel` and reused in both places. |
| 3 | Efficiency | `Persistence/EfImageIntakeStore.cs` `ProjectAsync` | `ImageCount` was added to the shared `ProjectAsync` projection used by `ListAsync` (the Awaiting queue), `ListByOriginReceiptsAsync`, `ListForCaseAsync`, and `SearchByRegistrationAsync`, so every one of those callers now pays for the multi-table image-count subquery even though only the Awaiting queue (`ListAsync`) needs it. | **Accepted risk, not applied.** Splitting the count out (a second projection variant, or a toggle parameter) would add a second query shape, which the plan explicitly rules out ("no per-row image query and no new query type" — Dependencies section). `ProjectAsync` is already the one shared projection point for `ImageIntakeSummary`; the other three callers are lower-traffic (case detail intake list, registration search, receipt-id lookup) and the added cost is one bounded subquery per row batch, not an N+1. Left as a documented cost of keeping one projection instead of two; a future ticket can split it if it becomes a measured hot path. |

Rebuilt and re-ran `dotnet build` (0 errors), `Pegasus.Core.Tests` (1225
passed), `Pegasus.ArchitectureTests` (100 passed), and
`TriageQueuesWebTests`/`AccessibilityTests` (39 passed) after applying
findings 1-2; all green.

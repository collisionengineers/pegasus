# Research — CASE-042 (2026-09-02, gpt-5.6-terra medium, wrapper-checked)

## Verification key

Repo premises are **VERIFIED** by the read-only commands shown inline.
Board-state and lane-ownership facts supplied in the brief are **ASSUMED** because
Kanmer is unavailable to Codex. No builds, tests, edits, or `corpus/` reads were
made. The wrapper (Claude) re-ran the checks marked in "Wrapper checks" below
against `C:/Users/PC/Documents/GitHub/pegasus` on `dev` (1e6ac077) and confirmed
every VERIFIED claim it sampled.

## Current behaviour

- **VERIFIED** (`git log --oneline --all -- src/Pegasus.Web/Pages/Cases/Index.cshtml
  src/Pegasus.Web/Pages/Cases/Index.cshtml.cs`): CASE-025 is merged history
  (`4f5f9574`, `cffdce63`) and owns the current three-pane `/Cases` rail.

- **VERIFIED** (`Get-Content ...Index.cshtml.cs`, lines 20-28, 79-97):
  `/Cases` has Workflow, `Pre-Case work`, and Exceptions groups. Its tab list is
  Not ready, Review, With Engineer, Complete, Triage, Held, and Unidentified.
  There is no Awaiting instruction tab.

- **VERIFIED** (`Get-Content ...Index.cshtml.cs`, lines 372-415): image-initiated
  records currently appear in the **Not ready** tab. `LoadNotReadyAsync` calls
  `IImageIntakeQueries.ListAsync(false, ...)`, retains only
  `AwaitingInstruction`, calls `ListImagesAsync` once per returned row, and
  concatenates them with formal Not ready Case rows.

- **VERIFIED** (`Get-Content ...Index.cshtml.cs`, lines 543-558): the current
  image row shows image reference plus normalized registration, the lifecycle
  chip, retained-image count, and registered date; it links to
  `/VehicleImages/{id}`. Its quick facts are State, Registered, and Chase.

- **VERIFIED** (`Get-Content ...ImageIntakeContracts.cs`, lines 100-109,
  182-219): `ImageIntakeSummary` supplies Id, origin receipt id, image
  reference, normalized registration, association values, registered-at,
  lifecycle state, and closure reason. `ListAsync(bool? associated, ...)`
  does not accept a lifecycle-state predicate and no count method exists.

- **VERIFIED** (`Get-Content ...EfImageIntakeStore.cs`, lines 654-667,
  765-811, 857-920): `ListAsync` projects all image intakes, then filters only
  by whether an associated Case exists. The projection has no vehicle, source,
  receipt-received time, or image count. `ListImagesAsync` is a separate query;
  using the present port for an Awaiting instruction queue would be one list
  projection plus one image query per displayed row (N+1).

- **VERIFIED** (`Get-Content ...Index.cshtml.cs`, lines 307-323):
  `/Cases` currently obtains only stage, Triage, and Unidentified counts.
  The selected tab's rows are loaded afterwards.

- **VERIFIED** (`Get-Content ...RailCountsPageFilter.cs`, lines 17-27,
  62-76): the shell Cases count is
  `not_ready + review + with_engineer + held + triage + unidentified`.
  It has no image-intake query and therefore does not include Awaiting
  instruction separately. CASE-042 needs the same AwaitingInstruction count in
  both `IndexModel.Count` and `RailCountsPageFilter`'s Cases total, while
  removing these rows from Not ready. (See "Wrapper checks": the Not ready
  stage count already folds these intakes in today.)

- **VERIFIED** (`Get-Content ...OperatorLabels.cs`, lines 134-143, 440-456;
  `git grep -n 'Pre-Case\|Awaiting instruction'`): existing relevant labels
  include Case stages, Triage state labels, and
  `ImageIntakeLifecycleState(AwaitingInstruction) =>
  "Awaiting definitive instruction"`. There is no central label for
  `Awaiting instruction`, `Pre-case`, or `Pre-Case work`; current `/Cases`
  literals contain `Pre-Case work`, `Triage`, and `Unidentified`.

- **VERIFIED** (`Get-Content ...13-cases.js`, lines 15-25, 40-41;
  `Get-Content ...05-state.js`, lines 39-62): the mockup adds `awaiting` after
  Triage in a `Pre-case` group. It filters image cases whose state is
  `awaiting`, gives the tab its own count, and includes it in the Cases rail
  total. The table is Image reference, Registration, Vehicle, Received,
  Images, and Source. Quick view shows image reference, registration, vehicle,
  state, images, received time, source, registration read, and actions.

- **VERIFIED** (`Get-Content ...04-fixtures.js`, lines 478-479;
  `Get-Content ...03-labels.js`, lines 36-40): the mockup fixture carries
  `vehicle`, `received`, `time`, `source`, `images[]`, and a VRM result. Its
  image state label is `Awaiting instruction`.

## Projection readiness

| Required queue field | Current source | Ready now? |
| --- | --- | --- |
| Reference | `ImageIntakeSummary.ImageIntakeReference` | Yes |
| Registration | `NormalizedVehicleRegistration` | Yes |
| Vehicle | No field in summary, detail, or current projection | No |
| Received | `RegisteredAtUtc` only; not the origin receipt received time | No |
| Image count | `ListImagesAsync` only | No, unless accepting N+1 |
| Source | No field in summary/current projection | No |

- **ASSUMED** (board context supplied): CASE-032 will extend
  `ImageIntakeSummary` with a custody half and row rendering support, but has
  no research or plan. CASE-042 must wait for its defined projection contract;
  the supplied description does not establish that it will provide vehicle,
  received time, image count, or source.

- **VERIFIED** (`Get-Content ...ImageIntakeContracts.cs`, lines 100-149;
  `Get-Content ...EfImageIntakeStore.cs`, lines 857-920): a faithful mockup
  table cannot be built from the current summary. Vehicle and registration-read
  result are absent. Source, receipt-received time, and image count also require
  an expanded projection or a new query shape.

## Existing handlers and reuse

- **VERIFIED** (`Get-Content ...Cases/Create.cshtml.cs`, lines 210-268,
  603-637): `/Cases/Create?receiptId={originReceiptId}` is an existing
  receipt-backed Case-creation route and uses `IAllocateIntake`.

- **VERIFIED** (`Get-Content ...UploadOutcome.cs`, lines 213-250, 309-323;
  `Get-Content ..._UploadOutcome.cshtml`, lines 44-56): the existing upload
  outcome supplies an Attach disclosure and posts an existing Attach handler
  with the image intake's origin receipt id. It also links ordinary eligible
  receipts to `/Cases/Create?receiptId=...`.

- **VERIFIED** (`Get-Content ...ImageIntakeCasePairing.cs`, lines 34-50,
  67-115): `IImageIntakeCasePairing.SyncMergeAfterLinkAsync` is the single
  Core owner that reconciles a successful receipt-to-Case link with the
  image-initiated lifecycle.

- **VERIFIED** (`Get-Content ...ImageIntake/Details.cshtml.cs`, lines 26-45;
  `Get-Content ...ImageIntake/Details.cshtml`, lines 169-185): the existing
  image-detail page exposes eligible candidates but has no attach/merge POST
  handler; it only has a close handler.

- **VERIFIED** (`git grep -n 'OnPostAttach\|AttachAsync' src/Pegasus.Web
  src/Pegasus.Core`): an existing upload confirmation attach flow
  (`UploadConfirmationPageModel.OnPostAttachAsync` →
  `UploadCaseDecision.AttachAsync`) can be reused, but the Cases quick view
  cannot render a working "Add to an existing case" control merely by linking
  to the image detail. It needs a route to that existing flow or a named
  handler added in the page's approved scope. An inert control is prohibited
  (D7).

## Tests and snapshots

- **VERIFIED** (`Get-Content ...TriageQueuesWebTests.cs`, lines 96-183,
  326-437): `TriageQueuesWebTests` already tests the merged Not ready count,
  image retained count/chase chip, tab markup using regex, and supplies
  `RegisterImageIntakeAsync`, which performs the real upload-and-register
  sequence. `SeedNotReadyCaseAsync` is the existing formal-Case fixture.

- **VERIFIED** (`Get-Content ...catalogue.json`; `rg -n
  'src/Pegasus.Web/Pages/Cases/Index.cshtml' docs/design/test-ui/catalogue.json`):
  `/Cases` currently has `queues--default` (populated Triage) and
  `queues--empty` only. It has no Awaiting instruction state.

- **ASSUMED** (board context supplied): UIIMP-014 owns
  `docs/design/test-ui/**` for this wave. CASE-042 must not edit snapshots or
  the catalogue. The dependency is an Awaiting instruction populated state and
  an empty state, plus a regenerated `/Cases` capture after the routed Razor
  page changes.

- **VERIFIED** (`Get-Content AGENTS.md`; `rg -n
  'Update-TestUiSnapshots|Test-UiCatalogue' AGENTS.md`): CI requires
  `scripts/Update-TestUiSnapshots.ps1 -Verify` and
  `scripts/Test-UiCatalogue.ps1`; UIIMP-014 must regenerate and commit the
  matching snapshot/catalogue files before CASE-042 can pass that gate.

## Migrations

- **VERIFIED** (`rg -n 'ImageIntake|awaiting_instruction|ImageIntakes'
  src/Pegasus.Infrastructure/Persistence/Migrations`): image-intake persistence
  already exists. A queue projection/count change alone needs no migration.

- **VERIFIED** (`Get-Content ...ImageIntakeContracts.cs`, lines 100-109;
  `Get-Content ...EfImageIntakeStore.cs`, lines 857-920): the mockup Vehicle
  value is not stored in the current image-intake contract. If the operator
  requires a populated vehicle rather than the mockup's em dash, that is a
  separate data-model decision and could require a migration; CASE-042 must not
  invent it.

## Gaps, risks, and questions

- Move AwaitingInstruction image rows from Not ready into a separate Pre-case
  tab, count them there, and add them to the shell rail total.
- Await CASE-032's projection contract before row work. The current
  `ImageIntakeSummary` cannot supply the requested six fields without N+1 and
  missing values.
- Resolve the `Pre-case` mockup versus shipped `Pre-Case work` wording as a
  design-contract question; do not choose a spelling in CASE-042.
- `Awaiting instruction` requires a central `OperatorLabels` entry. That path
  is a CASE-038 shared lock for this wave, so it is a dependency rather than a
  local CASE-042 change.
- The mockup quick actions are not both presently reusable as direct links:
  creation can reuse the origin receipt route subject to its existing
  eligibility, while Add requires a route to the existing attach flow or a
  deliberately scoped handler.
- Existing `/Cases` has no explicit empty-state prose; its empty collection
  renders an empty list. The mockup's "No images awaiting an instruction" is
  explanatory copy; the design rule (no explanatory copy, existing convention
  wins) already decides this — follow CASE-025's empty list. Not an operator
  question.
- **Open questions only the operator can answer** (recorded in
  `open-questions/`): whether the Pre-case group label is the mockup's
  `Pre-case` or the shipped `Pre-Case work`; and whether Vehicle must be
  populated now (a data-model change, separate ticket) or the column is
  dropped/dashed until a vehicle is recorded for image intakes.

## Wrapper checks (Claude, 2026-09-02)

Re-run on `C:/Users/PC/Documents/GitHub/pegasus` at `dev` 1e6ac077; the
research checkout `.worktrees/research` sat at cad00be9, three docs-only
commits behind `origin/dev` 897db953 with no `src/` or `tests/` drift
(`git log --oneline cad00be9..origin/dev -- src tests` is empty).

- Confirmed: `Tabs` at `Index.cshtml.cs:88-97` has no awaiting tab and the
  group constant is `PreCaseGroup = "Pre-Case work"` (line 80); image rows are
  concatenated into Not ready at lines 408-413 with a `ListImagesAsync` call
  per row; `ImageRow` at 543-558; `OperatorLabels.ImageIntakeLifecycleState`
  returns "Awaiting definitive instruction" (440-442); `RailCountsPageFilter`
  sums six figures (69-75); `ImageIntakeSummary` has no vehicle, source or
  image count (`ImageIntakeContracts.cs:100-109`); `IImageIntakeQueries` has
  only `ListAsync(bool?)` and `ListImagesAsync` (182-194); catalogue `/Cases`
  states are `queues--default` and `queues--empty` (`catalogue.json:373-390`);
  `/Cases/Create` loads by `receiptId` through `IAllocateIntake`
  (`Create.cshtml.cs:211-223, 603-610`); `OnPostAttachAsync` lives in
  `UploadConfirmationPageModel.cs:47` calling `UploadCaseDecision.AttachAsync`;
  `ImageIntake/Details.cshtml.cs` exposes only `OnPostCloseAsync` (line 48);
  CASE-025 commits `4f5f9574` and `cffdce63` exist.
- **Added finding (VERIFIED, `sed -n 56,72p
  src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs`):** the Not
  ready stage count returned by `IDashboardQueries.GetCaseStageCountsAsync`
  already adds unassociated `AwaitingInstruction` image intakes
  (`CountAsync(item.MergedIntoCaseId == null && item.LifecycleState ==
  awaitingInstruction)`) — INTK-013, asserted by
  `TriageQueuesWebTests.NotReadyRailCountMatchesRowsAcrossBothOrigins`
  (lines 95-130). So today the rail total and the Work Centre Not ready metric
  already count these intakes inside Not ready. Moving the rows to an Awaiting
  instruction tab means either (a) removing that addend from
  `EfDashboardQueries` and adding a separate awaiting count, or (b) double
  counting. Option (a) touches `src/Pegasus.Infrastructure/Persistence/
  EfDashboardQueries.cs`, possibly `src/Pegasus.Core/Operations/
  DashboardCounts.cs` (`CaseStageCounts` shape), the Work Centre Not ready
  metric (`Pages/Index.*`, UIIMP-008 lane, EPIC-011) and that INTK-013 test.
  This is a real scope/ownership question for the plan, and CASE-032's brief
  does not cover it. Codex's Files table omitted it; the wrapper added it.
- Codex's rail-count wording ("does not include Awaiting instruction
  separately") is correct but understated for the reason above.
- Board facts Codex marked ASSUMED were read by the wrapper from the board
  worktree: CASE-032 is `backlog` and `blocks` CASE-042; CASE-025 is
  `verifying`; CASE-038 holds the shared locks (`OperatorLabels.cs`, site
  CSS/JS, `Pages/Shared/*`) this wave; UIIMP-014 owns `docs/design/test-ui/**`.

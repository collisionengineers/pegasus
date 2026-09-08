# Plan — ENG-031: visual images in the one Case edit

## Objective and evidence

Complete D46 visual photo preparation from Files and Report while making
every preparation edit part of the existing global Save/Discard transaction.
Preserve immutable originals and the existing generated-report source freeze.

Evidence: research@`5c388de7a0cd23f7`, files@`f99fed081e68ece6`;
accepted dev `aefe4c32d078ad79c0368666b5666032e6865248`.
This replaces the obsolete September 2 implementation proposal, not its
historical attribution. Existing numeric storage/rendering is integrated
through CASE-047/PR674. No new curation store, schema or renderer is needed.

Root's 8 September dispositions explicitly retain global Save/Discard,
approve reuse of PrepareSaveAsync and approve native ClaimLease staging
provided failed/stale acquisition never persists or refreshes authority.
This plan remains untaken pending whole root read and ownership clearance.

## Governing behavior

Meets FRD-06 report-image preparation, FRD-11 immutable generated sources,
FRD-12 Assessment, design README:991–992, EPIC-012 D46 and Astra B06.
Update only the mapped FRD06/12/design paragraphs to state the actual
visual/global-save behavior; FRD11 and protected operator-notes stay unchanged.
Historical approval-snapshot machinery is replaced by the existing generation
freeze; the immutable-snapshot outcome is unchanged. Not used remains the
reflection/exclusion disposition. No new ADR is required.

## Contract and ownership

Production path: Case Files viewer or Report card → one page-local image
editor → existing case-edit-form → Details.OnPostSaveAsync →
ISaveCaseWorkspace → EfCaseWorkspaceStore → existing PrepareSaveAsync.
Queries and generated reports continue reading the same occurrence columns.

Extend SaveCaseWorkspaceRequest with an optional list of the existing
CaseAssetPreparationEdit. Null means untouched; a supplied empty list means
no image edits, not reset-all. Image-only nonempty payload is a real Case
edit. Reject duplicates and invalid geometry/roles/versions through the
existing policy. An image payload requires authenticated staff Engineer
authority (reuse AssessmentPolicy.RequireFindingConfirmationAuthority),
even though other workspace sections permit general staff/Automation.
EfCaseWorkspaceStore additionally applies existing AssessmentAccessPolicy to
the actual persisted workflow before any image mutation. No copied state list.

Keep ICaseAssetPreparationQueries and the existing EF class. Source census
finds no other production mutation caller: remove ICaseAssetPreparationStore,
its Save/Reset request types, committing methods, obsolete replay/history
helpers, DI mutation registration and both immediate Web handlers. Update
only their actual tests/query-constructor caller. There is no legacy route.

## Required transaction behavior

Inside the existing serializable workspace transaction, after existing
version/lease/archive/state checks:

1. Read the pre-edit preparation through the same existing query/mapping
   in this context. Add image values to the existing request hash.
2. For nonempty edits require every occurrence to belong to the Case and
   still name an exact current confirmed nonremoved version. Absence from
   the source map is a refusal for an edited row, never permission to skip
   checking it. Use existing Core source validation; do not narrow generic
   immutable download/OCR metadata readers.
3. Adapt PrepareSaveAsync to accept the existing edits and actor directly
   rather than keeping a retired command solely as an internal wrapper.
   Preserve per-image optimistic checks and Core role/order/crop validation.
   Invalid mixed payload rolls back Case facts, fields and every image.
4. Include normalized before/after preparation in existing CaseMutationHistory.
   One existing CaseMutationGuard.Complete, one existing MarkStaleAsync, one
   SaveChanges/commit. No separate image event/history/version increment.
   Exact replay changes nothing; changed same-key payload conflicts.
5. Existing generation snapshots/artifacts never change. Later global crop
   save stales the current generation through the existing mechanism.

Reset is an ordinary staged edit (NotUsed, no order, zero rotation, Full).
Supporting move buttons and drag stage the same order edits; neither submits
independently. Keep the existing unambiguous unique-primary refusal.

## One UI state, two entry points

- Raw current retained image viewing, paging, Rotate view and Save as require
  no Edit Case lease. Noncase galleries and PDFs keep existing behavior.
  A Crop target is emitted only for an eligible exact case occurrence and an
  Engineer in an editable assessment state; completed/read-only work keeps
  viewing but offers no editing action.
- Report already renders eagerly. It owns the single form-associated image
  value set; include unused candidates when editing so an unused image can
  be selected. Files and viewer target these controls instead of posting
  duplicate indexed payloads. Existing read-only report order/exclusion stays.
- Render one native dialog partial. Adapt only supplied crop geometry:
  drag frame, eight resize handles, quarter rotations, Free/4:3/3:2/1:1
  aspect lock, reset, full frame, live canvas preview. Crop fractions are
  interpreted against the rotated source, clamped and serialized to at most
  seven decimals. Server policy remains authoritative.
- Crop dialog Cancel/Escape discards only its temporary working rectangle.
  Apply changes the canonical page fields and dirty state; it does not call
  a persistence command. Opening another entry sees the same pending values.
  Global Discard releases the existing lease and reloads persisted preparation;
  global Save submits Case facts and pending images together.
- Keep numeric crop/role/order controls and keyboard move controls as the
  script-off route on the canonical Report editor. Files' native link reaches
  that editor. JS enhances both entrypoints without a duplicate form contract.
  Changed fields join existing input/dirty handling; no autosave.
- Use external Case-only JS/CSS and existing tokens. Preserve CSP (no inline
  handlers/styles); frame geometry may use SVG attributes/canvas drawing.
  Do not port prototype global state/history, fake photos or auto-demotion.
  No permanent derived images, new endpoint/package or crop service.
- Narrow site.js change only copies the selected optional crop target into
  the shared viewer control and hands off through existing close/dialog
  behavior. Case-workspace.js owns crop/staging once and registers the
  existing root-scoped idempotent mount binder for lazy Files.

### Lazy lease and refusal authority

Already editing: Apply stages only local associated fields and uses the
held token/original Case and preparation versions at eventual global Save.

Not editing: the dialog posts its proposed edit through existing ClaimLease
with the original expected Case version and operation key. The unchanged Core
claim records a lease, not a Case or image edit; EfCaseWorkflowStore.ClaimAsync
preserves workflow.Version. After it succeeds, render the same Details page
with the submitted edit staged, checking the returned/restored authority is
still valid and the current Case version still equals the submitted version.
No automatic image Save and no forced acquisition.

Use page-local submitted edit values for the immediate native POST response,
not a new TempData/session/database draft. Invalid input, foreign/expired
authority, changed Case/preparation/source or acquisition conflict preserves
the proposal and displays the refusal. A rerender may show current facts but
must retain the original submitted expected Case/preparation versions for the
proposal. Never combine old crop values with freshly loaded hidden versions.
The existing global form may use a page-local original-version override on
this refused response; it must not silently offer a successful fresh-version
retry. Only explicit discard/reload/re-entry establishes a new editing basis.
Existing other-section conflict presentation and authority remain intact.

Apply failure must remain recoverable with the original values and key; no
catch-and-repost loop and no second acquisition hidden behind a failure.
Tests must assert actual posted versions, not only the notice string.

### Reachable UI states

Loading has aria-busy and no Apply until dimensions decode; failed/unsupported
source keeps the native authorized link, shows existing-style error, and
cannot stage a crop. Empty image set omits the editor. Saving disables repeat
submit. Success is staged dirty state until global Save completes.

Dialog has a labelled heading, focus containment/return, Escape/Cancel,
keyboard arrows to move, Shift+arrows to resize and rotation buttons/shortcut.
Resize handles have keyboard equivalents. Responsive stage/preview fit narrow
screens without clipped actions or page-wide overflow; no explanatory panels.
Viewer close and dialog handoff must not leave the page inert.

## Exact edit map and exclusions

The complete authoritative path list is files@`f99fed081e68ece6` (35 paths,
including generated artifacts). No wildcard path authority.
No schema/migrations/grants/bootstrap/package/runtime/cloud/Box/Outlook edits.
No derivative bytes, renderer changes, new approval snapshot, separate
persisted draft or image command generations. No protected operator-notes
change or unrelated gallery/Case redesign.

Execution waits for ENG-029's Details/form/labels/FRD/design/fixture release,
INTK-064's DI release, and root's snapshot/index handoff. Confirm fresh live
claims and exact source before take. DELIV-040 is already Done; historic
ENG-034 host is integrated but its retained claim is not force-released.
Root resolves the stale blocking relationship before take if still present.

## Ordered implementation

1. After root approval/ownership, fresh gates and execution packet; take only
   the recorded ENG-031 branch/worktree from then-current accepted dev.
2. Extend the existing request/global transaction and remove sole-caller
   immediate contracts/DI path; migrate meaningful persistence tests.
3. Rebind Details/global form and native lazy-lease staging; cover authority
   and mixed-save/refusal behavior before building visual enhancements.
4. Add one dialog and Case-only visual/staging enhancement; wire Files/viewer
   and Report to the one canonical value set, retaining script-off controls.
5. Update only mapped governing paragraphs and focused tests. Root owns
   runtime/capture; freeze source and provide exact filters first.
6. Record every root attempt, actual callers and scope, then independent
   review at exact pushed head. Author never self-reviews or merges.

## Focused acceptance and commands

Core: existing CaseAssetPreparationTests and CaseWorkspaceTests; add image
payload authority/absence/invalid-input cases, not a new test harness.

SQL: existing CaseAssetPreparationPersistenceTests migrated to the global
store retain all fourteen existing source/hash/order/reset/stale/replay/
rollback/constraint assertions. CaseWorkspacePersistenceTests adds one mixed
Case+crop save with one version/history/invalidation, forced rollback and
same-key changed-image refusal. Existing report-generation fixture proves
frozen-image immutability after this real global save.

Web: current ten image-preparation methods in partial CaseDetailsWebTests
migrate to global/staged semantics without deleting their meaningful envelope,
order, refusal and shared-value assertions. Add exact no-lease Apply claim,
failed/stale claim keeps original values/version, global save, Discard and
non-Engineer/state refusal. Preserve ImageViewingWebTests.

Browser: one ReportImageCropBrowserTests using existing BrowserTestSupport and
supplied real image bytes. Prove both entrypoints; frame/handles/aspect/
quarter-turn coordinates and live preview; keyboard/focus/Cancel; no image
write before Save; global Discard; edit survives lazy mount and both views
agree. No mock geometry-only PASS.

Root only, cwd the ticket worktree, one platform:
`dotnet restore ./Pegasus.slnx --locked-mode`;
one needed Release build; focused Core filter
`FullyQualifiedName~CaseAssetPreparationTests|FullyQualifiedName~CaseWorkspaceTests`.
Author supplies exact expanded Integration method filter after source freeze;
include only changed owners/browser plus three existing case-details capture
methods, not all CaseDetailsWebTests. Reuse genuine source evidence and prior
unchanged report tests where exact inputs justify it; no repeated broad suites.

Root captures `case-details` using the three existing canonical capture
methods, then runs Update-TestUiSnapshots with -SkipCapture -Scope case-details,
-Verify -SkipCapture -Scope case-details, and Test-UiCatalogue. Additional
browser screenshots cover the real open crop dialog, narrow layout and
nondefault preview; report any visual gap honestly. Generated default,
conflict, unavailable and conditional index only. Final converged release CI
is root-owned, not a duplicate per-ticket loop.

## Stop condition

This phase stops after root reads the whole current plan/map/checklist and
resolves ownership. No source or take now. Implementation later stops frozen
for root verification, then Review at its authorized exact PR head. Failures,
unknown consumers/files, required packages or authority gaps stop for a
bounded disposition; no silent redesign. Ordinary Done needs exact merged
proof, not deployment.

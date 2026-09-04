# Files — CASE-038 (2026-09-02, gpt-5.6-terra xhigh, wrapper-checked)

Wrapper note: the three `docs/design/test-ui/**` rows were added by the
Claude wrapper. Codex handed the regenerated Details snapshots to UIIMP-014;
the repository rule (AGENTS.md, Commands: regenerate and commit
`docs/design/test-ui/` with the page change, CI verifies every change set)
puts them in CASE-038's own PR as a capacity-one lease, matching the
correction made on ENG-034's file map. Every path below was confirmed to
exist with `ls`/`grep` in the main checkout at `cad00be9`.

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | change | Replace exclusive panel routing with sticky identity/action/jump-nav frame; render the first three section hosts, addressed host, Engineer and Sign-off ribbon slots, and lazy placeholders; remove Open Assessment (lines 274–281). | Existing record ribbon, action bar, edit bar, `_CaseWorkflow`, `_CaseSummary`, `_CaseFiles`, `_CaseHistory`, `_CaseVehicle`, `_CaseInspectionAddress` partials as rendered today. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | change | Normalize `?section=` as a jump target over the eleven D30 keys; share authorized projection loading with an `OnGetSection` fragment handler; compose existing assessment data/handlers at the Case endpoint per the ENG-034 contract; retain one lease. | `OnGetAsync`, `CaseMutationPageModel`, `IGetCase`, `IGetAssessmentAccess`, `IGetAssessmentWorkspace`, `IStaffAccountQueries` name resolution. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml` | change | Replace six `?section=` selection links with the eleven D30 jump-nav entries (`#section-<key>` anchors) and scroll-spy hooks. | Existing model binding and accessible nav markup. |
| `src/Pegasus.Web/wwwroot/css/site.css` | change | Add the Case sticky-stack (`case-sticky`, measured `--sticky-h`), horizontal `section-nav`, section `scroll-margin-top`, lazy placeholder, and 1580/1100/760 reflow rules; retire the `top:61px`/`top:51px` side-nav offsets for the Case record. | Existing `.record*`, `.edit-bar`, `.case-workspace`, `.case-context`, and the 1360–760 breakpoint block (lines 752–800). |
| `src/Pegasus.Web/wwwroot/js/site.js` | change | Fetch and mount named lazy fragments, measure sticky height, implement scroll-spy and `?section=` jump, and bind newly mounted Case controls (dirty guard, dialogs, Ctrl+S target) without replacing existing forms. | Existing `fetch`/`FormData` heartbeat (line 803), CASE-007 dirty guard (line 568), keyboard handler (line 1446), dialog behaviour, reduced-motion conventions. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change | Centralize frame-owned ribbon, action and jump-nav labels, including Engineer, Sign-off and the eleven section headings. Capacity-one lease. | Existing `CaseWorkspace` nested class and `CaseStage`. |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | change | Replace exclusive-section (lines 149–161, 1280) and Open-Assessment (line 49) assertions with full-frame, addressed-section, fragment-handler, lease and no-regression assertions. | Existing Case stores, antiforgery helpers, lease tests, section-query cases. |
| `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs` | change | Add a seeded Case-record scenario at 1580/1100/760 with scroll/jump/lazy assertions and the existing overflow/clip checks (the generic route list has no `/Cases/{id}`). | `BrowserTestSupport`, the three-width theory, `AllowedClipSelector`, the seeded-case pattern in `OperatorJourneyTests`. |
| `docs/design/test-ui/pages/case-details--default.html` | change (regenerate) | The routed page's markup changes; `Update-TestUiSnapshots.ps1 -Verify` and CI must stay green on CASE-038's PR. Capacity-one lease. | Snapshot tooling (UIIMP-005/UIIMP-013). |
| `docs/design/test-ui/pages/case-details--conflict.html` | change (regenerate) | Same reason; the conflict state renders the frame. `unavailable` is expected byte-identical. | Snapshot tooling. |
| `docs/design/test-ui/catalogue.json` | change (only if needed) | Update the `default`/`conflict` branch descriptions for `Pages/Cases/Details.cshtml` (lines 322–346) if their wording ("section nav", side nav) no longer describes the frame; no new states — those are UIIMP-014's. | Existing `visual` entry. |
| `docs/design/README.md` | change | Add the five component-vocabulary rows (`section-nav`, `case-sticky`, `suggest-btn`, `derived`, `outcome-option`) to the class vocabulary table (line ~754) only after DELIV-041 merges and clears the governing-doc lock. | Existing Record component rows (line 800). |

## Must not touch (another EPIC-012 lane owns them)

- `src/Pegasus.Web/Pages/Cases/Assessment/**`, Engineer partials
  `_CaseDamage.cshtml`, `_CaseEstimate.cshtml`, `_CaseSettlement.cshtml`,
  `_CaseReport.cshtml`, and Assessment-specific tests — ENG-034.
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseEngineerNotes.cshtml`, Engineer
  notes Core/persistence/migration files — CASE-039.
- Sign-off Case data, `Pages/Cases/Eva/Send.*`, and EVA dialog behaviour —
  CASE-040 (the frame renders only the label and a no-value slot).
- `_CaseInspectionAddress.cshtml`, inspection resolution ports, and
  storage-location data — CASE-041 (report the duplicate `case-edit-form`
  id finding to that lane; do not edit the partial).
- `_CaseVehicle.cshtml`, new `_CaseValuation.cshtml`, `Vehicle.*`,
  `Custody.*`, and upload-request policy — CASE-029.
- `src/Pegasus.Web/Pages/Cases/Index.*` — CASE-042.
- `src/Pegasus.Core/Assessment/**`,
  `src/Pegasus.Infrastructure/Persistence/**`, and
  `src/Pegasus.Infrastructure/Persistence/Migrations/**` — ENG-035,
  CASE-029, and the serialized migration lane.
- Damage-map, Settlement/Report editor, report-image, fee-note,
  account-setting, and Operations files — ENG-036, ENG-029, ENG-031,
  DOCS-018, PLAT-068, PLAT-069, and DOCS-017.
- `docs/design/test-ui/**` beyond the three rows above (new per-section
  edit/read-only Case snapshot states and their catalogue entries) —
  UIIMP-014.
- Governing-decision text in `docs/design/README.md` and the other
  governing docs while DELIV-041 holds the lock; CASE-038 adds only its
  component rows after that lane merges. `docs/operator-notes.md` is
  protected and is not touched.

## Plan-stage corrections (wrapper, 2026-09-02, at `897db953`)

- The `docs/design/README.md` row above is withdrawn: DELIV-041 (#647,
  merged) already added the five vocabulary rows at lines 808–816. CASE-038
  makes no governing-doc edit.
- The `_CaseInspectionAddress.cshtml` "do not edit" line is narrowed by the
  plan: CASE-038 renames that partial's form (`id="case-edit-form"
  data-edit-save` → `id="case-inspection-address-form"`, no
  `data-edit-save`, comment updated; lines 71–76 only) as a declared
  `Pages/Cases/Shared/*` lock exception, because CASE-038 blocks CASE-041
  and the duplicate id is a frame invariant. Nothing else in that file.
- Six test files carrying the deleted `case-files` / `inspection-address`
  keys are retargeted as one mechanical step (plan Step 5, file:line list).

## Plan-stage corrections, second pass (wrapper, 2026-09-02, at `897db953`)

- The "Must not touch" line naming `_CaseDamage.cshtml`, `_CaseEstimate.cshtml`,
  `_CaseSettlement.cshtml` and `_CaseReport.cshtml` as ENG-034's is narrowed
  by the plan: CASE-038 **creates** the four files as heading-only shells
  (`@model Pegasus.Web.Pages.Cases.DetailsModel`, one `OperatorLabels`
  heading, nothing else) so its `<partial>` composition renders green before
  ENG-034 replaces their content (ENG-034 contract item 6). ENG-034 owns
  their content from its first PR onward; `Pages/Cases/Assessment/**` and
  the Assessment tests stay ENG-034's.
- The `Details.cshtml.cs` row's "compose existing assessment data/handlers at
  the Case endpoint" and its `IGetAssessmentWorkspace` reuse are withdrawn:
  the plan settles the handler host as option B, so CASE-038 swaps
  `CanOpenAssessment` for `AssessmentIsReadOnly` on the existing
  `IGetAssessmentAccess` load and adds `OnGetSectionAsync`; the assessment
  projection, estimates/editor state and the nine Assessment handlers arrive
  with ENG-034's forms.

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml` | create (heading-only shell) | Composed by `Details.cshtml` as the `section-damage` body; ENG-034 fills it. | `_CaseVehicle.cshtml` `@model DetailsModel` partial convention; `OperatorLabels.CaseWorkspace` heading. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml` | create (heading-only shell) | As above, `section-estimate`. | As above. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseSettlement.cshtml` | create (heading-only shell) | As above, `section-settlement`. | As above. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml` | create (heading-only shell) | As above, `section-report`. | As above. |

## Review-round amendment (Claude, 2026-09-04)

Round-1 review (finding 2, SHOULD-FIX) noted two files already in the diff
but missing from this map. Both are accepted on their merits — recorded
here, not reverted:

| Path | Action | Why |
| --- | --- | --- |
| `src/Pegasus.Web/Program.cs` | change (+28) | Fixes round-1 finding 1 at its cause: a matching-only route selector (`SuppressLinkGeneration = true`) so `/Cases/{id}` and every `?handler=` link generate exactly as before. |
| `docs/design/test-ui/index.html` | change (regenerate) | The Test UI harness's own catalogue-text regeneration for pages this ticket already owns; not a new page or a new governing decision. |

## Review round fixes (2026-09-04)

Round-2 review, finding 1 (BLOCKER): the round-1 finding-3 fix bound the
CASE-007 dirty guard as `form.addEventListener('input', ...)` on each
lease-carrying form's own subtree. `_CaseInspectionAddress.cshtml`'s
`#inspection-address` control renders outside `#case-edit-form`'s DOM
subtree (associated only via `form="case-edit-form"`); a native `input`
event bubbles the DOM tree, not the `form=` association, so typing only
the address left `dirtyForm` null and Finish editing released the lease
with no confirmation — silent data loss, newly introduced by the round-1
fix.

Fixed in `src/Pegasus.Web/wwwroot/js/site.js`: one delegated
`document`-level `input` listener resolves the owning form via the
control's `form` IDL property (which does honour `form=`), replacing the
per-form listener. Added
`InspectionAddressOutsideEditFormIsGuardedAndSaved` to
`tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs`: types
into the address control, confirms the Finish-editing dialog now appears,
saves, and asserts the typed address is what `SaveCase` persisted (a case
no existing test covered — the CaseDetailsWebTests.cs POST tests build
form data directly and never exercise the browser's `form=` association).

Finding 2 (SHOULD-FIX, record-only) is addressed by the amendment above.

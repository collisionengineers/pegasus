# Plan — CASE-025

Goal: `/Cases` as the §1.4 three-pane queue (rail groups + filters + per
kind rows + quick detail), on the PLAT-029 design vocabulary, D3 display
groupings via OperatorLabels, D14 blocked-rows-uncounted. Deliverable:
branch `task/case-025-cases-queues`, PR to `dev`. Subagent lane rules:
build only (no test runs, no snapshot scripts); STOP at the open PR.

## Steps

1. **Merge origin/dev** into the recovered branch (done — clean; only
   `Migrations/*` from TICK-061, no conflicts). STOP condition for this
   lane was not triggered.
2. **Repair the page model** (`Pages/Cases/Index.cshtml.cs`, base
   95f69958 kept):
   - Missing filter → exclusive semantics (prototype final layer): case
     rows Instructions = `!InstructionComplete && ImagesComplete`,
     Images = converse, Both = both incomplete; image-initiated rows are
     instruction-missing with images present (All + Instructions only,
     never under a named Principal). Reuses the completeness projection
     added in 95f69958 — no new rule.
   - Image rows: excerpt = "N retained images" via the existing
     `IImageIntakeQueries.ListImagesAsync` per image row (bounded
     awaiting-instruction set; no custody state exists in Core — see
     out-of-scope); right-hand Time = null; chase state stays a
     quick-detail fact (TICK-065 wording).
   - Quick detail: dropped the " · " string surgery — each non-case kind
     builds its definition-list facts from the source item at row-build
     time (`Facts` on the row record); the detail loader keeps only the
     Case's own `IGetCase` read (and the engineer-name resolution).
     Triage assignees resolve in one batch per page, so rows and detail
     show the name, not "Assigned".
   - `Href` retains filters per *target* tab (principal on case queues,
     missing on not_ready only).
   - Removed the invisible `sort` parameter and its plumbing; rows
     render newest-received-first, ThenBy title (the §1.4 design draws
     no sort control on Cases).
3. **Write the page** (`Pages/Cases/Index.cshtml`):
   - Header `page-header`/`page-title` h1 "Cases" + `_FreshnessBanner`
     (RefreshFields: tab, selected, principal, missing, page).
   - Filter bar (`filter-bar`, GET form, `data-auto-submit`): Principal
     select (case queues — a select that filters nothing is never
     drawn; FRD-12's "every queue" reads against rows that carry a
     Principal, which Triage/Unidentified rows do not), Missing select
     (not_ready only: All / Instructions / Images / Both missing),
     `noscript` Apply, Clear link rendered only while a filter is active
     (an inert Clear is an inert control).
   - `pane-layout pane-layout--3 queue-layout`: rail `pane`
     ("Case workflow", `scope-list`): `queue-group-label` Workflow /
     Pre-Case work / Exceptions; `scope-button` per tab with
     `scope-visual-icon` well, label, queried count; `queue-exception`
     on Held/Unidentified; `queue-group-divider` between groups. The
     pressed state is `aria-pressed` — the design system's own selected
     selector.
   - Middle pane: pane-head = scope label + "N items"; rows are
     full-row `row-button` links to the record's detail ("a row links
     to its detail and nothing else", FRD-12) with `aria-selected` on
     the `?selected=` row; per-kind lines per §1.4; `pagination` when
     the scope pages. No empty-state panel (read-only view economy).
   - Right pane "Quick detail": case → eyebrow origin, h2,
     `workflow-stepper--compact` (+ `workflow-exception` when Held),
     `blocker-list` Outstanding requirements (CaseRequirements), Current
     work `decision-row`s (Due / Engineer / Next action) + Open full
     Case (`btn--dark`); other kinds → `definition-list` + open button.
4. **Tests** (`TriageQueuesWebTests.cs`): rewritten to the new contract
   (exclusive Missing semantics across all four options; rail scope
   count equals merged rows across both origins + Dashboard tile
   agreement, INTK-013; image row file count + "Not yet due";
   unidentified tab vocabulary/GUID bans; D14 blocked-rows-uncounted;
   merged row list with rail-not-tabs; newest-first order). Not run
   here (lane rule) — the orchestrator's wave loop runs them.
5. **Build** `dotnet restore --locked-mode` + `dotnet build -c Release`
   — green (compiler feedback tier only).
6. **Simplification pass** — below.
7. **PR** to `dev`; post-implementation report; stop at the open PR.

## Reuse named

- Design vocabulary + behaviours: PLAT-029 `site.css` classes and
  `site.js` (`data-auto-submit`, roving row focus, `data-refresh-form`).
- Counts: `EfDashboardQueries.GetCaseStageCountsAsync` (+Complete),
  `IListTriage`, `IUnidentifiedStore.ListQueueAsync`, `IListIntake`
  BlockedIntake — all pre-existing queries; no second count rule.
- Labels: `OperatorLabels` (CaseStage/TriageState/Unidentified*/
  EmailHandle/IntakeFailure/ImageChaseState/SourceChannel/Office*),
  `_StatusChip`, `_FreshnessBanner`.
- Rows: `SearchCases` (per state + principal), `IImageIntakeQueries`
  (ListAsync/ListImagesAsync), `IGetCase` for the selected quick detail,
  `ActorDisplayNames.ResolveStaffNamesAsync` for names.
- Test fixtures: `StoreMinimalReceiptAsync` / `SeedNotReadyCaseAsync`
  (completeness flags added as parameters), new
  `RegisterImageIntakeAsync` helper deduplicating three tests' setup.

## Simplification pass (2026-08-28, whole branch diff incl. kept 95f69958)

Lenses: reuse / simplification / efficiency / altitude.

- **Applied** — nonsense always-empty expression on the image row
  (95f69958) replaced by the real "N retained images" line; " · "
  string-surgery detail loader (95f69958) replaced by row-built facts;
  inclusive Missing filter (95f69958) corrected to the contract's
  exclusive options; per-current-queue filter retention in `Href`
  (95f69958) decided per target tab; invisible `sort` param and its
  `SortToggleHref`/`OldestFirst`/`ParseOrder` plumbing removed (dead
  agent's leftovers of the old sortable columns); `AssigneeNameAsync`
  inlined (single caller); `BlockedRow` builds the handle once and a
  plain fact list instead of collection-expression spreads; triage
  assignee names batch-resolved per page (was going to be one read per
  selected row); compile-forced one-liners in `Triage/Details.cshtml.cs`
  and `Intake/Details.cshtml` delegate to `OperatorLabels.TriageState`
  (the label's one home).
- **Accepted as-is** — the open-button markup appears twice in the
  cshtml (case pane button sits inside the Current work panel; record
  kinds' beside the definition list): a shared partial for two
  differently-placed buttons is more machinery than the duplication
  costs. Global newest-first re-sort of merged rows keeps one ordering
  path for all five scopes. Strict `?selected=` (unknown id → 404)
  keeps the dead agent's deterministic semantics; a silent fallback
  would hide bad links.
- **Rejected** — rendering a Principal select on Triage/Unidentified
  scopes ("every queue" read literally): it would filter nothing, and
  an inert control is the sharper violation. Sorting parameters kept
  server-side "just in case": speculative.

## Out of scope (reported, not done)

- Image-intake custody state on the row/quick detail — no persisted
  projection exists; needs a Core projection (wave-3 counts/store
  territory), not a page-port invention.
- Triage "provider" on rows — `TriageSummary` carries no provider; a
  Core projection change would duplicate lane C2's detail work.
- Sort control on /Cases — not in the §1.4 contract (Inbox §1.3 owns
  the Received toggle); server keeps newest-first only.

## Acceptance

- Every rail count is a queried figure; Unidentified count excludes
  Blocked intake rows (they render uncounted with their own chip).
- D3 groupings only via OperatorLabels; no second label map in markup.
- No new CSS file, no inline styles, no explanatory copy.
- Deep links `/Cases?tab=not-ready|review|held|unidentified` select the
  right rail scope (hyphen normalisation); Work Centre Blocked metric
  lands on the Unidentified scope per D14.
- Solution builds Release green (compiler feedback tier; the wave loop
  owns test execution and snapshot regen).

## Review round 1 — REQUEST CHANGES dispositions (2026-08-28)

1. **aria-allowed-attr (blocking) — fixed in markup, no CSS change.**
   - Rail scope entries are now real `<button type="submit" name="tab">`
     inside one GET form (`class="scope-list"`), keeping `aria-pressed`
     — legal on buttons, the design system's own pressed selector, and
     the prototype's own element shape. The rail's selected visual is
     fully preserved (base, queue-layout and forced-colours rules all
     still match). A native submit opens the scope with no script; the
     active Principal filter rides a hidden input so it survives rail
     switches (the prototype keeps it in client state across tabs); the
     Missing filter resets when leaving Not ready, exactly as the
     per-target `Href` behaviour did.
   - Rows stay full-row anchors to their detail (FRD-12) and the
     selected row now carries `aria-current="true"` — the one
     current-state token a link may legally carry, and the codebase's
     link convention (Mail scopes, Assessment tabs). Consequence,
     reported rather than worked around: the `.row-button[aria-selected]`
     background/inset-bar highlight cannot legally apply to a link (or a
     button — `aria-selected` is illegal on both), so PLAT-029's CSS
     keys an unreachable pattern for link rows; a
     `.row-button[aria-current="true"]` selector (or class variant) is a
     PLAT-029/wave-5 follow-up, not this lane's file. Selection is still
     communicated: `aria-current` announces it, the quick-detail heading
     names the row, and focus shows the roving outline.
   - `data-row-list` wired onto the rail form and the rows pane, so the
     shipped ArrowUp/Down module now moves through both (§1.1 keyboard
     contract).
2. **Principal options from the unfiltered load (blocking) — fixed.**
   `LoadNotReadyAsync` now derives the select's options from the
   Missing-filtered rows plus the active principal
   (`PrincipalOptions`); decision and rationale recorded in research.
   Chose the product fix over narrowing the test: a select listing
   principals whose rows the filter removed is a menu of empty results.
3. **Hidden `selected` GUID tripping the vocabulary test (blocking) —
   test fixed.** The strip regex now removes hidden `<input>` elements
   (routing state, never operator-visible text) alongside href values;
   the GUID ban on visible text is untouched. Production kept carrying
   `selected` through the freshness form — refreshing preserving the
   selected row is the intended behaviour.
4. **Hard-coded Held badge (nit) — fixed.** The workflow-exception badge
   now reads `OperatorLabels.CaseStage(CaseLifecycleState.Held)`.

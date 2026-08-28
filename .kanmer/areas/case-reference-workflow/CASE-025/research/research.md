# Research — CASE-025 (recovery audit + contract verification)

Predecessor state: branch `task/case-025-cases-queues` at `95f69958`
("feat(cases): port queues work-centre state") — a previous agent died
after the page-model half. This document audits that commit and records
the read-only checks behind the plan.

## Audit of 95f69958 (verdicts)

Kept (sound, verified against Core contracts):

- `CaseSearchItem.InstructionComplete/ImagesComplete` (Core) + EF
  projection — the Missing filter reads recorded completeness facts, no
  second business rule. Columns exist (`Cases.InstructionComplete`,
  `ImagesComplete`); EF row shape extended to match.
- `CaseStageCounts.Complete` (5th param, default 0 → existing callers
  compile) + `EfDashboardQueries` counting `PostReportComplete` — D3's
  one rail-listed terminal. Wave-3 CASE-028 (rail/stage counts) has NOT
  merged to `origin/dev` (checked `git log` — only PLAT-048, TICK-061
  since branch point), so adding it here is what the ticket body directs.
- `OperatorLabels.TriageState` (moved off the page — one label map) and
  `CaseRequirements` builder; `Pages/Intake/Details.cshtml` one-line
  fixup was compile-forced by that move (lane C2's file; noted in PR).
- Page-model skeleton: `Tabs` rail with groups/icons, hyphen-normalising
  `Queue`, `?queue=` alias, search-param 301 to /Search (pre-PLAT-029
  behaviour preserved), merged with_engineer two-state load,
  unidentified + uncounted blocked rows (D14), quick-detail flow,
  `?selected=` deep-link state.

To repair (found in audit):

1. Image row Detail line was a nonsense expression always yielding
   `string.Empty` (`SourceChannel(...) is var _ ? "" : ""`); §1.4/FRD-12
   require "file count and custody". Verified: `ImageIntakeSummary`
   carries no file count; `IImageIntakeQueries.ListImagesAsync` (real
   EF implementation in `EfImageIntakeStore.cs:765`) is the existing
   projection. No persisted custody state exists for image-intake rows
   (`ImageIntakeImage` = ReceiptId/FileName/MediaType; the image record
   page renders a gallery with no custody chip) — row shows the file
   count only; custody needs a new Core projection (out of scope,
   reported).
2. Missing filter semantics inclusive (`instructions` = missing
   instructions regardless of images). The prototype's final layer
   (renderQueues/currentQueueItems, lines 1583/1761) is exclusive:
   Instructions = instruction-missing AND images-present; Images = the
   converse; Both missing = both absent; image-initiated rows count as
   instruction-missing with images present. FRD-12 lists the same four
   options and does not override. Repair to exclusive.
3. `LoadRecordDetailAsync` recovered facts by string surgery on the
   rendered " · " joins (`row.Meta[(IndexOf(" · ")+3)..]` — garbage when
   the separator is absent). Repair: kinds build their definition-list
   facts where the source item is in hand (row build); detail loader
   keeps only the queries the selected row genuinely needs (Case via
   `IGetCase`, image file count, triage assignee name).
4. `Href(tab:)` decided filter retention from the *current* queue
   (`ShowingCases`/`ShowingNotReady`), so switching not_ready→review
   carried the meaningless `missing` param. Repair: retention decided by
   the target tab.
5. Invisible `sort` param (received_asc) with no rendered control in
   the §1.4 contract (the sort toggle belongs to Inbox §1.3). Remove;
   rows render newest-received-first (ThenBy title).
6. Image row right-hand `Time` showed the chase state; the §1.4 image
   row has no due slot — chase stays as a quick-detail fact (TICK-065
   wording preserved via `OperatorLabels.ImageChaseState`).

## Contract checks (read-only, verified)

- Rail group order from EPIC-011 `context.md` §1.4 (binding): Workflow
  Not ready/Review/With Engineer/Complete · Pre-Case work Triage ·
  Exceptions (amber) Held/Unidentified. The prototype's own rail puts
  Triage inside Workflow; context.md §1 is the transcription that wins.
- Icons exist in the PLAT-029 sprite: clock, check-circle, user, check,
  file-text, pause, alert-triangle (checked `_LucideSprite.cshtml`).
- CSS vocabulary all present: `queue-layout`, `queue-group-label`,
  `queue-group-divider`, `scope-list`, `scope-button`,
  `scope-visual-icon`, `queue-exception`, `row-button`, `row-top`,
  `row-title`, `row-excerpt`, `row-meta`, `pane-layout--3`, `pane-head`,
  `pane-body`, `pane-scroll`, `detail-canvas`, `definition-list`,
  `definition`, `workflow-stepper--compact`, `workflow-exception`,
  `blocker-list`, `blocker`, `filter-bar`, `field`, `btn`, `pagination`,
  `empty`, `decision-row`, `section-label`.
- Routes: `/Cases` page; details at `/Cases/{id}`, `/Triage/{id}`,
  `/Unidentified/{id}`, `/Received/{id}` (Intake/Details),
  `/VehicleImages/{id}` (ImageIntake/Details) — all confirmed via
  `@page` directives.
- `TriageSummary` carries no provider/ref (registration, state,
  assignee, linked case, created) → triage rows honestly show
  registration · state · assignee · opened; no provider fabrication.
- `_StatusChip` already tones every label this page renders, including
  "Blocked intake" (red) and "Not yet due" (neutral).
- Rail nav count (§1.1) excludes Complete and stays in
  `RailCountsPageFilter` (PLAT-029's file) — untouched here.
- site.js: `data-auto-submit` forms, `.row-button/.scope-button`
  ArrowUp/Down roving focus, `data-refresh-form` freshness refresh all
  exist; the `data-select-href` preview module needs per-row templates
  (per-row `IGetCase` — too heavy here; not used).
- `SearchCasesQuery.PageSize` max 100 → `MergedPageSize = 100` is legal.
- origin/dev merge: only `Migrations/*` overlap (TICK-061); merged clean.

## Assumptions (not machine-checked)

- The Work Centre lane (UIIMP-008) will emit `/Cases?tab=not-ready`-style
  hyphenated deep links per §1.2/D14; hyphen normalisation covers them.
- A "blocked" tab value never arrives (D14 routes Blocked to
  `tab=unidentified`); unknown tabs 404 as before.

## Review round 1 additions (2026-08-28)

**Principal select options (finding 2) — decision.** The options are now
built from the rows the filters still show, plus the active principal
when no shown row carries it (`PrincipalOptions`). Reason: a select
offering principals whose every case the Missing filter removed is a
menu of empty results — the honest control lists what choosing it would
show, and keeps the operator's active choice visible so it can be
changed or cleared. The failing test's row-scoped assertions therefore
hold for the select too, with its intent (row filtering) unweakened.
Known limitation, recorded here once: the options are always a sample
of the loaded page (page-1 rows for paged scopes, the first
`MergedPageSize` rows for Not ready), not a census of the queue — the
pre-existing convention for this control.

**Deferred row fields.** Image-initiated custody, the Triage reference
and the Triage provider (§1.4/FRD-12 row shapes) have no Core
projection today (`ImageIntakeSummary` carries no custody;`
TriageSummary` carries no provider or reference beyond the
registration). They fall to lane C2 / INTK-046's detail-and-projection
work, not to this page-port lane.

**Aria legality (finding 1) — what the CSS actually keys on.** Verified
in `site.css`: `.scope-button[aria-pressed="true"]` (base + queue-layout
+ forced-colours) and `.row-button[aria-selected="true"]` (base +
queue-layout). There is no `[aria-current]` variant for either — the
design system's dual selector pattern exists only for `.tab` (line:
`.tab[aria-selected="true"],.tab[aria-current="page"]`). Codebase
precedent for current-state on links: Mail scopes and Assessment tabs
use `aria-current="page"` anchors; `aria-pressed`/`aria-selected` appear
only on real buttons (`damage-region`, command-palette `role="option"`).

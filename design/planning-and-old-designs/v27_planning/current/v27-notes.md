# Case record v27 — the baseline of the live page

Built 16 September 2026 from `origin/dev` at `5765a527a`. This version proposes
nothing: it is the live Case record reproduced as an offline mockup so that the
v27 round can be planned against a faithful picture of what exists. The
operator's reference file for the round is
[`../pegasus_case_dashboard_2026-09-15.html`](../pegasus_case_dashboard_2026-09-15.html);
its inventory and its differences from the live page are in
[`../pegasus_case_dashboard_2026-09-15-features.md`](../pegasus_case_dashboard_2026-09-15-features.md)
and [`../pegasus_case_dashboard_2026-09-15-differences.md`](../pegasus_case_dashboard_2026-09-15-differences.md).

## 1. What this version carries that v26 did not

The v26 mockup was the proposal; Stage 2 implemented it and the live page has
moved on since (PRs 749–758, Releases 52–53). The baseline reproduces the live
page, so against `pegasus_case_workspace_v26.html` it differs where live does:

| v26 mockup | Live page, and this baseline | Shots |
| --- | --- | --- |
| Take over on a colleague's lease (proposal R) | No control at all while a colleague holds the lease; the chip reads "E Mawdsley is editing" and every section head states it | 09 |
| Case tasks sub-panel on Notes (proposal) | No tasks panel; Add Case note and Record chase only | 34, 35 |
| Three damage clickers (Plan, Elevations, Dial) | Plan only, per zone severity and note, three chips for Underside / Interior / Mechanical | 14, 15 |
| Tabs default with a strip variable | Scroll default; the Scroll / Tabs switch on the section row | 37 |
| Valuation buttons per source and an Add valuation dialog | Glass's, Brego and Super CAP are entry cards with month, mileage, retail, trade, Get valuation and Save; no Add valuation; the AI market research button beside the Valuation month | 16, 17 |
| Files: Documents and Images tabs | A third Correspondence tab with Compose and the query e-mails table | 32 |
| Mockup-only Flag and Delete on mail | Not present anywhere on the record (ADR-0052) | — |

## 2. Live rules the mockup mirrors

Each rule is one line in `v27-build/case-record.js` (`computeFlags`) and is
asserted by the self-check.

| Rule | Source |
| --- | --- |
| The Case is editing while this browser carries a lease token and the Case is not archived | `Details.Frame.cs` `IsEditing` |
| A colleague's live lease renders no control; the ribbon chip and every section head name the holder | `Details.cshtml` (`ColleagueIsEditing`), `SectionAvailability` |
| Edit Case reads "Enable return" in Completed and Query | `Details.cshtml`, `IsPostReportReadOnly` |
| Case data edits while editing and not post-report; engineering edits only With Engineer and only for an Engineer or Administrator | `CanEditCaseData`, `CanEditEngineering`, `CanEditAssessmentField` |
| While editing, a section that cannot edit is locked and states one availability sentence: "Available With Engineer" or "Return the Case to the Engineer to edit" | `SectionAvailability`, `.is-locked` |
| Section heads carry Edit only outside a session, on an editable Case, never on Files or Notes | `SectionOffersEdit` |
| The Actions menu: Hand to Engineer (Review, editing), Send to EVA (Review or With Engineer, no lease needed), Mark report sent (Report preparation, editing, detected evidence), Mark completed (Post report, editing), Return to Review, Return to Engineer (Completed or Query), Archive case (closed, editing), Place on Hold / Release Hold, Create upload link, Correct principal, Create audit, and Close case in red after a separator | `Details.cshtml` `canHandToEngineer` … `offersAdverseClosure` |
| The menu appears outside a session only when Send to EVA is available | `offersActionsMenu` comment in `Details.cshtml` |
| Create audit: Inspection + Audit, no Audit yet, a report generated, editing | `Details.Frame.cs` `CanCreateAudit` |
| Next action: None when closed or archived; Hand to Engineer in Review; the first outstanding requirement in Not ready or Held; the first readiness reason in With Engineer; Generate report; Mark completed once Sent evidence exists; Send prepared report; else Prepare delivery | `Details.Frame.cs` `NextAction` |
| Figures: outcome chip (red for total loss, blue otherwise, "Total loss · Cat N"), legal chip (amber unroadworthy), repair cost inc VAT, Engineer's Value, repair cost of value | `Details.cshtml` aside |
| State chip: amber Not ready / Held, navy Review / With Engineer / Query, green Completed, neutral Closed; "Held · review on 24 Sep" | `_StatusChip.cshtml`, `StateChipText` |
| Stepper: Report preparation and Post report both read With Engineer; Completed and Query read Complete | `_CaseOverview.cshtml` |
| Lifecycle actions sub-panel: Return to Review, Unlink report evidence, Archive case, and the Report sent line | `_CaseOverview.cshtml` |
| Vehicle: Look up DVLA & MOT only while editing and only with a registration; Experian is a gated seam; the lookup line reads "Looked up … · Current", "Not yet looked up" or "Lookup failed · …" | `_CaseVehicle.cshtml`, `VehicleLabels.LookupLine` |
| Damage: 19 panels, 4 wheels, 3 chips; location and severity derived (one headline, else "Multiple · …", highest rank) | `_CaseDamage.cshtml`, `DamagePlanGeometry`, `AssessmentVocabulary.DamageZones` |
| Valuation: entry cards only while editing; recorded cards, the applied calculation and its increases in read; Cazana is a gated seam; Apply is the only route to Engineer's Value; commercial VAT blocked when the claimant is VAT registered | `_CaseValuation.cshtml`, `ValuationCalculations` |
| Estimate head: Import (opens the session first from read), one Glass's / Resume slot, Send to AI, More (New estimate, Compare), Expand | `_CaseEstimate.cshtml` |
| Estimate: the Draft edits in place with the header grid; an accepted or Current version reads as one summary line and the grid; Use estimate / Duplicate / Discard for the selected version; discounts and VAT categories with Overridden and Reset to repairer status | `_CaseEstimate.cshtml`, `RenderBottom` |
| Glass's session line: Open (Close session behind a guarded disclosure), Waiting (Complete import while editing, "Available while editing" otherwise), Failed with its code, held elsewhere with Open on | `_CaseEstimate.cshtml`, `GlassLabels.StateLabel` |
| Retained estimate sources sub-panel when an import's custody is still pending | `PendingEstimateSources` |
| Settlement: figures strip (TL: Engineer's Value − Salvage = Equity, repair cost with "Exceeds Engineer's Value"; else labour hours and repair cost of value), the Decisions strip, the Proposed column only when a proposal exists, Accept / Accept all only while editing, salvage rows only for Total loss, the reason only when Unroadworthy | `_CaseSettlement.cshtml` |
| Report: Generate report primary or the "Report not ready" gate; More (Preview draft, Generate fee note, Include fee note); preview card; generation facts; stale notice and the record's stale bar; Reviewed recipients → Prepare delivery → Send prepared report; content switches; Images in report; Report image preparation while editing | `_CaseReport.cshtml`, `CaseReportGenerationState` |
| Files: custody chip (Box · confirmed / preparing / unavailable), Add evidence, More (Open in Box only when confirmed, Open Operations, Create upload request while editing); Documents rows with View, Save as, Remove; Images tiles with tag chips, Tag picker and Crop while editing, placeholder tiles for unreadable custody, intake groups; Correspondence with Compose; Public upload requests with Withdraw link | `_CaseFiles.cshtml`, `_CaseDocuments.cshtml`, `_CaseImages.cshtml`, `_CaseCorrespondence.cshtml` |
| Notes: Add Case note without a lease (not post-report, not archived); Record chase while editing and a chase is scheduled; the merged timeline with system and note rows | `_CaseHistory.cshtml` |
| Viewer: Rotate, Zoom, Download, Crop and In report only while editing; crop tools on the stage; filmstrip with excluded images greyed | `_CaseViewer.cshtml` |
| The finish-edit confirm when Cancel is pressed with unsaved changes | `_EditFinishConfirm.cshtml` |
| The shell: rail with counts and Administration for Administrators only, collapse, utility bar with search and the bell, the working-set tab, the account, notifications and command dialogs | `_Layout.cshtml`, `_ShellDialogs.cshtml` |

## 3. Frame rules, measured

Measured in the built file at 1580×1000 over the DevTools protocol:

- ribbon 56px, section row 40px, `--sticky-h` 97px (set from the sticky block's
  real height as `site.js` does);
- aside 285px at 1441px and above; at 1440 and below it folds into a two-column
  strip above the sections, one column at 760;
- 13.5px cell text and 36px controls from the live tokens (`--control`);
- body font `"Inter Variable"` resolved from the embedded face, so the type is
  the live type, not a fallback.

## 4. Decisions taken in the build, and their authority

None is a design decision. Each keeps the baseline honest:

- Inline the live `site.css` and `case-workspace.css` verbatim instead of
  distilling them, so no rule is lost in translation
  ([mockup-build reference](../../../../.claude/skills/razor-html-mockup-creation/references/mockup-build.md):
  "the live shell with the proposal inside it").
- Transcribe the Razor output by hand for one fixture rather than capturing a
  running instance: no seeded development database exists and the integration
  fixtures are sparse; the coverage audit (§ 6) checks the transcription.
- Keep the read value and the edit control in every cell, as the live markup
  does, and switch modes with `.is-editing` and `.is-locked` exactly as
  `site.css` does, so entering edit never moves the page.
- Every posted form is intercepted and either changes the mock state the way
  the live handler would (claim lease, hold, close, hand-off, generate,
  prepare delivery, create audit, correct principal, add note) or toasts the
  handler name; nothing leaves the page.
- Fixtures are synthetic: MA59BDY / QDOS26214, Ms L Carter, QDOS, A Patterson,
  a total loss with two estimate versions; the six images are generated scenes
  labelled as such.

## 5. Deliberate departures from live

- The working set shows exactly one tab (this record); the live strip is
  rendered per browser by `site.js` and can hold up to six plus a menu.
- The rail counts (Inbox 4, Cases 12), the bell (1 unread) and the clock are
  fixed figures.
- Reply-chain mail, the Compose page, the Upload page, Operations, Box and the
  Audit case are links to nowhere: the shell's other routes are not in this
  file (sign-off item C).
- The image viewer's crop tools open and close but do not draw a crop
  rectangle; the live stage does.
- The Glass's launch does not open a window; the strip sets the session state
  instead.
- Send to AI, Get valuation, Complete import, Discard, Import, Duplicate, Use
  estimate, Save estimate and the reason dialogs toast their handler; only the
  state changes named in § 4 are simulated.

## 6. Coverage audit

`v27-build/audit.py` extracts every label constant and literal the live
`Details.cshtml`, the `_Case*.cshtml` partials, `_CaseDialogs`, `_EvaHandoff`,
`_Layout`, `_ShellDialogs`, `_ReasonDialog` and `_EditFinishConfirm` render,
and checks each against the built file. 16 September: 494 references, 18 not
present, all accounted for:

- 13 are constants from other pages that share a name with one this page uses
  (`Principal code`, `Create principal`, `Draft reply with AI`, `Approved
  mailboxes and mail categories`, `Printed name`, `Save AI settings`, `Save
  credential`, `Provider API`, `Post-report`, `Glass's repair estimate
  credential`, `Version`, `Part £`, `Report position`) and are not rendered by
  the Case record;
- `Materials discount`, `Specialist discount`, `Overall discount` render only
  when that discount is non-zero; the fixture carries a parts discount, which
  proves the row;
- `No notifications` and `Notifications unavailable.` are the bell's empty and
  failed states; the fixture has two notifications.

The audit also drove seven additions before the count settled: the stale-save
conflict table, the Case unavailable page, the one-time upload secret, the
claimant-VAT-registered block on commercial VAT, the EVA policy variants, the
intake photograph group and the retained estimate sources sub-panel.

## 7. Sign-off before v27 proposals are drawn

Each item is "Confirm, or …". Nothing below changes an FRD.

- **A. Fidelity.** Confirm the baseline stands in for the live Case record
  for the v27 round, or name any surface that reads wrong against production
  and it will be corrected before proposals are drawn.
- **B. Fixture.** Confirm the MA59BDY / QDOS26214 total-loss fixture (the v24
  case) carries forward, or name the Case shape v27 should be drawn on
  (repairable, Review-stage, image-initiated, Audit).
- **C. Shell scope.** The baseline covers the Case record only. Confirm v27 is
  a Case record round, or name the other routes (Work Centre, Cases, Inbox,
  Upload, Search, Operations, Administration) that need a baseline before
  their proposals.
- **D. Placement.** The folder sits on `dev`, uncommitted, because no branch
  was asked for. Confirm `task/v27-planning` from `origin/dev` as the skill
  expects, or that the round stays on `dev`.
- **E. Build inputs.** Confirm `v27-build/` stays in the folder so the
  baseline can be regenerated when `origin/dev` moves, or that only the built
  file, self-check and shots are kept.
- **F. Reference file.** Confirm the operator's reference stays at the
  folder root beside its two screenshots, or moves into `current/` as the
  v26 layout had it.

## 8. Self-check

`v27-selfcheck.html` loads the mockup in a 1580×1000 frame and drives every
lifecycle state × layout × section (160 combinations), the read and edit
frames, roles, case types, Glass's states, proposals, report and custody
states, every dialog by name in a state that offers it, the viewer, the damage
clicker, the valuation calculator, the estimate tabs and VAT override, every
collapse, the file tabs, the layout and rail switches, the state-changing
mock actions, every strip button and the query presets.

Run it from the repository root:

```text
node design/planning-and-old-designs/v27_planning/current/v27-build/selfcheck.mjs
```

(`--dump-dom` stalls on the 950 KB frame, so the driver reads the result over
the DevTools protocol; the harness itself is the plain
[skill template](../../../../.claude/skills/razor-html-mockup-creation/assets/selfcheck-template.html)
with the checks filled in.)

16 September 2026: `RESULT {"fail":[],"okCount":608}`, no console errors, on
the Playwright Chromium 1234 build (151.0.7922.34). The screenshot driver
reports console errors per page load: 65 loads, none.

16 September, after the damage selector variants (§ 11): `RESULT
{"fail":[],"okCount":628}`; 71 loads, none with console errors.

16 September, after areas (§ 12) and the refined mark (§ 13): `RESULT
{"fail":[],"okCount":630}`; 74 loads, none with console errors.

16 September, after the reference proposals (§ 15): `RESULT
{"fail":[],"okCount":663}` — the baseline blocks run with every proposal off,
then block 20 turns them all on; 89 loads, none with console errors. Built
from `cdbe014a6` (`origin/dev` moved by an intake fix that touches none of
this record's inputs).

## 9. Query presets

`state` (`not-ready` `review` `with-engineer` `post-report` `held` `completed`
`query` `closed` `created-in-error`), `edit` (`1` or `colleague`), `role`
(`engineer` `administrator` `user`), `layout` (`scroll` `tabs`),
`rail=collapsed`, `data` (`empty` `conflict` `unavailable`), `kind`
(`inspection` `inspection-audit` `audit`), `audit=1`, `archived=1`, `glass`
(`open` `waiting` `failed` `elsewhere`), `proposal=1`, `report` (`none`
`confirmed` `stale` `prepared`), `ai=estimate`, `custody` (`pending`
`failed`), `lookup` (`never` `failed`), `eva` (`zip` `api` `api-off`
`api-failed`), `section=<key>`, `estimate=e2`, `tab` (`images`
`correspondence`), `collapse=<key,key>`, `expand=1`, `dialog=<name>`,
`notifications=1`, `viewer=<n>`, `strip=0`, the design variables `clicker`
(`zones` `pins` `brush` `area` `arrow`) and `logo` (`refined` `live`), the
fixture switch `repairer` (`liverpool` `london`), and the reference
proposals: `proposals=all|none` or `p=<key,key,…>` from the list in § 15.
The file opens with every proposal on; `proposals=none` is the baseline.

## 10. Known limits

- The type is the embedded Inter Variable upright; the italic face is not
  embedded (the record uses none).
- Images are generated SVG scenes; there are no files behind the mockup, so
  View on a document and Save as toast rather than open.
- The one Glass's failure code shown is representative; live codes are the
  gateway's.
- The proposal fixture matches the recorded values, so Outcome, Salvage
  category, Roadworthiness and the reason read Accepted; Engineer's Value
  reads Awaiting and Salvage value reads Corrected.
- Rail counts, notification rows, the clock and the lease expiry are fixed.
- The other shell routes are not in this file (sign-off item C).

## 11. Damage selector — four free-mark variants (16 September)

The operator asked for *"4 new varying options for the vehicle damage
selector"* with one requirement: *"we dont want to select specific
panels/sections - we want to be able to click anywhere on the vehicle to
indicate the damage"*. The strip variable **Damage selector** (query
`clicker=`) switches the Damage section between the live clicker and four
proposals; **Reset marks** restores each variant's fixture. Shots 66–71.

What the four share:

- The live Plan silhouette, legend, three chips (Underside, Interior,
  Mechanical) and the recorded list stay where they are; only how the damage
  is placed changes.
- No panel is chosen and no panel is painted. A mark lands where the click
  lands. The area or areas under the mark are derived and named under the
  mark's row ("Rear", "LH Front, LH Side") — the eight areas
  of § 12, not panels — and the derived Impact location, Impact severity and
  Incident narrative read from those areas.
- Each mark has a severity and a note in its row, as a zone does today; the
  row's × removes it. Marks are numbered on the diagram and in the list.
- A click outside the silhouette does nothing. Read mode shows the marks and
  ignores the pointer.

| Variant | Gesture | Mark | Severity |
| --- | --- | --- | --- |
| **A · Pins** (66, 70) | click anywhere; drag a pin to move it | a numbered pin with a severity-coloured halo | from the row (default Moderate) |
| **B · Brush** (67) | draw over the damage; one stroke is one mark | a soft translucent stroke; the panels the stroke crosses are all derived | from the row |
| **C · Areas** (68) | press and drag to size a circle | a translucent disc; every panel the disc covers is derived | from the row |
| **D · Impact arrows** (69, 71) | press where the vehicle was struck and drag in the direction of the impact | an impact dot with an arrow along the direction of force | from the arrow's length (five bands), then editable in the row |

Why four different gestures rather than four looks: they answer different
questions about the damage. A pin says *where*; a brush says *where and how
far it spreads*; an area says *where and how big*; an arrow says *where it
was struck and from which direction*, which is what a collision engineer
reads first and what the narrative sentence ("collision/impact damage to the
rear") already implies.

What each variant records (in `case-record.js`, `markAreas`, revised by
§ 12): the area under a pin or an impact point; every area a stroke passes
through; every area a disc's centre and eight ring points fall in. That
mapping is a mockup rule, not a decided one.

- **G. Damage selector.** Confirm one of A–D (or a combination, for example
  D's impact arrow plus A's pins for secondary damage) to carry into Stage 2,
  or reject all four and keep the live panel clicker.
- **G1.** Confirm the mark's point is what Pegasus stores (with the derived
  areas alongside, so the report diagram still has something to draw), or
  that only the derived areas are stored and the mark is presentation.
- **G2.** Confirm a mark spanning several areas is one recorded damage with
  several areas (the mockup's reading), or one impact per area as the live
  record holds one per zone today.
- **G3.** For D, confirm the arrow length sets the severity and the
  direction is recorded as a new fact for the narrative, or that the arrow
  is presentation only.

Documentation impact if any variant is chosen: FRD-06 § Damage record
(D39/D45 — "each region carries a severity and a note" and the derived
location and severity) and FRD-12 § Case workspace (the Plan clicker
sentence), plus the report's marked diagram in FRD-11.

## 12. Areas, not panels (16 September)

The operator's next change: *"rather than denoting the actual panels, we
just want to state an 'area' of the vehicle that was affected"*, with the
list **Front, LH Front, LH Rear, LH Side, Rear, RH Front, RH Rear, RH
Side**. Applied to all four variants of § 11:

- A mark's row names the area(s) it sits in and nothing else — "Rear",
  "LH Front, LH Side" — with no kind word and no panel (the operator, 16
  September: the row "doesnt need to specify the word area").
- Impact location and the narrative derive from the areas ("Rear"; "Multiple
  · Rear, Front"); Impact severity is the highest mark, as today.
- While editing, faint dashed guides show the eight areas on the silhouette
  and the readout under the diagram names the area under the pointer.
- The eight areas are Core's eight broad zones (`BroadDamageZones`: front,
  left_front, left_rear, left_side, rear, right_front, right_rear,
  right_side), so nothing new is needed in the record's vocabulary; only the
  words differ (see G5).

How a point becomes an area (`areaAt` in `case-record.js`): the silhouette is
cut into a front third, a rear third and the sides between them, and into
left, centre and right; centre-front is Front, centre-rear is Rear, the
corners are LH/RH Front and LH/RH Rear, the sides are LH/RH Side. Wheels and
mirrors count as the vehicle. The live Plan clicker is unchanged.

- **G4.** The centre of the middle band (the roof) is not one of the eight
  areas; the mockup gives it to the nearer side. Confirm, or name the area
  (or add "Roof").
- **G5.** The list says LH / RH; the live vocabulary says Left / Right for
  the broad zones and N/S / O/S for the panels (`AssessmentVocabulary.
  DamageZones`, CONTEXT.md). Confirm LH / RH as the operator words for the
  eight areas (a vocabulary change that reaches the report's narrative), or
  keep Left / Right.
- **G6.** The three chips Underside, Interior and Mechanical are not in the
  list. Confirm they stay as they are beside the eight areas, or that they go.
- **G7.** The list heading still reads "Recorded zones". Confirm "Recorded
  areas" for the variants, or keep the live label.

## 13. The refined mark (16 September)

The operator supplied `../logo/pegasus-mark-refined.png` (1254×1254,
transparent) with *"replace old logo with this one on the mockup"*. The build
folder crops it to its bounds and writes 128px and 256px renditions beside
it; the mockup embeds the 128px one in the rail's brand slot (52×52 in the
live CSS, 36×36 when the rail is collapsed) and shows it by default. The
strip variable **Logo** switches back to the live lockup so the two can be
compared (`?logo=live`). Shots 72–74.

- **H. Logo.** Confirm the refined mark replaces `pegasus-lockup.png` in
  Stage 2 (the file under `wwwroot/images/marks/` and its README), or keep
  the live lockup.

## 14. Reference-file differences still to fold in (16 September)

The operator asked what remains from
[`../pegasus_case_dashboard_2026-09-15-differences.md`](../pegasus_case_dashboard_2026-09-15-differences.md).
Status as of this version. "Needs a decision first" means the reference
conflicts with a settled FRD or ADR rule, so it is drawn only after the
operator says the rule changes.

### Folded in

| Reference feature | Where |
| --- | --- |
| Click anywhere on the vehicle for damage, no panel selection | § 11–12, Damage selector A–D, areas |
| (Not in the reference) the refined mark | § 13 |
| Report wording well — drawn on instruction although it crosses FRD-11's template rule; see I2 | § 15, `wording` |

### Not started — no rule in the way

| Reference feature | Section it would land on |
| --- | --- |
| Composed sentences on the record: matter line, assessment method, VAT explanation, storage charges, mileage source, condition, nature of incident, "what the report carries" | Overview, Inspection, Vehicle, Damage, Valuation |
| Inspection location "Other…" free entry | Inspection (live has Manual entry — confirm it is the same thing) |
| CAP as a guide source | Valuation |
| Salvage value as a % slider of PAV with 5–25 % snaps | Settlement |
| Unroadworthy reason bank (click to insert, save to bank) | Settlement |
| Delete-all lines with a confirm; per-row Undo toast | Estimate |
| Off-pattern imported cells shown amber, never discarded | Estimate |
| Regional uplift suggested from the repairer / claimant / storage postcode | Estimate (rate cards) |
| Supplementary panel: diff against the last sent version and a composed "Following receipt of…" paragraph | Estimate, Report |
| Versions / Compare with per-line diff colouring and a printable sheet | Estimate (live Compare shows totals only) |
| Provenance chips per line naming the import (AX / GL) | Estimate |
| Address book with one-click Cc suggestions | Report (Reviewed recipients) |
| Attach Breakdown PDF and an image contact sheet | Report |
| Filename dot per re-send and the "supersedes" body | Report (see FRD-11 correction rule before drawing) |
| Header badges (outcome, legal, repairs % of PAV) in the ribbon | Ribbon (live keeps them in the Figures aside) |
| Section map of nine (Claim, Images, Decisions as their own) | The whole record (D29/D30 settled ten — confirm before drawing) |
| Engineer's Value, Sign-off Engineer, claimant VAT status, disclose / commentary ticks, unrelated damage placed where the reference puts them | Overview, Valuation, Settlement, Report |
| Images: click to include, "6 per page" | Files / Report (live is role-based Report position) |
| Report / Fee tabs; fee from the principal fee table | Report |
| Queries as a panel on Notes | Notes (live: the Query lifecycle state) |

### Needs a decision first (conflicts with a settled rule)

| Reference feature | Rule it crosses |
| --- | --- |
| Take over a colleague's lock; Ask to release | FRD-01 — no take-over, no time given to a non-holder (settled again at v26 R) |
| Inline padlocks on Principal, Our ref, Case status, Instructed | FRD-01 — Our ref immutable; status by lifecycle actions; Principal by Correct principal |
| Product type select (Standard / C. / A. / AP. / D.) | FRD-01, ADR-0051 — Case type at creation; Audit Case by Create audit |
| Engineer's value typed in two places | FRD-06 — adopted only by Apply |
| Average mileage 7,100/yr shortcut | FRD-06 — the estimator abstains rather than defaulting a figure |
| Import overwrites the grid after a confirm | D16 — one file imports immediately as a new Draft, no confirm, nothing overwritten |
| Target-%-of-PAV rescaling; contract sum that rescales the spec | FRD-11 — the Core-computed total is the contract cap; Send to AI is the only target-% route |
| WhatsApp as a send channel | FRD-05/08 — WhatsApp is manual evidence intake only |
| Edit after send reopens the Case implicitly | FRD-01 — Report sent enters post-report work; Return to Engineer is reasoned |
| Assigned engineer as a select on the record | FRD-01/12 — Hand to Engineer / Assign to me with the lease |

Suggested order for the next rounds, cheapest first and each its own
lettered item: the composed sentences (they change no rule and read straight
from recorded fields); the Estimate additions (undo, delete-all, provenance
chips, richer Compare, supplementary paragraph); the Settlement helpers
(salvage slider, reason bank); then the placements and the ribbon badges;
the decision-first list only on instruction.

## 15. The reference proposals, built (16 September)

The operator: *"implement all the 'nothing in the way' section now on the
mockup"*. Every row of § 14's first table is on the mockup as its own switch
on the strip's **Proposals** row (`p=` / `proposals=all|none`), all on by
default so the file opens as the proposal, with **All off** for the
baseline. Each one is a proposal awaiting its letter; none is live. Shots
75–89 (`p-` in the name).

| Switch | What it does on the record | Shot |
| --- | --- | --- |
| `composed` | Derived read-only cells composed from the recorded fields, in the live "derived" style: **Matter line** (Overview), **Assessment method** and **Recovery and storage charges** (Inspection), **Engineer's comments** and **Pre-incident condition** (Vehicle), **Drives the calculation** (Estimate, from the repairer VAT status), **What the report carries** (Valuation, honouring the disclose switch). Every one follows its fields as they are typed. | 75–79 |
| `cap` | CAP as a fourth guide source: an entry card while editing, a recorded card in read, selectable as the basis. | 78 |
| `salvage` | Under the salvage value, a 0–100 % slider of the Engineer's Value with 5 / 10 / 15 / 20 / 25 % snaps and a readout; the amount and the % are one fact, last touch wins. | 82 |
| `bank` | Under the unroadworthy reason, the firm-level phrase bank: click inserts with "and", "Save this wording to the bank" adds the typed reason. | 82 |
| `estdel` | **Delete all lines** on the editable draft behind a confirm, and an **Undo** toast after a single line is removed. | 79 |
| `offpattern` | A value that does not fit the line's operation (unit £ on Repair / R&I / Paint / Blend, panel hours on Paint / Blend, paint hours elsewhere) reads amber with a tooltip; nothing is discarded. | 79 |
| `uplift` | A **Regional uplift** + 15 % choice on the estimate header whose chip reads "Suggested · Repairer (CR0)" when the repairer, claimant or storage postcode is in London or the Home Counties, "London & Home Counties" otherwise; ticking it lifts the labour rate to £95.77. The strip's **Repairer** switch moves the fixture repairer from Bootle to Croydon to show it. | 79 |
| `prov` | The Audatex lines' Source chips name the import: "imported · AX". | 80 |
| `compare` | Compare estimates gains From / To; only once two different versions are chosen does it show the summary line with the £ delta and counts, the per-line table with added / changed / removed rows coloured and changed cells in bold, and **Print comparison sheet** (a print stylesheet prints the dialog alone). The live totals table above stays as it is. | 81 |
| `supp` | A **Supplementary — changes vs Audatex 1** panel under the estimate tabs: the diff of the draft against the Current version with the £ delta, **Explain the change on the report** with a reason (supplementary estimate / dismantling / further inspection / further images), and the composed paragraph ("Following receipt of a supplementary estimate … The estimated repair cost has reduced from £3,004.79 to £1,047.02.") shown as **Supplementary damage** when ticked. | 80 |
| `abook` | Reviewed recipients become an address book: To with a dropdown of Principal / this case / CE addresses, Cc as chips with a typed entry and one-click suggestions for the QDOS handler and the claim source. | 83 |
| `attach` | An **Attach** row on delivery: Report, Fee note, Breakdown, Images; the prepared block lists what was ticked. | 83 |
| `resend` | **File name** (`QDOS26214 MA59BDY Total loss report.pdf`, one more `.` per re-send) and **Message** composed on delivery; after a send the message reads "updated … which supersedes our report dated 16 September 2026". | 83 |
| `feetab` | **Report / Fee** tabs at the top of the Report section; the Fee pane is the fee note from the QDOS principal fee table (£132.00 + VAT = £158.40) with its source line; the live fee fields stay on the Report pane. | 84 |
| `badges` | Outcome, legal status and "Repairs 104% of value · £3,004.79 / £2,900.00" as chips in the ribbon (green under 66 %, amber to 79 %, red from 80 %); the aside keeps the three figures and drops its chips. | 75 |
| `nine` | The reference's map: **Case details**, **Claim** (claimant, case contact, accident band), Inspection details, **Vehicle** with Damage and Valuation folded in as sub-panels, Estimate, **Decisions** (Settlement), Report, **Images** (Files with the Images tab first), Notes. Nine links on the section row; the moved panels keep every behaviour. | 87–89 |
| `place` | Sign-off Engineer beside an Assigned engineer cell on the Case column; unrelated damage and its deduction on Vehicle; the three report content switches on Valuation. | 75, 77, 78 |
| `include` | On the Images tab while editing, a tile shows its in-report tick and a click toggles it (Supporting ↔ Not used) instead of opening the viewer; the Report section's count follows. | 85 |
| `queries` | A **Queries** sub-panel on Notes with the live empty-state style. | 86 |
| `wording` | **Report wording** on the Report section, drawn on instruction (16 September, *"didnt include the report wording section"*): every narrative block the report prints, in print order — Nature of incident, Engineer's comments, Supplementary damage (when explained), PAV commentary and Unrelated damage (with their switches), Vehicle history check (pass-through), Pre-incident condition, Settlement (outcome-driven, reserve rounded up to £50 for a repairable), Salvage (total loss, by category). Each block is composed from the fields and tracks them live; while editing it can be edited in place (the chip turns to "edited · no longer tracking fields" with **Recompose from fields**), renamed, removed (returning as a **+** button), reordered by dragging, and a **New paragraph** added. Read mode shows the blocks as derived cells. Removing PAV commentary or Unrelated damage clears its report switch. | 90, 91 |

Costs seen while building, for the decision:

- `badges` crowds the ribbon at 1580 px: the four facts truncate ("CASE WORKS…", "Ms L Ca…") once the three chips join the state chip. Either the badges or the facts give way.
- `nine` nests Damage and Valuation as cards inside Vehicle, so those two lose their own head tools and availability sentence; the Overview grid drops to two columns.
- `supp` reads oddly on this fixture because the draft is three lines against a thirteen-line Current version, so almost every row is "removed"; a real supplementary would show a few added and changed rows.
- `composed` adds seven read-only cells across five sections; each is a sentence the report already composes, now visible on the record.

- **I. Reference proposals.** For each of the nineteen switches: confirm it
  carries into Stage 2 as drawn, name the change, or reject it. The
  documentation impact of an accepted switch is one FRD-12 sentence for a
  placement or a panel and, for `composed`, `supp`, `resend` and `attach`, an
  FRD-11 sentence on what the report and its delivery carry; `cap` needs a
  guide-provider decision under ADR-0031 before it can fetch anything.
- **I1.** `nine` and `badges` change settled frame rules (D29/D30 ten
  sections; the aside's Figures): confirm the rule changes, or keep them as
  strip variables for comparison only.
- **I2.** `wording` crosses FRD-11 § Assessment-report outcomes ("the
  operator has already supplied the report and correspondence templates …
  the renderer must not substitute placeholder or inferred content") and the
  Report section's fixed content switches. Confirm the rule changes so that
  the report's narrative blocks are composed from the record's fields and
  may be edited, reordered, renamed and added to by the Engineer before
  generation (an FRD-11 and ADR-0050 change, with the QuestPDF template
  taking blocks rather than fixed paragraphs), or keep it as a strip
  variable.

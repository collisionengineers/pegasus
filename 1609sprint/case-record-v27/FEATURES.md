# Case record v27 — feature description

Snapshot: 16 September 2026. Every feature below is described as product
behaviour on the live Case record (`/Cases/{id}`, ten sections, FRD-12
§ Case workspace), not as the mockup draws it. Each entry names its origin
(the reference file § or the operator's instruction), the mockup switch and
shots that show it, the settled rule it touches, and its sign-off letter in
[OPEN-ISSUES.md](OPEN-ISSUES.md). "Live" means the behaviour exists today on
`dev` and the feature changes it; "Proposed" is the v27 behaviour.

Status words: **Proposal** (nothing in its way; awaits its letter),
**Rule change** (crosses a settled FRD/ADR rule; drawn on instruction or
listed only), **Live** (already built; no work).

## 1. Damage — click anywhere on the vehicle

Origin: the operator, 16 September — *"we dont want to select specific
panels/sections - we want to be able to click anywhere on the vehicle to
indicate the damage"*, then *"rather than denoting the actual panels, we just
want to state an 'area' of the vehicle that was affected"* with the list
Front, LH Front, LH Rear, LH Side, Rear, RH Front, RH Rear, RH Side, then
the rows *"doesnt need to specify the word area"*. Mockup: strip variable
**Damage selector** (`clicker=pins|brush|area|arrow`, `zones` = live); shots
66–71. Letters G, G1–G7.

### 1.1 Live

The Damage section's Plan clicker offers 19 detailed panels, 4 wheels and
three chips (Underside, Interior, Mechanical). Clicking a panel records an
impact for that zone with a severity (five grades) and a note; each zone
appears once. Core derives `assessment.impact_location` (the one zone's
headline, or `multiple`) and `assessment.impact_severity` (the highest
rank) from the zone list; the report prints the marked diagram with the
chosen panels filled and numbered (FRD-06 § Damage record, D39/D45).

### 1.2 Proposed — common to every variant

- The Plan silhouette, legend, three chips and the recorded list stay. Only
  how damage is placed changes.
- **No panel is chosen or painted.** A click anywhere on the silhouette
  places a *mark* where the click lands. A click outside the silhouette
  does nothing. Read mode shows the marks and ignores the pointer.
- **Each mark records the area or areas it sits in**, from the eight areas
  Front, LH Front, LH Rear, LH Side, Rear, RH Front, RH Rear, RH Side. The
  eight are Core's existing `BroadDamageZones` (`front`, `left_front`,
  `left_rear`, `left_side`, `rear`, `right_front`, `right_rear`,
  `right_side`); only the operator words differ (G5).
- The mark's row in the list names the area(s) alone — "Rear", "LH Front,
  LH Side" — with a severity select and a note, as a zone row does today,
  and a × that removes it. Marks are numbered on the diagram and in the
  list.
- **Impact location** derives from the areas: one area's name, or
  "Multiple · Rear, Front". **Impact severity** is the highest mark, as
  today. The **incident narrative** reads from those ("The vehicle has
  suffered moderate collision/impact damage to the rear").
- While editing, faint dashed guides show the eight areas on the silhouette
  and a readout under the diagram names the area under the pointer.
- How a point becomes an area: the silhouette is cut into a front third, a
  rear third and the sides between; and into left, centre and right.
  Centre-front is Front, centre-rear is Rear, the corners are LH/RH Front
  and LH/RH Rear, the sides LH/RH Side. Wheels and mirrors count as the
  vehicle. The roof (centre of the middle band) goes to the nearer side
  (G4).
- Underside, Interior and Mechanical stay as chips beside the areas (G6);
  the list heading becomes "Recorded areas" (G7).

### 1.3 The four variants (one to be chosen — G)

| Variant | Gesture while editing | What is drawn | Severity |
| --- | --- | --- | --- |
| **A · Pins** (shots 66, 70) | click anywhere; drag a pin to move it | a numbered pin with a severity-coloured halo | from the row, default Moderate |
| **B · Brush** (67) | draw a stroke over the damage; one stroke is one mark | a soft translucent stroke | from the row |
| **C · Areas** (68) | press at the centre and drag to size a circle | a translucent disc | from the row |
| **D · Impact arrows** (69, 71) | press where the vehicle was struck, drag in the direction of the impact | an impact dot with an arrow along the direction of force | from the arrow's length in five bands, then editable in the row |

What each records: A and D the area under the point; B every area the
stroke passes through; C every area the disc's centre and eight ring points
fall in. A mark spanning several areas is one recorded damage with several
areas (G2). D additionally records the direction of impact if G3 says so.

### 1.4 What the report prints

The report's Damage section keeps the diagram and the impact table. With
marks, the diagram prints either the marks themselves at their points (G1
"store the point") or the derived areas shaded (G1 "areas only"). The
"Nature of Incident" sentence and the "Impact Magnitude" row read the
derived location and severity as today.

Status: **Proposal** (G). Documentation: FRD-06 § Damage record (the unit
becomes the eight areas with or without the point; the 23 panels stop being
entered), FRD-12 § Case workspace (the clicker sentence), FRD-11 § outcomes
(what the diagram prints), CONTEXT.md if LH/RH becomes the term (G5).

## 2. Brand mark

Origin: the operator supplied `logo/pegasus-mark-refined.png` (1254²,
transparent) — *"replace old logo with this one on the mockup"*. Mockup:
`logo=refined|live`; shots 72–74. Letter H.

- Live: the rail's brand slot shows `wwwroot/images/marks/pegasus-lockup.png`
  (`_Layout.cshtml:84`), 52×52, 36×36 when the rail is collapsed.
- Proposed: the refined mark, cropped to its bounds and rendered at 128 px
  and 256 px, replaces the lockup in that slot at both sizes. Nothing else
  on the shell changes.

Status: **Proposal** (H).

## 3. Proposals from the reference, by section

All twenty-two switches on the mockup's **Proposals** row (`p=` /
`proposals=all|none`) plus `wording`; shots 75–94. Letters I (each switch
carry / change / reject), I1 (`nine`, `badges`), I2 (`wording`), I3
(`ticks`, `signoff`, `reportdate`).

### 3.1 Overview

**Matter line** (`composed`, shot 75). A derived read-only cell "Matter"
reading "Road Traffic Accident: {claimant}: {incident date}" — the exact
line the report's letterhead already prints
(`AssessmentReportLayout.cs:160`). Follows the fields as they are typed.
*Proposal.*

**Assigned engineer and Sign-off Engineer side by side** (`place`, shot
75). The Case column shows an "Assigned engineer" cell (read-only; the
lease-bound Hand to Engineer / Assign to me actions remain the only way to
change it) with the Sign-off Engineer select beside it, moved from the
Report section. Read mode shows the two names. *Proposal.* Rule touched:
FRD-12's Report section list (D31 placement sentence).

### 3.2 Inspection

**Assessment method** (`composed`, shot 76). A derived cell "Vehicle located
at: {resolved address}" — Image Based Assessment, or the claimant / repairer
/ storage / manual address the Inspect-at choice resolves to (D33). This is
the sentence the report's introduction prints (`Introduction(snapshot)`).
*Proposal.*

**Recovery and storage charges** (`composed`, shot 76). A derived cell that
reads one of three sentences when a charge is entered (recovery + storage /
recovery only / storage only) and "No charges entered" otherwise. The report
prints these as table rows today, not a sentence, so the sentence wording is
new copy (J9). *Proposal.*

### 3.3 Vehicle

**Engineer's comments on mileage** (`composed`, shot 77). A derived cell
under Mileage source showing the sentence the report prints for that
source ("The mileage has been provided by the owner." …
`MileageSentence`). *Proposal.*

**Pre-incident condition sentence** (`composed`, shot 77). A derived cell
"The vehicle is considered to be in {condition} condition for its age and
type." — the report's Pre-Incident Condition paragraph. *Proposal.*

**Unrelated damage on Vehicle** (`place`, shot 77). Unrelated damage and its
deduction move from Damage to Vehicle, under the condition fields. Read and
edit behaviour unchanged. *Proposal.*

### 3.4 Valuation

**CAP guide source** (`cap`, shot 78). A fourth guide card, CAP, beside
Glass's, Brego and Super CAP: month, mileage, retail, trade, Get valuation
(notice while no provider is connected), Save; a recorded CAP card in read;
selectable as the basis for the calculation. *Proposal;* D40 lists three
guide sources, so FRD-06 § Valuation sources gains one, and connecting a CAP
provider needs its own decision (ADR-0031 as FRD-06 cites it).

**What the report carries** (`composed`, shot 78). A derived cell "{guide}
— Retail £x · Trade £y · Engineer's value £z." honouring the disclose
switch ("Source not disclosed — …" when off). Record-only; the report prints
these as its Vehicle Data rows (J9). *Proposal.*

**Report content switches on Valuation** (`place`, shot 78). "Disclose guide
source on report" and "Include valuation commentary" (with the commentary
text) move from the Report section to the Valuation section; "Include
unrelated damage" moves beside unrelated damage on Vehicle. *Proposal.*

### 3.5 Estimate

**Drives the calculation** (`composed`, shot 79). Above the VAT categories,
a derived cell explaining the VAT default: "VAT registered — VAT defaults to
all categories." / "Not VAT registered — VAT defaults to parts and
materials only." plus "Overridden on this estimate" when the categories no
longer match the repairer status. Record-only copy (J9). *Proposal.*

**Delete all lines and Undo** (`estdel`, shot 79). On the editable Draft:
a **Delete all lines** head action behind a confirm dialog; after a single
line is removed, a six-second toast with **Undo** that puts the line back at
its position with its origin intact. *Proposal.*

**Off-pattern cells** (`offpattern`, shot 79). A value the line's operation
does not price — a unit amount on Repair / R&I / Paint / Blend, panel hours
on Paint / Blend, paint hours elsewhere — renders amber with a tooltip
stating how it is treated; nothing is discarded. Core already computes
these as `EstimateTotals.OffPattern` and retains them in specialist
treatment; today the page does not show them. *Proposal.*

**Regional uplift** (`uplift`, shot 79). A **Regional uplift +15 %** choice
on the estimate header beside the rate card. Its chip reads
"Suggested · Repairer (CR0)" when the repairer's, claimant's or storage
postcode outward code is in London or the Home Counties, "London & Home
Counties" otherwise; ticking it lifts the hourly rate (£83.28 → £95.77).
The vocabulary already holds `rates.regional_uplift` as a flag; nothing
applies it today. *Proposal;* the percentage and the postcode set are new
policy (J10).

**Provenance chips** (`prov`, shot 80). The Source column names the import
route on imported lines — "imported · AX" (Audatex PDF), "imported · GL"
(Glass's) — instead of the plain "Imported"; Amended and Manual unchanged.
*Proposal.*

**Compare with per-line diff and print** (`compare`, shot 81, 81b). The
Compare estimates dialog keeps its totals table and gains **From** / **To**
selects. Only once two different versions are chosen does it show a summary
line (£ delta, added / changed / removed counts), a side-by-side table with
added rows red, changed orange (changed cells bold) and removed green, and
**Print comparison sheet**, which prints the dialog alone. *Proposal.*

**Supplementary** (`supp`, shots 80, 93, 94). Under the estimate tabs, a
**Supplementary — changes vs [Choose a version]** line. Nothing else shows
until a version is chosen; then the diff of the Draft against it with the
£ delta, **Explain the change on the report** with a reason (supplementary
estimate / dismantling / further inspection / further images), and the
composed paragraph ("Following receipt of a supplementary estimate the
following additional items are now required: …; The repair time for … has
been revised; … is no longer required. The estimated repair cost has
increased from £A to £B.") printed as a **Supplementary damage** block when
ticked. *Proposal* on the record; the printed paragraph is new report
wording (J8, and I2 if the wording well carries it).

### 3.6 Settlement

**Salvage slider** (`salvage`, shot 82). Under Salvage value while editing,
a 0–100 % slider of the Engineer's Value with 5 / 10 / 15 / 20 / 25 % snap
buttons and a readout "= 25 % of £2,900"; the £ amount and the % are one
fact, last touch wins; the saved field is still `assessment.salvage_value`.
*Proposal.*

**Unroadworthy reason bank** (`bank`, shot 82). Under the reason while
Unroadworthy: a firm-level list of starter phrases; a click inserts one,
joining with " and "; **Save this wording to the bank** adds the typed
reason for everyone. *Proposal;* the bank is new organisation-wide
reference data (J12).

**Decision tick rows** (`ticks`, shot 92). Outcome, Salvage category and
Roadworthiness render as rows of tick buttons while editing, as the
reference draws them; the select stays the form control underneath and the
buttons drive it. Read mode unchanged. *Proposal (I3).*

### 3.7 Report

**Address book** (`abook`, shot 83). Reviewed recipients become: **To**
with a dropdown of the Principal's addresses, this Case's contacts (claim
source, repairer, storage, claimant when an e-mail is recorded) and CE
staff; **Cc** as chips with a typed entry and one-click suggestion buttons
for the Principal handler and the claim source. The claim source is never
copied implicitly — a click is explicit (J13). *Proposal.*

**Attachments** (`attach`, shot 83). An **Attach** row on delivery: Report
(always), Fee note, Breakdown (the estimate document — see
`../estimate-generator/PLAN.md` Phase 2), Images (a contact sheet of the
report's images); the prepared block lists what was ticked. *Proposal;*
Breakdown and Images are new artifact kinds (J14).

**Re-send naming and message** (`resend`, shot 83). A **File name** readout
("QDOS26214 MA59BDY Total loss report.pdf", one more "." before the
extension per re-send as the version tell) and a composed **Message**;
after a send the message reads "updated … which supersedes our report dated
{date}". Today the attachment is `{REF}_assessment.pdf` and the message body
is empty. *Proposal;* the file-name rule and the covering message are new
correspondence copy (J15).

**Report / Fee tabs** (`feetab`, shot 84). Two tabs at the top of the Report
section. Fee shows the fee note preview (£132.00 + VAT = £158.40 in the
fixture) with a source line naming the Principal fee table; the live agreed
fee and description lines stay on the Report tab. *Proposal;* Pegasus has
no Principal fee table today (J16).

**Sign-off follows the Engineer** (`signoff`). When Hand to Engineer or
Assign to me assigns an Engineer who is flagged Sign-off Engineer, the
Case's Sign-off Engineer becomes that Engineer. Today D31's default applies
at creation only. *Proposal (I3;* J17 on when an operator's own choice must
win).

**Report date on generate** (`reportdate`). Generate report writes today's
date into an empty Report date on the record; a recorded date is left
alone. The printed date is unchanged — the report already prints the
generation date unless the override is on. *Proposal (I3;* J18).

**Report wording** (`wording`, shots 90, 91). A **Report wording** panel on
the Report section listing every narrative block the report prints, in
print order: Nature of incident, Engineer's comments, Supplementary damage
(when explained), PAV commentary and Unrelated damage (with their
switches), Vehicle history check (pass-through), Pre-incident condition,
Settlement (outcome-driven; a repairable's reserve rounded up to the next
£50), Salvage (total loss, by category). Each block is composed from the
fields and tracks them live ("composed · tracks fields"); while editing it
can be edited in place (the chip turns to "edited · no longer tracking
fields" with **Recompose from fields**), renamed, removed (returning as a
**+ {title}** button), reordered by dragging, and a **New paragraph**
added. Read mode shows the blocks as derived cells. Removing PAV commentary
or Unrelated damage clears its report switch. *Rule change (I2):* FRD-11's
template rule ("the renderer must not substitute placeholder or inferred
content") and ADR-0050's fixed paragraphs; salvage wording for categories
A, B and N is not accepted today (J7).

### 3.8 Files

**Click to include** (`include`, shot 85). On the Images tab while editing,
each tile shows its in-report tick; a click toggles Supporting ↔ Not used
instead of opening the viewer; the Report section's image count follows.
Close-up and Overview keep their single-role rule (J19). *Proposal.*

### 3.9 Notes

**Queries panel** (`queries`, shot 86). A **Queries** sub-panel on Notes in
the live empty-state style, listing post-report items (the Query lifecycle
state's linked correspondence). *Proposal;* what it lists is J20.

### 3.10 Frame

**Ribbon badges** (`badges`, shot 75). Outcome, legal status and
"Repairs 104 % of value · £3,004.79 / £2,900.00" as chips in the ribbon
(green under 66 %, amber to 79 %, red from 80 %); the Figures aside keeps
its three figures and drops its chips. Cost seen: at 1580 px the ribbon's
four facts truncate. *Rule change (I1):* FRD-12's Figures aside.

**Nine sections** (`nine`, shots 87–89). The reference's map: Case details,
Claim (claimant, case contact, accident band), Inspection details, Vehicle
with Damage and Valuation nested as sub-panels, Estimate, Decisions
(Settlement), Report, Images (Files with Images first), Notes. Nine links
on the section row; every moved panel keeps its behaviour. Costs seen:
nested Damage and Valuation lose their own head tools and availability
sentence; the Overview grid drops to two columns. *Rule change (I1):*
D29/D30's ten sections and `?section=` keys.

## 4. Decision-first features (listed, not drawn)

Each crosses a settled rule and is built only if the operator says the rule
changes. The reference behaviour is described so the decision is concrete.

| Reference behaviour | Rule it crosses |
| --- | --- |
| Whole-case lock with heartbeat, 15-minute stale, **Ask to release**, **Take over** another person's lock | FRD-01 § Case edit authority — one server lease; a non-holder gets no take-over control and no time (settled again at v26 R) |
| Padlocked inline edits of Principal, Our ref, Case status, Instructed | FRD-01 — Our ref immutable; status through lifecycle actions; Principal through Correct principal |
| Product type select (Standard / C. / A. / AP. / D.) | FRD-01, ADR-0051 — Case type at creation; Audit Case by Create audit |
| Engineer's value typed on Decisions as well as Valuation | FRD-06 — Engineer's Value adopted only by Apply |
| "Average mileage 7,100 / yr" tick that overwrites the odometer | FRD-06, ADR-0012 — the estimator abstains rather than defaulting a figure |
| Import overwrites the grid behind a confirm | D16 — one file imports immediately as a new Draft; nothing overwritten |
| Target-% of PAV slider that rescales prices and rate; contract sum that rescales the spec | FRD-11 — the Core-computed total is the contract cap; Send to AI is the only target-% route |
| WhatsApp as a send channel | FRD-05/08 — WhatsApp is manual evidence intake only |
| Edit after send implicitly reopens the Case | FRD-01 — Report sent enters post-report work; Return to Engineer is reasoned |
| Assigned engineer as a select on the record | FRD-01/12 — Hand to Engineer / Assign to me under the lease |

## 5. Reference features already live (no work)

Notes bands (Principal generic + case-specific, claim source); claim
fields; inspect-at choices incl. Manual entry; repairer and storage fields;
DVLA-chipped vehicle facts and Look up DVLA & MOT; mileage unit toggle;
mileage source and condition selects; valuation presets (admin data) and the
VAT-on-commercial block for a VAT-registered claimant; the adjustment order
VAT → previous-TL % → extras → condition with Apply; the four discounts; VAT
categories defaulted from repairer status with Overridden / Reset; totals
strip and the three worklists; the six decision fields with salvage rows
only for total loss and the reason only when unroadworthy; Generate / stale
/ regenerate; readiness gate; agreed fee and fee note; the merged Notes
timeline and Add Case note.

## 6. Cross-cutting behaviour every proposal keeps

- Read mode shows values, never empty inputs; derived cells use the live
  `derived` style and go `empty` when nothing is recorded.
- Edit furniture appears only in an edit session; the lease and role rules
  of `Details.Frame.cs` are untouched by every proposal.
- Composed sentences on the record and in the report come from one Core
  owner so they cannot drift (BACKEND § 3).
- Every new operator-visible sentence is approved copy; none is invented in
  Stage 2 (the necessary-copy rule).

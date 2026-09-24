# Case workspace guardrails

These rules are mandatory for `src/Pegasus.Web/Pages/Cases/**` unless the current operator task
explicitly instructs a Case-workspace redesign.

## Frame is fixed by default

The Case record has no generic page header.

Its stable frame is:

1. Views card in the aside once an Audit exists;
2. sticky 56px Case ribbon;
3. sticky 40px section row;
4. Case sections;
5. Figures + Next action context aside.

The ribbon owns:

- Case reference / registration context;
- Claimant;
- Principal;
- Engineer;
- state chip, including held review date when applicable;
- Case type chip;
- colleague-editing state when applicable;
- Edit Case, or Editing + Cancel + Save while editing;
- one Actions menu.

Do not turn the ribbon into a button shelf.

The section row owns:

- section navigation;
- Refresh;
- Scroll/Tabs switch.

Scroll remains the default unless the operator explicitly changes it.

There is no working-set strip above the ribbon, and no strip of view tabs replaces it.

## Inspection and Audit views

An Inspection + Audit Case whose Audit has been created has two views of one record (operator,
24 September 2026; FRD-16):

- The **Views** card is the first card in the aside and renders only once the Case has its Audit.
  Its rows are "Inspection · {Case/PO}" with a "Sent" chip and "Audit · a.{Case/PO}" with the Case
  state chip; the current view is plain, the other a link (`?view=inspection|audit`,
  server-rendered). The Audit view is the default. Without an Audit there is no card and `view`
  is ignored; a standalone Audit and a Triage Case have one view.
- Do not add a view switch to the ribbon or the section row, and do not put the Audit reference on
  the ribbon. The Scroll/Tabs switch is unchanged.
- **The Inspection view is read-only.** No Edit anywhere in it. Each editable section head shows
  the approved `.gated` label "Read-only · Audit created" instead; Files and Notes carry no
  label. Actions that need no Case lease (Add evidence, previews, downloads) stay; every
  lease-requiring edit exists only in the Audit view. A lease holder at `?view=inspection` keeps
  the ribbon's editing controls, but every section still renders read-only.
- Carry `view` only where the Inspection view is reachable (Refresh, section links, previews,
  lazy section loads). The Next action always goes to the Audit, and writes return to the default
  view.
- Report in the Audit view shows the Inspection's sent report as one `.pv` line with an
  "Inspection view" link above the Audit card; in the Inspection view the card shows the sent
  Inspection report with no generation or delivery. Files shows the audit folder chip after the
  Case folder chip, mirroring its states and tones.
- The words "View" and "Changed from Inspection" belonged to rejected options and are not used.

## Section order and ownership

Preserve this order:

1. Case details
2. Claim
3. Original report on Audit Cases
4. Inspection details
5. Vehicle, with Damage and Valuation nested inside
6. Repair Spec
7. Decisions
8. Report
9. Files
10. Notes

Each section is the owner of its domain. Do not duplicate its full content elsewhere.

Important ownership decisions:

- Case details contains the Notes band/current overview facts, not a second Notes timeline.
- Claim contains claimant and claim facts; Original report belongs only to Audit Cases.
- Inspection owns inspection/storage-location details and storage money inputs.
- Vehicle owns one accepted mileage field with provenance rows, not multiple competing mileage boxes.
- Damage owns the Plan damage clicker and engineering damage facts.
- Valuation owns guide-source cards and valuation calculation.
- Repair Spec owns specification tabs, header/lines, Import, Send to AI and Compare.
- Decisions owns settlement decisions and settlement-only figures.
- Report owns report content switches, generation/preview/finality controls and report commentary.
- Files owns Case images, documents, crop/tag/viewer tools, upload requests and correspondence/file surfaces.
- Notes owns the single Case timeline, notes and chase recording.

Case images are not repeated under Damage. A Report image-selection/preview strip may summarize the
same evidence only where Report needs that decision.

## Read/edit geometry

Read and edit share one geometry and one look (operator, 23 September 2026).

- Do not create a separate edit page or visually unrelated edit panel.
- Labelled fact cells stay in the same grid position, and the same cells render in both modes:
  never add a field that appears only while editing or only while reading.
- A value is a greyed box wherever it cannot be edited — every cell while reading — and a white
  control where it can. A cell rendered without a control carries `ro`. No padlock marks a field;
  the greyed box is a value, never a disabled control.
- Where a value came from is one `src-tag` word in the cell's label line, in both modes; a staff
  value carries none. Do not reintroduce a provenance icon or tooltip.
- Entering edit must not teleport the operator or substantially reflow the page.
- Edit from a section head enters the one Case-wide edit session; that section stays where it was
  on the screen.
- Save and Cancel act in place.
- Immediate-post actions should not end the edit session unless their contract requires it.
- A colleague's lease is read-only until an eligible staff member uses the
  audited Take over action required by FRD-14.

Normal Cancel discards the edit without a redundant confirmation. Dirty-navigation protection may
still guard leaving/switching when appropriate.

## Availability

Each section may show one concise availability condition when it cannot be edited.

Do not add multiple locks, pills and warnings for the same blocked condition.

If a control is absent until a prerequisite is met, do not add a redundant locked pill merely to
explain that same absence unless the design authority requires the explanation.

## Actions menu

Keep lifecycle/consequential progressions in the existing Actions menu according to the state
contract. Typical entries include:

- Hand to Engineer;
- Send to EVA;
- Mark report sent;
- Mark completed;
- Return to Review / Return to Engineer;
- Archive;
- Place on Hold / Release Hold;
- Correct principal;
- Create audit.

Close case remains destructive, separated and styled in red.

Do not surface the same lifecycle action again inside arbitrary section bodies.

## Valuation

One route per guide source.

Glass's, Brego, Super CAP, CAP and Cazana each own one card, the same in read and edit, containing:

- guide month;
- retail value;
- trade value;
- Get valuation (while editing).

A card has no mileage box (operator, 24 September 2026): the Case's own accepted mileage is used by the
lookup and carried by the adopted Engineer's Value.

The boxes are greyed while reading and inputs of the Case form while editing. Get valuation fills
the same card in place, without redrawing the page, or shows the card's notice when the source has
no working provider. The ribbon Save is the writer (23 September 2026): it records every changed
card with whatever was entered — any box may be left blank — and an untouched or blank card records
nothing. A card opens holding only what is recorded, in both modes.

The calculator has no Apply (operator, 23 September 2026): the ribbon Save adopts the Engineer's
Value when the calculation changed since the page opened, and an unchanged calculation adopts
nothing. Where a surface points the operator to the value, it says **Set in Valuation**.

Do not reintroduce:

- a Save on the card;
- an Apply button on the calculator;
- Add valuation;
- a second generic valuation dialog;
- a second source-button row;
- a separate writer path for fetched vs typed values.

AI market research remains its own distinct action/card.

## Repair Spec

Preserve the Estimate workbench and its existing Expand/full-screen presentation toggle.

Do not move its main controls into the Case ribbon.

The spec has no Save of its own (operator, 23 September 2026): its controls belong to the Case form
and the ribbon Save records it with everything else. Do not reintroduce a Save repair spec button.
Apply and Remove scaling save the Case first and then act on the saved spec.

Do not restore the redundant locked "A confirmed Engineer's Value is required" pill. The Send to
AI control is simply unavailable/absent until its requirement is met, according to the current
contract.

Toolbar and header controls must remain compact and on one line where the existing design expects
that. Scope width fixes locally; do not make every select full width.

Read and edit are one layout (operator, 23 September 2026): a spec that cannot be changed renders
the editor's header cells, grid columns and contract, discount and VAT bars, each value greyed in its
control's place (`EstimateBody` in `_CaseEstimate.cshtml`). Do not reintroduce a read-mode summary
line or a separate read table; only tools (add/delete lines, the Target % of value controls, Reset
to repairer status) are edit-only, and a scaled spec's Target % bar reads with its Scaled state.

## Damage

Use the approved Plan clicker, not a newly invented Elevations/Dial/alternative presentation.

The recorded-zone model and visual markers must remain aligned: a drawn disc is saved as drawn and
names exactly the areas it touches (Core reads them off the disc). Do not snap, regrow or rebuild a
drawn disc from its areas, and keep every disc capped at half the vehicle's width and clipped to the
body on the page and the report.

Do not re-add the Files image strip to Damage.

## Files / viewer

Files is the Case evidence home.

Preserve:

- image tiles and full-screen viewer;
- tag picker using the shared menu convention;
- crop on the viewer stage;
- original Download semantics;
- crop affecting tiles/report presentation rather than mutating the original source.

Viewer controls must not overlap the image stage. Keep the crop toolbar coherent at supported widths.

## Aside

The context aside contains the Views card (only once an Audit exists), then Figures and Next
action.

Do not recreate a separate "Current position" card that repeats the ribbon.

Below the established wide-screen threshold the aside folds into the approved strip pattern; do not
invent a second breakpoint just for one Case feature.

## Tests / review

Any Case UI change should be checked against:

- the v26 Case frame contract;
- `CaseRecordFrameV26WebTests` and the most local affected Web tests;
- 1580px and smaller-desktop rendered views;
- read and edit modes when the feature is editable;
- colleague-editing / blocked state when the change touches edit authority;
- Scroll/Tabs if the change affects section presentation;
- both views, and the Inspection view's read-only label, when the Case has an Audit.

A feature addition does not authorize moving existing fields/actions to make room. Fit the feature
inside its owning section unless the task explicitly requests a layout redesign.

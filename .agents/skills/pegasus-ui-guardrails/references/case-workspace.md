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
- Edit Case, or Editing + its status word + Done while editing (save as you go, operator,
  29 September 2026), with the Case form's default Save now that the script hides;
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
  Its rows are "Inspection · {Case/PO}" with a "Sent" chip only when the Inspection report was sent
  (none when the Audit was created first) and "Audit · a.{Case/PO}" with the Case
  state chip; the current view is plain, the other a link (`?view=inspection|audit`,
  server-rendered). The Audit view is the default. Without an Audit there is no card and `view`
  is ignored; a standalone Audit and a Triage Case have one view.
- Do not add a view switch to the ribbon or the section row, and do not put the Audit reference on
  the ribbon. The Scroll/Tabs switch is unchanged.
- **The Inspection view edits the Inspection's own values** (operator, 2 October 2026): the
  same section-head Edit, the one Case lease and the one Save form as the Audit view, in the
  same geometry. Do not reintroduce a read-only label or a per-view lease. A save there changes
  nothing of the Case's state, due date, completeness or matching.
- Carry `view` on everything the Inspection view renders that comes back to the Case: section
  links, Refresh, previews, lazy section loads, the Next action's links, and every POST form
  (hidden `view`), whose handler writes the view's work (`WorkOf(view)`) and returns to that view.
- The Next action is the viewed work's (operator, 2 October 2026): the Inspection view states
  the Inspection report's own step and lists its blockers, nothing once that report is sent;
  the Audit view states the Audit's. Once the report is sent on an Inspection + Audit Case with
  no Audit, the step is Create audit with the Actions menu's own control (`data-dialog-open` or
  the `.menu-gated` span with its reason).
- Report in the Audit view shows the Audit report alone (operator, 2 October 2026); the Views
  card is the route to the Inspection report. In the Inspection view the card is the Inspection
  report with its Generate, Prepare delivery and Send controls, before and after it is sent
  (operator, 1 and 2 October 2026), and the Actions menu's Mark report sent takes its evidence;
  every form carries `view=inspection` and acts on the Inspection's own work, never on the
  Case's state. Files shows the audit folder chip after the Case folder chip, mirroring its
  states and tones.
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
- Valuation owns the guide-source rows, the valuation calculation, the Retail, Trade and Engineer's value boxes and the On the report content switches.
- Repair Spec owns specification tabs, header/lines, Import, Send to AI and Compare.
- Decisions owns settlement decisions and settlement-only figures.
- Report owns generation/preview/finality controls, report wording, report date, commentary and the Fee pane. Its head keeps the one **Not ready** label; the blocker list is the aside's Next action (below).
- Files owns Case images, documents, crop/tag/viewer tools, upload requests and correspondence/file surfaces. A document row may offer **Import as repair spec**, which runs Repair Spec's own import on that file (operator, 25 September 2026).
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
- Every change saves as it is made (operator, 29 September 2026): a cell as it is left, a
  composite editor when it is left or after a short pause. A landed save redraws the notices,
  the ribbon, the aside and the dialogs and never a section the operator is typing in. Done
  releases the lease. There is no Save and no Cancel; do not reintroduce either, or an
  unsaved-changes question.
- Immediate-post actions should not end the edit session unless their contract requires it.
  An action, Refresh or a link away waits for a change not yet sent to land first.
- A colleague's lease is read-only until an eligible staff member uses the
  audited Take over action required by FRD-14.

## Availability

Each section may show one concise availability condition when it cannot be edited.

Do not add multiple locks, pills and warnings for the same blocked condition.

If a control is absent until a prerequisite is met, do not add a redundant locked pill merely to
explain that same absence unless the design authority requires the explanation.

## Actions menu

Keep lifecycle/consequential progressions in the existing Actions menu according to the state
contract. The menu is offered in and out of an edit session (operator, 29 September 2026): an
item taken outside one runs under a lease claimed for it, and nothing that needs a lease is
offered while a colleague holds it. Typical entries include:

- Hand to Engineer;
- Send to EVA;
- Mark report sent;
- Mark completed;
- Return to Review / Return to Engineer;
- Archive;
- Send chaser, on any open Case where staff mail is composed in (operator, 5 October 2026): a
  link to the composer (`/Inbox/Compose?caseReference=…&purpose=chaser`), no dialog, no lease,
  no reason; the composer pre-fills To, Subject and Message and staff edit them there;
- Place on Hold / Release Hold;
- Correct principal;
- Create audit, the one item always listed (on an Inspection + Audit Case, in every state): when
  Core refuses it, or a colleague holds the lease, it renders disabled inside a `.menu-gated`
  span whose `title` states the reason on hover (operator, 1 October 2026). Do not extend this
  treatment to other items without the same explicit instruction.

Close case remains destructive, separated and styled in red.

Do not surface the same lifecycle action again inside arbitrary section bodies.

## Valuation

The sources are rows and the chosen one opens (design D, operator, 6 October 2026). In order:

1. the source rows under one line of column heads (Retail, Trade, Guide month);
2. under the chosen source's row, one block: the calculation, then one row of three boxes, Retail
   value, Trade value and Engineer's Value. With no source chosen the block closes the list;
3. On the report.

The three boxes are fields of the Case form, greyed while reading. Choosing a source as the basis
fills them in place by script and moves the block under its row; the operator may overtype any of
them. Without script they are typed.

One route per guide source.

Glass's, Brego, Super CAP, CAP and Cazana each own one row, the same in read and edit, containing:

- retail value;
- trade value;
- guide month;
- Get valuation (while editing, only when the source has a connected provider);
- Use this value (while editing).

Each box keeps its own label for a screen reader and shows it at 760px and below, where the column
heads go. A row has no mileage box (operator, 24 September 2026): the Case's own accepted mileage
is used by the lookup and recorded with a calculated Engineer's Value when the Case has one.

The boxes are greyed while reading and inputs of the Case form while editing. Get valuation fills
the same row in place, without redrawing the page, or shows the row's notice when the source has
no working provider. A source with no connected provider shows that notice from the start and has
no Get valuation button (an unavailable action is omitted, not disabled). The Case's save is the
writer (23 September 2026, saved as you go since 29 September 2026): it records every changed row
with whatever was entered — any box may be left blank — and an untouched or blank row records
nothing. A row opens holding only what is recorded, in both modes.

AI market research has its own standing row: its latest figures, the Valuation month and its own
Get valuation, present before any research has run and not offered in the Inspection view. While a
job runs the row reads Researching for its month; a recorded research row states the month, mileage
and date it was asked with. Do not put a research row or button above the sources.

The calculator has no Apply (operator, 23 September 2026): its result fills the Engineer's Value
box, and a save records a calculation that changed since the last save, or one the Engineer chose
with **Use this value** (28 September 2026), against its basis card. Any other save records no
calculation. **Use this value** is one button on the row, not a second writer: it
chooses the source, fills the three boxes and switches on a field of the Case form. The preview
uses the retail as typed and the claimant's VAT as the form holds it; its amounts for commercial
VAT and previous total loss stand in those cells' label lines, are dimmed while pending, and a
failure or Core's own reason is shown, never "None yet" unless no source is chosen while editing.
Where a surface points the operator to the value, it says **Set in Valuation**.

The Engineer's Value stands in one place, its box (operator, 6 October 2026). While it holds a
recorded calculation's figure its label carries that calculation's source as one `src-tag` word,
the calculator opens on that calculation, and that source's row opens while reading. A different
figure saved over it is the Engineer's own: no word, the calculator opens blank, and the earlier
calculation stays in the Case's history only. The section head's figure and the word follow each
save without a reload.

Do not reintroduce:

- a Save on a source;
- an Apply button on the calculator;
- a "Proposed Engineer's Value" total or lines box;
- an "Applied Engineer's Value" block, or "None yet" beside a figure;
- Add valuation;
- a second generic valuation dialog;
- a second source-button row;
- a separate writer path for fetched vs typed values.

## Repair Spec

Preserve the Estimate workbench and its existing Expand/full-screen presentation toggle.

Do not move its main controls into the Case ribbon.

The spec has no Save of its own (operator, 23 September 2026): its controls belong to the Case form
and each save records it with everything else. Do not reintroduce a Save repair spec button.
Apply and Remove scaling wait for a change not yet sent to land and then act on the saved spec.

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
drawn disc from its areas, and keep every disc capped at half the vehicle's width. Each disc is drawn
as the one yellow comic burst, unnumbered and unclipped, on the recorded Vehicle type's drawing (car,
van or motorbike), alike on the page and the report.

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

While the report is not ready, Next action lists every report blocker (With Engineer, in place of
its one line), each row linking to the section that owns the fact rather than repeating it. A stale generation's warning notice sits at the
top of Next action; there is no page-wide stale bar and no second stale notice in Report
(operator, 28 September 2026). Beside the sections the sticky aside is capped at the viewport and
scrolls on its own; folded above them, the blocker list scrolls inside its panel. Do not let a long
list push the sections down or hide below a sticky aside.

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
- both views, each editing its own work, when the Case has an Audit.

A feature addition does not authorize moving existing fields/actions to make room. Fit the feature
inside its owning section unless the task explicitly requests a layout redesign.

# v28 working log

A running record of changes made to the v28 mockup as the collaboration goes,
newest entry last. Each entry says what the operator asked for, what was
changed, the choices made along the way and what is still open. The decisions
themselves are kept current in [`v28-notes.md`](v28-notes.md); the history of
how the round came about is in [`discussion-log.md`](discussion-log.md).

A temporary design review artifact, not application code and not design
authority.

## How changes are made

The captured pages in `states/` are never edited. They stay the faithful
baseline of `origin/dev` `904903fd1`. Every change sits in the proposals layer,
`assets/mock/proposals.js` and `assets/mock/proposals.css`, which the shim
loads over a captured page. Each proposal has an id (P1, P2, ...).

- Open a family file and use **Proposals / Baseline** in the top bar to compare.
- On a state page: `?proposals=off` is the baseline, `?skip=P4,P6` turns
  named proposals off.
- The self-check proves both sides: the baseline view must carry no proposal
  change and must match the running application; each proposal must hold on
  every state with the layer on.

## 18 September 2026 — first collaboration pass

**Asked for.** Keep this log. Use the better mark from
`v27_planning/logo`. Address issues C to H of the notes where a mockup can.
Successful operations green, unsuccessful red, "in general, intelligent colour
schemes" ("Case created" is grey today). "Provider cancelled" is just
"Cancelled"; never the word "provider" on the front end. Remove the lifecycle
strip on the Case record's Overview. Bring in the damage selector chosen from
v27, the area selector. Note the choices. Say what from v26 and v27 never got
done.

### P1 · The refined mark

- **Changed.** The rail's brand slot and the navless frame's brand use
  `pegasus-mark-refined` (128 px in the rail, 256 px on the navless card) in
  place of `pegasus-lockup.png` and `logo_no_margin.png`.
- **Choice.** The mark only, no wordmark inside the image: the rail already
  sets "PEGASUS / Case management" in type beside it, and the live lockup's
  own tiny "Pegasus" lettering is illegible at 52 px.
- **Stage 2.** Replace both image files under `wwwroot/images/` and the marks
  README. No CSS change is needed in the rail; the navless card wants the
  image at 96 px square.
- **Open.** The favicon is not in the capture and was not changed.

### P2 · Status colour by meaning

- **Changed.** A chip's tone now follows what its words mean:

  | Tone | Meaning | Labels |
  | --- | --- | --- |
  | Green | an operation or outcome that succeeded | Case created, Linked, Linked to case, E-mail linked, Reply linked, Complete, Completed, Sent, Report sent, Delivered, Saved, Stored, Document stored, Approved, Accepted, Applied, Resolved, Registered, Connected, Configured, Succeeded, Passed, Roadworthy |
  | Red | one that did not | anything Failed or a Failure, Could not be read, Case not created, Unavailable, Denied, Rejected, Refused, Blocked, Error, Overdue, Lease lost, Conflict, Unroadworthy, Storage failed, Lookup failed |
  | Amber | waiting on someone or something | Query, Creating case, Not yet processed, Awaiting ..., Pending, Draft, Not configured, Password change required, Overridden, Today, Chase due, "...: preparing" |
  | Navy | in hand | unchanged: Review, With Engineer, Open, Editing, Active, Enabled |
  | Neutral | settled, nothing to act on | Cancelled, Closed, Archived, Dismissed, Created in error, Staff-closed, Disabled, No recorded activity |

- **Why "Case created" was grey.** The Inbox passes that label to
  `Shared/_StatusChip.cshtml`, whose tone table has no entry for it, so it
  falls through to neutral. The intake log tones the same words green through
  its own function. One table should own this.
- **Choices.** Active and Enabled stay navy: they are standing states, not
  outcomes, and a Staff accounts page of green chips would spend green on
  nothing. Repairable stays blue, since an outcome category is neither success
  nor failure. "Could not be read" moves from amber to red and "Unavailable"
  from neutral to red, because both are things that did not work. This widens
  the design authority's rule that green is "confirmed completion only".
- **Stage 2.** Add the labels to `_StatusChip.cshtml`'s table, retire the
  per-page tone functions in favour of it, and amend the tone paragraph in
  `design/README.md`.
- **Limit.** The fixture shows few of these labels. The rule is in the layer
  for every page; the proof on screen is the intake log, the Figures chips
  and the upload outcomes.

### P3 · "Provider" never appears

- **Changed.** "Provider cancelled" and "Provider cancellation" read
  "Cancelled" (Search state filter, the close-Case outcome list). The Triage
  tab's column and fact read "Principal". The Inbox category reads "Principal
  chasing for update". Any other visible "provider" becomes "principal".
- **Choice.** "Principal" is the glossary's word (CONTEXT.md lists "Work
  Provider" under Avoid), so the replacement is that, not a new word.
- **Stage 2.** `OperatorLabels` and `_StatusChip` keys; the enum
  `ProviderCancelled` and history keys such as
  `provider_inspection_mode_applied` are internal and stay.
- **Open.** "Principal chasing for update" sits beside "Client chasing for
  update" in the same list. If those two mean the same sender, one should go.

### P4 · Lifecycle strip removed

- **Changed.** The Not ready / Review / With Engineer / Complete strip at the
  top of Overview is gone, in read and in the edit session.
- **Choice.** Nothing replaces it. The ribbon's state chip already says where
  the Case is, and Lifecycle actions (Return to Review and the rest) keep their
  own panel in the edit session.
- **Stage 2.** Remove the `.stepper` block from `_CaseOverview.cshtml` and its
  CSS.

### P5 · Damage: areas, not panels

- **Changed.** v27's variant C with v27 section 12's eight areas. While
  editing, press and drag anywhere on the vehicle to size a disc; drag a disc
  to move it; each disc is one row with a severity, a note and a remove
  button. The row names the areas under the disc ("LH Rear, Rear") and nothing
  else. Impact location, Impact severity and the count derive from the discs.
  Faint dashed guides show the areas while editing and a readout names the
  area under the pointer. Read mode shows the discs and ignores the pointer.
  The live panel clicker, its numbered markers and its zone fills are off.
- **Choices, following v27's open items.**
  - G4: a disc on the roof's centre line falls to the nearer side. No "Roof".
  - G5: LH / RH as the operator wrote them, not Left / Right.
  - G6: Underside, Interior and Mechanical stay as they are.
  - G7: the list heading reads "Recorded areas".
  - G2: a disc spanning several areas is one recorded damage with several areas.
  - The fixture's two recorded zones (Boot / tailgate, Rear N/S corner) open as
    one disc over the left rear, because that is how this model would have
    recorded them.
- **Stage 2, and it is not small.** The record stores a zone code per impact
  today. Areas need the disc (centre and radius, as a fraction of the plan)
  stored with the derived `BroadDamageZones` codes, which Core already has.
  FRD-06 (damage record), FRD-12 (the Plan clicker sentence) and FRD-11 (the
  report's marked diagram) each need a sentence, and the report diagram needs
  to draw discs. v27 G1 (is the disc stored, or only the areas) is still the
  operator's call; the mockup assumes stored.
- **Open.** Whether the report's narrative says "LH Rear" or "left rear".

### P6 · Issues C to H of the baseline notes

| Item | What was done |
| --- | --- |
| C · saving many fields is refused | Not a mockup matter. It is a code defect on `904903fd1`: the history reason lists every changed field and overflows `CaseHistory.Reason`. Needs a ticket: widen or summarise the reason ("12 fields changed"), and tell the operator why a save was refused. |
| D · Access denied in the full shell | Drawn in the navless frame with the rest of the error family, with "Return to Work Centre". Stage 2: `Layout = "Shared/_LayoutAuth"` on `AccessDenied.cshtml`, and the comment on its page model that argues for the shell goes. |
| E · "Updated" printed twice | The line above the metric strip is gone; the header keeps it. New cases and AI jobs keep their own refresh time in their panel heads, because they refresh separately. |
| F · Due date wrapping to four lines | Date cells in the Cases table never wrap (the live `.nowrap` class, applied to them). |
| G · "Sept" and "Sep" | "Sep" everywhere. Stage 2: one date formatter for the record. |
| H · Query has no tone | Amber, as waiting on someone (P2). |
| H · hub icons differ from the nav | Each hub card takes its nav item's icon (Service health, Reports). |
| H · "Lease expires HH:mm" on AI jobs | Not in the capture (no AI jobs in the fixture), so not drawn. Needs the operator's wording; "lease" is internal vocabulary. |
| H · manual Create Case offers no Audit type | Left alone. It follows the rule that an Audit Case comes from Create audit; explaining that on the form needs operator-approved copy. |
| H · no Administration page uses a mark | Left alone. With P1 the question is whether the panel marks under `images/marks` are wanted at all; the design README still documents them. |

### Carried over from v26 and v27

Asked: "are there any items from v27 or v26 that we didn't get done?"

- **v26** was approved for Stage 2 on 13 September and built. Nothing of v26
  was found missing from the pages captured here: Scroll is the default, the
  Plan clicker shipped, there is no Take over on a colleague's session and no
  Case tasks panel, the three valuation cards and the open-records strip are
  live. v26 items that the fixture cannot show (Create audit, the crop stored
  as a rectangle) were not checked.
- **v27** never went to Stage 2. Its sign-off list was never settled, and none
  of its proposals is in the live source:
  - the damage selector (now P5) and the refined mark (now P1);
  - all the "nothing in the way" proposals of its section 15: composed
    sentences on the record, CAP as a guide source, the salvage slider of the
    Engineer's Value, the unroadworthy reason bank, Delete all lines and Undo on
    the estimate, amber off-pattern cells, regional uplift, import provenance
    chips, the richer Compare with a printable sheet, the Supplementary panel,
    the recipients address book, the Attach row, re-send file naming and
    "supersedes" wording, Report / Fee tabs, ribbon badges, the nine-section
    map, the placements, click-to-include images, a Queries panel on Notes,
    tick-button decisions, sign-off following the assigned Engineer, report
    date stamped on generate, and the Report wording editor;
  - its "needs a decision first" list, untouched by design.
- None of those is in v28 yet apart from P1 and P5. They can be added to the
  proposals layer one at a time.

### Evidence

`node v28-build/selfcheck.mjs`: `RESULT {"fail":[],"okCount":1361}`. It now
also checks, on every state, that the baseline view carries no proposal change
and that each proposal holds with the layer on. Twelve proposal shots at three
widths are in `v28-shots/` as `p01` to `p12`; the baseline shots are unchanged.
The parity run against the running application was not repeated, because the
captured pages did not change.

## 18 September 2026 — second pass

Work now happens on `dev` in the main checkout; the worktree is gone (operator,
18 September: "stop working in a worktree").

**Asked for.** The Lifecycle actions container "shouldn't exist"; Return to
Review should be a button under the Actions dropdown. The damage selector
needs a Reset button. Stop showing Cazana as "not connected": display it the
same as the others, which are also not connected. Get valuation needs to look
like a button, in the bottom centre of the box. The Estimate section is missing
"our Compare option"; rename "Estimate PDF" to "Print Estimate"; both under
More.

### P7 · No Lifecycle actions container

- **Changed.** The "Lifecycle actions" panel on Overview is gone.
- **Found.** Return to Review is already an item of the ribbon's Actions menu
  in the live page, above Place on Hold, Correct principal and Close case. The
  panel was a second copy of it, so nothing had to move.
- **Stage 2.** Remove the `data-lifecycle-actions` block from
  `_CaseOverview.cshtml`. With P4 that leaves Overview opening straight on the
  Case, Principal and Claimant cards.

### P5 · Reset added to the damage selector

- **Changed.** A Reset button sits under the diagram while editing.
- **Choice.** Reset returns the discs to what the record held when the page
  opened, not to an empty vehicle. That matches what Reset means on the
  Administration rows (back to the saved values), and an empty vehicle is one
  click per row away with the remove button. If Reset should clear everything
  instead, it is a one-line change.

### P8 · Valuation cards

- **Changed.** While editing, Cazana is an entry card like Glass's, Brego and
  Super CAP: Retail, Trade, Guide month, Mileage, Get valuation, Save. The
  greyed "Cazana is not connected" card is gone. In read mode Cazana shows
  nothing until a guide is recorded, which is what the other three do.
- **Changed.** Get valuation leaves the card heading, where it was a borderless
  text link, and becomes an ordinary small button at the bottom centre of each
  card. Save stays at the bottom right.
- **Choices.** The label keeps the live wording and casing, "Get valuation".
  The AI market research button beside the Valuation month is untouched.
- **Stage 2.** `_CaseValuation.cshtml` renders Cazana through the same entry
  card loop as the other sources and drops the seam card; Core already
  names Cazana as a source but keeps it a disabled seam (`Valuations.cs`), so it
  needs to accept a hand-entered guide like the other three;
  `.entry-actions` becomes a three-column row in `case-workspace.css`.
- **Open.** What Get valuation does for a source with no connection. Today
  Brego and Super CAP post to the server like Glass's; whatever they answer,
  Cazana would answer the same.

### P9 · Estimate: Print Estimate and Compare under More

- **Found.** Compare is live, but the page renders it only when a Case holds
  two or more estimates, and renders the More menu only when it has something
  to hold. The fixture had one estimate, so neither showed. `enrich.mjs` now
  enters a second estimate ("Example Bodyshop supplementary") through the live
  editor, the record was recaptured, and the real Compare link and the real
  Compare estimates dialog are in the baseline.
- **Changed.** "Estimate PDF" is renamed "Print Estimate" and moves from its
  own row above the estimate into the More menu, above Compare. The row it
  leaves is removed when it holds nothing else. While editing, More reads New
  estimate, Print Estimate, Compare.
- **Choices.** "Print Estimate" is cased as the operator wrote it, although the
  neighbouring items are sentence case ("New estimate"). The icon changes from
  an eye to a document, since it no longer says "view".
- **Open.** With one estimate, live shows no Compare at all. The mockup keeps
  that rule: More then holds Print Estimate alone. Say if Compare should show
  disabled instead. Also whether Print Estimate should open the print dialog
  rather than the PDF preview it opens today.
- **Stage 2.** `_CaseEstimate.cshtml`: render More whenever an estimate exists,
  move the document link into it, rename the label in `EstimateLabels`.

### Evidence

Both fixture hosts were restarted on a fresh database, the Case was worked
through the live edit session again and all 73 states were recaptured, so the
record's ids changed and every screenshot was retaken. Sixteen proposal shots
(`p01` to `p16`); `p13` to `p16` are new: valuation read and editing, the More
menu open, the Compare dialog. The self-check result is in the discussion log.

## 18 September 2026 — third pass

**Asked for.** Remove the "Use estimate · No repairer VAT status recorded"
chip. The Upload received page "looks like absolute trash and needs a massive
amount of work". Rulings on the open items. All nineteen v27 features
implemented, "carefully so that our existing design isn't broken but they fit
in", taking `pegasus_case_dashboard_2026-09-15.html` (now in the v28 folder) as
the conceptual source.

### Rulings applied

| Item | Ruling (operator, 18 September) | Done |
| --- | --- | --- |
| P2 | Active and Enabled are green; "Could not be read" and "Unavailable" are red | Active and Enabled moved from navy to green; the two reds were already in |
| P3 | Combine both chasing categories into "Update Request" | One Inbox category, "In-progress case · Update Request"; the second option is gone |
| P5 | "LH Rear etc" | LH / RH stand, on the record and in the composed Nature of incident sentence |
| P8 | Get valuation on an unconnected source "says Error and to contact admin" | Brego, Super CAP, CAP and Cazana answer with a red notice in the card: "Error. Contact an administrator." Glass's is left to the application |
| P9 | Compare shows greyed out with one estimate; Print opens the PDF preview, which can be printed from | Compare is a disabled item in More until a second estimate exists; Print Estimate keeps the preview behaviour |
| P6 · C | "Will plan for issue C later, just note it here that it's an issue" | Noted below; nothing drawn |
| P6 · AI jobs | "Use that wording for the AI jobs" | Read as: keep the live wording, "Lease expires HH:mm". **Check this reading**; if it meant something else, say what the line should read |
| P6 · marks | "Which marks are unused?" | All nine panel marks, see below |

**Issue C, noted as asked.** On `904903fd1`, saving a Case with many changed
fields is refused: the history reason lists every changed field and overflows
`CaseHistory.Reason`, and the operator sees only "The case action was not
applied". To be planned later.

**Unused marks.** Under `src/Pegasus.Web/wwwroot/images/marks/`, no page,
partial, stylesheet or script references `access.png`, `accounts.png`,
`automation.png`, `checkmark.png`, `configuration.png`, `mailboxes.png`,
`organisations.png`, `principals.png` or `roles.png`. Only
`pegasus-lockup.png` (the rail) and `images/logo_no_margin.png` (the navless
frame) are used, and P1 replaces both.

**New copy for approval.** "Error. Contact an administrator." (P8) and
"Update Request" (P3) are the operator's words. Everything else in this pass
reuses live wording or the reference file's.

### P10 · The Use estimate lock chip

- **Changed.** The dashed chip "Use estimate · No repairer VAT status
  recorded" is gone.
- **Choice.** A greyed-out Use estimate button stands where it was, the same
  treatment P9 gives Compare, so the action stays findable. The reason is still
  on the record: P12's "Drives the calculation" line says the estimate cannot
  be used until a repairer VAT status is recorded. Say if the button should go
  too.

### P11 · Upload received, reworked

- **What was wrong.** The destructive Discard panel led the page and was open;
  thumbnails were squeezed into a 28 px icon column; the two fields ran the
  full page width; Discard submission and Cancel were unstyled text; the
  confirmation tick box sat above its own sentence.
- **Changed.** Same content, same words. Files in this submission comes first,
  on the left, as cards with a 132 px thumbnail, the file name and the outcome
  beside it, and a file count in the head. This submission sits beside it:
  registration as a short monospace field, Reason, then Create a vehicle-image
  case with Cancel on the same row and Add to an existing case under it.
  Discard this submission is last, folded shut with the live plus and minus
  marker; open, its tick box sits inline with its sentence and the button is
  the danger variant. Below 1100 px the two columns stack.
- **Stage 2.** `UploadGroupStatus.cshtml` markup order and a page stylesheet;
  no wording or handler changes. The single-file status page
  (`UploadStatus.cshtml`) is not in the capture and will want the same
  treatment.

### The v27 features, P12 to P30

Built in `assets/mock/proposals-record.js` from the record's own parts (`fc`,
`fv`, `fi`, `sub-panel`, `dec`, `src-tag`, `status`, `menu`, `tabs`), so they
read as part of the page. Each is a switch: `?skip=P14`.

| Id | Feature | Where it sits | Notes |
| --- | --- | --- | --- |
| P12 | Composed sentences | Matter line (Case details); Assessment method and Recovery and storage charges (Inspection); Mileage statement and Pre-incident condition statement (Vehicle); Drives the calculation (Estimate); What the report carries (Valuation) | Read-only cells in the live "derived" style; each follows its fields as they are typed |
| P13 | CAP as a guide source | A fourth entry card after Super CAP | Read mode shows it only once it holds a guide, as live does for the others |
| P14 | Salvage slider | Under Salvage value | 0 to 100 % of the Engineer's Value with 5, 10, 15, 20, 25 % snaps; amount and share are one fact, last touch wins. Shows when the outcome is Total loss |
| P15 | Reason bank | Under Unroadworthy reason | Click inserts, joined with "and"; "Save this wording to the bank" adds the typed reason. Shows when Unroadworthy |
| P16 | Delete all lines, Undo | Beside Add line on an editable draft | Delete all lines asks first; removing one line or all raises an Undo toast for eight seconds |
| P17 | Regional uplift | Last cell of the estimate header | + 15 % on the labour rate; the chip reads "Suggested · Repairer (CR0)" when the repairer, claimant or storage postcode is in London or the Home Counties |
| P18 | Import provenance chips | A line's Source chip | "imported · AX" or "imported · GL". The fixture's lines are all hand-entered, so nothing shows here; the rule is in the layer |
| P19 | Richer Compare | In the live Compare estimates dialog, under its totals table | From and To; then the net difference, counts, a per-line table with added, changed and removed rows coloured and changed cells bold, and Print comparison sheet |
| P20 | Supplementary | A panel under the estimate tabs | Empty until "Changes vs" has a version; then the differences, "Explain the change on the report" with a reason, and the composed "Following receipt of…" paragraph |
| P21 | Recipients address book | The delivery form's To and Cc | To opens a list of Principal, this case and CE addresses; Cc is chips with one-click suggestions and typed entry |
| P22 | Attach row | The delivery form | Report, Fee note, Breakdown, Images; the prepared block lists what was ticked |
| P23 | Re-send naming | The delivery form | File name with one more "." per re-send; after a send the message says the report supersedes the earlier one |
| P24 | Report and Fee tabs | Top of the Report section | Fee shows the fee note from the agreed fee with VAT and total |
| P25 | Ribbon badges | The ribbon's chip group | Outcome and roadworthiness move up from the Figures aside; "Repairs N% of value" joins them once a current estimate gives a repair cost (green under 66 %, amber to 79 %, red from 80 %) |
| P26 | Nine-section map | The section row and the record | Case details, Claim, Inspection details, Vehicle (Damage and Valuation inside it), Estimate, Decisions, Report, Images, Notes |
| P27 | Click to include | Images tab while editing | A tile toggles its in-report tick instead of opening the viewer; the Report section's count follows |
| P28 | Queries | A panel on Notes, above the history | Live empty-state style |
| P29 | Tick-button decisions | Outcome, Salvage category, Roadworthiness | The select stays the posted control; the buttons drive it, so the live show-and-hide rules still fire |
| P30 | Report wording | Report section, above Images in report | Every narrative block in print order, composed from fields; while editing: reword, rename, remove, bring back, move, add a paragraph, recompose |

**Choices made to keep the existing design intact.**

- Nothing new was styled where a live pattern existed. Composed sentences use
  the live derived cell; panels are live sub-panels; chips are live `src-tag`
  and `status`; the delivery form is the live `recip-form` markup from
  `_CaseReport.cshtml`, extended rather than replaced.
- Claim's claimant cells run four across, as cells do in every other
  full-width section, rather than keeping the one-column stack they had inside
  Overview's third-width card.
- Images becomes the first and selected tab of the renamed section.
- The wording blocks move with up and down buttons, not drag, so the control is
  reachable from the keyboard. v27 used drag.
- One-line composed sentences do not reserve a paragraph's height.

**Costs seen, for the decision.**

- P25 crowds the ribbon at 1440: with three chips plus Editing, the facts
  truncate ("CASE WORKSPACE · …", "development…"). Either the badges or the
  facts give way. v27 found the same.
- P26 nests Damage and Valuation inside Vehicle, so they lose their own head
  tools and section link, and Vehicle becomes a very long section.
- P30 crosses FRD-11's fixed-template rule (v27 item I2). P13 needs a guide
  provider decision. P14, P15, P17, P21 to P23 each need a small Core or
  settings home (the bank, the uplift rule, the address book).
- P21 to P23 are drawn on a delivery form the fixture cannot reach, because no
  report is generated there. The markup is copied from the live partial, but
  it has not been compared with a running page.
- P19 and P20 read the two fixture estimates from a table inside the layer,
  because a captured page carries only the selected estimate's lines. The
  figures are the ones `enrich.mjs` enters.

**Not brought over.** v27's `offpattern`, `place`, `signoff` and `reportdate`
switches were not in the list asked for and are not built.

### Fixture

An Engineer's Value is now applied through the live Apply as Engineer's Value
button (£9,200.00 from the Glass's guide), so Settlement, the Figures aside,
the salvage slider and the composed sentences have a figure. All 73 states
were recaptured.

### Evidence

The self-check gained assertions for P3, P10, P11 and, on every record state,
that each record proposal built something, that the section row reads the nine
names in order, and that no composed sentence is empty. Result in the
discussion log. Twenty-six proposal shots, `p01` to `p26`.

## 18 September 2026 — fourth pass

**Asked for.** The Vehicle head "ended up too large". No Basis radio: "we can
just click to select which valuation we are using". Delete all lines and Add
line are stacked, not side by side. The estimate "never got the slider that
adjusts the costs down" (reference file, section 6). The Use estimate lock
chip "seems like nonsense". No Estimate notes. A "Contract repair agreed"
tick box. No Repair days; no Estimate name ("can just double click on estimate
tab to rename it"). Labour rate card and labour rate "seem to be the same
thing so can be combined". The Glass's button "and presumably all the Glass's
states" are missing. "We rename it to Repair Spec".

### P6-I · The Vehicle head

- **Found.** It is a live defect, not something the proposals did: the head is
  108 px against 51 px for every other section, in the baseline too. The "Not
  yet looked up" text carries the class `empty`, and the global rule
  `.empty{padding:36px 18px}`, meant for empty-state blocks, pads it.
- **Changed.** A panel head's `meta.empty` takes no padding. The head is 51 px.
- **Stage 2.** Scope the `.empty` block rule, or stop giving that span the
  class.

### P8 · A valuation card is chosen by clicking it

- **Changed.** The Basis radio and its label are gone. Clicking a card, or
  pressing Enter or Space on it, makes it the basis; the live navy outline
  (`.sel`) still marks the choice and the live calculation still follows,
  because the hidden radio is what changes. A click inside a field or on a
  button does not select, and a card with no retail figure cannot be chosen.
- **Stage 2.** Keep the radio for the form post and no-script use, visually
  hidden, with the card as its label.

### P16 · Delete all lines beside Add line

- **Found.** My button missed its row in the third pass: the row is
  `div.cluster.est-add-line`, not a `.button-row`, so it fell back to a row of
  its own above. Now in the same row, right-aligned, Delete all lines first.

### P10 · Use estimate, revised

- **Changed.** The third pass kept a greyed-out Use estimate. The operator
  called the gate itself nonsense, so Use estimate is now simply available
  whatever the repairer VAT status. The composed line under the header reads
  "No repairer VAT status recorded: no VAT is charged." instead of saying the
  estimate cannot be used.
- **Stage 2.** Drop the VAT-status condition from `UseEstimateCondition`; an
  unknown status then means the totals carry no VAT, which the rollup already
  says ("VAT 20 % on nothing").

### The Repair Spec rework, P31 to P35

| Id | Change | Notes |
| --- | --- | --- |
| P31 | The section and its link are named "Repair Spec" | Settlement's "From current estimate", More's "New estimate" and "Print Estimate" keep their words; say if they should follow |
| P32 | Estimate notes, Repair days and Estimate name are gone from the header | The name is changed by double-clicking the selected tab: it becomes editable in place, Enter or clicking away keeps it, Escape puts it back. The three controls stay in the form, hidden, so nothing is lost on save |
| P33 | One labour rate control | The rate card and the rate were two cells for one fact. One cell, "Labour rate (£/h)": the card on the left chooses the figure, the figure on the right can be typed, and typing sets the card to "Keep entered rate". Regional uplift (P17) stays its own cell |
| P34 | Target % of value | A bar in the live estimate-bar pattern: slider, typed %, a readout, floors, Apply, Remove scaling. One factor lowers part prices and paint materials, and the labour rate, each down to its floor (labour £/h, prices %). Hours are never touched. Moving the slider previews: the changed cells turn amber, the rollup and the readout follow ("£916.00 → £552.00 (6.0%) · prices ×0.51"). Apply keeps it and shows a Scaled chip; Remove scaling puts the specification back exactly as it was |
| P35 | Contract repair agreed | A bar with the tick box and "agreed total sum £". Ticking seeds the sum from the specification and sets Outcome to Contract repair in Decisions (un-ticking puts the previous outcome back; choosing Contract repair in Decisions ticks the box). Typing a different sum rescales the specification to it through P34. A chip says when the specification is above or below the agreed sum, and the composed sentence reads "A contract repair has been agreed for the total sum of … Costs cannot increase above this figure." |

**Choices.**

- The slider runs from the specification as estimated down to what the floors
  allow. On this fixture that is 10 % down to 6 % of the Engineer's Value,
  because a £916 estimate against a £9,200 value leaves little room; a real
  total loss has far more travel.
- The floors default to £50 an hour and 50 % of price, the reference file's
  figures. They are the operator's to set.
- Scaling needs an Engineer's Value; without one the bar is disabled and says
  "Apply in Valuation", the live phrase.

**This crosses settled rules, as v27 recorded.** FRD-11 makes the Core-computed
total the contract cap and Send to AI the only target-% route. P34 and P35 put
both on the record under the Engineer's hand. Stage 2 needs that rule changed
first, a stored "natural" specification to return to, and a history event for
each scale and each agreed sum.

### P36 · The Glass's slot and its states

- **Found.** Not missing from the design: missing from the fixture. The
  application renders the Glass's slot only for an account with an enabled
  Glass's login, and the local host has none, so the capture has no Glass's at
  all.
- **Changed.** Drawn with the markup, chip tones and wording of
  `_CaseEstimate.cshtml` and `GlassLabels`, so it is what live shows rather
  than a design of mine. While editing, the head carries **Glass's** (account
  free) or **Resume** (session held). `?glass=` picks a session state, and the
  family file lists them:
  - `open` and `recording`: the session line "Glass's session · Open", started
    time, Close session (not while recording);
  - `waiting`: amber, "The Glass's estimate is held. Not yet recorded.", with
    Complete import while editing and "Available while editing" in read;
  - `failed`: red chip, the failure code, "The Glass's estimate was not
    recorded.";
  - `unknown`: "The Glass's session result is not yet known.";
  - `recorded`: the green sentence "The Glass's estimate was recorded as a
    draft.";
  - `cancelled`: "The Glass's session was closed."
  Close session opens the live guarded form: Reason, the confirmation tick,
  the consequence sentence, the red button.
- **Limit.** Because it is drawn, not captured, it is outside the parity check.
  Enabling a Glass's login on the fixture account would let the real thing be
  captured instead.

### Evidence

`LIVE=1 node v28-build/selfcheck.mjs`, both fixture hosts running:
`RESULT {"fail":[],"okCount":1970}` — 73 states, 35 presets, 65 states compared
with the running application, no linked route left uncaptured. 142 shots at
three widths, no page errors. New
assertions on every record state: the title reads Repair Spec; no Estimate
notes, name or days; no Basis radio; the Vehicle head under 70 px; Use estimate
not greyed; while editing, one labour rate cell, Delete all lines level with Add
line, and P34, P35 and P36 present. Thirty-four proposal shots; `p27` to `p34`
are new. Both hosts were restarted on a fresh database for the parity run, so
the Case was enriched again and all 73 states recaptured.

## 19 September 2026 — P29 corrected

**Asked for.** "Under decisions for roadworthy and repairable, tickboxes are
the wrong choice. tickbox = multiple choice. Radio buttons are the standard
convention for where only one choice is possible."

- **Agreed, and it was my error.** Outcome, Salvage category and Roadworthiness
  each take one answer: the control underneath is a single-select. Drawing them
  as tick boxes said "choose any number of these", which is not what they mean.
  P29 came from v27, where they were called tick rows; I carried the shape over
  without questioning it.
- **Changed.** Each is a radio group: a round control with a filled dot when
  chosen, `role="radiogroup"` with `role="radio"` children and `aria-checked`,
  one tab stop per group, and arrow keys moving and choosing within it, which
  is how a radio group behaves. The select stays the control the form posts.
- **Choice.** A radio cannot un-check itself, so the select's empty option is
  offered as its own radio, **Not recorded**, the record's own word for an
  unset value. Before this, clicking the chosen tick box cleared it, which is
  the behaviour a radio group must not have.
- **Checked elsewhere.** Every other tick box in the layer is a genuine
  yes-or-no or a real multiple choice, so each stays: Contract repair agreed
  (P35), Regional uplift (P17), Attach's four files (P22), an image's
  in-report tick (P27), the Upload discard confirmation (P11), and the live
  VAT-charged-on row.
- **Stage 2.** The same rule: a single-select becomes a radio group, never tick
  boxes; keep the select for the post and for no-script use.

### Evidence

`node v28-build/selfcheck.mjs`: `RESULT {"fail":[],"okCount":1844}`. The
self-check now asserts, on every editing record state, that no tick box is used
for a single choice and that all three decision groups are radio groups with
exactly one checked option and one tab stop. Shot `p21` retaken.

## 20 September 2026 — fifth pass

**Asked for.** A deep pass through the reference file against the notes and
the mockup, with everything it does implemented. "Breakdown" is the same
document as Print Estimate. The report composer needs draggable text. No
subagents. Rulings: of the nine behaviours that cross a settled rule, only the
product type select is wanted; the edit session stays (no autosave); the
fixture may be re-seeded with more images and recaptured.

**Found.** The copy of the reference file in this folder is not the build the
v27 inventory describes. The v27 inventory and differences list read a
2,705-line build; this copy is 3,201 lines and diverges from line 401. The
five hundred lines added after 16 September (a working image workflow, a
produce-PDF preview with three documents, sub-card folding, per-browser view
state, autosave and every act logged) were never inventoried, so they never
reached the "nothing in the way" list, the v28 proposals or this log. Four
v27 switches had been dropped in v28 (`offpattern`, `place`, `signoff`,
`reportdate`). P34's price floor was 50 % where the file says 65 %. Two
reference behaviours turned out to be live already: the section link follows
the reader as the page scrolls (`case-workspace.js` `spy()`), and the sections'
fold state is remembered in the `pegasus-collapsed` cookie.

Everything below is in a third layer file, `assets/mock/proposals-record-2.js`
and `.css`, loaded by the shim after `proposals-record.js`, because that file
had reached a thousand lines. Ids P37 to P49; the corrections keep their ids.

### Corrections

| Item | Changed |
| --- | --- |
| P34 | The price floor input reads 65, the file's figure; the labour floor of £50 was right. The fourth-pass entry above that called 50 "the reference file's figures" was wrong. Apply and Remove scaling now freeze a version (P43) and write a System note (P44) |
| P30 | Each wording block has a drag grip; dropping a block on another puts it above that one, dropping it on the add row sends it to the end, and a drop mark shows the target. The up and down buttons stay for the keyboard. A drop is carried out by pressing the block's own move buttons, so the order P30 holds is the one that prints. A read-only "Repair reserve (computed)" cell on Decisions shows the repair cost rounded up to the next £50 for a repairable outcome, beside the live typed field, whose placeholder takes the figure |
| P29 | When the outcome leaves Total loss, a "Salvage: not applicable" line explains why the rows are gone, and the change of applicability is logged |
| P22 | "Breakdown" reads "Repair Spec" and its tooltip names it as the document Print Estimate opens; Report, Fee note and Images carry tooltips |

### The four dropped switches, P37 to P40

- **P37 · Off-pattern cells.** While editing, a unit price on a Repair, R&I,
  Paint or Blend line, panel hours on a Paint or Blend line, or paint hours on
  any other operation reads amber (`viol`) with the tooltip "Outside the usual
  pattern for this operation; imported as received. Zero it or change the
  operation to normalise." The roll-up gains "Off-pattern items (treated as
  specialist)" while the amount is non-zero. Nothing is cleared. Stage 2: Core
  already computes `OffPattern` and the anomaly list in `Estimates.cs`; the
  page only has to show them.
- **P38 · Placements.** Claimant VAT status moves from Settlement to the
  Claim section's claimant card; the three report content switches (and the
  read-mode Report content cell) sit in an "On the report" card on Valuation;
  Unrelated damage and its deduction sit in a card on Vehicle; Sign-off
  Engineer sits beside a read-only "Assigned engineer" cell on Case details.
  Each moved control keeps its id, name and form.
- **P39 · Sign-off follows the Engineer.** When the ribbon's Engineer
  changes, the Sign-off Engineer select takes the same person if they are an
  option. The fixture account is not flagged for sign-off, so the select is
  empty and the rule cannot be shown; noted in section 9.
- **P40 · Report date on generate.** Generate report, and Produce PDF, fill
  an empty Report date with today's date; a recorded date is left alone.

### Never inventoried, P41 to P47

- **P41 · Working images.** While editing, every tile on the Images tab has
  Rotate (90° steps, the live rotation badge shows the angle), Full page
  (a navy chip; the image prints on its own page), Remove (the tile is hidden
  and an eight-second Undo toast follows) and a grip that drags the tile to a
  new place; the order is mirrored into the Report section's images strip and
  the count reads "n of m in report". "Add images…" and dropping a file on
  the grid add a tile from a local file, read into a data URL and never
  posted. The live viewer gains Full page and Remove beside Rotate, Crop and
  Include. A note under the grid reads "Included images print two per page; a
  Full page image takes its own page."
- **P42 · Produce PDF.** Beside Generate report, "Produce PDF…" opens a
  preview dialog showing the report as it will print: letterhead, details
  table, the P30 wording blocks in print order, the outcome's value boxes,
  the three worklists, "Prepared and authorised by", then the image sheet two
  per page with Full page images alone. The foot switches between Report,
  Repair Spec and Images and prints the one shown through the browser's print
  dialog with the file name preset. Print Estimate under More opens the same
  Repair Spec document. Produce PDF also stamps the report date (P40).
- **P43 · Versions.** "Versions (n)" under More, beside Compare, opens a
  dialog listing the working draft and every frozen version: number, when and
  who, how it came about, lines, rate, total inc VAT, a green "Sent on report"
  chip, Compare with current and Restore. Delete all lines, Apply, Remove
  scaling, Complete import, Restore and Prepare delivery freeze the outgoing
  draft; an unchanged draft reuses the last version rather than repeating it.
  Restore copies the version's lines, rate and materials into the draft. An
  origin line under the tabs reads, for the fixture, "Populated 06 May 2031
  11:30 by development-offline-administrator, entered by hand as "Example
  Bodyshop supplementary" · 1 line · labour rate £48.00/h". The two fixture
  estimates seed v1 and v2; v2 carries the Sent on report chip so the chip can
  be seen, although no report has been sent on this Case.
- **P44 · Every act logged.** Removing or restoring a line, deleting all
  lines, a labour rate or uplift change, a scale applied or removed, an
  agreed sum, a decision made by radio, every wording act (edit, rename,
  remove, add back, move, new paragraph, recompose), every image act, the
  product type and a version restore each prepend a System note to the Notes
  timeline in the live row style, attributed to the ribbon's Engineer.
- **P45 · Sub-cards fold.** Every sub-panel with a heading takes the live
  `data-collapse` contract with the key `case.<section>.<heading>` and the live
  toggle, bound through `pegasusMountBinders`, so it folds and is remembered
  like the sections.
- **P47 · Stale banner.** While the Report section shows its Stale notice, a
  one-line amber banner under the ribbon repeats it with "Go to Report". The
  fixture has no generated report, so `?demo=stale` draws it.

### Beyond the inventory

- **P48 · Materials per line.** A "Material £" column after Paint h, live on
  Paint, Blend, Repair, Specialist and Other lines and dashed on the rest; a
  line keeps a non-zero figure whatever its operation. The estimate's Paint
  materials field becomes the read-only column total and the roll-up line
  reads "Materials (column total)". The fixture's one line carries the
  estimate's £210. Flagged in section 9 as a Core change.
- **P49 · Product type.** On Case details while editing, a Product type
  select: Standard, Commercial (C.), Audit (A.), Audit, TL (AP.), Diminution
  (D.). A choice other than Standard shows as a ribbon chip and is logged.
  Read mode shows the cell. Drawn on the operator's ruling; it crosses FRD-01
  and ADR-0051 and section 9 says so.

**Not done, by ruling.** Take over and ask to release, inline padlocks, an
assigned-engineer select, Engineer's value in two places, the average-mileage
shortcut, import overwrite after a confirm, WhatsApp, implicit reopen after
send, autosave. Export, Import and Reset demo in the file are demo furniture.

**Choices.**

- The versions history is kept in the layer, seeded from the two fixture
  estimates, because a captured page carries one estimate's lines.
- A dropped wording block is moved by pressing P30's own buttons the right
  number of times, rather than a second order kept in the new file.
- P45 leaves a sub-card's own proposal mark alone (P28's Queries, P38's
  cards) and marks only the live cards it binds.
- The fixture's extra images are seeded by the visual host (three more
  `IAddCaseDocument` calls under fresh leases, in the ignored `artifacts/`
  folder), not through the live Upload page as planned: that route needs the
  intake worker, which the fixture does not compose. Two of the four are then
  tagged Close-up and Overview through the live tag picker by `enrich.mjs`.

**Stage 2, in one line each.** P37: show Core's anomalies on the page. P38:
Razor placement only. P39: one handler on the hand-off. P40: one line in the
generate handler. P41: a Full page flag and an order on `CaseAssetReportRole`,
remove as Not used with an undo window, upload from the record. P42: the
QuestPDF template gains the Repair Spec and Images documents and a preview
route. P43: an estimate version log with origin and the report's estimate
dependency shown. P44: history events for each act. P45: `data-collapse` on
the sub-panels. P47: one notice in the layout. P48 and P49: Core changes
after the rulings above.

### Evidence

`LIVE=1 node v28-build/selfcheck.mjs`, both fixture hosts running:
`RESULT {"fail":[],"okCount":2015}`, 73 states, 35 presets, 65 states at live
parity. 155 shots at three widths, no page errors; proposal shots `p35` to
`p47` are new. All 73 states were recaptured on a fresh fixture database with
four images, two of them in the report.

### P50 · One place for an image (same day)

**Asked for.** Images should not have three separate sections; and the
section renamed "Images" holds Documents and Correspondence as well.

- **Found.** Live itself shows an image in three places while editing: the
  Images tab, the Report section's "Images in report" strip, and its "Report
  image preparation" cards, which render once an image is in the report and so
  appeared with the re-seeded fixture. P26 had renamed the Files section
  "Images" after the reference, which was wrong for a section with three tabs.
- **Changed.** Each tile on the Images tab carries its report role (Not used,
  Close-up, Overview, Supporting) and, for Supporting, its order, beside the
  P41 tools; the include tick and the role are one fact. The count "n of m in
  report" sits on the tab's heading. The two Report-section panels are hidden.
  Read mode shows the role as a chip on the tile. P26 no longer renames the
  section: it reads "Files" with the Images tab first.
- **Stage 2.** `_CaseReport.cshtml` drops the images strip and the preparation
  partial; `_CaseImages.cshtml` gains the role and order controls the partial
  had, staged into the same `preparationEdits` fields.
- **Open.** Whether "Files" is the wanted name; "Evidence" is the live word on
  the Add button.

### P51 · Original report (same day)

**Asked for.** For Audit Cases a new section, Original report: who did the
report (a name from the corpus, such as Laird Assessors or Exclusive Vehicle
Assessors), the date it was done, and its outcome (roadworthiness and
repairable status).

- **Changed.** A section "Original report" after Claim, with its own link in
  the section row, shown only on an Audit Case (first keyed to P49, then to
  the Case type cell once P49 was dropped).
  While editing: Assessor (a select of the firms Core's third-party report
  profiles know: Laird Assessors, Exclusive Vehicle Assessors, Connexus Vehicle
  Assessors, Montgomery Assessors, and Other with a name field), Report date,
  Roadworthiness and Repairable status as radio groups in the P29 style, with
  Not recorded as the empty choice. Read mode shows the four cells. Every
  change is logged (P44). The fixture Case is an Inspection, so `?demo=audit`
  makes it an Audit and fills example values.
- **Choices.** The firm list is Core's, not typed here. The outcome is split
  into the two facts the operator named rather than one combined verdict,
  matching `ThirdPartyReportDamage.Roadworthiness` and `Outcome`.
- **Stage 2.** The cells map to `ThirdPartyReportIdentity.Issuer` and
  `ReportDate` and `ThirdPartyReportDamage.Roadworthiness` and `Outcome`,
  which the extraction already records when an original report is filed; a
  filed report fills them with a chip saying so. An FRD-16 sentence adds the
  section to the Audit record.

## 20 September 2026 — finalisation

**Asked for.** Finalise the build with a check for broken UI rules, and fold
in the two open PRs, before Stage 2.

- **P49 dropped.** The reading behind the product type select was wrong. The
  instruction types are Inspection, Inspection + Audit (Create audit on the
  Inspection Case makes the linked Audit Case) and standalone Audit, exactly
  as FRD-01 and ADR-0051 have them. Every Audit is `a.`; AP no longer exists;
  Commercial and Diminution routes stay deferred. `p49ProductType`, its shots,
  self-check facts and §9 open item are gone; `?demo=audit` now sets the
  fixture's Case type cell to Audit and its reference to `a.QDOS31001` so
  P51 can be shot. P51 reads the Case type cell.
- **Copy sweep.** Against `docs/design/README.md`: no guidance sentence, no
  banned word, one home per fact. Removed: the P41 note under the image grid;
  the P37 tooltip sentence (the cell now carries the label Off-pattern); the
  P29 salvage sentence (a label and a value); the P38 Assigned engineer cell
  (the ribbon owns it); the consequence clauses on P44 history rows; the
  P30 chips' word "composed" ("tracks fields", "from Estimate", "edited");
  the preview's inline rotate style (a `data-rot` attribute and a rule). The
  self-check now fails on a product type control, a repeated assigned
  Engineer, the image note, or a proposal element with a two-sentence
  tooltip or muted note.
- **Rulings taken for Stage 2** (in §9): P5, P32, P41, P31, P48, P30/P34/P35,
  P51.
- **The PRs.** PR 792 removes the Engineer role from every capability gate:
  sign-off eligibility (P38, P39) is enabled + flagged + signature. PR 793
  makes an estimate upload import in one act and removes Complete import: the
  captured estimate-import states and P36's Complete import are behind it,
  P43 freezes v1 at import. Neither changes a drawn surface beyond that.

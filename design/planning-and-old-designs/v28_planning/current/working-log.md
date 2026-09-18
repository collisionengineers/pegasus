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

# FRD-25: Repair estimates, imports and Glass's sessions

> Owner capabilities: ENG-01, EXT-06, EXT-09, EXT-12 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- A repair spec a staff member creates is the one in use at once: typed in,
  imported, or returned from Glass's. Each Case has at most one Current repair
  spec, and once an Inspection + Audit Case has its Audit, the Inspection and
  the Audit each have one.
- The Current repair spec stays editable while the Case is writable.
  **Use repair spec** switches back to another one.
- A Glass's estimate belongs to its repair spec. **Glass's** on that spec
  reopens the same estimate, and its saved return updates that spec. On any
  other spec it starts a new estimate. On a spec with lines made outside
  Pegasus it first asks whether to import that spec into Glass's; the
  import lands as a new Glass's spec beside it and opens in the same Glass's
  window (18 November 2026; R1171).
- An imported spec takes the Standard labour-rate card. AI and
  automation material stays a Draft until a staff member presses
  **Use repair spec**.
- An import is keyed by Case plus source hash. The same hash replays the same
  live spec.
- Estimate PDFs need readable embedded text. An unreadable or ambiguous
  source refuses the whole import, with no OCR and no partial spec.
- A Glass's answer that was lost stays `Unknown` and holds the account until
  the owning staff member closes the session with a reason. A session the
  Automation owns may be closed by any member of staff who can open the
  Case's assessment (18 November 2026; R1180).
- The Automation Actor runs Glass's sessions of its own, under a Glass's
  login the deployment holds for it. A session it finishes lands as a Draft
  (18 November 2026; R1177).
- An Engineer may let the Automation help in their own open Glass's session
  from the Case page. The Automation then adds and removes positions there;
  the Engineer's own Save & Exit brings the estimate back as any return
  does (18 November 2026; R1170, R1183 to R1185).
- Glass's launch and return run in the background. The Glass's window waits,
  then opens the estimator or reports the outcome on the Case.

## Purpose

This document owns the repair estimate on a Case: the canonical repair
specification and its versions, where estimate figures may come from, the
retained PDF estimate import, and interrupted Glass's sessions. It serves the
PRD outcomes for source-labelled Case data and a definitive Engineer report.
The Engineer's findings, damage, valuation and settlement are owned by
[FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md). The
inspection address and vehicle data are owned by
[FRD-06](frd-06-vehicle-and-engineering-evidence.md). Case states and edit
leases are owned by
[FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels) and
[FRD-14](frd-14-record-edit-leases.md#case-edit-lease).

## Behaviour

### Canonical repair specifications

**One Current spec.** A Case holds several live repair specifications and at
most one Current one, shared by all of the Case's report projections. A spec
a staff member types in, imports or brings back from Glass's becomes Current
when it is created (operator, 25 September 2026). The one it replaces stays
in the list, and **Use repair spec** switches back to it. An AI draft only
proposes: it stays a Draft until a staff member presses **Use repair spec**.
The Automation Actor may edit any live spec in place, the Current one
included, but never puts a spec in use (operator, 7 October 2026;
[FRD-10](frd-10-mcp-automation-and-actor-boundary.md#ai-job-and-estimate-tools)).
The Current spec stays editable while the Case is writable; each change marks
a generated report stale, and the numbered versions below keep the history.
**Discard** deletes a spec that is not Current, with its lines and numbered
versions; the Case history keeps who discarded it, when and why (operator,
11 October 2026). A report already generated or sent keeps its own copy.
Create audit copies every estimate, with its lines, into the Audit;
revision snapshots are not copied. The Current choice is copied after a
Total loss; after Repairable, Cash in lieu or Contract repair no copy is
Current and staff press **Use repair spec** on the Audit (operator,
6 November 2026; [R729](../rulings.md);
[FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).
From then on the Inspection and the Audit each have their own Current spec,
feeding their own report. Every estimate edit, import, Glass's return and
Use repair spec acts on the Audit's estimates; the Inspection's stay as its
report was sent.

**An estimate is imported onto the repair spec in view (v38 P3, operator,
12 October 2026).** A file a staff member imports from the Repair Spec section
lands on the spec the section shows; one imported from Files lands on the spec
in use; with no spec, the import creates one. The spec's lines and source are
replaced. Its name, labour-rate card, VAT and discounts are kept. The grid the
import replaces is frozen first as a numbered version, and the import is frozen
after it; the updated spec is Current. The spec then supplements the version
the last sent report used, else the grid replaced: its supplementary statement
is composed from the two with the reason "Supplementary estimate received",
not yet printed, and the section opens on it (the Supplementary panel above
the grid, with Dismiss, which clears what the spec supplements; the versions
keep the history). An Automation (MCP) import creates a spec, as before.

**A Glass's estimate belongs to its repair spec (operator, 6 October 2026).**
The return that makes a spec records on it the Glass's stock vehicle its
estimate stands on, with the estimate id and the vehicle facts it was proved
against. A later return for that vehicle updates the same spec, as an import
onto a spec does: the grid replaced is frozen first, the return is frozen
after it, the spec supplements the version the last sent report used, and the
updated spec is Current. Edits made in Pegasus since the last return are
overwritten. A spec discarded since is gone, so the return lands as a new spec
that belongs to the same estimate. Create audit copies the tie, so
the Audit's copy belongs to the same Glass's estimate. A duplicated spec
belongs to none. A spec made before this rule carries no tie and starts a new
estimate.

Each spec keeps its stable identity, ordered technical lines, source route,
source artifact identity, version and hash, the selected labour-rate card
version if one was selected, and its creating actor and time. Its totals are
calculated from its lines and header whenever they are read. A spec carries
at most 500 lines: there is no business bound, and the ceiling is what the Case
page form and the printout carry (9 November 2026, R767). A save, an AI draft
or an import over it is refused whole. Glass's,
Audatex PDF, JSON, an AI draft and manual entry are provenance routes, never
authorities. Obsolete development-state estimates need not be converted or
kept.

**Import is keyed by Case plus source hash.** A raw artifact imported through
either caller of the shared import command uses that key. The same Case with
the same hash is a replay that returns the existing live spec and leaves the
spec in use as it was; the page reports that the file was already imported
and shows that spec, and a Glass's return reports its estimate as recorded
rather than as the spec in use. A discarded spec is gone with its source, so
importing that file again creates a new one. A different artifact creates
the next spec. A Glass's return that updates its spec answers only for that
spec: the same export replays to it, and another spec holding that hash is
not it. The provider and parser are detected from the registered types; an
ambiguous artifact is refused, never guessed.

**Authority is checked twice.** Before reading a replay or parsing the source,
the command proves the typed actor, the current persisted Case version, and
the edit lease holder, token and expiry. Both that check and the final save
need an assessment-writable state: Not ready, Review, With Engineer or Report
sent ([FRD-13](frd-13-case-lifecycle-and-workflow.md#states-and-labels)). A file
occurrence must name the exact confirmed, non-removed document version; a
correctly paired historical version is still valid evidence. The new spec is
guarded again in the save transaction. A staff import becomes the Current
spec; an automation import through MCP stays a Draft.

**A Glass's import fills an empty VIN.** The VIN a Glass's export or
calculation sheet names fills the Case's VIN in the import's transaction,
only where the Case holds none
([FRD-06](frd-06-vehicle-and-engineering-evidence.md); operator, 7 October
2026).

**Glass's calculation PDFs.** These keep ordered rows from the sheet's
operation sections (Body, Mechanical, Electrical, Auxiliary work,
Upholstery, Dent repair, Mechatronics and Special work; at least one),
Paint and Additional costs, included-operation context, source guide codes,
unambiguous manufacturer part identities, notes and printed amounts. PDF
labour hours are already net of overlap and do not reuse the XML gross-time
conversion. The sheet's labour time unit, `1 Hour` or N work units to the
hour, converts every printed time to hours, and the XML's `TimeUnit` does the
same (60 is the Hour option). Parts and position appendices are evidence for existing rows,
never extra charges. Repeated printed charges and visibly clipped text stay
as printed. Whole-row, section and document reconciliation is required.
Section labour must equal the printed rate × section hours as the source
computes it; Additional costs prints no rate, so its labour is checked at
the Body rate, and its hours stay out of the Total Labour hours while its
cost is in the Total Labour cost. The document's Total Labour is the total
hours at the one rate rounded once, plus Additional costs, and may differ
from the sum of the section labours by a penny. A paint level the one table below does
not name refuses the import. Missing or ambiguous required evidence refuses
the whole import. Source rates and VAT do not select a Pegasus rate card or
decide a repairer's VAT status.

**Glass's rows, both routes (operator, 29 September 2026).** The Glass's XML
export and the calculation PDF land the same spec:

- An additional operation (the XML's `Extra costs`, which includes Glass's
  own set-up time; the PDF's Auxiliary work or Additional costs `EC` row) is
  a Specialist line. With hours it is priced by work units
  at the spec's rate; with none, its amount is a fixed Specialist sum.
- An included operation is a no-charge Other line, with neither hours nor a
  price, noted as included in its row. It is never a new part. The grid draws
  it as included: indented under the row it belongs to, its note across the
  figure columns, with no figure boxes while reading (operator, 9 October 2026).
- A part's side prints after its description, `(L)` or `(R)`, as Glass's
  sheet prints it.
- A guide time or price the engineer changed in Glass's, Glass's reason for
  it, and a row's annotations and selected criteria are the line's note.
- A paint row's level is its line type, from one table both routes share. New
  paint: `I`, `K1R`, `K1N`, `K1G` (the XML's material kind and level B 3,
  K 2, K 3, K 4). Blend: `B` (B 4, K 5). Repair: `II` inner surface (the
  operator's reading, 5 October 2026), `III`, `IV`, `SP`, `K2` (B 0, B 1,
  B 2, B 6, K 0). A row with no level (preparation, colour mixing, sample,
  the clear-coat surcharge) is preparation. Glass's marked-up paint
  material (the sheet's `Z`, the XML's `PaintMatExtraAppl`) is the note
  "Markup material".
- A discount, surcharge, small-material, sourcing, disposal or
  environmental-fee value the XML prints as non-zero (operator, 5 October
  2026) is imported and noted on the first line with room, beside Glass's
  net. It changes no line and no total.
- Every operation lands from one table both routes share (19 November 2026,
  R1209). An Old for New position (the sheet's `RN`, Renew) is a new part at
  the price and hours Glass's exports, which are already net of the
  deduction. Its note is the changed guide price and time and Glass's own
  reason. Partial renew is a new part. Partial repair (`PR`), Paintless dent
  repair (`DP`) and both Synthetics repairs (`PS`) are a repair. Dismantle and
  assemble (`DM`) is a remove and refit. Adjustments (`A`), Sealing (`S`),
  Control and adjustment (`CA`), Remove air (`RA`), Cavity wax protection
  (`HO`), Diagnostic (`D`), Underbody coating (`UN`) and Wheel balancing
  (`WB`) are Other labour. Surcharge (`EC`) is the additional operation above.
  The XML is matched by the names exports have shown and by the labels
  Glass's dialog shows, without regard to case. The sheet prints `PN` for
  both Partial renew and Synthetics repair normal, so a printed `PN` is Other.
- A free paint row (the XML's `Free_Paint`: special table metal and plastic,
  user defined paint) is a paint line by its level, as a painted part is
  (19 November 2026, R1209).
- A repair kind or printed code Pegasus has not seen is an Other line with
  its price and hours, noted "Printed operation:" and the kind. It no longer
  refuses the import. An XML position type not seen yet is read by its
  prefix: `Paint_` is paint preparation, `Part_` or `Free_` a part row by its
  repair kind. A position with no repair kind, and a position type with none
  of those prefixes, still refuse the import, and the log names the type
  (19 November 2026, R1209).
- On the calculation sheet, a row the parts appendix does not list keeps no
  part number, and a user-defined part listed with no number is read. A flag
  printed run together with its amount (`1,234.00*F`) is read as if printed
  apart (19 November 2026, R1209).

**Labour rate on a new spec.** A new repair spec — imported, returned from
Glass's, or started with **New repair spec** — takes the Standard
labour-rate card (operator, 13 October 2026). The staff member can change it.
A source document's own rate never picks the card.

**The labour rate bar (v38 P5, operator, 12 October 2026).** The rate stands
in a bar under the grid: Standard, then Prestige/Aluminium, and a Custom ·
typed segment, all selectable; the "+ 15 % regional uplift" tick with its
London & Home Counties chip (suggested when a repairer, claimant or storage
postcode is in scope); and the rate the spec prices at in a box at the right.
Choosing a card sets the figure; the uplift adds 15 % to a card's rate and
shows its working beside the box; a typed figure is Custom, marked Manual, and
turns the uplift off. The spec records the card and its base rate, or the
typed rate alone.

**Readable text is required.** Estimate PDFs need readable embedded text.
Unreadable, scan-like or unsupported estimates are refused, with no OCR and
no partial spec. A staff upload stores the confirmed source through the normal
Case document flow, then parses it immediately in the same Import action.
After an interrupted or refused parse, retrying the same source reuses the
confirmed retained document under current Case authority without uploading it
again, and any file already confirmed in Case Files imports from there. Source attribution and complete arithmetic must agree before an import
succeeds. Replaying the original operation does not revive its old lease or
assume another Case-version increment.

**Engineer acts on a spec (v28, ruled 20 September 2026; the reference's
bars since v38, 12 October 2026).** Target % of value scales a spec down
under the Engineer's hand: one factor lowers every part price, every
materials figure and the labour rate the spec prices at, each to its floor
(£50 an hour and 65 % of price unless the Engineer sets others); hours never
move. The scaled rate lands on a whole pound or 50p, never below its floor
and never above the rate it started from, as a typed figure with no card and
no uplift (v38 P6). The slider runs from the lowest share of the Engineer's
Value the floors allow, rounded up to a whole percent, to the spec's own
share, above 100 % where the spec costs more, and goes no higher, so scaling
only lowers prices; a contract repair's agreed sum below that share is the
starting target instead (operator, 8 October 2026). The bar reads the spec's
total "as estimated", or "scaled from estimate" with the total the last
scaling started from. Moving the slider previews (P34; operator, 28 September 2026): Core
scales and totals the spec as the editor holds it, read on the save's own
terms, the changed cells show its figures in amber and cannot be typed in,
and the rollup and the readout follow; Apply and Remove scaling are offered
while a preview is shown, and Remove scaling drops the preview. The readout, previewed and recorded
alike, ends `labour at floor` when the labour rate stops at its floor.
Nothing is recorded until Apply, and any save records the spec as
edited, not as previewed. Apply waits for a change not yet sent to land
(every save records the spec as edited, [FRD-16](frd-16-case-record-workspace.md#case-workspace)),
then freezes the saved spec, saves the scaled specification and freezes it
again as the scaled version; Remove scaling likewise waits and returns the
spec to the version frozen before. A contract repair's agreed sum is Case data the
Engineer records beside the specification, in Decisions' cell or in the
contract bar's own box; recording it sets the outcome to Contract repair,
typing it previews the scaling to the sum as the slider does, and once the
spec drifts from the sum again the bar's mismatch chip rescales it on press
(v38 P4). Every import, scale, removal, restore and sent report freezes a
numbered version with how it came about, and an import onto a spec freezes
the grid it replaces first; Restore makes a frozen version the spec's content
after freezing the outgoing one, and a version frozen as Unknown restores as
Registered (operator, 22 October 2026); the version a sent report used is marked.
Compare reads any two of a Case's specifications, or any of a spec's frozen
versions, line by line and side by side, every line listed with its value at
each side's rate; opened from More it starts at the version the last sent
report used against the spec's working draft; every version row offers
Compare with current, and the comparison prints on its own sheet (v38 P2). A
specification supplements the frozen version an import changed it against,
with a reason, and the composed supplementary statement prints on the report
when the Engineer says so (v38 P3). Send to AI proposes only.

**Corrections and projections.** A correction is an edit of the spec, Current
or not; an imported line keeps the values its source printed beside the
amendment, and the numbered versions keep what a scale, restore or sent
report replaced. There is no separate supersede step (operator,
25 September 2026). The editor reads and shows hours to two decimals with the
stored hours untouched; the rollup, gross, slider and Versions draft read the
stored figure, and the statement and Compare sheet read two decimals like the
report (operator, 22 October 2026). A Case with no Current spec cannot generate a report. The
specification uses one line vocabulary and one calculation basis. The three assessment-report lists (new parts, repairs
and additional operations) are one deterministic names-only projection of
those ordered lines, not a second renderer-owned specification. New parts
lists Replace lines; Repairs lists Repair lines only; Additional operations
lists Paint, Blend, Specialist and Other lines; R&I lines are on no list, so
they leave the report, as the Case page's own lists do (operator,
2 November 2026; R608).

**Replay in the estimate editor.** The editor has no save of its own: the
Case's one Save carries the specification with the rest of the Case
(operator, 23 September 2026), and a specification it leaves unchanged is not
rewritten. The Save keeps the Case version and line identities the staff
member submitted. Retrying the same operation keeps that
intent: source evidence and amendment timestamps are resolved only for a new
operation, not rebuilt before replay detection. A prior successful operation
returns the same estimate identity in its current state, even after later
edits, and never reapplies the older edit. Changed intent under the same
operation key is refused, as is a new operation against a stale Case
version.

### Glass's launch and return

**One button (operator, 6 October 2026).** The Estimate section offers
**Glass's** and no Resume. What it does depends on where it is pressed:

- While the staff member's own session on the Case still holds the account,
  it continues that session: an interrupted preparation carries on, an open
  estimate reopens, and a held return lands.
- On a repair spec that belongs to a Glass's estimate, it reopens that
  estimate in a new session, under the pressing staff member's own account.
  No vehicle is made.
- On a spec with lines that belongs to no Glass's estimate, it first asks
  "Import into Glass's?": "This estimate was made outside Pegasus. Import it
  into Glass's and open it there?" (18 November 2026; R1171). **Import and
  open** rebuilds the spec in Glass's (below). **Open an empty estimate** is
  the press the next item describes, and **Cancel** closes the question.
  Every such spec is asked, whatever route it came by, and the outside
  estimate's vehicle is not checked against the Case.
- On any other spec, on **New repair spec**, or with no spec, it starts a new
  estimate on a new stock vehicle. This is how a Case comes to hold a second
  Glass's spec.

A fresh estimate for a spec comes from clearing its lines inside the
estimator, or from discarding the spec. The portal's own Reset Repair Estimate
is not supported: the vehicle then shows no estimate and the reopen is refused
at `glass.details.profile`.

**Rebuilding an estimate made outside Pegasus (18 November 2026; R1168,
R1169, R1171, R1173).** **Import and open** starts a new estimate on a new
stock vehicle, as any first press does. Pegasus then opens Glass's editor
itself, adds the spec's lines as Glass's own dialogs serve them, saves, and
brings the estimate back through the return path, so it lands as a new
Glass's repair spec beside the outside one. The same work then reopens that
spec's estimate for the staff member, as **Glass's** on that spec reopens
it, and the Glass's window opens it with no second press (18 November 2026;
R1171). A reopen that does not open leaves the new spec on the Case, where
**Glass's** reopens it; no line is sent to Glass's again. No figure is
typed: a rebuilt line carries Glass's own figures (R1168). A line Glass's
has no row for is left off and listed (R1169). In this version a Specialist line is added
when exactly one row of Glass's special-operation list has exactly its
name; an included operation and paint preparation are not sent, because
Glass's adds them itself; every other line is left off and listed as not
built in this version.

A new part is added too (18 November 2026; R1172), pressed **Part** in its
own dialog, when Glass's part list gives its row without an equipment
question. A Glass's export's part is found by its part code and the side its
description names, and an Audatex sheet's by its part number. Where Glass's
lists the same part (the same number, price and hours) on more than one
drawing, the first is used. It is left off and listed:

- as waiting on an equipment answer, when the row Glass's would give depends
  on the vehicle's equipment (variants of the part, time rows by equipment,
  or a part shown only with some equipment), or the number is a variant's.
  The rebuild answers no equipment question, and leaves off a part shown
  by equipment even where the vehicle's criteria show it; only the
  Automation's own add, which adds every part a person could, answers the
  question or adds such a part, in its session or with help on
  (19 November 2026; R1206;
  [FRD-10](frd-10-mcp-automation-and-actor-boundary.md#glasss-sessions));
- as having no row, when Glass's part list has none with that code and side,
  or that number, or the row offers no Replace;
- as having more than one row, when the code or number finds rows that are
  not the same part;
- as having no Glass's part code or part number, when the line has neither
  (a spec typed in, drafted or imported as JSON);
- as a refused dialog, when the dialog Glass's serves is for another part or
  another side. Nothing is posted for it.

A Glass's export's repair and remove-and-refit lines are sent too, with
whatever hours Glass's serves for them (18 November 2026; R1172). Each is
found by its part code and side as a new part is, and left off for the same
reasons. Its row must also offer the line's operation. Glass's must have at
most one time row of that operation for the part; more than one is listed as
having more than one row. Its dialog is the one Glass's serves for the part,
with the operation changed to R (Repair) or UI (Uninstall and install) as a
person changes it, and is pressed **Part**, or **OK** for a part that cannot
be painted. Nothing is typed (R1168). Glass's often serves no price and no
hours, as it does for Repair in every dialog captured. The dialog then asks
whether to go on without a price or a time, the question is answered OK, and
the line lands with no figures; the Engineer types the hours in Glass's
(18 November 2026; R1188). A dialog that would ask for a reason
(more hours than Glass's own), reload itself or open another dialog for the
operation is a refused dialog. A repair or remove-and-refit line from any
other route has no Glass's part code and is left off as not built in this
version. So is a Glass's PDF's partial repair (PR), which is not the Repair
the build sends.

A Glass's calculation PDF prints a labour guide (`S0503XRX`, `NN`), not the
part code, so its parts find no row. A paint line is left off and listed as
not built in this version even beside its part: a Glass's export's paint row
carries a paint code (`1640` for a bonnet), not its part's code, so nothing
on the spec ties the two.

- A line whose dialog Glass's refuses before anything is sent is left off
  and listed, and the build goes on.
- A line whose answer is lost is listed as not confirmed, and nothing more is
  added. What Glass's saved is brought back, and its export decides what the
  new spec holds.
- When Glass's editor cannot be opened, or nothing could be placed, nothing
  is saved, and the staff member gets the empty estimate **Open an empty
  estimate** gives. The Case's Glass's line then says "The estimate could not
  be imported into Glass's, so an empty estimate was opened." to the staff
  member who ran the build, beside the code it stopped at, while that session
  is the one the page shows them (18 November 2026; R1186). A build a restart
  interrupted is continued as a launch and says nothing of it.
- A save whose outcome cannot be known leaves the session `Unknown`,
  holding the account, as any lost answer does; a save Glass's reports as
  not made leaves it `Failed`. Nothing is imported either way.

The staff member who ran the build sees, on the new spec, the list of the
outside estimate's lines: what was sent, or why not, and Glass's price or
hours where they differ from the outside estimate's. The reopen in the
same window is part of the build, so the list stays until the staff
member's next press of **Glass's** on the Case (18 November 2026; R1173).
The list's headings, outcomes and reasons are the operator's (18 November
2026; R1187).

**Any staff member who holds the Case edit may reopen a spec's estimate.**
While a colleague holds the edit, nobody else can press Glass's. After a
take-over, or once the colleague's edit has ended, their session may still be
live on the same estimate. The reopen ends that session and frees its account.
While that session is landing a return, the reopen is refused and can be
retried. Work the colleague later saves in Glass's stays in the estimate and
arrives with the next return.

**Glass's runs in the background (operator, 28 September 2026).** A launch, a
reopen and a continued session check the Case authority, the account and the
vehicle, record the session, and answer at once. The provider work then runs in the background
([ADR-0058](../adr/0058-glass-provider-work-in-the-web-host.md)). The Glass's
window says "Preparing Glass's…" while it runs. While a build runs it says
"Building the estimate in Glass's… 12 of 34", the lines tried so far of
those it tries (18 November 2026; R1171). Once the build's return is brought
back, the window waits on the reopen of the new spec and opens the
estimator; if that reopen did not open, it reports the build's outcome on the
Case as after any return. When the
estimator is ready, the window refreshes the Case's Glass's controls and
opens the estimator. If the launch did not open, the window hands the
outcome back to the Case.

The provider's return is the same. It is checked and claimed at once: a
stranger is refused before the one-use token is spent. The window then says
"Bringing the estimate back…" while the relay, export, retention and import
run. The finished outcome is reported on the Case as before. The same return
delivered again while the import runs waits on the same work.

Only the owning staff member sees the window or its progress. A session the
Automation owns has no window. The estimator address is never shown on the
page or in its progress answer.

**Automation help in an Engineer's open session (operator, 18 November
2026; R1170, R1182 to R1185).** The Glass's session line of the owner's own
open (`Active`) session offers **Let the Automation help** beside Close,
while no work runs for it. Once pressed, the line shows the chip
"Automation help on" and the button **Stop Automation help** (R1183). While
the Engineer's Glass's window has not handed its editor key to Pegasus, the
chip reads "Automation help on · Glass's window not connected": the Pegasus
helper is missing or out of date, or the window is closed (R1183). Help is
per session and off for every new session; the stall retry continues the
same session, so help stays on. Turning it on or off changes nothing on the
Case and asks for no Case version or edit lease, as Close does not; it never
moves the session's version, and each toggle is a history row on the
session. Other staff on the Case see nothing of it.

While help is on, the Automation's Glass's tools find the session by its
Case ([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#help-in-an-engineers-open-session)).
They may read the estimate as in the Automation's own session (R1206),
search its lists, preview a dialog, add positions as in the Automation's
own session, posted as Glass's dialog serves them with nothing typed
(R1182), and remove any position of the estimate, because the Engineer
asked for the help and watches the window (R1185). Since 19 November 2026
(R1206) an add may also press Paint or Part and Paint, choose another
operation the dialog offers, tick Both and the optional mandatory parts,
as the Automation may in its own session; a preview posts nothing and
records no history row. They may also add a part whose block
asks Glass's equipment question, with the Automation's answer, which is
stored at Glass's for the vehicle as the editor stores the Engineer's own
and may untick an option the vehicle has (19 November 2026; R1206). A part
with no answer is refused with the question, as in the Automation's own
session. Every part the Engineer could add with a click the Automation can
add, decided as that click decides it on the row it names: wheels, parts
that offer no Replace, parts with variants, any operation on a part that
asks, and a part found by a variant's part number. A part the car's
equipment hides, which the Engineer cannot click either, is refused,
naming what hides it (19 November 2026; R1206). One Automation act runs at a time, and
each is a history row on the session as the Automation's. After an add or a
removal the helper reloads Glass's own selected list. The Engineer sees that
refresh and the chip, and nothing else is drawn: the smallest build, decided
by the lead without a ruling. The Automation never saves, exits or closes
the Engineer's session and never reads their Glass's login. The Engineer's
own Save & Exit brings the estimate back as any return does: the spec is
Current at once, the Save & Exit being the staff act that puts it in use
(R1184). Once the session leaves `Active` the tools refuse and Pegasus
forgets the window's key.

While a session's work runs it cannot be closed. The Close control is absent,
and a Close posted anyway is refused with a reason, so nothing the work creates
at the provider is left without a session. A second click of the same Launch
waits on the first click's work. If the queue is full, the work runs in the
staff member's own request, as it did before, and is never dropped.

**A plate Glass's does not know (operator, 2 October 2026).** The launch's
lookup is Glass's own plate search, made once; the portal does not repeat
it. An answer without a type number is Glass's "vehicle details have not been
found" and settles nothing. The launch then inserts a placeholder vehicle into
the account's stock list — the portal's own "Add Unqualified Vehicle", with
the Case registration as its model text so the stock list names the Case —
one per launch and never reused, proves it, selects it and opens the
estimator as usual. The Engineer identifies the real vehicle inside the
estimator with its Alternative vehicle selection. The Case page shows
nothing new: the session line reads as for any launch, and the host log says
the launch is on a placeholder. Get valuation on such a plate answers the
card's information sentence, as Glass's vehicle-age and Cazana's no-data
answers do, because nothing is broken (operator, 10 October 2026); a
placeholder has no value
([ADR-0062](../adr/0062-glass-placeholder-vehicle-for-unknown-plates.md)).

The return of a placeholder session is checked as any other, except for the
vehicle's identity: the export may name no plate and no mileage, or the
Case's own, and any other vehicle's is refused; the type number it names is
the Engineer's choice and is recorded on the session, not compared. This rule
stands until the first live return has been read.

### Glass's interrupted sessions

A Glass's launch records its callback and external account before contacting
the provider. Vehicle and estimate identities are kept as soon as they
arrive. Pressing **Glass's** again continues an interrupted preparation or
a known vehicle that has not started an estimate; an existing estimate
reopens through its vehicle, as the portal does, and must answer one of the
session's own estimate ids. A start whose answer was lost is asked again on
the recorded vehicle: the portal answers a start on a vehicle with the
estimate that vehicle holds, so no second vehicle or estimate is made
(captures of 6 October 2026). These actions sit in the Case estimate section
and do not need credentials reset.

**An estimator that does not open (issue 1065).** Glass's editor sometimes
stalls at start-up inside its own frames, before any pane loads; the stall is
the provider's. Pegasus changes none of Glass's files or requests. The Pegasus
helper extension, which an Engineer installs, defines the framing page's
dialog helper before Glass's own script asks for it, with Glass's own class,
so the stall does not happen in a browser that has it (operator, 18 November
2026; R1170). The helper reads only the editor frame's own address and the
two cookies of Glass's load balancer in that frame, which name the Glass's
server the window's session is on (19 November 2026;
[ADR-0091](../adr/0091-pegasus-sends-the-glass-editor-its-own-requests.md),
decision 5), hands them to Pegasus, and asks for the selected list again when
Pegasus says a position was added or removed. The Case page keeps the estimator window in view after the handoff:
it may read how deep the window's frames nest and whether it is closed,
nothing more. When the frames have not grown past the provider's skeleton
within 30 seconds, the page presses **Glass's** again once itself, which is
the same continuation as the staff member's press. If that start stalls too,
or the window is closed without a return while the session is open, the page
says so and the next press is the staff member's (operator, 10 October 2026).

**Interrupted background work.** Work can stop part-way: the host restarts,
or the work runs past its time limit. A session found waiting with no work
running for it is settled when the window next asks:

- Prepared never reached the provider. It stays Prepared and can be
  continued, and the Case says the estimate did not start.
- Launching or a claimed import may have reached the provider. It becomes
  `Unknown`, keeps the account, and follows the Unknown rule below. A build
  is Launching until its own return is claimed. One stopped part-way is not
  taken up again: pressing **Glass's** continues the session as a launch, so
  no line is sent twice (18 November 2026; R1171).
- A claimed import keeps the provider's return with its claim. If the relay
  back to Glass's had not begun, continuing the session makes it once. If
  it had begun, continuing looks the export up again and never relays again.

**A return that cannot be brought in (issue 916, operator ruling).** Three
things follow when the return is accepted but the estimate does not land:

- If Pegasus's own reader refuses the export, the session is `Failed` with
  `glass.export.unreadable`, and the downloaded XML is kept on the Case first,
  as a rejected Glass's export. It is not a Draft's source. The PDF sheet is
  inside that XML. The host log names the position or field the reader refused,
  in position numbers, field names and short code values only, never the
  registration, a token or a URL. Once the reader accepts the file, staff can
  import the kept XML from Files. An export refused for another vehicle's
  registration, mileage or type number, or for carrying no estimate, is kept
  the same way, so work an Engineer corrected inside Glass's is not lost; it
  is not offered **Fetch again**, because the export is not this session's
  vehicle.
- A session that failed only because the export could not be read, or could
  not be fetched after the relay succeeded (the export grid or the download
  refused, offered more than one export or one off Glass's own origin, or
  exceeded the size cap), offers **Fetch again** beside Glass's in the Case's
  Glass's controls. It signs in, selects the recorded vehicle and fetches the
  export for the same estimate. It makes no vehicle and starts no estimate. It
  is refused while the account holds another live session. A relay answered by
  a redirect (the sign-in page) may have been acted on, so that session is
  `Unknown`, and continuing it looks the export up without relaying again.
- If custody throws while storing the export, the session is `Unknown` with
  `glass.custody.failed`, holds the account and can be continued. Continuing
  fetches the export again and does not relay Save & Exit again. It is never
  reported as interrupted. If custody instead answers that an artifact failed,
  the session is `Failed` with the same code and cannot be continued.

A session has no lifetime (operator, 9 October 2026). A saved return lands,
and a press continues the staff member's own session, however long ago it was
launched; only a return under a replaced or disabled credential is `Expired`.

Every continued session presents the current Case version and live edit
lease, including preparation, an active estimate and a waiting import. The
current registration (normalized for spacing and case) must match the one the
stock vehicle was made with. A mismatch refuses further provider work and
leaves the account held; staff restore the original registration or confirm
external closure before starting a new session. A corrected mileage does not
stop it: the vehicle keeps the mileage it was made with, and is proved and its
export read against that. A placeholder holds no registration or mileage at
Glass's, so it continues whatever the Case now records, and its return is read
against the Case's current facts (operator, 9 October 2026). A session that
has recorded no vehicle yet holds nothing at Glass's either, so a corrected
registration or mileage continues it on what the Case now records (operator,
10 October 2026). A valid
continuation replaces the protected import authority with the authority just
proved. Credential generation must still match the account used at launch.

A reopen of a spec's estimate follows the same rule against what the spec
recorded: on a real stock vehicle the Case must still record the registration
the estimate was started for. A changed registration is refused before
Glass's is contacted and before a session is recorded, and holds nothing. A
corrected mileage, or any change on a placeholder, reopens the estimate. The
session is new and uses the pressing staff member's current credential.

Before selecting a vehicle or reopening an estimate, the provider detail form
must identify the expected vehicle ID, registration, mileage and NatCode, and
show the configured repair profile. A placeholder's form must identify the
recorded vehicle ID, a numeric type number (the one the launch recorded, once
it has) and an empty registration, and show the profile; it has no mileage
to prove. Missing or contradictory controls refuse the action. A launch reads
the form of the vehicle it has just created once more, 500 ms later, when the
first reading does not identify it, and settles on the second refusal; no
other proof is read again.

The configured profile is one deployment setting because Glass's keeps
repair profiles for the organisation, so every staff login offers the same one
under the same id (operator, 9 October 2026). What "show the profile" means
depends on whether the provider has started an estimate on the vehicle. Before a start, the repair-profile control is enabled
and offers the configured profile. Once a start has allocated an estimate the
provider locks the control and marks the profile that started it selected, so
a continued session, a reopen or Fetch again requires exactly one disabled
repair-profile control whose selected option is the configured profile
(operator, 5 October 2026). A vehicle whose first start went unanswered may
show either, and is proved as whichever it shows.
The identity controls are checked the same way in both phases.

Reopening follows the portal: after the vehicle is proved, it starts with
estimate ID 0, as the portal does, and accepts only an answer naming one of
the estimate IDs the session recorded, or the one the spec recorded. Any other
answer leaves the session `Unknown` without opening the estimator: it is the
wrong vehicle, or an estimate reset at the portal. A URL issued by the provider
establishes no claim that the hosted editor has initialized successfully.

**What stops a reopen (operator, 6 October 2026).** A session reopening its
spec's estimate makes nothing at Glass's: the vehicle and the estimate were
recorded before it began. Whatever stops it short of another estimate's answer
is a plain `Failed` that frees the account, and the next press reopens the
estimate again. A session continuing its own first launch keeps the earlier
rule: what stops its reopen is `Unknown` and holds the account.

Keeping a returned estimate's source files does not use up the staff member's
still-valid Case edit authority. The import uses that authority to land one
spec, which becomes Current. When that authority is no longer current — a
Case save, a hand-off or a fresh edit session while Glass's was open, or an
expired or lost lease — the return lands the spec under the returning staff
member's own session: the live lease they hold, or a fresh lease taken for
them when nobody holds the Case (operator, 6 October 2026). The landing
advances the Case without ending their session, and a page they keep open
catches up on it as on any system write. While another staff member holds
the Case, the retained result waits for its owner to press **Glass's**, so
nobody's session is overtaken; the host log says why a held estimate waits. Callback replay
creates neither another spec nor another change.

**Unknown answers hold the account.** A provider write whose answer was lost
stays `Unknown` and keeps the account. It must not create another vehicle or
calculation, and must not release the account just because time passed. A
vehicle whose creation went unanswered is never made again. A start that went
unanswered on a recorded vehicle is asked again there when **Glass's** is
pressed, because it answers the estimate that vehicle holds. The
owning staff member can close any session that still holds the account, except
one in the middle of an import, only after confirming Glass's is closed and
no estimate is open, and with a reason; the Close Glass's session dialog states that
condition and its press is the confirmation (3 November 2026, R674). Stale versions and closure by another
staff member are refused. An account holds one live session: while it is held
from another Case, every Case the staff member opens names that Case instead of
offering a launch, and a second launch is refused before the provider is
contacted. Every estimate id a session was launched under is kept, and the
provider may return any of them. Checkpoints and explicit closure are audited
permanently without provider credentials, callback tokens or document
content.

**The Automation's sessions (18 November 2026; R1177, R1180).** The Automation
Actor runs Glass's sessions of its own through its tools
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#glasss-sessions)), under
a Glass's login the deployment holds for it; Pegasus never reads a staff
member's stored login for it. Its session starts a new estimate. A finished
one lands as a Draft repair spec that records its Glass's estimate; only
staff press **Use repair spec**. On its Case, while the session holds
the login or once it has failed, the session line reads "Glass's session ·
Open · the Automation, started 10:42". Any member of staff who can open the
Case's assessment may press **Close session** on it, with a reason; the dialog
says "The Automation is building this Glass's estimate. Closing ends its
session without saving, and the Glass's account is free again." Close is
absent, and refused, while the session's work runs, as for staff. A member of
staff whose own stored login the Automation's session holds sees, on every
other Case, "The Glass's account is in use by the Automation" and "Open on
{reference}" instead of a launch. A session the Automation owns has no
lifetime either.

**What the Automation gives a position's dialog (19 November 2026; R1206).**
In its own session and in an Engineer's with help on, the Automation gives
a dialog what a person gives it without typing
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#glasss-sessions)):
Paint on its own and Part and Paint, in place of R1182's refusal, with the
paint section as Glass's serves it; another operation the dialog offers,
only Repair, Uninstall and install, Sealing and Control and adjustment,
whose positions the return reads back from Glass's export, with Glass's
question before no figures answered OK for Repair by itself (R1188) and
for anything else only when the Automation says so (any other operation,
Renew among them, waits on the operator's decision under issue 2155,
because the return refuses a whole export on a repair kind it does not
read); Both, with the other
side's own dialog posted as served; and the optional parts to tick on the
"Mandatory parts:" page. It may preview the dialog first, and nothing is
posted for a preview. A typed part number or text and free rows still wait.
The rebuild of an outside estimate keeps R1168, R1169 and R1172 as above.

**Typed hours, price and overlap hours (19 November 2026; R1206).** In the
dialog of a special operation, or of a part that cannot be painted, the
Automation may type the labour hours, the price and the overlap hours, as a
person would ([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#glasss-sessions)).
They are its own input, never Pegasus's. Hours above Glass's own need the
Automation's own reason, which Glass's keeps on the position; without one
the add is refused before anything is posted, and Pegasus never writes one.
Overlap hours above the hours are refused, because the return could not
read that position back.

**Changing a position already on the estimate (19 November 2026; R1206).**
The Automation may change any position of the estimate through the dialog
the editor opens for it, in its own session and in an Engineer's with help
on ([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#glasss-sessions)),
with what that dialog lets a person change. The position keeps its id. The
act records what Glass's served and what was sent, and the position's own
figures afterwards. A paint position's own dialog is not changed.

**Paint choices (19 November 2026; R1206).** On a part that can be painted,
pressed with Paint or Part and Paint, and in a change, the Automation may
choose the paint type, the level and Surcharge and type the paint hours, as
a person would ([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#glasss-sessions)).
Glass's own script sets the paint price and hours for the type and level.
The type and level are chosen on a metal part only, whose levels the return
reads back.

**What the Automation reads of an open estimate (19 November 2026; R1206).**
In its own session, and in an Engineer's with help on, the Automation may
read back what Glass's shows a user, one request to Glass's each, and nothing
changes at Glass's
([FRD-10](frd-10-mcp-automation-and-actor-boundary.md#glasss-sessions)): the
estimate's positions with Glass's running totals, each with Glass's own
label; one position's repair type, price, hours, side, part number and paint
price and labour time; and the Summary of Glass's calculation, by trade, with
its labour and material totals, VAT and repair costs. A figure Glass's does
not give is absent, never zero. A read Glass's does
not answer gives its code and leaves the session as it was.

### Inspection location and estimate sources

Every Collision Engineers assessment is a desktop inspection. A physical
inspection address is report data, not evidence that anyone attended. The
Principal setting picks a physical vehicle location or the literal
`Image Based Assessment`; a staff override needs a recorded reason and is
reversed the same way. Address suggestions may use Principal usage frequency,
accident location and image or vision evidence; a suggestion never becomes a
confirmed fact by itself. The full inspection address rules are in
[FRD-06](frd-06-vehicle-and-engineering-evidence.md#inspection-address).

Repair cost figures come from external estimate imports (including Audatex
and Glass's), AI estimates returned through MCP, or staff file import. Manual
repair totals are never invented to get around the estimate contract. The
repairer's VAT status is recorded on each repair specification, the one
owner of that fact. The default is Registered and does not block **Use
repair spec**; Not registered remains a choice. A hand-picked set of VAT
categories the report has no wording for still blocks
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#estimate-vat-on-the-rendered-report)).
Report readiness reads the status from the Current repair specification; the
Case records it nowhere else. Supplied, observed, derived and professionally
accepted values keep their distinctions.

### Retained PDF estimate import

The estimate-import command accepts the supplied Glass's calculation and
Audatex full-report PDFs through their deterministic provider mappings. It
keeps the original document and its source hash before importing the spec;
the same Case and hash replay the same import. Printed totals, rates, line
structure and provider identity must agree. PDF net labour is not reduced
again by the XML-specific overlap rule.

Each Audatex LABOUR and PAINT WORK section prints its own time basis,
`Time Basis N WU = 1 HR`; the basis can differ between the two sections of
one report. The section's work units reconcile to its printed total and are
then divided by that basis, so every line carries hours, and paint time is
paint hours. A section without a readable basis refuses the import. The
printed Total Paint And Material Cost lands on the first paint line, as
materials sit on lines. Extras keep their printed price. A LABOUR line whose
description starts with "Specialist" (Audatex's opinion-time specialist
operations) is a Specialist line by hours, costed at the rate as labour and
listed under Additional operations, not an R&I line (operator, 2 November
2026; R608). The parts footer is read past Sub Total: `Deduction from RRP`,
`Sundry Parts` and `Total Parts`. A Sundry Parts amount above zero lands as
one New part line named `Sundry Parts` at the printed amount, so the parts
total equals the document's Total Parts (R614). A deduction percentage above
zero becomes the estimate's parts discount (R615); Audatex works the sundry
out after the deduction while Pegasus discounts every part line, so such an
estimate's parts total reads a few pence below the document's (32p on the
one known 5 % case). The footer must reproduce the document's own
arithmetic, `Sub Total × (1 − deduction) + Sundry = Total Parts`, or the
import is refused like any other section that does not add up; an older
report that prints no footer imports as before. A value whose `*` mark
prints joined to it (`3.0*`) reads as the same value as one printed apart
from it (operator, 4 November 2026; R659).

Readable embedded text is required. An unusable font map, a scan-like page
or a parser failure gives an explicit refusal, never an OCR request
([ADR-0061](../adr/0061-ocr-every-scanned-document-page.md)). The staff Import
action accepts one supported file and stores the source through the existing
Case document upload mechanism before parsing it immediately. The confirmed
source appears in Case Files even when parsing refuses it; retrying the same
file uses that retained source, while a replay of the same operation returns
the same import. A staff Import makes the imported spec Current. The stored
source has the External estimate type, and a Glass's session's XML export,
calculation-sheet PDF and rejected XML the CE estimate type (operator, 17
November 2026; [R1139](../rulings.md)); a file imported from where it already
sits in Case Files keeps its type
([FRD-05](frd-05-documents-extraction-and-custody.md#document-types);
operator, 22 October 2026).

## States and transitions

| Thing | States |
| --- | --- |
| Repair specification | Draft (live) until discarded, which deletes it. At most one spec is Current: a staff-created spec is Current at once, and Use repair spec switches. Edits keep the spec; imports, scales, restores and sent reports freeze numbered versions |
| Glass's session | prepared (work running in the background), launched, building (a launch that rebuilds a spec made outside Pegasus, held as launching until its own return is claimed; 18 November 2026, R1171), `Unknown` (holds the account), continued, closed by the owning staff member with a reason, or ended when a colleague holding the Case reopens the same estimate. A session the Automation Actor owns is started, finished as a Draft, or closed by it or by staff with a reason (18 November 2026, R1177, R1180). A staff member's `Active` session may have Automation help on, which its owner turns on and off; it is an attribute of the session, not a state, and acts only while the session is `Active` (18 November 2026, R1170, R1183). Interrupted work settles Prepared as resumable and anything past it as `Unknown`. A `Failed` session whose export was unreadable, or could not be fetched after a successful relay, can fetch that estimate again |

## Edge cases and fail-closed behaviour

- An ambiguous estimate artifact, an unreadable PDF, or a reconciliation
  mismatch refuses the whole import.
- A Case with no Current repair spec, or a Current spec with no lines or no
  labour rate, cannot generate a report.
- The Current repair spec cannot be discarded; switch to another one first.
- A lost Glass's answer stays `Unknown` and holds the account until the
  owning staff member closes it with a reason, or, for a session the
  Automation owns, any member of staff who can open the Case's assessment
  (18 November 2026; R1180).
- A **Glass's** press against a Case that changed since the page rendered, or
  whose edit lease ended, is refused with "The Case changed. Reload it and
  retry." It is not an error page.
- A reopen of a spec's estimate after the Case registration changed is refused
  before Glass's is contacted. A corrected mileage, or a corrected plate on a
  placeholder, reopens it.
- An estimate reset with the portal's own Reset Repair Estimate cannot be
  reopened from Pegasus.

## Acceptance evidence

Core and integration evidence covers identity, current authority, account
exclusivity, uncertain writes, callback replay, custody and one import that
lands as the Current spec on the rate card.
Browser evidence covers save-before-launch, refusal without provider work,
waiting in the Glass's window, fresh controls, stale Close and preservation of
edits during return.

The hosted editor must also pass live acceptance on the deployed artifact.
The walk is issue 1066's, on Cases created after the last wipe; Chrome is
primary, Edge covers one fresh launch and one reopen, and each step is
recorded in operations as it passes:

1. Three fresh launches across two vehicle models, cold and warm browser;
   the estimator title names the Case vehicle each time. A launch that meets
   the provider's start-up stall and opens on the page's own second press, or
   on the staff member's, passes with the stall noted.
2. Lines added, Save & Exit: one repair spec, Current, with retained XML and
   PDF; replay of the callback creates nothing further.
3. Glass's again on that spec: the same lines open and one stock row stands
   at Glass's; a changed line and Save & Exit update the same spec in place.
4. Reopens before a save and after a save, including a page reload and a
   host restart in between.
5. Fetch again on a session whose export could not be read.
6. New repair spec, then Glass's: a second, separate estimate.
7. A second Engineer takes the Case over and reopens the first spec. Whether
   a second Glass's login can open another login's stock record needs a
   second login; both stored credentials are one login today, so the
   take-over alone is recorded as not proving it.
8. Create audit, then Glass's on the Audit's copy: the same estimate.
9. One estimator session longer than 30 minutes returning its export.
10. A Glass's return after an Assign Engineer hand-off and re-claim.
11. Expired-lease recovery, Close from the original Case window after a
    reopen, and a second Case refused while the account is held.
12. The checks owed since Releases 82 to 90: a placeholder launch, return
    and reopen on a plate Glass's does not know; a return that fills an empty
    VIN and never replaces one; the Repair Spec still full screen after a
    return; busy buttons on the Glass's paths; and a changed registration
    refused on a real stock vehicle while a session with no recorded vehicle
    follows the Case.
13. Glass's on a spec imported from another system with Specialist lines
    named as Glass's special operations and new parts: the question,
    **Import and open**, the window's progress, and a new Glass's spec beside
    the outside one with its list, opened in the same window, a part whose
    row needs no equipment answer added at Glass's own price and hours and
    one that does listed as waiting on an equipment answer; then **Open an
    empty estimate** on the same spec gives a new empty estimate (18 November
    2026; R1171 to R1173).
14. The Automation starts a session on a Case, adds positions and finishes:
    a Draft repair spec lands, linked to its estimate, and **Use repair
    spec** stays staff's. Staff see the Automation's line on that Case, and
    a member of staff closes a second Automation session with a reason
    (18 November 2026; R1177 to R1180).
15. Automation help (walk E4): with the Pegasus helper loaded, the Engineer
    opens Glass's and presses **Let the Automation help**, and the chip says
    help is on. The Automation searches the lists and adds a special
    operation, and the window shows it within about 2 seconds. The Engineer
    adds a position with a Glass's dialog open while the Automation adds
    one: both are on the estimate. The Automation removes a position the
    Engineer added. Save & Exit brings it all back in use; the tools then
    refuse with `glass.help.not_active`, and the history rows are read back
    (18 November 2026; R1170, R1182 to R1185).

Manual export/import does not satisfy this integration's acceptance.
Supplier startup failures remain open until that journey passes
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `ENG-01`, `EXT-06`, `EXT-09`, `EXT-12` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md).
- Technical constraints:
  [ADR-0061](../adr/0061-ocr-every-scanned-document-page.md).

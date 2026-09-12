# Case workspace v26 — what changed from v25 and why

`pegasus_case_workspace_v26.html` is v25 plus the corrections from the
12 September usability pass, the Glass's behaviour that landed on `origin/dev`
on 11 September (CASE-047 B04), and the manager's v24 decision review folded in
without an inert column. Everything in `v25-notes.md` still applies unless a
row below replaces it. Screenshots are in `v26-shots/` (headless Chromium at
1580×1000, 1440×900 and 760×1000; `02-read-scroll-full` is the whole page).

The dark strip at the bottom left is demo control, not product UI. It now has
three rows: lifecycle state, Glass's session state, and an AI-proposal toggle.
For screenshots the page also accepts query parameters (`state`, `layout`,
`edit=1`, `section`, `glass`, `proposal=1`, `ev=1`, `closeopen=1`, `expand=1`,
`collapse=a,b`);
they exist only so the states can be rendered without a click.

## Usability corrections

| v25 problem | v26 | Screenshot |
| --- | --- | --- |
| Edit only from the ribbon | Every editable section head has Edit; it enters the one page-wide edit session and the page does not move | `01`, `04` |
| Images buried in Files (9th of 10) | Damage carries the evidence strip under the workbench; Files keeps the tagged, croppable set | `16` |
| Six controls on the Estimate head | Import, Glass's / Resume, Send to AI, one More menu (New estimate, Compare), Expand. Import stays visible in read mode and opens the edit session first | `04`–`11` |
| Drop zone always visible with a how-it-works sentence | Shown only while editing an estimate with no lines; label only | `05` |
| Eight estimate header fields in read mode | One summary line in read mode; the grid returns on Edit | `04` vs `05` |
| All seven valuation presets in read mode | Only applied increases render in read mode; the list is complete on Edit | `17` |
| Explanatory copy in Next action, drop zone, Files, Notes, Report | Next action is one label and one link; hints removed; Report date and commentary read as empty values | `01`, `18` |
| Report and Files heads had four buttons each | One primary (Generate report / Add evidence) plus one More menu | `18` |
| Scroll was the default everywhere | Tabs is the default in With Engineer; Scroll elsewhere; the switch remembers a manual choice for the session | `01`, `02` |
| Aside hidden below 1361px | Below 1441px the Figures and Next action cards fold into a two-up strip above the sections | `20` |
| Ribbon height varied with content | Ribbon 56px, section row 40px; sticky block is measured, not assumed | all |
| Notices could not be dismissed | Every notice and the stale bar carry a dismiss × | `09`, `18` |
| No way to fold a section away | Every section head has a collapse chevron; the choice is remembered per browser | `23` |
| Add line at the top of the grid, out of sight | Add line sits under the grid. In edit mode the grid always ends with one blank line; typing into it makes it real and a fresh blank line appears below. A line left blank is dropped on Save or Cancel, never stored | `05` |

## Glass's — mirrors `origin/dev` at 37d00f4c2

The v25 line had one state and a one-click Close. The live record has ten
session states and a guarded close. v26 reproduces the live rules:

| Rule (source) | v26 |
| --- | --- |
| Button absent without an enabled account, never disabled (`CanLaunchGlass`) | The slot is absent when the Engineer has no Glass's credential |
| Launch and Resume are both offered while a session holds the account (`CanResumeGlass`) | One slot: reads Glass's when free, Resume while held. **Deviation from live, for sign-off** |
| States Prepared, Starting, Open, Recording, Waiting, Recorded, Failed, Unknown, Expired, Cancelled (`GlassLabels.StateLabel`) | Session line chip carries the label; line shows only while the account is held or the session failed |
| Failure code row when present | Monospace code at the line's right (`09`) |
| Close needs a reason, the confirmation "Glass's is closed and no estimate remains open", one consequence sentence, red Close session (`CloseGlass`) | Inline disclosure under the session line, not a modal (`07`) |
| Close is refused mid-import (FRD-06) | No Close while Recording |
| Waiting (AwaitingImport) keeps the retained result until authority returns; Complete import lands the Draft (`PendingEstimateSources`) | Waiting line offers Complete import while editing, states the condition otherwise (`08`) |
| Held from another Case: name it, link it, offer no launch (`GlassSessionElsewhere`) | Line reads "Open on QDOS26199" with no Glass's slot (`10`) |
| One outcome sentence per settled state (`GlassLabels.OutcomeMessage`) | Notice at the top of the Estimate section, not the page top (`08`, `09`, `11`, `12`) |
| Estimator opens in a named window; the Case keeps its lease; the return reloads the Case at Estimate (`glass-return.js`) | Launch keeps the edit session; demo Return lands a source-labelled Draft tab and the Recorded notice (`11`) |
| Acceptance is **Use estimate** (FRD-06) | Draft actions read Use estimate, Duplicate, Discard |

## Manager's v24 decisions, folded in

- **Decisions strip at the top of Settlement** (`13`–`15`): Outcome,
  Engineer's Value, Salvage category, Salvage value, Roadworthiness and the
  unroadworthy reason in one place. These are the same fields as before, moved,
  not duplicated. Engineer's Value is read-only here and links to Valuation
  because Core adopts it only by an explicit Apply.
- **Proposed column renders only when a reviewed AI proposal exists.** With
  none, the strip is two columns and nothing is greyed. With one, each row
  shows the proposal, an AI tag, and Awaiting / Accepted / Corrected; Accept
  and Accept all appear while editing; the Engineer's Value row offers Apply
  in Valuation instead of Accept. The status is derived from the recorded
  value, never stored separately. The label is Proposed, not Brian.
- **Lock glyph on identity cells in edit mode** (`03`): Case type, Our ref,
  Received, Principal and Registration show a lock and a quiet fill while the
  rest of the page edits. Same read-only cell as before; no per-field padlock
  interaction.
- **Not adopted, as in v25:** product type, composed sentences on the page,
  mi/km toggle, average-mileage arithmetic, a Fee tab, numbered sections.
  Principal-wide notes need a Core field that does not exist, so nothing is
  drawn for them.

## Frame rules this mockup follows

1. Ribbon 56px, section row 40px, sticky block measured at runtime.
2. Content grid of 12 columns with 36px cells; aside 285px at 1441px and
   above, folded above the sections below that.
3. One primary plus one menu per panel head. The Estimate head is the one
   exception: Glass's stays visible beside Send to AI because launching the
   estimator is the Engineer's main act in that section.
4. No control in a head that the section's state does not allow; availability
   is stated once per section.
5. Read mode shows values, never empty inputs or unapplied options.

## Shell pages — `pegasus_shell_v26.html`

Every rail destination is mocked in a second file that shares the Case
record's tokens and shell. Its routes are hash routes; the Case record opens
from any list as `pegasus_case_workspace_v26.html`, and that page's rail,
working-set tabs, New case button and search box come back to the shell.

| Route | Page |
| --- | --- |
| `#/` | Work Centre: five attention metrics, Needs attention list, Today pane with the next permitted action |
| `#/inbox`, `#/inbox/{id}` | Inbox with scopes, mailbox / folder / queue filters, message preview; the message record with Message, Attachments, Thread and Case tabs and the Decision card |
| `#/upload` | Upload with the live accepted-file line; `?sample=1` lands the six-outcome sample submission |
| `#/cases` | Workflow rail (Workflow, Pre-Case work, Exceptions), scope list, Quick detail; Triage, Awaiting instruction, Held and Unidentified scopes |
| `#/cases/new` | Create case with the live field set (Claim number, Claim Source, Mileage and unit, Inspection date) |
| `#/triage/{id}`, `#/unidentified/{id}`, `#/images/{id}` | The three pre-Case records with their dialogs |
| `#/search` | Case or reference, Registration, Claimant, Claim reference, Principal, State; More filters; Case results with the Vehicle column; Selected Case |
| `#/operations` | AI Job List, Attention required, Active upload links, EVA handoffs |
| `#/admin` | The hub with the live three groups; Staff accounts & roles (with the Glass's column and Manage login from the account), Contacts (principals, claim sources, repairers, storage, third party engineers, salvage agents), Workflow configuration, Mail settings (Default sender, mailboxes, categories), Valuation presets (Fixed valuation additions), Service health, Action logs, Reports (MI01–MI03), AI jobs, Automation & AI |
| `#/signin`, `#/uploads/{token}` | The navless frames |

**Mockup strip.** Every shell page carries the same dark control strip as the
Case record, bottom left. Row one is global: role (Administrator, Engineer,
User; User sees no Manage group and gets the live Access denied page),
data (live fixtures or every list empty, for the empty states), freshness
(Current, Partial, Stale, Unavailable on the rail and utility bar), the
page-level Unavailable notice with the live wording, and the rail collapse.
Row two follows the route: Work Centre kinds; Inbox scopes and sort; the
message record's tabs, Reply, Forward and classification; Upload's Idle,
Files chosen, Storing and Decided; the Cases scopes and the Missing filter;
Create case manual or seeded from a received file; Search with no query,
results or no results; the Operations partial-data notice; Administration's
automation, AI and password-change states and the hub; Triage's five states
and assignment; Unidentified open or resolved and the other items; the image
record's three states. Row three opens any dialog by name, the sign-in and
public upload frames, and the Case record. The same states are reachable by
query string for screenshots (`role`, `data`, `fresh`, `unavailable=1`,
`dialog`, `sample=1`).

**How it was built.** The pages are the EPIC-012 v2 prototype modules from the
private pack, concatenated with the vendor name replaced by "AI", the Case
route handed to the v26 file, and a recorded list of exact-string corrections so
each page carries the labels, groupings and columns the live Razor pages have
on `origin/dev` (12 September inventory of `Pages/*.cshtml` and the
`OperatorLabels` constants). The corrections are one list, applied by the
build, each asserted to match exactly once. Sources stay in the pack; the file
is self-contained.

**Where the mockup deliberately differs from live.** Case lists are tables,
not row-buttons (denser; the earlier design choice). Operations keeps the
Recipient, Created and Files columns on upload links. Staff accounts show a
Glass's column so the whole picture is on one screen; live reaches the
credential only from the account. Contacts fold Principals in as a type with
the code, as live does, but principal settings still open the prototype's
principal dialog.

**Self-check.** `v26-shell-selfcheck.html` renders every route, opens and
closes every dialog, and drives the main flows; run it the same way as the
Case record's check. 13 September: `{"fail":[],"okCount":279}` including the strip, the working set (a Triage and a message joining and leaving it, the empty strip) and the viewer, no console
errors on any of the 33 rendered routes and states (`v26-shots/s*.png`).

## Working set — the tab strip, reworked

The strip above the record used to hold a Work Centre tab, one case tab and a
"+ Open" tab on a grey band. It now holds the working set only, and looks like
one thing with the record beneath it.

- **Open records only.** Cases and the pre-Case records (Triage, Unidentified,
  Image intake, a message) join the strip when opened and leave when closed.
  Work Centre stays in the rail. "+ Open" is gone; records open from Cases,
  Search, the Inbox or Ctrl K. With nothing open the strip is not there.
- **Fused with the record.** The strip sits on the page background with one
  hairline under it. The active tab is white, carries the red accent once, and
  runs into the record card with no seam, so the tab and the card read as one
  surface. The card's own top edge is gone.
- **Reference and registration.** Each tab shows the reference in bold and the
  registration in mono; a message shows its subject, a Triage its registration.
- **State on the tab.** An amber dot for unsaved edits, a lightning glyph while
  a Glass's session is open, a lock while a colleague holds the record. Close ×
  shows on the active tab and on hover.
- **Six, then more.** Six tabs are shown; the rest sit in a "N more" menu at the
  end of the strip.
- **Persisted and shared.** The set is kept in the browser and read by both
  files, so opening a Triage in the shell shows it on the Case record's strip
  and closing it there removes it in the shell. Middle-click closes a tab.

Mockup strip row `Tabs` (or `?tabs=1|4|9`) seeds one, four or nine open
records; the shell's row also offers None. Shots `34`, `35`, `38`, `39`.

Every decision in the strip is a variable on the mockup strip, on both files,
remembered per browser and shared between them (`?tabopt=key:value,...`):

| Row | Variable | Options | Default |
| --- | --- | --- | --- |
| Tab layout | `place` | Fused strip · Grey band (the original band with bordered tabs, card keeps its edge) · Rail list (under Cases in the rail, no strip) · Breadcrumb (Cases › current ⌄ with the set in a menu) | Fused |
| Tab label | `label` | Ref + reg · Ref only · Reg only | Ref + reg |
| Tab label | `icons` | Icons · No icons | Icons |
| Tab label | `density` | Regular (34px) · Compact (28px) | Regular |
| Tab rules | `max` | Max 4 · 6 · 8 · No limit, rest in the "more" menu | 6 |
| Tab rules | `glyphs` | State glyphs · No glyphs | On |
| Tab rules | `close` | × on hover · × always | Hover |
| Tab rules | `wc` | Work Centre tab on or off | Off |
| Tab rules | `open` | "+ Open" tab on or off | Off |

Reset returns to the defaults. Shots `40` (band with Work Centre and Open),
`41` and `44` (rail list, both files), `42` and `45` (breadcrumb, both
files), `43` (compact, reg only, no icons, × always, max 8), `46` (the strip).

## Image viewer

Images now open in a viewer rather than a toast. On the Case record the viewer
is full-screen: title, tag and position, Rotate, Zoom, Download, and while
editing an "In report" toggle that drives the report image set. A filmstrip
along the bottom shows every image with the excluded ones greyed; ← → step,
R rotates, Z zooms, Esc closes. It opens from the evidence strip in Damage,
from Files › Images, and from a report thumbnail outside an edit session (a
small view glyph opens it during one, since the click there toggles inclusion).

In the shell the viewer is a dark dialog with the same controls, stepping
through the record's gallery, and a page preview for documents. Gallery tiles
carry thumbnails. `?viewer=N` on the Case record and `?dialog=viewer` on a
shell record open it. Shots `36`, `37`.

**Crop and tag.** Crop enters a mode where dragging over the image draws the
region; Apply keeps it, Clear removes it, Esc cancels, Enter applies. The
crop is a stored rectangle, not a new file: the viewer, the filmstrip, the
report and Files thumbnails and the shell gallery tiles all show the cropped
region, and Download still returns the original. A Tag select (Overview,
Close-up, Supporting, Third party) sits beside it; the tag shows in the
viewer title and as the chip on the thumbnail. Both are present only while
the Case is in an edit session; on pre-Case records they are always present.
Shots `47` (selecting), `48` (applied, tagged), `49` and `50` (shell).

The images are synthetic: there are no files behind this mockup, so each
name renders a generated scene. Stage 2 uses the stored evidence.

## Damage clicker — three variants

The Damage workbench carries three interchangeable vehicle clickers. The
`Damage clicker` row on the mockup strip (or `?clicker=plan|side|dial`)
switches between them; the choice is remembered per browser. All three drive
the same zone model as the live diagram (19 panels, 4 wheels, and the
Underside, Interior and Mechanical chips), so the recorded-zones list, the
derived Impact location and Impact severity and the incident narrative do
not change between them.

| Variant | What it is | Shots |
| --- | --- | --- |
| Plan | One top-down silhouette drawn as the actual panels: bumper ends wrap to the corners, wings sit beside the bonnet, doors beside the glasshouse, quarters beside the rear screen. Panel seams, lamps, mirrors and a soft drop shadow. | `30` |
| Elevations | The insurance-sheet layout: front, rear, near side and off side. Damage is hatched, and a zone that shows in two views (a corner, a wing) lights in both. The end views are captioned with which side is which. | `31`, `33` |
| Dial | A point-of-impact ring of fourteen perimeter sectors around a plan-view core. Bonnet, screens, roof, tailgate and wheels are clicked on the core; everything on the outside is clicked on the ring. | `32` |

Shared behaviour:

- Severity is painted, not just marked: five graded fills from Light to Heavy,
  with the legend under the diagram. Changing the severity in the list
  repaints the zone.
- Each recorded zone carries a number on the diagram and the same number in
  the list, so "2" on the tailgate is row 2.
- Hovering a zone names it under the diagram; hovering a list row lights its
  zone. Both are absent, not disabled, outside an edit session (D21).
- No explanatory copy on the workbench. Captions are FRONT, REAR, N/S, O/S and
  the view names only.

## Replica pass — every live surface, 13 September

A read-only audit of every Razor handler and panel against the two files
produced a gap list; all of it is now mocked, with live labels read from the
`.cshtml` sources. Highlights:

- **Received-file record** (`#/intake/{id}`): allocation failure with Retry,
  Block, Re-evaluate, Correct draft, retained instruction analysis,
  registration readings with Dismiss, Register images, Open the Triage, Link
  and Unlink selected Case with candidate search, instruction details,
  inspection-address confirmation, scanned PDF pages, suggested fields,
  decision evidence, Create a case. Seven strip states. Shot `52`.
- **Image-initiated Case**: optional Principal (select with "Not known" as
  the empty option) in the header, the Principal panel, the Awaiting
  instruction table and its Quick detail. Fixtures with and without. `53`.
- **Triage**: set principal, unassign, unlink case, response evidence link
  and unlink, supersede finding and two-active reconciliation, chaser send
  and reconcile, Take over, Post-send correction. `54`.
- **Unidentified**: Link to Case search with candidates, Close with reason.
- **Mail**: Reply all, AI query response, Move to recommended folder. The
  mockup-only delete and flag are gone (no live handler).
- **Search** gains the Vehicle images group with lifecycle chip. **Create
  case** carries the full live field set and the "This item cannot become a
  case" refusal with the inspection-address choice. `56`.
- **AI suggestions review** (`#/cases/{id}/suggestions`) with live copy. `55`.
- **Administration**: Force logout, Delete, Take over, Printed name and
  Qualifications on accounts; Resolve folders and Set default sender on
  mailboxes; Take over on the Glass's credential. **Connector consent**
  (`#/connect/authorize`), Accept and Deny.
- **Case record**: per-tile tag picker with colour chips and New tag on a
  shared vocabulary; the live Crop dialog (Aspect, Rotate left and right,
  Full frame, Reset, Save crop) sharing the viewer's crop; report image
  preparation (Role, Order, Rotate, Reset, Crop); document removal with a
  reason; custody placeholder tile; intake photographs per intake; the full
  Report flow (gated Generate, artifacts Report / Report with fee note / Fee
  note, generation state and stale notice, Reviewed recipients with Add To
  and Cc, Prepare delivery, Send prepared report, Mark report sent, Unlink
  report evidence); Lifecycle actions (Return to Review, Unlink report
  evidence, Archive case), closure outcomes, replacement case after a
  wrong-principal correction; EVA handoff with Download ZIP and gated Send
  via API; Vehicle lookup states; inspection-address confirmation; edit
  lease with Claim, Take over, Renew editing, Release and the expiry line;
  Add Case note and Record chase; a Case tasks sub-panel marked Proposal.
  `57` to `60`.

Departures from live, kept deliberately: Take over on a colleague's lease
(live renders no control) and the Case tasks panel (live has handlers but no
page) are proposals; "Link report evidence" is not a live button, the bar's
Mark report sent is the link.

## Decisions that need sign-off before Stage 2

Everything in `v25-notes.md` A–F still stands. New:

- **G. One Glass's slot.** Live renders Launch in the head and Resume in the
  session panel at the same time. v26 shows one slot that changes label.
  Confirm, or keep both as today.
- **H. Tabs by default in With Engineer.** Confirm the default, or keep Scroll.
- **I. Decisions strip.** It moves five Settlement fields, keeps them there and
  shows the proposal column only from a real reviewed proposal. Confirm the
  position (top of Settlement) or ask for its own section.
- **J. Section-head Edit** enters the one page-wide session. Confirm that a
  per-section lease is not wanted.
- **K. Evidence strip in Damage** is read-only here; tagging and crop stay in
  Files. Confirm, or ask for tags on the strip.
- **L. Case lists as tables.** Live renders row-buttons; the mockup keeps the
  table. Confirm the table for Stage 2, or keep the row-button list.
- **M. Glass's column on Staff accounts.** An overview the live page does not
  have. Confirm, or keep the credential behind the account only.
- **N. Two files.** The shell and the Case record are separate files here; in
  Stage 2 they are one application. Nothing to decide unless a page is
  missing from the table above.
- **O. Damage clicker.** Pick one of Plan, Elevations or Dial for Stage 2
  (see § Damage clicker). Only one ships; the strip switch is mockup-only.
- **P. Working set.** Open records only, fused into the record, six then a
  menu, persisted per browser. Every variable is on the strip; pick the
  combination for Stage 2 (see the table under § Working set).
- **R. Proposals inside the replica.** Take over on a colleague's lease, the
  Case tasks panel, and the working-set strip have no live counterpart.
  Confirm each, or drop it before Stage 2.
- **Q. Crop as a stored rectangle.** The viewer's crop is a region kept on
  the image record, applied wherever the image is shown, with the original
  retained. Confirm this over writing a derived file.

## Self-check

`v26-selfcheck.html` loads the mockup in a frame and drives every section in
every lifecycle state and layout, the edit session (change, damage zone,
valuation apply, phantom estimate line, discounts, VAT override, Use estimate,
Glass's launch, guarded close, every session state, Complete import, held
elsewhere), the decisions strip, report generation and the stale bar,
dismissals, save and cancel, collapse on every section, full-screen estimate,
rail, Files tabs, notes, hold, menus, the colleague guard and the three
damage clickers. Run it with
file access allowed and read the RESULT line:

```powershell
& $chrome --headless=new --allow-file-access-from-files `
  --virtual-time-budget=30000 --dump-dom `
  design/planning-and-old-designs/v26_planning/current/v26-selfcheck.html |
  Select-String 'RESULT'
```

13 September: `{"fail":[],"okCount":371}` with no console errors, on the
Playwright Chromium 1228 build. The later checks switch through the three
damage clickers, open the image viewer from the evidence strip (step, rotate,
zoom, include, close), and seed nine open records to confirm six tabs plus a
menu, the state glyphs, and that closing a tab persists.

## Known limits

- The demo Return lands a fixed Glass's line set; the real return imports the
  retained calculation PDF through the canonical import command.
- Failed shows one representative code. Live codes are the gateway's.
- The proposal fixture matches the recorded values, so most rows read
  Accepted on first view; change a value or apply an Engineer's Value to see
  Corrected.
- Same font and dialog limits as v25.

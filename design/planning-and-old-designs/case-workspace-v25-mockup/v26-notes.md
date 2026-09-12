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

## Self-check

`v26-selfcheck.html` loads the mockup in a frame and drives every section in
every lifecycle state and layout, the edit session (change, damage zone,
valuation apply, phantom estimate line, discounts, VAT override, Use estimate,
Glass's launch, guarded close, every session state, Complete import, held
elsewhere), the decisions strip, report generation and the stale bar,
dismissals, save and cancel, collapse on every section, full-screen estimate,
rail, Files tabs, notes, hold, menus and the colleague guard. Run it with
file access allowed and read the RESULT line:

```powershell
& $chrome --headless=new --allow-file-access-from-files `
  --virtual-time-budget=12000 --dump-dom `
  design/planning-and-old-designs/case-workspace-v25-mockup/v26-selfcheck.html |
  Select-String 'RESULT'
```

12 September: `{"fail":[],"okCount":213}` with no console errors, on the
Playwright Chromium 1228 build.

## Known limits

- The demo Return lands a fixed Glass's line set; the real return imports the
  retained calculation PDF through the canonical import command.
- Failed shows one representative code. Live codes are the gateway's.
- The proposal fixture matches the recorded values, so most rows read
  Accepted on first view; change a value or apply an Engineer's Value to see
  Corrected.
- Same font and dialog limits as v25.

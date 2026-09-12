# Case workspace v25 — merged mockup notes

`pegasus_case_workspace_v25.html` is a self-contained, offline mockup of one Case record.
It merges the v24 dashboard mockup with the live Pegasus shell (tokens, rail, utility bar,
workspace tabs, Lucide glyphs and the damage-diagram geometry are taken from `origin/dev`).
Fixture values are the v24 file's synthetic MA59BDY / QDOS26214 data. Nothing here is
application evidence; it is a design proposal for Stage 2 implementation.

Open the file in a browser. The dark strip at the bottom left ("Mockup state") is not
product UI: it switches the lifecycle state so each availability rule can be seen.

## What it demonstrates

| Behaviour | How to see it |
| --- | --- |
| Two-row sticky block, 91px at 1580 (today ~136px, before WP6 ~250px) | Scroll anywhere |
| Read and edit share one geometry; entering edit does not move the page | Scroll to Valuation, press Edit Case, then Cancel |
| Save keeps you where you are and writes one attributed history line | Edit, change a field, Save, open Notes |
| Availability is stated once per section, never silently disabled | Mockup state → Review, press Edit Case, look at Damage/Valuation/Estimate/Settlement/Report heads |
| A colleague's edit is a chip in the action row, and every field reads only | Mockup state → Colleague editing |
| Complete/Query offer Return to Engineer, not a half-working edit | Mockup state → Complete |
| Valuation calculator on the real policy shape | Edit → Valuation: pick a basis card, tick a preset, set a deduction, Apply |
| Figures follow the decision | Aside "Figures", Settlement strip and Report readiness all update after Apply |
| Stale generation is visible from any section | Report → Generate report, then edit any field |
| Scroll and Tabs share one edit session | Toggle Scroll/Tabs while editing; values survive |
| Outcome and legal status as chips | Settlement → change Outcome / Legal status |
| Estimate full screen | Estimate head → Expand (Escape or × returns; edits and position survive) |

## Decisions the mockup takes (and their authority)

1. **Sticky block = ribbon row + section row.** Ribbon: reference (h1) with registration
   eyebrow, Claimant, Principal, Engineer, State chip, then exactly two controls: the primary
   action (Edit Case, or Editing + Cancel + Save) and one **Actions** menu holding Send to
   EVA, Mark report sent, Place on Hold, Create upload link, Correct principal and, below a
   divider in red, Close case. Section row: section links, outcome + legal chips, Refresh,
   Scroll/Tabs. Sign-off Engineer leaves
   the ribbon (it is a Report fact and the Figures aside no longer repeats it). Back to Cases
   is dropped from the ribbon because the rail and the workspace tab strip already navigate.
   *Authority:* refined pack durable rules (compact frame), operator issue 1 (cramped top bar).
2. **One geometry for read and edit.** Every fact is a labelled cell whose value box and
   control box are the same 36px-minimum box in the same grid position; only the border and
   background change. No separate edit panels, no `detail-list` ↔ `field` swap.
   *Authority:* operator issue 3 (edit and non-edit versions shown together / do not
   correspond), design README "one edit form".
3. **Edit, Cancel and Save act in place.** No navigation, no scroll change, no reload
   (implemented in Stage 2 as lease claim + fragment refresh with a full-POST fallback that
   returns to the same section). *Authority:* your instruction on teleporting.
4. **Availability is a per-section label** ("Available With Engineer", "E Mawdsley is
   editing", "Return the Case to the Engineer to edit"), using the existing `.gated`
   pattern. *Authority:* design README "present, disabled, states the condition".
5. **Storage rate and recovery charge sit under Inspection details → Storage**, beside the
   storage location; Settlement keeps only the derived storage charge.
   *Authority:* operator issue 7 (inspection facts live in Inspection details).
6. **One mileage box with a source chip** replaces four mileage boxes; DVLA/DVSA figures
   are provenance rows, not inputs. *Authority:* operator vehicle-section issues.
7. **Aside = Figures + Next action.** Current position (state, due, engineer, sign-off,
   editing) is removed because the ribbon carries it. *Authority:* operator issue 3 and
   the refined pack's Figures card.
8. **Valuation calculator** uses the Core policy shape exactly: basis card, commercial VAT
   (blocked when the claimant is VAT registered), previous total loss 10/20 %, presets with
   suggested amounts, custom additions, condition deduction, ordered lines, Apply with an
   applied-history row. Cazana stays a disabled seam. *Authority:* `ValuationCalculations.cs`,
   design README D21.
9. **Report content switches stay in Report** (disclose guide source, valuation commentary,
   unrelated damage) rather than beside Valuation as v24 had them. One place per fact.
10. **Not adopted from v24:** "Brian" panel, padlocks, send-as-approval, on-page composed
    sentences, product types, average-mileage arithmetic, Fee tab, v24 tokens.

## Decisions that need your sign-off before Stage 2

- **A. Save without a reason dialog.** The mockup saves immediately and records
  "Case data saved by <name> — <fields>" in the timeline. Today every Case save demands a
  free-text reason (`Details.cshtml` save-reason dialog). Removing it changes FRD-01/12 and
  the audit event. If you want to keep a reason, say so and it becomes an optional field in
  the action row, not a modal.
- **B. Sign-off Engineer out of the ribbon** (kept in Report and in the Figures aside? The
  mockup shows it only in Report). Confirm or ask for it back in the ribbon.
- **C. Back to Cases removed from the record frame** (rail + workspace tabs remain).
- **C2. Close case lives inside the Actions menu** (red, separated by a divider) instead of
  standing alone in the bar. Your 9 Sept review point 12 asked for it to be separate and
  noticeable; the menu keeps it separated but one click further away. Confirm or ask for
  it back in the bar.
- **C4. Estimate full screen.** The Expand control on the Estimate head; in Stage 2 it is a
  presentation-only toggle on the existing section host (no second form).
- **C3. Rail collapse.** A Collapse control at the foot of the rail narrows it to 64px
  (icons and counts only) and remembers the choice per browser.
- **D. Outcome and legal-status chips on the section row** rather than in the ribbon.
  They only render when recorded.
- **E. Storage money fields moving to Inspection details.**
- **F. Immediate-post actions (suggestion chips, lookups, add note, estimate actions) no
  longer end the edit session.** Stage 2 keeps the lease on success and warns before an
  immediate post while unsaved edits exist.

## Coverage against the live record (audit of origin/dev, 11 Sep)

Restored after the audit: Glass's launch and session line (Resume / Close session), New
estimate, Import, Send to AI, Compare, estimate header fields (name, repair days, rate card,
labour rate, paint materials, other costs, VAT %, repairer VAT status), estimate notes and
part numbers; tyre, seat-belt, spare, centre-belt, material-transfer and unrelated-damage
deduction fields with the incident narrative; Settlement excess, betterment, reserve,
diminution, hire start and daily cost, repair and report delays, derived storage charge and
repair days, and the seven salvage fields; Report draft preview, fee-note-only generation,
generation facts, fee description, override report date, Engineer's comments (moved back
from Settlement), valuation commentary text, statement of truth, To and Cc; Files Box and
Operations links, Save as, Remove, public upload requests table, Queries with Compose;
Record chase; the workflow stepper, outstanding requirements panel, Due and Contact in
Overview; principal default and repairer contact in Inspection; Year in Vehicle; and
state-dependent progression in the Actions menu (Hand to Engineer / Send to EVA / Mark
report sent / Mark completed / Return to Review / Return to Engineer / Archive / Hold ·
Release Hold).

Deliberately not reproduced (dialog contents or replaced by design): the save-reason
dialog (decision A); "Current position" aside (ribbon carries it); Registration and
Sign-off in the ribbon (decision B); Back to Cases (C); presence strip (now a chip);
Renew editing (script hides it today too); the contents of Hold, Close, Correct principal,
Hand to Engineer, Mark report sent, EVA handoff, Import, Compare, Delete estimate, Send to
AI, Crop and Remove dialogs; the evidence viewer overlay; report-image Role/Order/Rotate
controls (shown as role tags on the strip, edited in Files); per-cell hidden labels; the
read-only recorded-lines grid (one grid serves both modes).

Vocabulary note: the live button reads "Send to Claude"; CONTEXT.md bans that label, so the
mockup says "Send to AI".

## Known limits of the mockup

- Add valuation, Versions, Preview, Prepare delivery, More actions and Close case open
  alerts or do nothing; their real dialogs already exist in the application.
- Files → Images shows the report strip again for illustration; the real Images tab
  (tags, crop) is the dev implementation.
- Fonts fall back to the system stack; the app self-hosts Inter.
- Screenshots in `v25-shots/` were rendered by headless Chromium at 1580×1000, 1440×900
  and 760×1000.

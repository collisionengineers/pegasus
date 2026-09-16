import pathlib
here = pathlib.Path(__file__).resolve().parent
root = here.parents[1]


def patch(path, pairs, append=''):
    s = path.read_text(encoding='utf-8')
    for old, new in pairs:
        assert s.count(old) == 1, (path.name, s.count(old), old[:90])
        s = s.replace(old, new)
    path.write_text(s + append, encoding='utf-8')


patch(root / 'current/v27-notes.md', [
    ("`notifications=1`, `viewer=<n>`, `strip=0`, and the design variables\n`clicker` (`zones` `pins` `brush` `area` `arrow`) and `logo` (`refined` `live`).",
     "`notifications=1`, `viewer=<n>`, `strip=0`, the design variables `clicker`\n(`zones` `pins` `brush` `area` `arrow`) and `logo` (`refined` `live`), the\nfixture switch `repairer` (`liverpool` `london`), and the reference\nproposals: `proposals=all|none` or `p=<key,key,…>` from the list in § 15.\nThe file opens with every proposal on; `proposals=none` is the baseline."),
    ("16 September, after areas (§ 12) and the refined mark (§ 13): `RESULT\n{\"fail\":[],\"okCount\":630}`; 74 loads, none with console errors.",
     "16 September, after areas (§ 12) and the refined mark (§ 13): `RESULT\n{\"fail\":[],\"okCount\":630}`; 74 loads, none with console errors.\n\n16 September, after the reference proposals (§ 15): `RESULT\n{\"fail\":[],\"okCount\":663}` — the baseline blocks run with every proposal off,\nthen block 20 turns them all on; 89 loads, none with console errors. Built\nfrom `cdbe014a6` (`origin/dev` moved by an intake fix that touches none of\nthis record's inputs)."),
], append="""
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
| `compare` | Compare estimates gains From / To, a summary line with the £ delta and counts, a per-line table with added / changed / removed rows coloured and changed cells in bold, and **Print comparison sheet** (a print stylesheet prints the dialog alone). | 81 |
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
""")

patch(root / 'current/discussion-log.md', [], append="""
## 16 September 2026 — the reference proposals

The operator: *"implement all the 'nothing in the way' section now on the
mockup"*. The nineteen items of `v27-notes.md` § 14's first table are on the
strip's **Proposals** row, each its own switch, all on by default, with All
off for the baseline. The self-check runs the baseline blocks with every
proposal off and then a block with them all on (663 checks, none failing);
shots 75–89. Items I and I1 raised. The nine-section map and the reference
placements are DOM moves on the live sections, reversible from the strip, so
the baseline is untouched underneath.
""")

patch(root / 'current/README.md', [
    ("The proposals so far\nride on it as strip variables: **Damage selector**, four click-anywhere ways to mark\ndamage that record the eight vehicle areas rather than panels (`v27-notes.md` § 11–12,\nsign-off G–G7, `?clicker=pins|brush|area|arrow`), and **Logo**, the refined mark in the\nrail (§ 13, sign-off H, `?logo=refined|live`).",
     "The proposals ride on it as\nstrip variables: **Damage selector**, four click-anywhere ways to mark damage that\nrecord the eight vehicle areas rather than panels (`v27-notes.md` § 11–12, sign-off\nG–G7, `?clicker=pins|brush|area|arrow`); **Logo**, the refined mark in the rail (§ 13,\nsign-off H, `?logo=refined|live`); and the **Proposals** row, the nineteen reference-file\nfeatures with nothing in their way (§ 15, sign-off I, `?proposals=all|none` or\n`?p=composed,cap,…`). The file opens with the proposals on; `?proposals=none` is the\nbaseline, and every baseline screenshot and self-check block is taken that way."),
    ("| `v27-build/` | The build inputs and drivers: `case-record.src.html` (markup with placeholders), `case-record.js` (mock behaviour), `build.py`",
     "| `v27-build/` | The build inputs and drivers: `case-record.src.html` (markup with placeholders), `case-record.js` (mock behaviour), `proposals.js` (the § 15 proposals, spliced in by the build), `build.py`"),
    ("Items G–G7 in § 11–12 ask\nwhich of the four damage selector variants (if any) carries into Stage 2 and settle the\narea wording; item H in § 13 is the refined mark. Nothing is\napproved yet.",
     "Items G–G7 in § 11–12 ask\nwhich of the four damage selector variants (if any) carries into Stage 2 and settle the\narea wording; item H in § 13 is the refined mark; items I and I1 in § 15 are the nineteen\nreference proposals. Nothing is approved yet."),
])

patch(root / 'pages/cases/case-record/README.md', [
    ("- [74-logo-live.png](../../../current/v27-shots/74-logo-live.png) — the live lockup for comparison",
     """- [74-logo-live.png](../../../current/v27-shots/74-logo-live.png) — the live lockup for comparison
- [75-p-overview-edit.png](../../../current/v27-shots/75-p-overview-edit.png) — proposals on: ribbon badges, placements, matter line
- [76-p-inspection-vehicle.png](../../../current/v27-shots/76-p-inspection-vehicle.png) — composed sentences on Inspection
- [77-p-vehicle-edit.png](../../../current/v27-shots/77-p-vehicle-edit.png) — Vehicle with Damage and Valuation folded in, composed sentences, unrelated damage
- [78-p-valuation-edit.png](../../../current/v27-shots/78-p-valuation-edit.png) — CAP card, report switches, what the report carries
- [79-p-estimate-edit.png](../../../current/v27-shots/79-p-estimate-edit.png) — supplementary panel, regional uplift (Croydon), off-pattern cell, delete all
- [80-p-estimate-supp.png](../../../current/v27-shots/80-p-estimate-supp.png) — provenance chips and the supplementary panel in read
- [81-p-compare-diff.png](../../../current/v27-shots/81-p-compare-diff.png) — Compare with the per-line diff and Print
- [82-p-settlement-edit.png](../../../current/v27-shots/82-p-settlement-edit.png) — salvage slider and the reason bank
- [83-p-report-edit.png](../../../current/v27-shots/83-p-report-edit.png) — address book, attachments, file name and message
- [84-p-report-fee-tab.png](../../../current/v27-shots/84-p-report-fee-tab.png) — the Fee tab
- [85-p-files-include.png](../../../current/v27-shots/85-p-files-include.png) — click to include
- [86-p-notes-queries.png](../../../current/v27-shots/86-p-notes-queries.png) — the Queries panel
- [87-p-nine-sections.png](../../../current/v27-shots/87-p-nine-sections.png) — the nine-section map
- [88-p-nine-claim.png](../../../current/v27-shots/88-p-nine-claim.png) — the Claim section
- [89-p-nine-vehicle-full.png](../../../current/v27-shots/89-p-nine-vehicle-full.png) — Vehicle with Damage and Valuation, whole page"""),
    ("and the refined mark in the rail (sign-off H).", "the refined mark in the rail (sign-off H), and the nineteen reference-file features with nothing in their way, each a switch on the strip's Proposals row (sign-off I)."),
])

patch(root / 'pages/cases/case-record/states/README.md', [
    ("- Design variable — Logo: refined mark (72, 73), live lockup (74)",
     "- Design variable — Logo: refined mark (72, 73), live lockup (74)\n- Design variables — Proposals (nineteen switches, `v27-notes.md` § 15): all on (75–89) or all off (every baseline shot); fixture switch Repairer: Bootle or Croydon (79)"),
])

patch(root / 'pages/cases/case-record/how-it-should-work.md', [
    ("Open: which of the reference file's absent or differently built features\n([differences](../../../pegasus_case_dashboard_2026-09-15-differences.md)) the\noperator wants proposed for this page.",
     "Open: which of the nineteen reference-file features now on the mockup's Proposals\nrow ([`../../../current/v27-notes.md`](../../../current/v27-notes.md) § 15, item I)\ncarry into Stage 2, and whether `nine` and `badges` may change the settled frame\nrules (I1)."),
])
print('docs')

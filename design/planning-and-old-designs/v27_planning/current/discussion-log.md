# v27 — discussion log

Chronological. Operator words are quoted. This log records how the round came
to be; it is not design authority.

## 16 September 2026 — the reference file and the baseline

The operator opened the round with a reference file, `pegasus_case_dashboard_2026-09-15.html`
(the v25 · 9 Sep dashboard), and asked first: *"Examine all its features and
functions being used and describe all of them back to me"*, then *"save this
in the v27 folder"*. The inventory is
[`../pegasus_case_dashboard_2026-09-15-features.md`](../pegasus_case_dashboard_2026-09-15-features.md).

Next: *"compare it to the current design and features and identify
differences"*, saved on request as
[`../pegasus_case_dashboard_2026-09-15-differences.md`](../pegasus_case_dashboard_2026-09-15-differences.md).
The comparison was read against the live Case record on `dev` and FRD-01,
FRD-06, FRD-08, FRD-11 and FRD-12, and sorts every feature into Same, Built
differently, Absent or Conflicts with a settled decision.

Then the Stage 1 skill was invoked with: *"Create a mockup of the *existing*
Pegasus UI and save to this folder"*. Read as: before any v27 proposal, a
faithful offline picture of the live Case record, so that proposals can be
drawn and reviewed as diffs against it.

What was explored:

- Capturing a running instance was considered and set aside: no seeded
  development database exists (`--initialize-development` creates identity
  and roles only), the retired visual host is gone, and the integration
  fixtures are sparse. A hand transcription of the Razor output with the live
  CSS inlined gives the same pixels for one fixture and can be audited.
- The v26 mockup was not reused as the base: its markup vocabulary
  (`case-section`, `v25-identity`, `viewer-*`) differs from the live one
  (`record-section`, `ribbon`, `case-viewer-*`), so a baseline built on it
  would not be the live page.

What was built: `pegasus_case_record_v27.html` from `origin/dev` at
`5765a527a`, the self-check (608 checks, none failing), 65 screenshots, and a
coverage audit of every live label string, which added seven states the first
transcription had missed (stale-save conflict, Case unavailable, the one-time
upload secret, claimant-VAT block, EVA policy variants, intake photograph
group, retained estimate sources).

Sign-off items raised: A–F in `v27-notes.md` § 7 (fidelity, fixture, shell
scope, placement, build inputs, reference file location). No design decision
is open; the round's proposals have not started.

## 16 September 2026 — damage selector variants

The operator: *"add 4 new varying options for the vehicle damage selector.
Requirement on this - we dont want to select specific panels/sections - we
want to be able to click anywhere on the vehicle to indicate the damage"*.

Built as the strip variable **Damage selector** on the Damage section: A ·
Pins, B · Brush, C · Areas, D · Impact arrows, each a click-anywhere gesture
on the live Plan silhouette with the panels under the mark derived for Core
rather than chosen. Every variant keeps the recorded list, severity and note
per mark, the derived Impact location and severity and the narrative.
Twenty self-check assertions cover the four (fixtures, read-mode inertness,
off-vehicle clicks, dropping, stroking, sizing, arrow length to severity,
and switching back to the live clicker). Shots 66–71.

Sign-off items raised: G (which variant), G1 (what is stored), G2 (one mark
spanning panels), G3 (arrow length and direction as facts). See
`v27-notes.md` § 11.

## 16 September 2026 — areas instead of panels, and the refined mark

The operator: *"rather than denoting the actual panels, we just want to
state an 'area' of the vehicle that was affected"*, with the list Front, LH
Front, LH Rear, LH Side, Rear, RH Front, RH Rear, RH Side. Applied to all
four variants: the rows, the derived location and the narrative now speak in
those eight areas, with dashed guides and a hover readout while editing. The
eight are Core's broad zones, so only the words are new. Items G4–G7 raised
(roof, LH/RH wording, the three chips, the list heading). Self-check 630.

Then: *"replace old logo with this one on the mockup"* with
`logo/pegasus-mark-refined.png`. Embedded at 128px as the default brand mark
with a strip switch back to the live lockup; item H raised. Shots 72–74.

## 16 September 2026 — row wording

The operator, on a cropped screenshot of the mark rows: *"doesnt need to
specify the word area"*. The rows now carry the area names alone ("Rear",
"LH Side, RH Side"), as the live zone rows carry the zone name alone. The
operator also asked which differences from the reference file are still to
be folded in; the answer is recorded in `v27-notes.md` § 14.

## 16 September 2026 — the reference proposals

The operator: *"implement all the 'nothing in the way' section now on the
mockup"*. The nineteen items of `v27-notes.md` § 14's first table are on the
strip's **Proposals** row, each its own switch, all on by default, with All
off for the baseline. The self-check runs the baseline blocks with every
proposal off and then a block with them all on (663 checks, none failing);
shots 75–89. Items I and I1 raised. The nine-section map and the reference
placements are DOM moves on the live sections, reversible from the strip, so
the baseline is untouched underneath.

The operator, on Compare: *"Compare shouldn't be showing unless the dropdown
has selected it"*. The per-line diff, its summary and Print now appear only
once both From and To are chosen; the dialog opens with the live totals table
and two empty choices.

The operator: *"didnt include the report wording section"*. The reference
file's report wording well was on the decision-first list (FRD-11's template
rule); drawn now on instruction as the `wording` switch — the composed
narrative blocks in print order, editable, renameable, removable, reorderable,
with recompose and new paragraphs — and item I2 raised for the rule change
it needs. Shots 90–91.

The operator, on the Supplementary panel: *"it still shows"* — the earlier
Compare remark had meant this panel too. It now shows nothing until **changes
vs** has a version chosen. Asked what else from the reference is not yet on
the mockup: three small behaviours the differences file had read as "Same"
were still missing — the decision tick rows, Sign-off Engineer following the
hand-off, and the report date stamped on generate — added as `ticks`,
`signoff` and `reportdate` (item I3). Everything else left is on the
decision-first list.

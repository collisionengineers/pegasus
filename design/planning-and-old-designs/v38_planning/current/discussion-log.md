# v38 discussion log

Chronological record of how the round came to be. It is not design authority. See the [notes](v38-notes.md) for what stands.

## 12 October 2026: the brief

The operator opened the 15 September Case dashboard reference (`v27_planning/pegasus_case_dashboard_2026-09-15.html`):

> Lets go back to this design again. What significant differences are there to todays design?

The whole-page comparison was given in the session (most of the dashboard had been adopted by v28 on 18–22 September; three of the 16 September "conflicts" had since moved towards it: save as you go with Done, Take over, click-a-card valuation). The operator narrowed the question:

> focus more on the repair spec itself only.

The Repair Spec comparison followed (one grid versus named specs; Compare spec to spec, not version to version, with no side-by-side and no print sheet; the version-freezing acts; Supplementary chosen by hand; every column open on every type against the reference's per-type map; the ABP rate segments versus the rate-card select; the slider's range; the agreed sum informational rather than driving; the head tools the reference lacks). Asked which of the reference's Repair Spec ideas to bring back, the operator answered:

> Per-type column availability, Version-to-version Compare, Automatic Supplementary, Agreed sum drives the spec, labour rate also below repair spec. create a mockup

Those five are the request. The plan approved the same day: fork the v36 tooling, keep the v36 frames for the rest of the page, render the Repair Spec section from the reference's own grid data mapped to today's seven types, one variable per proposal, Today and Proposal, Diff and Split, the Playwright self-check and the three widths.

## 12 October 2026: Stage 1 built

Built the same day: `pegasus_repair_spec_v38.html`, the model, renderer and runtime under `lib/`, the build and check scripts, 37 numbered shots. The first draft of the notes lettered ten items (the rate bar's placement, the agreed sum's trigger and its box, Compare's default pair and labels, Supplementary's base and Dismiss, the type map and the dash in edit mode, the folder).

## 12 October 2026: the items withdrawn

The operator:

> things i tell you to do are not issues to be settled.

Nine of the ten restated the request or asked about mechanics the reference file already has. They were withdrawn and the mockup now follows the reference on each: the rate bar is drawn as well as the head cells ("also"); typing the agreed sum rescales and the mismatch chip rescales on press; Compare opens on the last sent version against the working draft with Compare with current and Print comparison sheet; Supplementary fills itself after an import against the last sent version, with Dismiss; a cell the type does not price reads "—" and an imported value in one stays amber. Section 4 of the notes records what was taken from the reference and the three column choices the reference could not make (Part no., Paint h, Other). Item A (the folder) is the one left. Self-check rerun: see the notes' section 7.

## 12 October 2026: Supplementary matched to the reference

The operator, on the first draft's Supplementary (filled, but still a sub-panel at the foot on a second spec):

> try harder to make it match. this supplementary bit is at the top clearly, and i think its from uploading/importing something onto an existing repair spec

Redrawn as the reference has it: the supplementary estimate is imported onto the existing repair spec (the grid it replaces frozen as a version, the import frozen too, as a Glass's return is today), so the proposal's Case holds one spec with three versions; the panel opens at the top of the section above the grid with the version it changes against, the cost movement, a full comparison link, one line per change, the Explain tick, the reason and Dismiss; the statement appears once Explain is ticked. Today's fixture keeps the live behaviour (an import makes a second spec, the sub-panel at the foot waits for Changes vs) so the two can be compared. Self-check rerun: see the notes' section 7.

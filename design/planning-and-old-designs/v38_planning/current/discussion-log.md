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

Built the same day: `pegasus_repair_spec_v38.html`, the model, renderer and runtime under `lib/`, the build and check scripts, 37 numbered shots, `RESULT {"fail":[],"okCount":370}`. Items A–J raised in the notes; nothing yet settled.

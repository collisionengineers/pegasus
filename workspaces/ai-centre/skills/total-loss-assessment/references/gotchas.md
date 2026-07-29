# Gotchas — Real Mistakes from Previous Sessions

> **Source-workspace boundary:** Reduce to validator-linked current checks; delete historical session notes after distillation. This is evidence, an example, a package-local format, or an experiment only; `Pegasus.Core`, current operator authority, and an authorised human own every accepted fact, cost, category, outcome, legal position, send, report issue, and approval.


These are verified errors. Check each one before building.

1. **Don't confuse trim levels.** A wing badge that says "X5M" might be retrofit; the VIN tells you whether it's actually an X5 M or X5 xDrive. State which you assumed.

2. **Don't include ADAS calibration on cars that don't have ADAS.** Pre-2017 mainstream cars usually don't. Pre-2010 anything almost certainly doesn't. Check before including.

3. **Don't include older vehicle allowance on borderline cars.** ABP says "over 10 years" — a 9-year-old car in 2026 is borderline; flag it before including.

4. **Don't use prestige rate on Kia / Honda / Toyota / Ford / Vauxhall / Nissan etc.** Even "approved repairer" rate is standard + £5, not prestige + £5.

5. **Don't put labour-time specialist items as `rnr`.** They hide on the Engineer's Report. Use `specialist_wu`. See `references/eva-routing.md` for the full list.

6. **Don't invent part numbers as if verified.** All parts without actual catalogue lookup must be `'unpriced': True` (shows `*` in PDF).

7. **Don't call the airbag warning light "pre-existing fault" on a side-impact job.** It almost certainly means SRS deployed. Mandatory full diagnostic + likely belt/airbag renewals + headliner removal labour.

8. **Don't forget storage charge** when the vehicle has rear screen out, deployed airbags, or is otherwise non-roadworthy.

9. **An instructed ceiling (e.g. QDOS's 80% of PAV) caps what may be AGREED, never what is COSTED.** Always complete the line-by-line estimate. When the total crosses the ceiling, that is the finding — state the repair total, the ceiling figure, and the ratio, and let the engineer decide. Never present a total-loss position without the costed estimate that evidences it.

10. **Don't add the standard ABP package on transcription jobs.** It double-counts. Set `sundry_parts_pct: 0.0` and back-calculate `paint_material_base` to match the source exactly.

11. **Keep CH46 9PY on all PDFs.** Even if a third-party letter shows a different postcode (e.g. CH49 6LH). The chrome is part of the tested layout.

12. **Don't modify `audatex_gen_v4.py`.** If it throws a Python error, fix the input dict. The error message tells you what's wrong with the input.

13. **EVA's last row in the Parts screen is always an empty manual-entry row.** It is NOT a ghost row from the PDF parser. Do not try to "fix" it.

## Estimate sanity checks

Run these checks after drafting the operations list, before presenting the estimate or calling
`build_pdf`:

- **Thin output:** if the operation list only covers the obvious outer panel, re-open the photos and
  check adjacent trim, lamps, brackets, sensors, wheel/tyre, suspension, glass, and inferred strip/refit.
- **Zero or near-zero parts total:** a real impact assessment with renewals normally has parts. If
  the parts subtotal is very low, confirm whether the job is genuinely repair/refinish only or
  whether renewals were missed.
- **Under-replacement:** renew instead of repair where the part is torn, split, sharply creased,
  structurally distorted, mount-broken, or more expensive to repair than replace.
- **Wrong side:** verify nearside/offside against the photo orientation, registration plate view,
  steering-wheel side, fuel-filler side, and sequence of images before finalising row descriptions.
- **Missed inferred damage:** an airbag/SRS light, wheel displacement, smashed rear screen, fluid
  leak, broken lamp mount, or distorted shut line usually implies additional checks or renewals.
- **Paint routing gap:** each renewed or repaired painted panel needs the matching paint operation;
  adjacent panels need blend operations where colour match or repair method makes blending necessary.

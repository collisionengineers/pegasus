# Gotchas — Real Mistakes from Previous Sessions

> **Source-workspace boundary:** This file is package-local validator evidence only; it is not a Pegasus caller, policy, current instruction, or acceptance authority. Current validator behavior and tests prevail over any narrative example.


These are verified errors. Check each one before building any output. Items 5, 6, 10, 11, 12,
and 13 apply to the Audatex/EVA payload and PDF; items 14–15 apply to document rendering; the
rest apply to every assessment.

0a. **Don't issue a final salvage category, structural condemnation, or total-loss verdict from
    photos alone.** Keep those provisional, name the missing evidence, and route to
    `salvage-categorisation` / AQP review where the decision has compliance consequences.

0b. **Don't state DVLA/DVSA facts (mileage, MOT, markers, tax) without a `vehicle-history-check`
    lookup.** If the connector is unavailable, label the fact as unverified case evidence.

0c. **Don't blur evidence labels.** A conclusion presented without its
    `official/live source` / `maintained reference` / `case evidence` / `inference` label reads
    as overclaiming when challenged.

1. **Don't confuse trim levels.** A wing badge that says "X5M" might be retrofit; the VIN tells you whether it's actually an X5 M or X5 xDrive. State which you assumed.

2. **Don't include ADAS calibration on cars that don't have ADAS.** Pre-2017 mainstream cars usually don't. Pre-2010 anything almost certainly doesn't. Check before including.

3. **Don't include older vehicle allowance on borderline cars.** ABP says "over 10 years" — a 9-year-old car in 2026 is borderline; flag it before including.

4. **Don't use prestige rate on Kia / Honda / Toyota / Ford / Vauxhall / Nissan etc.** Even "approved repairer" rate is standard + £5, not prestige + £5.

5. **Don't put labour-time specialist items as `rnr`.** They hide on the Engineer's Report. Use `specialist_wu`. See `references/eva-routing.md` for the full list.

6. **Don't invent part numbers as if verified.** All parts without actual catalogue lookup must be `'unpriced': True` (shows `*` in PDF).

7. **Don't call the airbag warning light "pre-existing fault" on a side-impact job.** It almost certainly means SRS deployed. Mandatory full diagnostic + likely belt/airbag renewals + headliner removal labour.

8. **Don't forget storage charge** when the vehicle has rear screen out, deployed airbags, or is otherwise non-roadworthy.

9. **An instructed ceiling (e.g. QDOS's 80% of PAV) caps what may be AGREED, never what is COSTED.** Always complete the line-by-line estimate. When the total crosses the ceiling, that is the finding — state the repair total, the ceiling figure, and the ratio, and let the engineer decide. Never present a total-loss or near-ceiling position without the costed estimate that evidences it. See `references/estimate-construction.md`.

10. **Don't add the standard ABP package on transcription jobs.** It double-counts. Set `sundry_parts_pct: 0.0` and back-calculate `paint_material_base` to match the source exactly.

11. **Keep CH46 9PY on all PDFs.** Even if a third-party letter shows a different postcode (e.g. CH49 6LH). The chrome is part of the tested layout.

12. **Don't modify `audatex_gen_v4.py`.** If it throws a Python error, fix the input dict. The error message tells you what's wrong with the input.

13. **EVA's last row in the Parts screen is always an empty manual-entry row.** It is NOT a ghost row from the PDF parser. Do not try to "fix" it.

14. **collisionrenderer transport fault — bounded diagnosis, max 2 calls.** Known issue: the connector's `validate`/`render` can receive the `data` argument as an escaped JSON string instead of an object; .NET deserialisation then fails at the root (a type/deserialisation error, often citing a byte offset). Protocol: (1) retry once passing `data` as a structured object; (2) if it fails identically, run the connector's own sample payload once — if that also fails with the same root-level error, it is transport, not your content. **Stop diagnosing.** Present the validated payload, note the fault in one line, and move on. Do not inspect byte offsets, build minimal test objects, or repeat the diagnosis per tool.

15. **Assessment before render ceremony.** Renderer health checks, template-shape fetches, house-style reads, and payload lint belong at render time, after the estimate and pack content exist — never as session pre-flight.

16. **Regional uplift requires stated location evidence.** The 15% uplift applies to hourly rates and storage for London and the home counties (`abp-reference-data.2026.json`). There is **no packaged postcode list** — the region call is an `inference` from case evidence. State the evidence used (e.g. "vehicle kept in <town> per the instruction letter"), label it, and flag marginal or unknown locations for the engineer. Never claim a check against an internal list.

17. **A justified charge is costed, not narrated.** Verified failure: a run justified storage in
    prose ("storage justified if held off-road pending repair") and flagged likely ADAS
    calibration, yet added neither to the estimate — the total understated the defensible cost.
    If the pack can argue for a charge, the estimate carries it as a `C`/`E`/`P` line or a
    stated per-day accrual; if it cannot, drop the argument. See the costing posture in
    `references/estimate-construction.md`.

**Estimate sanity checks** now live in `references/estimate-construction.md` — run them after
drafting the estimate table, before presenting the pack.

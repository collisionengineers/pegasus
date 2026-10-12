# v38 current: the Repair Spec section, today and as proposed

**What this is.** An offline HTML mockup of the Case page's Repair Spec section as it is today and as proposed, made at the operator's request on 12 October 2026 after a comparison of the 15 September Case dashboard reference with the live page. It is a temporary design review artifact: not application code, not design authority and not implementation evidence. The Stage 2 pull request removes or keeps this folder as the operator instructs.

**How to open it.** Open [pegasus_repair_spec_v38.html](pegasus_repair_spec_v38.html) in a browser. No server or network is needed. It opens reading the Current spec, as today, scrolled to the Repair Spec section; the strip at the bottom left is closed until clicked.

**Mockup controls.** The dark panel at the bottom left is a demo control, not product UI. It holds:

- Design: Today or Proposal (the master toggle)
- Mode: Read or Edit; Spec (today only): the earlier spec (with a sent version) or the Current one (just imported); the proposal holds one spec, the import having landed on it
- Presets: the Target % preview at 60 %; an agreed contract sum of £2,750; Explain on report ticked
- Widgets: Diff (outline and label every region a proposal changed, with a legend), Split (Today and Proposal side by side at half size)
- Dialog by name: Repair Spec versions, Compare repair specs, Delete all lines, Discard repair spec
- One variable per proposal: P1 `cols` (today, dash), P2 `cmp` (today, side), P3 `supp` (today, auto), P4 `agreed` (today, drives), P5 `rate` (head, both, bar)

Every choice is also a query string, for example `?design=proposal&mode=edit&spec=2&sum=2750&diff=1`.

**What is real and what is drawn.** The page frame (shell, ribbon, aside and every section but one) is the Case page as the running synthetic visual host rendered it on 9 October 2026 for the v36 round, restyled with the live `site.css` and `case-workspace.css` from this checkout; the runtime removes the two things the tip no longer draws (the Scroll/Tabs switch, the Decisions metric strip). The Repair Spec section is rendered from a fixture by [lib/repair-spec-v38-render.js](lib/repair-spec-v38-render.js) with the classes and label strings of `_CaseEstimate.cshtml` and `_RepairSpecDiff.cshtml` at `origin/dev` 6d47cd3cb, and its arithmetic ([lib/repair-spec-v38-model.mjs](lib/repair-spec-v38-model.mjs)) mirrors Core's `EstimateTotals.Compute`, `RepairSpecificationScaling`, `RepairSpecificationComparison.Compare` and `SupplementaryStatement`. The fixture's lines are the reference file's own repair grid mapped to today's seven line types; the spec names, dates and the Engineer's Value are fixture. Today's fixture holds two specs (the live import makes one); the proposal's holds one, the supplementary estimate imported onto it as the reference does. Every word is the application's own label or a fixture's; the four new labels are lettered in the notes. The only new styles are in [lib/repair-spec-v38.css](lib/repair-spec-v38.css).

| File | Holds |
| --- | --- |
| [pegasus_repair_spec_v38.html](pegasus_repair_spec_v38.html) | The mockup |
| [pegasus_case_dashboard_2026-09-15.html](pegasus_case_dashboard_2026-09-15.html) | The operator's reference file (the v25 · 9 Sep dashboard), copied from `../../v27_planning/` |
| [build-repair-spec-v38.mjs](build-repair-spec-v38.mjs) | `node build-repair-spec-v38.mjs` rebuilds the mockup from `captured/`, `lib/` and the live CSS in this checkout |
| [captured/](captured/README.md) | The two v36 frames (read and edit) and the tile images |
| [lib/](lib/repair-spec-v38-runtime.js) | The model (fixture and arithmetic), the renderer, the runtime (strip, presets, widgets, interactions) and the design CSS |
| [check-repair-spec-v38.py](check-repair-spec-v38.py) | Self-check and screenshot capture (Playwright) |
| [verification.json](verification.json) | The dated self-check record and the shot list |
| [v38-shots/](v38-shots/) | The mockup's numbered screenshots |
| [v38-notes.md](v38-notes.md) | The change table, live rules mirrored, frame rules, departures, the sign-off list, known limits |
| [discussion-log.md](discussion-log.md) | The operator's brief and each round |

## Status

Stage 1, delivered 12 October 2026. The five proposals are the operator's request and their mechanics are the reference file's; neither is an item. One item (A, keep or remove the folder) is open in [v38-notes.md](v38-notes.md) section 6. Stage 2 (the Razor implementation, FRD-25 and FRD-16 updates) starts on the operator's go-ahead.

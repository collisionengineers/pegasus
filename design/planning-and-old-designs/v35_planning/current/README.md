# v35 current: the Case aside's Next action

**What this is.** An offline HTML mockup of the Case page's aside (Figures and Next action) as it is today and as twelve proposals, made at the operator's request on 8 October 2026. In designs 7 to 12 Next action holds one step and the rest lives elsewhere. It is the record of a design review round: not application code, not design authority and not implementation evidence. The operator asked for it to be kept (item I).

**How to open it.** Open [pegasus_case_rail_v35.html](pegasus_case_rail_v35.html) in a browser. No server or network is needed.

**Mockup controls.** The dark panel at the bottom left is a demo control, not product UI. It holds:

- Design: Live today, or 1 grouped by section, 2 one line each, 3 Report not ready as its own card, 4 one row per section, 5 first item in full, 6 follows the page; and with one step in Next action, 7 the rest in a dialog, 8 in a closed card, 9 on the page, 10 in Report, 11 a checklist of sections, 12 a bar across the page
- Case state: Not ready with the original report, images and Case facts missing; Review with Assign Engineer and eleven blockers (the operator's screenshot); With Engineer with the same blockers; With Engineer with an AI draft, a cancellation and four blockers of the special kinds; Generate report; a stale generation; Create audit
- Step control (1–12): secondary or primary button
- Viewer: Administrator or User (the Accounts blocker links only for an Administrator)

The mockup opens on the Not ready Case. Every choice is also reachable by query string, for example `?design=4&state=near&role=user`.

**What is real and what is drawn.** The Case page around the aside is the page the application rendered, captured for the v33 round and restyled with the live `site.css` and `case-workspace.css` from this checkout (`origin/dev` 6be875542). The ribbon's state chip and Engineer follow the chosen state. "Live today" redraws the aside exactly as `_CaseAside.cshtml` renders it. The proposals use the live classes and tokens; the only new styles are in `lib/rail-v35.css`. The blocker words are Core's own (`AssessmentPolicy`, `CaseReportReadiness`); the fixtures are synthetic.

| File | Holds |
| --- | --- |
| [pegasus_case_rail_v35.html](pegasus_case_rail_v35.html) | The mockup |
| [build-rail-v35.mjs](build-rail-v35.mjs) | `node build-rail-v35.mjs` rebuilds the mockup from `captured/`, `lib/` and the live CSS in this checkout |
| [captured/](captured/frame-read.html) | The Case page as rendered for v33 (copied from v34) |
| [lib/](lib/rail-v35-runtime.js) | The states, the four aside drawings and the design CSS |
| [check-rail-v35.py](check-rail-v35.py) | Self-check and screenshot capture (Playwright) |
| [verification.json](verification.json) | The dated self-check record |
| [v35-shots/](v35-shots/r3-01-live-notready-1580.png) | Screenshots: `r4-*` for designs 7–12, `r3-*` for 1–6 (`python check-rail-v35.py --r3`), unprefixed for rounds 1 and 2 |
| [v35-notes.md](v35-notes.md) | What changes, live rules mirrored, departures, the sign-off list, known limits |
| [discussion-log.md](discussion-log.md) | The operator's brief and each round |

## Status

The operator chose design 8 and settled every item on 8 October 2026 ([v35-notes.md](v35-notes.md), section 12), and Stage 2 implemented it. FRD-16 and FRD-13 own the behaviour from then on; this folder is kept as the record (item I).

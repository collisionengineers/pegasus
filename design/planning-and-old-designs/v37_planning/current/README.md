# v37 current: three Management Reports proposals

**What this is.** Offline HTML mockups of Administration › Management Reports, made at the operator's request on 9 October 2026, with proposals for the reports behind the page. It is a temporary design review artifact. It is not application code, not design authority and not implementation evidence. The final Stage 2 pull request removes or keeps this folder as the operator instructs (item S).

**How to open it.** Open [pegasus_management_reports_designs_v37.html](pegasus_management_reports_designs_v37.html) in a browser and pick a design. Each design file opens on its own; no server or network is needed. [pegasus_management_reports_live_v37.html](pegasus_management_reports_live_v37.html) is today's page drawn from the same data, for side-by-side comparison.

**Mockup controls.** The dark panel at the bottom left is a demo control, not product UI. It holds:
- the design switcher: A, B, C and Today;
- the 12 page states;
- the ten open choices and proposals as switches. Inspection and Audit is item D, Explanatory notes C, MI labels M, Person choices N, Queues now E, Cases by stage F, Previous period G, Period choice H, Month bars I, and Outcomes O.

Every state is also reachable by query string:
- `state=`, and `opt=` with values such as `work:columns`, `notes:keep`, `queues:on`;
- `report=` (B);
- `measure=` and `principal=` (C);
- `work=`, `workm=`, `sort=`, `dir=`, `msort=` and `mdir=`;
- `embed=1` hides the strip.

| File | Holds |
| --- | --- |
| [pegasus_management_reports_designs_v37.html](pegasus_management_reports_designs_v37.html) | Comparison page: the three designs, what they share, and the recommendation |
| [pegasus_management_reports_a_v37.html](pegasus_management_reports_a_v37.html) | A · Tidied sections |
| [pegasus_management_reports_b_v37.html](pegasus_management_reports_b_v37.html) | B · Overview first (recommended) |
| [pegasus_management_reports_c_v37.html](pegasus_management_reports_c_v37.html) | C · Month ledger |
| [pegasus_management_reports_live_v37.html](pegasus_management_reports_live_v37.html) | Today: `Reports.cshtml` at 970ef9f10 drawn from the same fixtures |
| [build-management-reports-v37.mjs](build-management-reports-v37.mjs) | `node build-management-reports-v37.mjs` rebuilds all five files from `lib/` and the live shell, CSS and sprite in this checkout |
| [lib/](lib/shared.mjs) | The shared shell (from v32, with admin.css and the Administration nav added), fixtures, the one runtime for all four pages, and CSS |
| [check-management-reports-v37.py](check-management-reports-v37.py) | Self-check and screenshot capture (Playwright) |
| [pagedocs-v37.py](pagedocs-v37.py) | Writes the page folder READMEs and the screenshot table from `v37-shots/` |
| [capture-live-v37.py](capture-live-v37.py) | Captures the running page from the local visual host and diffs its structure against the Today file |
| [captured/](captured/structure-reports-live.json) | The running page's DOM and the structure diff |
| [live-shots/](live-shots/live-reports-1440.png) | The running page at 1580, 1440 and 760, full page, and a sorted head |
| [v37-shots/](v37-shots/verification.json) | Screenshots and the dated self-check record |
| [v37-notes.md](v37-notes.md) | What the operator settled, changes, live rules mirrored, departures, lettered sign-off items, known limits, what needs new recording, Stage 2 sketch |
| [discussion-log.md](discussion-log.md) | The operator's brief and each round |

## Status

Stage 1 delivered on 9 October 2026; every lettered item is open. Stage 2 waits for the operator's answers and approval.

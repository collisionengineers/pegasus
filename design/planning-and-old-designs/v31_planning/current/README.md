# v31 current: three Triage Case proposals

**What this is.** These are offline HTML mockups of the Triage Case page, made at the operator's request on 5 October 2026. It is a temporary design review artifact. It is not application code, not design authority and not implementation evidence. The final Stage 2 PR removes or keeps this folder as the operator instructs.

**How to open it.** Open [pegasus_triage_case_designs_v31.html](pegasus_triage_case_designs_v31.html) in a browser and pick a design. Each design file opens on its own; no server or network is needed.

**Mockup controls.** The dark panel at the bottom left is a demo control, not product UI. It holds:
- the A/B/C switch
- the 13 page states
- the item B switch (the next step beside Actions, or everything in Actions)
- an opener for every dialog

Every state is also reachable by query string: `state=`, `opt=primary:on|off`, `dialog=`, `tab=` (C only), `files=documents|correspondence` and `embed=1`.

| File | Holds |
| --- | --- |
| [pegasus_triage_case_designs_v31.html](pegasus_triage_case_designs_v31.html) | Comparison page: the three designs, what they share, and the recommendation |
| [pegasus_triage_case_a_v31.html](pegasus_triage_case_a_v31.html) | A · Image stage |
| [pegasus_triage_case_b_v31.html](pegasus_triage_case_b_v31.html) | B · Inspection split (recommended) |
| [pegasus_triage_case_c_v31.html](pegasus_triage_case_c_v31.html) | C · Contact sheet |
| [build-triage-designs.mjs](build-triage-designs.mjs) | `node build-triage-designs.mjs` rebuilds all four files from `lib/` and the live shell, CSS and sprite in this checkout |
| [lib/](lib/shared.mjs) | Shared shell (copied from v30), fixtures, synthetic vehicle scenes, runtime and CSS |
| [check-triage-designs.py](check-triage-designs.py) | Self-check and screenshot capture (Playwright) |
| [v31-triage-shots/](v31-triage-shots/verification.json) | Screenshots and the dated self-check record |
| [v31-notes.md](v31-notes.md) | Changes, live rules mirrored, departures, lettered sign-off items, known limits |
| [discussion-log.md](discussion-log.md) | The operator's brief and each round |

## Status

As of 5 October 2026, Stage 1 is delivered and waiting for the operator's choice of design (item A) and the settlement of items B–F in [v31-notes.md](v31-notes.md). Stage 2 does not start until then.

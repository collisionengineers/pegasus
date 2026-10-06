# v33 current: five Valuation proposals

**What this is.** Offline HTML mockups of the Valuation section of the Case page, made at the operator's request on 6 October 2026. The calculation sits far from the Engineer's Value box it fills, the same figure is shown again as "Proposed Engineer's Value", and "Applied Engineer's Value" reads "None yet" beside a filled box. It is a temporary design review artifact. It is not application code, not design authority and not implementation evidence. The Stage 2 pull request removes or keeps this folder as the operator instructs.

**How to open it.** Open [pegasus_valuation_designs_v33.html](pegasus_valuation_designs_v33.html) in a browser and pick a design. Each design file opens on its own; no server or network is needed. [pegasus_valuation_live_v33.html](pegasus_valuation_live_v33.html) is today's section, as the application rendered it, for side-by-side comparison.

**Mockup controls.** The dark panel at the bottom left is a demo control, not product UI. It holds:
- the A to E switch and Today
- the eleven page states (six on Today)
- the two undecided choices as switches: how a recorded calculation is shown (item D) and where AI market research sits (item F)

Every state is also reachable by query string: `state=` and `opt=record:line,ai:own`.

**What is real and what is drawn.** The Case page around the section (rail, ribbon, section row, aside, the other sections collapsed to their heads) is the page the application rendered, captured from the integration-test host and restyled with the live `site.css` and `case-workspace.css` from this checkout. Today's section is that capture too. The five designs are drawn inside it from the same classes.

| File | Holds |
| --- | --- |
| [pegasus_valuation_designs_v33.html](pegasus_valuation_designs_v33.html) | Comparison page: the five designs, what they share, and the recommendation |
| [pegasus_valuation_a_v33.html](pegasus_valuation_a_v33.html) | A · Calculation under the boxes |
| [pegasus_valuation_b_v33.html](pegasus_valuation_b_v33.html) | B · Worksheet |
| [pegasus_valuation_c_v33.html](pegasus_valuation_c_v33.html) | C · Three columns (recommended) |
| [pegasus_valuation_d_v33.html](pegasus_valuation_d_v33.html) | D · Chosen source opens |
| [pegasus_valuation_e_v33.html](pegasus_valuation_e_v33.html) | E · Side by side |
| [pegasus_valuation_live_v33.html](pegasus_valuation_live_v33.html) | Today: the captured section in six states |
| [build-valuation-designs.mjs](build-valuation-designs.mjs) | `node build-valuation-designs.mjs` rebuilds all seven files from `captured/`, `lib/` and the live CSS in this checkout |
| [captured/](captured/capture-test.patch) | The Case page as rendered (read and edit), the Valuation section in six states, and the temporary test that produced them |
| [lib/](lib/valuation-fixtures.mjs) | The five designs' descriptions and states, the design CSS and the runtime |
| [check-valuation-designs.py](check-valuation-designs.py) | Self-check and screenshot capture (Playwright) |
| [v33-valuation-shots/](v33-valuation-shots/verification.json) | Screenshots and the dated self-check record |
| [v33-notes.md](v33-notes.md) | What changes, live rules mirrored, departures, lettered sign-off items, known limits |
| [discussion-log.md](discussion-log.md) | The operator's brief and each round |

## Status

Stage 1 is delivered and waiting. Nothing in the application has changed. Items A to O in [v33-notes.md](v33-notes.md) await the operator; Stage 2 starts only when they are settled.

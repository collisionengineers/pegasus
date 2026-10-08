# v34 current: the Valuation section as guide cards

**What this is.** An offline HTML mockup of the Case page's Valuation section, redrawn as a grid of guide cards after two screenshots the operator supplied on 8 October 2026. It is a temporary design review artifact. It is not application code, not design authority and not implementation evidence. The Stage 2 pull request removes or keeps this folder as the operator instructs (item K).

**How to open it.** Open [pegasus_case_valuation_v34.html](pegasus_case_valuation_v34.html) in a browser. No server or network is needed. Today's section, design D as implemented, is in the v33 round's [conformance screenshots](../../v33_planning/current/v33-conformance/verification.json).

**Mockup controls.** The dark panel at the bottom left is a demo control, not product UI. It holds:
- the eleven page states
- the two undecided choices as switches: the chosen card's word (item B) and how a source with no provider reads (item C)
- Reset

Every state is also reachable by query string, for example `?state=recorded-read&opt=word:basis,manual:sentence`. The strip's choices are kept per browser under the `v34.options` key.

**What is real and what is drawn.** The Case page around the section is the page the application rendered: the rail, ribbon, section row, aside and the other sections collapsed to their heads. It was captured for the v33 round (`origin/dev` f5bc6e5f1) and restyled with the live `site.css` and `case-workspace.css` from this checkout (acb2ffbd1). The section is drawn inside it from the live classes and tokens. The only new styles are in `lib/valuation-v34.css`, and the −10 % / −20 % choice reuses the live Scroll / Tabs switch.

| File | Holds |
| --- | --- |
| [pegasus_case_valuation_v34.html](pegasus_case_valuation_v34.html) | The mockup |
| [build-valuation-v34.mjs](build-valuation-v34.mjs) | `node build-valuation-v34.mjs` rebuilds the mockup from `captured/`, `lib/` and the live CSS in this checkout |
| [captured/](captured/frame-edit.html) | The Case page as rendered for v33, while editing and while reading |
| [lib/](lib/valuation-v34-fixtures.mjs) | States and strip choices, the design CSS and the runtime |
| [check-valuation-v34.py](check-valuation-v34.py) | Self-check and screenshot capture (Playwright) |
| [v34-shots/](v34-shots/verification.json) | Screenshots and the dated self-check record |
| [v34-notes.md](v34-notes.md) | What changes, live rules mirrored, departures, lettered sign-off items, known limits |
| [discussion-log.md](discussion-log.md) | The operator's brief and each round |

## Status

Awaiting the operator's sign-off on items A to K in [v34-notes.md](v34-notes.md). Stage 2 has not started.

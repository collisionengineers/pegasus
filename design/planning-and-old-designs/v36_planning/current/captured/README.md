# Captured frames

Read from the running synthetic visual host on 9 October 2026 (`origin/dev` 37c4b96f5, host patched under the ignored `artifacts/ui-baseline-review/visual-host/` for the CaseWorks schema). Each `frame-<state>-<mode>.html` is `document.documentElement.outerHTML` after `site.js` and `case-workspace.js` ran and every lazy section had mounted, taken by `../walk-v36.py`. `measure-<state>-<mode>.json` holds the geometry read at 1580, 1440 and 760 (sticky block, ribbon cells and chips, section row overflow, aside, panel heads, cells, controls and the control-height ladder). `walk-<state>.json` is the walk log; `extra.json` the edit-session pass. `frame-work-centre.html` and `frame-cases-list.html` are the two pages the busy-state finding draws. `images/` holds the four synthetic tile images the host served, inlined by the build.

Synthetic fixtures only; no customer data.

# v36 current: a full walk of the Case page

**What this is.** An offline HTML mockup of the whole Case page as it is today and as proposed after a full walk in read mode and in the edit session, made at the operator's request on 9 October 2026. It is a sharpening round inside the v26 frame, not a redesign. It is a temporary design review artifact: not application code, not design authority and not implementation evidence. The Stage 2 pull request removes or keeps this folder as the operator instructs.

**How to open it.** Open [pegasus_case_walk_v36.html](pegasus_case_walk_v36.html) in a browser. No server or network is needed. It opens on the With Engineer Case, reading, as today; the strip at the bottom left is closed until clicked.

**Mockup controls.** The dark panel at the bottom left is a demo control, not product UI. It holds:

- Design: Today or Proposal (the master toggle)
- Page: Case page, Work Centre, Cases list (the last two only for the busy-state finding)
- Case state: With Engineer (read and edit), Review, Held, a colleague editing, Not ready standalone Audit, Inspection + Audit in the Audit view (read and edit) and in the Inspection view
- Mode: Read or Edit, where the state has an edit frame; Layout: Scroll or Tabs; Section: land on a section
- Widgets: Diff (outline and number every region the proposal changed, with a legend), Split (Today and Proposal side by side at half size), Ruler (outline every control by its height: 36 green, 32 blue, 40 grey, 22–30 red), Busy (the pressed state of the navigating link buttons), Flicker (the ribbon and aside drawn twice after a save), Landing (where a Workflow action returns the reader: top, or the section)
- Dialog by name; the viewer on image 1
- One switch per finding, grouped by surface, with its tier (a: alignment inside the contract; b: lettered for sign-off; c: behaviour), and the operator's three items as choices: Get valuation (today, head-link, foot, inline), "Not recorded" (today, box, segments-none, select), the spec grid (today, aligned, prefix)

Every choice is also a query string, for example `?design=proposal&state=engineer&mode=edit&section=valuation&opt=getval:foot&diff=1`.

**What is real and what is drawn.** The frames are the Case page as the running synthetic visual host (`artifacts/ui-baseline-review/visual-host`, `origin/dev` 37c4b96f5) rendered it on 9 October 2026 with `site.js` and `case-workspace.js` run, so what the scripts hide or reveal is as live; they are restyled with the live `site.css` and `case-workspace.css` from this checkout and their scripts removed. Today is the frame untouched. The proposal is one function per finding in [lib/findings-v36.js](lib/findings-v36.js) rewriting the frame with the live classes and tokens; the only new styles are in [lib/case-walk-v36.css](lib/case-walk-v36.css). Every word is the application's own or a fixture's; the two fixture departures are named in the notes (Glass's drawn as connected for the Get valuation variants; the Held step's review date).

| File | Holds |
| --- | --- |
| [pegasus_case_walk_v36.html](pegasus_case_walk_v36.html) | The mockup |
| [build-case-walk-v36.mjs](build-case-walk-v36.mjs) | `node build-case-walk-v36.mjs` rebuilds the mockup from `captured/`, `lib/` and the live CSS in this checkout |
| [walk-v36.py](walk-v36.py), [walk-extra-v36.py](walk-extra-v36.py), [make-cases-v36.py](make-cases-v36.py) | The live walk: capture frames, measurements and `live-shots/` from the running host; the extra passes; the Cases created for the round |
| [captured/](captured/README.md) | The post-script frames per state and mode, the measurements, the walk logs and the tile images |
| [live-shots/](live-shots/) | Screenshots of the running host, by state, mode, width, section, dialog and Tabs (evidence for the findings) |
| [lib/](lib/case-walk-v36-runtime.js) | The runtime (strip, presets, widgets), the findings and the design CSS |
| [check-case-walk-v36.py](check-case-walk-v36.py) | Self-check and screenshot capture (Playwright) |
| [verification.json](verification.json) | The dated self-check record and the shot list |
| [v36-shots/](v36-shots/) | The mockup's numbered screenshots |
| [v36-notes.md](v36-notes.md) | The findings table, live rules mirrored, frame rules, departures, the sign-off list, known limits |
| [discussion-log.md](discussion-log.md) | The operator's brief and each round |

## Status

Stage 1. Awaiting the operator's decisions on the lettered items in [v36-notes.md](v36-notes.md) section 6, including the three items the operator raised on 9 October 2026 (Get valuation placement, "Not recorded" as a radio, busy states on the arrow link buttons) and the Repair Spec grid alignment. Stage 2 does not start until every letter is settled.

# Case workspace v25 mockup

A temporary design review artifact, created at explicit operator request per
[`docs/index.md`](../../../docs/index.md)'s carve-out for such material. It is not
application code, not design authority, and not implementation evidence. **Remove this
folder once Stage 2 (the Razor implementation) lands, or once the design here is formally
accepted or rejected.**

## What this is

`pegasus_case_workspace_v25.html` is a self-contained, offline mockup of one Pegasus Case
record. It merges the operator's private reference mockup
(`pegasus_case_dashboard_v24.html`, included alongside it in this folder) with the actual
shell, tokens, section structure and Core policy shapes of the live application, taken
from `origin/dev`. It proposes fixes to real, identified problems in the deployed Case record:
a five-row sticky header, read and edit modes that render as two different pages, actions
that reload to the top of the page, editing that is silently unavailable with no stated
reason, and repeated facts across the ribbon, aside and body.

Open the HTML file directly in any browser — no server, build or network access needed.
The dark strip at the bottom left ("Mockup state") is a demo control, not product UI: it
switches the Case's lifecycle state so each availability rule can be exercised, and on the
Case record also picks between the three vehicle damage clickers (Plan, Elevations, Dial).

## Files

| File | Purpose |
| --- | --- |
| `pegasus_case_dashboard_v24.html` | The operator's original private reference mockup that `pegasus_case_workspace_v25.html` was built from. |
| `pegasus_case_workspace_v25.html` | The v25 interactive mockup (superseded by v26, kept for comparison). |
| `pegasus_case_workspace_v26.html` | The current interactive mockup: v25 plus the 12 September usability pass, the live Glass's session rules, and the manager's decision review. |
| `v26-notes.md` | What v26 changes and why, the Glass's rules it mirrors, the three damage clicker variants, and the new sign-off items G–O. |
| `v25-notes.md` | Design decisions taken and their authority, and the list of items still needing operator sign-off before Stage 2 implementation begins. |
| `discussion-log.md` | Chronological record of the request, the exploration, and each round of operator feedback that shaped the mockup. |
| `v25-shots/` | Headless-Chromium screenshots taken across the v25 session's iterations, at 1580, 1440 and 760px. |
| `pegasus_shell_v26.html` | Every other page in the rail (Work Centre, Inbox, Upload, Cases, Search, Operations, Administration and the pre-Case records) on the same shell; opens the Case record file from any list. |
| `v26-shell-selfcheck.html` | Scripted check that renders every shell route, opens every dialog and drives the main flows; see `v26-notes.md` § Shell pages. |
| `v26-selfcheck.html` | Scripted check that drives every v26 section, state and interaction in a headless browser; see `v26-notes.md` § Self-check. |
| `v26-shots/` | Headless-Chromium screenshots of every v26 state (lifecycle, Glass's session, decisions, breakpoints) and, as `s*.png`, every shell route. |

## Status

v25 was built and iterated across three rounds of feedback. v26 (12 September)
applies the usability review, folds in the Glass's behaviour on `origin/dev` and
the manager's decision review. Not yet given final approval to proceed to
Stage 2. See `v25-notes.md` (A–F) and `v26-notes.md` (G–K) for the decisions
awaiting sign-off.

# Case workspace v25 mockup

A temporary design review artifact, created at explicit operator request per
[`docs/index.md`](../../../docs/index.md)'s carve-out for such material. It is not
application code, not design authority, and not implementation evidence. **Remove this
folder once Stage 2 (the Razor implementation) lands, or once the design here is formally
accepted or rejected.**

## What this is

`pegasus_case_workspace_v25.html` is a self-contained, offline mockup of one Pegasus Case
record. It merges a private reference mockup (`pegasus_case_dashboard_v24.html`, kept in
the operator's local `pegasus_pack/ui/`, not published here) with the actual shell,
tokens, section structure and Core policy shapes of the live application, taken from
`origin/dev`. It proposes fixes to real, identified problems in the deployed Case record:
a five-row sticky header, read and edit modes that render as two different pages, actions
that reload to the top of the page, editing that is silently unavailable with no stated
reason, and repeated facts across the ribbon, aside and body.

Open the HTML file directly in any browser — no server, build or network access needed.
The dark strip at the bottom left ("Mockup state") is a demo control, not product UI: it
switches the Case's lifecycle state so each availability rule can be exercised.

## Files

| File | Purpose |
| --- | --- |
| `pegasus_case_workspace_v25.html` | The interactive mockup. |
| `v25-notes.md` | Design decisions taken and their authority, and the list of items still needing operator sign-off before Stage 2 implementation begins. |
| `discussion-log.md` | Chronological record of the request, the exploration, and each round of operator feedback that shaped the mockup. |
| `v25-shots/` | Headless-Chromium screenshots taken across the session's iterations, at 1580, 1440 and 760px. |

## Status

Built and iterated across three rounds of feedback. Not yet given final approval to
proceed to Stage 2. See `v25-notes.md` for the specific decisions awaiting sign-off.

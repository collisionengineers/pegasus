# v29 current: five Upload flow alternatives

Open one HTML file directly in a browser. The **Mockup controls** strip at the
bottom left is a review control, not product UI. It switches among the five
options and thirteen query-string states. The files are self-contained and offline;
no upload or Case mutation occurs.

| Option | Design | Open |
| --- | --- | --- |
| A | Decision beside compact files | [A](pegasus_upload_a_v29.html?state=decision) |
| B | Guided single column | [B](pegasus_upload_b_v29.html?state=decision) |
| C | Destination first, files in evidence index | [C](pegasus_upload_c_v29.html?state=decision) |
| D | Full-width operations table | [D](pegasus_upload_d_v29.html?state=decision) |
| E | Evidence gallery | [E](pegasus_upload_e_v29.html?state=decision) |

Every file supports `?state=select`, `chosen`, `storing`, `processing`,
`decision`, `registered`, `mixed`, `single`, `failed`, `discarded`, `no-match`, `multiple`, and `attached`.
The [notes](v29-notes.md) compare the options and hold the lettered sign-off
list. The [discussion log](discussion-log.md) records feedback. The
[self-check](v29-selfcheck.html) and [screenshots](v29-shots/) are design
evidence only, not application or deployment evidence.

The five files embed the current `origin/dev` site CSS, fonts, and the shell
captured in v28. `build.mjs` reproduces them; `shoot.ps1` captures all states
at 1580Ã—1000, 1440Ã—900, and 760Ã—1000.

## Status

Stage 1. Layout choices remain open; Case-first priority is settled by operator feedback. No Razor change is
authorized by these mockups.

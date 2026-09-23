# v29 mockups: Case referencing and structure

**Temporary design review artifact.** It was created at the operator's request on
23 September 2026. It is not application code, not design authority and not
implementation evidence. The final Stage 2 PR keeps or removes it as the operator
instructs.

## What it is

Each page is the application's own server-rendered HTML, captured from a running
Pegasus.Web at `origin/dev` `446c3ce2f` against a synthetic local fixture: an
Inspection + Audit Case `QDOS31001`, a standalone Audit Case `a.QDOS31002`, one
open Triage and one Unidentified item. The v29 proposals load over the captured
pages from [`assets/mock/proposals.js`](assets/mock/proposals.js) and
[`assets/mock/proposals.css`](assets/mock/proposals.css). The captured
pages themselves are never edited.

## How to open it

Open a family file in a browser straight from the file system. There is no
server. The dark bar at the top is demo control, not product UI:

- pick a captured state, or a proposal preset under it;
- switch the proposals layer off to see the live baseline;
- open the next family.

| File | Surfaces |
| --- | --- |
| [pegasus_case_record_v29.html](pegasus_case_record_v29.html) | The Inspection + Audit Case: Audit and Inspection views, Create audit, the Report of each view |
| [pegasus_audit_record_v29.html](pegasus_audit_record_v29.html) | A standalone Audit Case, the regression check |
| [pegasus_triage_case_v29.html](pegasus_triage_case_v29.html) | A Triage Case at `/Cases/{id}` in the Case frame |
| [pegasus_work_centre_v29.html](pegasus_work_centre_v29.html) | Work Centre with the Triages metric, the Cases list, Create case with Triage |
| [pegasus_unidentified_search_v29.html](pegasus_unidentified_search_v29.html) | Open the Triage from Unidentified, Search finding a Triage Case |

Presets are query strings on a state page:

- `?stage=sent` shows the Inspection report sent, before Create audit;
- `?view=inspection` shows the Inspection view;
- `?dialog=create-audit` opens the Create audit dialog;
- `?opt=key:value` switches one undecided choice.

[`proposals.js`](assets/mock/proposals.js) lists them all.

| Other files | Holds |
| --- | --- |
| [v29-notes.md](v29-notes.md) | What changed and why, the live rules mirrored, departures, the lettered sign-off list, the self-check result and the known limits |
| [discussion-log.md](discussion-log.md) | The operator's words and what each round changed |
| `states/` | The captured pages (the live baseline) |
| `v29-shots/` | Screenshots at 1580×1000, 1440×900 and 760×1000: `sNN` baseline, `pNN` proposal |
| `v29-build/` | The capture, frame, screenshot and self-check scripts, the manifest and the presets |

## Status

Stage 1 is complete and **awaiting the operator's sign-off**. Every lettered item in
[v29-notes.md](v29-notes.md#6-sign-off-list) is open. Stage 2 does not start until
each is settled and the operator approves.

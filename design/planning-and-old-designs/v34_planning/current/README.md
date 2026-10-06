# v34 current: the report dispatch screens as built

A temporary design review artifact created at operator request on 6 October 2026. It is not application code, not design authority and not implementation evidence; the sign-off list in [v34-notes.md](v34-notes.md) decides what Stage 2 changes. Remove or retain the folder by operator instruction in the Stage 2 PR.

## What this is

PR 1015 was rebuilt on 6 October 2026 to the operator's rulings. Its screens exist in Razor but were never put to the operator as a design, so this round captures them as rendered and asks for sign-off before any is accepted. Each mockup is one offline HTML file: the live shell with the page as the server rendered it from the integration tests' own fixtures (`captured/`), the live CSS, fonts, scripts and the Pegasus mark inlined from `src/Pegasus.Web/wwwroot`, and a strip to switch states. There is no server, build or network behind a file.

Open any `pegasus_*_v34.html` in a browser. Add `?state=<key>` for a state and `&strip=0` to fold the strip.

**Mockup controls** (bottom left) is demo control, not product UI. It lists the file's states; choosing one reloads the page in that state. Posts and application links stay on the page.

## Files

| File | What it is | States (`?state=`) |
| --- | --- | --- |
| `pegasus_case_report_delivery_v34.html` | Case record, Report section, the one-step Send report form under the Principal's rules | `plain` default rules; `rules` two questions, a possible Stop, a hold and after-send tasks; `stop` a rule Stop applies; `missing` a required companion not generated |
| `pegasus_case_tasks_v34.html` | Case record, Tasks section | `edit` the edit session with Add, Assign, Complete, Cancel; `read` the list alone (the lazy fragment) |
| `pegasus_case_next_action_blocker_v34.html` | Case record aside and ribbon after Report sent with open tasks | `next` Mark completed greyed, tasks listed; `archive` a closed Case, Archive greyed |
| `pegasus_work_centre_tasks_v34.html` | Work Centre, the Open tasks row | `row` |
| `pegasus_contact_report_sending_v34.html` | Contacts, a Principal's Report sending panel | `kerr-edit` (editing, Default rules), `dfd`, `ax`, `mp`, `pch`, `rjs`, `qdos` (the seeded rules as read) |
| `captured/*.html` | The server-rendered pages the mockups are built from, and `capture-test.patch`, the temporary test hooks that wrote them (never committed to the tests) | |
| `build-v34.py` | Builds the five files from `captured/` and `wwwroot` | |
| `check-v34.py` | The self-check and screenshots: every state in headless Chromium, console errors and markers asserted, captures at 1580×1000, 1440×900 and 760×1000 plus a whole-page and a surface crop at 1580 | |
| `v34-shots/` | The screenshots and `verification.json` (dated result and shot list) | |
| `v34-samples/` | Three MP sample report PDFs for item V (with images; image-free with the lead slot left out; image-free with a framed empty slot) and their first pages as PNG | |
| `v34-notes.md` | What the round shows, the rules it mirrors, the sign-off list, the self-check result, known limits | |
| `discussion-log.md` | The round's conversation, in order | |

## Status

Awaiting the operator's sign-off on every lettered item in [v34-notes.md](v34-notes.md#6-sign-off-list). No item is settled. Stage 2 (the Razor changes those items decide) has not started; PR 1015 stays held until it has.

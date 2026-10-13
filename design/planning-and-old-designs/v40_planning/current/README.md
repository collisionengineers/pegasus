# v40 current: Contacts

A temporary design review artifact created at operator request on 13 October 2026. It is not application code, not design authority and not implementation evidence. The sign-off list in [v40-notes.md](v40-notes.md#6-sign-off-list) decides what Stage 2 changes. Remove or retain the folder by operator instruction in the Stage 2 PR.

## What this is

The Contacts list and a contact's page, organised by the contact's one type (operator ruling, 13 October 2026). `pegasus_contacts_v40.html` is one offline file: the live shell and the Principal's Report sending, Salvage matrix, Default inspection location and Principal API panels as the server rendered them for v39, the live CSS, fonts and scripts inlined from `src/Pegasus.Web/wwwroot`, and the list, the contact form, Report generation and the Add contact dialogs written from the live Razor with the proposal applied. There is no server, build or network behind it.

Open the file in a browser. Add `?state=<key>` for a state; see the strip for the rest.

**Mockup controls** (bottom left) is demo control, not product UI. It switches the page and state, opens a dialog, and sets the round's undecided choices (variables). Every choice is also a query string: `state`, `dialog` (`type`, `create`, `matches`), `create` (a type), `type`, `q`, `sort`, `tab`, `opt=key:value,…` and `strip=0`.

## Files

| File | What it is |
| --- | --- |
| `pegasus_contacts_v40.html` | The mockup. States: `list` (proposed), `list-live` (today), `principal`, `principal-saved`, `principal-refused`, `principal-inactive`, `principal-live` (as captured 6 Oct), `claim-source`, `repairer`, `storage`, `tpe`. Variables: `list` grouped/single, `layout` stack/tabs, `notes` fit/rows, `links` grid/list, `match` open/none |
| `build-v40.py` | Builds the mockup from the v39 capture and `wwwroot` |
| `v40-selfcheck.html` | The scripted self-check: every state, dialog, variable and query preset in a 1580×1000 frame, ending `RESULT {"fail":[],"okCount":N}` |
| `check-v40.py` | Runs the self-check headless and takes the screenshots at 1580×1000, 1440×900 and 760×1000, plus a whole-page shot of each page state |
| `v40-shots/` | The screenshots and `verification.json` (dated result and shot list) |
| `v40-notes.md` | What changed, the live rules mirrored, frame rules, decisions, departures, the sign-off list, the self-check result, known limits, the Stage 2 plan |
| `discussion-log.md` | The round's conversation, in order |

The previous renders sit in their own rounds: v39's Report sending captures (`../../v39_planning/current/pegasus_contact_report_sending_v39.html`) and v28's list and contact captures (`../../v28_planning/current/v28-shots/s51-admin-contacts-1580.png`, `s52-admin-contact-edit-1580.png`).

## Status

Awaiting the operator's sign-off on every lettered item, A to T, in [v40-notes.md](v40-notes.md#6-sign-off-list). No item is settled. Stage 2 has not started and waits on the domain-model PR that gives a contact one type.

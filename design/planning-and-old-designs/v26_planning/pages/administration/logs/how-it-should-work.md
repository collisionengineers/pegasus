# Administration › Logs — how it should work

Decided with the operator on 13 September 2026 while planning the received-file record ([`../received-file/how-it-should-work.md`](../../cases/received-file/how-it-should-work.md)).

## Logs becomes a group of two

Administration › Logs has two tabs on one page, sharing the filter bar (From, To, Search, Sort):

1. **Action logs** — as today.
2. **Intake log** — every received item, the cleaned-up successor of the received-file page. Administrators only.

## Intake log

One row per receipt, newest first, paged:

| Column | Value |
| --- | --- |
| Received | office time |
| Source | mailbox and sender, upload and who, provider API and principal |
| Item | file name or subject; the original opens in the viewer |
| Outcome | Case created · Unidentified · Triage · Vehicle images · Could not be read · Processing failed · Closed (with the reason) |
| Became | the Case reference, Unidentified reference, Triage or image reference it produced, as a link |
| Attempts | processing and allocation attempts, when more than one |

Filters: outcome, source kind, principal, date range, text. Search by reference, registration, claim number, sender, file name.

Row detail (a drawer, not a page): the retained original, the processing evidence (registration readings, suggested fields, decision evidence, allocation attempts) in a plain list, and the technical actions:

- **Re-evaluate with current policy** (reason required)
- **Retry allocation** (when the last allocation attempt failed)
- **Retry OCR** (when OCR failed)

Each action writes to the Action log as now. No Block, no Correct draft, no Link: those are operator actions and live on operator pages.

## Side panel

The existing intake metrics (Failed intake, Oldest pending intake due) move onto the Intake log tab as its head-line counts and become links into the filtered list.

## Audience

Administrators only (decided 13 September).

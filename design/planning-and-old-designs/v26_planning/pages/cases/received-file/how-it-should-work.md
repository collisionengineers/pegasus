# Received file — how it should work

Decided with the operator on 13 September 2026.

## D1. The receipt stays; the operator page goes

- The intake receipt remains as a record: hash, retained original, source, time, processing outcome, attempts. It is what makes intake idempotent, retryable and auditable (FRD-02). Nothing about the record changes.
- `/Intake/Details` is removed as an operator page. No User or Engineer surface shows a receipt, an "origin receipt", a decision code or an allocation attempt.
- The receipt's history is visible in one place only: **Administration › Logs › Intake log** (Administrators), specified in [`../action-logs/how-it-should-work.md`](../../administration/logs/how-it-should-work.md).

## D2. Every action on the old page moves to where a person already is

| Old action on the received-file page | Goes to | Note |
| --- | --- | --- |
| Block with reason | Unidentified › Close with reason | Blocked stops being a concept for operators; a refused item is a closed Unidentified item with its reason. See [`../unidentified/how-it-should-work.md`](../unidentified/how-it-should-work.md). |
| Record corrected draft | Create case, seeded from the item | already there; the copy on the receipt page is dropped. See [`../create-case/how-it-should-work.md`](../create-case/how-it-should-work.md). |
| Create a case from this item | Create case | as now |
| Link / Unlink selected Case | Unidentified › Link to Case; Case › Files › Add evidence | the receipt is never the thing being linked, the message or file is |
| Registration reading results, Dismiss suggestion, Register images, Open the Triage | Unidentified record and the image record | see [`../unidentified/how-it-should-work.md`](../unidentified/how-it-should-work.md) |
| Retry allocation, Re-evaluate with current policy, Retry OCR | Operations, as failed processing with the action on the row (Work Centre D1); the same actions on the Intake log row, which also holds the history | technical; Operations is where a failure is noticed, the Intake log is where its past is read |
| Inspection-address confirmation, instruction details, suggested fields, decision evidence | Create case (seeded) shows the suggested values; the evidence is in the Intake log | no operator needs the evidence panel to do their job |
| Scanned PDF pages, assets | the file viewer, opened from the message or the Case | the original, not the receipt |
| "View received item" links on Triage, Unidentified and image records | "Open message" or "Open file": the Inbox message, or the original in the viewer | never the receipt |

## D3. Outcomes a person can act on show where the material is

- **Could not be read** (Unsupported, OCR required that failed, Technical failure on a file): shown on the Inbox message's attachment and on the upload confirmation as "Could not be read", with the reason in operator words. The person re-sends or re-uploads; nothing else to do.
- **Processing failed** (system): Operations, per the Work Centre's D1. Not a person's problem until Operations says so.
- **Needs a person**: Unidentified, as now.

## D4. The Work Centre's Blocked metric goes

With Blocked gone as an operator concept the metric is removed. Refused material is a closed Unidentified item and is counted nowhere; unreadable material shows on its message; failed processing shows on Operations. The Work Centre decision that kept the metric unchanged (P7, not adopted) is superseded; see [`../work-centre/how-it-should-work.md`](../../work-centre/how-it-should-work.md).

## D5. Unidentified widens

Unidentified becomes everything a person has to sort, including material that is readable but must not become a Case. Its Close with reason is the one "no" in the system. The glossary entry ("cannot be established") widens accordingly and "Blocked intake" leaves CONTEXT.md; see [`../unidentified/how-it-should-work.md`](../unidentified/how-it-should-work.md).

## D6. Automation is unchanged

The MCP intake tools keep reading and writing receipt decisions on the record. Nothing operator-facing depends on the decision vocabulary any more, so the record can keep it.

## Where these decisions land

| Page | Entry |
| --- | --- |
| Work Centre | D7: Blocked metric removed, strip of four; Mail kind unchanged |
| Cases list | Blocked rows leave the Unidentified tab; Closed filter for closed Unidentified items |
| Unidentified | Close with reason absorbs Block; registration readings, Register images, Open the Triage; Open message / Open file; definition widens |
| Create case | the only place a draft is corrected; refusal offers Open message / Open file |
| Message | attachment outcomes in operator words; attachments open in the viewer |
| Upload | "Could not be read" on the confirmation; override and reversal on the Case's Files |
| Case record | Files › Add evidence and Remove with reason take the link and reversal |
| Triage, Image-initiated Case | Open message / Open file instead of View received item |
| Operations | failed intake processing rows with Retry allocation, Retry OCR, Re-evaluate |
| Administration hub, Logs | Logs card; Action logs + Intake log tabs |

## Documentation impact when the FRD is written

- CONTEXT.md: remove "Blocked intake"; widen "Unidentified".
- FRD-02: replace "Blocked intake records a reason and visible warning, offers reasoned resolve and retry actions" with Unidentified › Close with reason; the four received-file outcomes on the surface become message and upload outcomes.
- FRD-12: Work Centre strip of four; Cases › Unidentified tab lists Unidentified only; Administration › Logs with two tabs; `/Intake/Details` removed from the route table.

## Open questions, answered 13 September

1. **Close with reason vocabulary:** free text only.
2. **Intake log audience:** Administrators only.
3. **Unreadable e-mail attachments:** land in a work list. An item that could not be read becomes an Unidentified item with the reason "Could not be read" (and the file kind), so it ages and is due like any other Unidentified item. The Inbox message badge stays as well. Nothing is invisible.
4. **Closed Unidentified items:** follow the existing standard. Unidentified items are Open or Resolved, keep their U-reference for ever, are never deleted, and can be reopened (Core already has `ReopenUnidentifiedRequest`, writing a "Resolved to Open" history row). Close with reason is a resolution; the Closed filter lists resolved items indefinitely; Reopen is offered on a closed item.
5. **Operations audience:** still deferred.

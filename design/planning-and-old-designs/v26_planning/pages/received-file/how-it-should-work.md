# Received file — how it should work

Decided with the operator on 13 September 2026.

## D1. The receipt stays; the operator page goes

- The intake receipt remains as a record: hash, retained original, source, time, processing outcome, attempts. It is what makes intake idempotent, retryable and auditable (FRD-02). Nothing about the record changes.
- `/Intake/Details` is removed as an operator page. No User or Engineer surface shows a receipt, an "origin receipt", a decision code or an allocation attempt.
- The receipt's history is visible in one place only: **Administration › Logs › Intake log** (Administrators), specified in [`../action-logs/how-it-should-work.md`](../action-logs/how-it-should-work.md).

## D2. Every action on the old page moves to where a person already is

| Old action on the received-file page | Goes to | Note |
| --- | --- | --- |
| Block with reason | Unidentified › Close with reason | Blocked stops being a concept for operators; a refused item is a closed Unidentified item with its reason. See [`../unidentified/how-it-should-work.md`](../unidentified/how-it-should-work.md). |
| Record corrected draft | Create case, seeded from the item | already there; the copy on the receipt page is dropped. See [`../create-case/how-it-should-work.md`](../create-case/how-it-should-work.md). |
| Create a case from this item | Create case | as now |
| Link / Unlink selected Case | Unidentified › Link to Case; Case › Files › Add evidence | the receipt is never the thing being linked, the message or file is |
| Registration reading results, Dismiss suggestion, Register images, Open the Triage | Unidentified record and the image record | see [`../unidentified/how-it-should-work.md`](../unidentified/how-it-should-work.md) |
| Retry allocation, Re-evaluate with current policy, Retry OCR | Administration › Logs › Intake log, on the row | technical, Administrator-only |
| Inspection-address confirmation, instruction details, suggested fields, decision evidence | Create case (seeded) shows the suggested values; the evidence is in the Intake log | no operator needs the evidence panel to do their job |
| Scanned PDF pages, assets | the file viewer, opened from the message or the Case | the original, not the receipt |
| "View received item" links on Triage, Unidentified and image records | "Open message" or "Open file": the Inbox message, or the original in the viewer | never the receipt |

## D3. Outcomes a person can act on show where the material is

- **Could not be read** (Unsupported, OCR required that failed, Technical failure on a file): shown on the Inbox message's attachment and on the upload confirmation as "Could not be read", with the reason in operator words. The person re-sends or re-uploads; nothing else to do.
- **Processing failed** (system): Operations, per the Work Centre's D1. Not a person's problem until Operations says so.
- **Needs a person**: Unidentified, as now.

## D4. The Work Centre's Blocked metric goes

With Blocked gone as an operator concept the metric is removed. Refused material is a closed Unidentified item and is counted nowhere; unreadable material shows on its message; failed processing shows on Operations. The Work Centre decision that kept the metric unchanged (P7, not adopted) is superseded; see [`../work-centre/how-it-should-work.md`](../work-centre/how-it-should-work.md).

## Open

- Whether Unidentified › Close with reason needs a reason vocabulary (Duplicate, Not for us, Spam, Refused) or stays free text.
- Whether Engineers may open the Intake log read-only. Default: Administrators only.

# Open decisions

This register contains unresolved product or technical choices only. Accepted
technical choices belong in [ADRs](adr/README.md), behavior in [FRDs](frd/README.md),
and product scope in the [PRD](prd/pegasus-product.md). The current operator task
and its linked PR/CI records own delivery work.

Add a question here only when it is genuinely unresolved, with its owner,
affected contract and the decision needed; remove it once its outcome has a
durable owner.

## Tractable PDF fields

Owner: [FRD-02](frd/frd-02-intake-and-source-identity.md#ways-intake-starts)
(`EXT-17`). Tractable emails a PDF after guided capture. Decision needed:
which fields Pegasus reads from that PDF, and whether its images are taken
from the PDF or from a separate attachment.

## Outlook category write

Owner: [FRD-08](frd/frd-08-email-mailbox-and-background-processing.md#classification-destination-and-folder-catalogue)
(`MAIL-13`). FRD-08 describes an Administrator allowlist of Outlook
categories that staff may apply. The code applies no Outlook category today;
its only Outlook write is the confirmed folder move. Decision needed: build
the category write, or remove the allowlist from FRD-08.

## Capability wording with no owning requirement

The 18 September 2026 capability review found these clauses in the old
capability wording and in no FRD, PRD or ADR. The
[capabilities](capabilities.md) labels no longer carry them. Decision needed
for each: write it into the named owner, or drop it.

| ID | Clause with no owner | Likely owner |
| --- | --- | --- |
| MAIL-03 | One shared classification policy across all supported mailboxes, stated as a rule | FRD-08 |
| MAIL-10 | Unlink and relink of a message from a Case in the Inbox | FRD-20 |
| MAIL-17 | Standing-note preferences; report delivery through the Provider API; management-event recording | FRD-27, FRD-21 |
| MAIL-21 | An acceptance cohort for the classification rules | FRD-08 |
| INT-13 | JPEG and PNG named as the image-led formats | FRD-19 |
| INT-20 | Validation, missing-value and contradiction display on the draft | FRD-23 |
| TRI-01 | A distinct Inbox label for Triage | FRD-20 |
| CASE-01 | The end-to-end journey for every active Case type, stated as one rule | FRD-13 |
| CASE-11 | Typed claim, accident, contact and inspection data named in the Case identity FRD | FRD-01, FRD-23 |
| CASE-17 | Overdue display beside Due by | FRD-13, FRD-15 |
| CASE-31 | Addendum, query document, invoice input and statistics as consumers of the one accepted record | FRD-11 |
| UI-04 | Definitions of Sent to Engineer and Reports sent, and a week window | FRD-15 |
| UI-13 | A contrast requirement | FRD-12 |
| UI-15 | Inspection, vehicle, media, salvage, text and administration as workbench parts | FRD-16 |
| DOC-07 | A document export action | FRD-05 |
| EXT-02 | The supplied, external and estimated mileage classification named in the MOT section | FRD-06, FRD-23 |
| EXT-11 | Engineer cost and payment inputs, accounting status, staff-role-neutral visibility | FRD-11 |
| EXT-13 | Independently licensed valuation-source adapters | FRD-24 |
| MCP-05 | Automation Actor actions for the classified-email workspace | FRD-10 |
| API-04 | Credential reset and resume | FRD-09 |
| AI-07 | `AI Assessor` as a selectable Engineer option that owns no button, queue, model or transport | FRD-27, FRD-13 |
| AI-08 | Microsoft Foundry as the candidate; house style and letterhead; named Engineer approval before sending | FRD-27 |
| ENG-02 | Narratives derived from Engineer-owned figures without retyping | FRD-24 |
| OPS-08 | An alert for matching failures | ADR-0002 |
| OPS-13 | Policy and quota checks before deployment | ADR-0007 |

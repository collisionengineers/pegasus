# Research — TICK-010: MAIL-22 settled taxonomy

*The research. Not the files document — this is what you **learned**, not what you will **touch**.*

## Question

Is the operator-confirmed Received/Sent taxonomy, mirrored Reply rule, `Other` name/reason contract, and category/destination separation already encoded in Core and persistence, and what (if anything) this ticket still owes without expanding into the 0.3.0 mailbox workspace?

## Findings

- MAIL-22 is a QDOS-alpha allocation. Capabilities.md: "This row owns allocation only; the linked requirements clause owns behavior and routes to accepted provenance." The clause is FRD-08 § Settled mailbox taxonomy and correction. Operator confirmation is in `docs/operator-notes.md#confirmed-mailbox-categorisation` (provenance only; not a second policy owner).
- "User-confirmed" in the capability title means Alex confirmed the taxonomy tables, not that staff confirm every message in the Inbox. Per-message classification/correction/folder-move from opened message detail is the 0.3.0 workspace (UI-10; `Message.cshtml.cs` remarks say those actions have not landed).
- Core already encodes the settled tables in `MailTaxonomy` / `MailCategory` (`src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs`): eight Received families with confirmed subtypes, four Sent families, `Other(name, reasoning)` requiring both fields, `IsReplyContext` on the underlying family (Reply is never a recorded type), and no queue/folder/destination properties. `MailTaxonomyTests` locks the tables, rejects unconfirmed subtypes, and asserts category/result types carry no destination fields.
- Persistence already stores `Direction`, `Family`, `Subtype`, `IsReplyContext`, `OtherName`, `OtherReasoning` (`IntakeMailClassificationDecisionEntity`, mapped in `EfIntakeReceiptStore`). Integration tests cover classified+reply and ambiguous candidates (`MailboxIntakeIntegrationTests`). There is **no** persist/reload test for `MailCategory.Other` or any `MailCategory.Sent(...)` value.
- The QDOS policy (`qdos_mail_classification` v3) never produces `Other` and never classifies Sent families. That is MAIL-21 / boundaries.md (automated application beyond the delivered QDOS tells is deferred), not a MAIL-22 gap.
- Category → operational queue is MAIL-02 / MAIL-23 / UI-14. Category → designated Outlook folder is MAIL-05–07. Authorised correction/reversal history is MAIL-04. UI-14 already notes "the classification decision is already persisted (MAIL-21/22), so the remaining work is surfacing it."
- [[TICK-009]] owns the versioned policy, predicate evidence, ambiguity outcome, and acceptance cohort. MAIL-22 must not change QDOS predicates or invent precedence.

## Implications

- Do not build a confirmation UI, folder move, or correction command. Those are later tickets and would overlap UI-10 / MAIL-04 / MAIL-05.
- The remaining MAIL-22 slice is to prove the taxonomy contract that staff and later tickets will write: persist and reload `Other` (both directions) and a Sent family (with and without reply context) through `IIntakeReceiptStore`, so the MAIL-22 types are not unit-only.
- Keep category/destination separation: new tests must not add queue or folder fields.

## Open questions

See `open-questions`. Default taken: no staff UI in this ticket.

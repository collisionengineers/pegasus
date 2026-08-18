# Research — MAIL-04

## Question

What existing owner, caller, persistence boundary, and acceptance surface should carry explainable classification evidence, policy version, and correction history without duplicating business policy?

## Verified findings

- `docs/frd/frd-08-email-mailbox-and-background-processing.md` is the canonical behavioural owner. It requires source identity, policy key/version, outcome, material evidence, ambiguity/confidence facts, actor and time; corrections append structured before/after history and never erase the original decision.
- `Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` is the single Core taxonomy/result owner. `QdosMailClassificationPolicy` supplies policy identity and predicate evidence, and `ProcessIntake` is the real classification caller.
- `EfIntakeReceiptStore` persists the current classification decision and predicate evidence; `EfRetainedMailboxMessageStore` projects retained messages for the Mail UI. The current decision record is replaced during re-evaluation, so append-only correction history is the material missing behaviour.
- `Pages/Mail/Message.cshtml(.cs)` already displays the current retained-message classification. The workspace should extend this caller rather than create a parallel classification implementation.
- Existing classification tests cover policy decisions and persistence, but not an authorised correction/reversal history and deterministic downstream recomputation.
- The operator's 2026-08-18 instruction to drive the email-workspace epic through functional completion designates this post-alpha capability for implementation. It does not approve any real Outlook or cloud mutation; tests must use local/in-memory/SQL fixtures and mailbox adapters/fakes.

## Implications

Reuse the Core taxonomy and existing receipt/retained-mail stores. Add one Core-owned correction command/port and append-only persistence/audit shape, then expose it only from exact-message detail with reason and explicit confirmation. Preserve the prior decision and fail closed on stale identity/version, invalid category, absent actor/reason, or unsupported mailbox state. No production mailbox write is needed for this ticket.

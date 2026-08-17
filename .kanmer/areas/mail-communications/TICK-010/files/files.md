# Files — TICK-010

*The files document. Not the research — this is the **surface area** of the change, not the findings behind it.*

Surveyed BEFORE planning. Two tables, and the second is the one that earns its keep.

## Where the change lands

What this ticket will modify, and why each file is in scope.

| Path | Why |
| --- | --- |
| `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs` | Existing persist/reload coverage is classified-Received+reply and Ambiguous only. Add Other (Received and Sent) and Sent-family (with and without reply context) round-trips through `IIntakeReceiptStore`. |

## Context files

What an implementer must **read** to avoid a trap — files they will not necessarily edit.

| Path | What it tells the implementer |
| --- | --- |
| `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` | Factories and validation: `Other` requires name+reason; Sent has no subtypes; Reply is a flag. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` (`MapMailClassificationDecision`) | Incomplete Other (name without reasoning or direction) must remain `InvalidDataException`. |
| `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs` | Column set already includes Other and reply; no migration. |
| `tests/Pegasus.Core.Tests/Intake/Classification/MailTaxonomyTests.cs` | Exact settled tables. Do not add unconfirmed subtypes. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` § Settled mailbox taxonomy | Behaviour owner. Category ≠ destination. |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | Staff confirmation UI is allocated 0.3.0 work; do not add a handler. |

## Ripple effects

- No schema change. Existing mappings already store Other/Sent.
- Core taxonomy tests stay the lock on family names.
- Do not change `QdosMailClassificationPolicy` (MAIL-21 / [[TICK-009]]).

## Out of scope

- Inbox confirmation, correction, unlink/relink, folder recommendation.
- Automated rules for billing, in-progress, post-report, Sent, or remaining subtypes.
- Queue/folder mapping (MAIL-02/23).
- MAIL-21 cohort harness ([[TICK-009]]).

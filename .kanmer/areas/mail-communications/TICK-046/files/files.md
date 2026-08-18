# Files — MAIL-04

## Change surface

| File/module | Expected change | Risk |
| --- | --- | --- |
| `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` | Reuse taxonomy/result; add only the minimal correction request/history contract if no existing port fits | High: one-list and Core-owner invariants |
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Carry current decision/history projection needed by the exact-message caller | Medium: shared workspace contract |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` and classification entities/configuration | Append correction history transactionally without deleting the original decision | High: durable evidence and concurrency |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Project corrected current value plus history/evidence | Medium |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml(.cs)` | Show evidence/version/history and confirmed correction action | Medium: authorization, validation, accessibility |
| focused Core, Web, and integration tests | Prove append-only history, fail-closed validation, stale-write handling, display and deterministic projection | High |

## Ripple effects

A schema change may require one migration and model-snapshot update. Queue/folder mapping tickets consume the corrected classification and must read the canonical current projection; they must not copy the taxonomy or precedence. Automation Actor support is owned by TICK-062 and should call the same Core command later.

## Context files

| File | Why read it |
| --- | --- |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Binding end-state behaviour and workspace boundaries |
| `docs/design/README.md` | UI interaction/accessibility rules |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs` | Existing policy identity and evidence production |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | Real caller and validation conventions |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Existing persistence fixture/convention |

## Out of scope

No new taxonomy, rule precedence, arbitrary Outlook destination, bulk correction, historical cohort replay, real mailbox mutation, or separate Automation Actor implementation.

# Post-implementation report — TICK-046

## Summary

Implemented the exact-message MAIL-04 classification dossier and correction workflow. Authorised staff can see the current category/outcome, policy key/version, predicate evidence, actor/time and permanent correction history, then correct one retained message with a required reason. Core fails closed on invalid/stale input; Infrastructure atomically preserves structured before/after evidence and prevents later automated replay from overwriting an accepted correction.

## Changes

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Core/Intake/RetainedMail.cs` | Added dossier/history contracts, persistence port, concurrency error and one authorised correction use case | Keep correction validation and semantics in the single Core owner |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Implemented exact-message dossier reads and transactional append-only correction | Reuse the retained-message/receipt join and enforce mailbox-scoped optimistic writes |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` | Reused versioned-envelope serialization, captured decision attribution and protected staff-corrected decisions from replay overwrite | Preserve one persistence format and prevent silent reinterpretation |
| `src/Pegasus.Infrastructure/Persistence/MailboxEntities.cs`, `MailboxModelConfiguration.cs`, `PegasusDbContext.cs` | Added decision version/actor/time/concurrency and correction-history entity/configuration | Store durable attributable history with a unique per-decision version |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260819104953_MailClassificationCorrectionHistory.*`, model snapshot | Added schema, legacy attribution backfill, unique history index and least-privilege Web grants | Make the contract deployable against existing data and runtime roles |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Registered the Core port and use case against the existing retained-mail store | Wire the real Web caller without a parallel implementation |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml(.cs)` | Rendered evidence/policy/attribution/history and added a reasoned exact-message correction form | Deliver the accessible operator surface on the existing message detail page |
| `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` | Added authorization, evidence preservation, reason and stale-version tests | Prove Core fail-closed policy independently |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Added real-migration correction, history, stale write and replay-protection coverage | Prove transactional durable behaviour |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Added real page-pipeline evidence/correction rendering coverage | Prove the composed caller exposes the dossier |

## Governing docs

- `docs/frd/frd-08-email-mailbox-and-background-processing.md`: the implementation preserves source-linked material evidence, policy identity/version, actor/time, structured before/after history and fail-closed correction semantics. It does not perform an Outlook mutation or reinterpret historical decisions.
- `docs/design/README.md`: the form uses the existing Razor validation, labelled controls, status notice, explicit per-message action and normal focus/navigation conventions.
- No governing document was changed: the FRD already owns the exact delivered behaviour. No ADR was required because the existing Core/Infrastructure/Web boundary carries it.

## Risks / follow-ups

- This is locally built and SQL/fake-backed, not deployed or verified against a live mailbox. No live Outlook/cloud write was attempted or required.
- Policy cohort re-evaluation remains a separate explicit operation; ordinary intake replay deliberately leaves a staff correction intact.
- Downstream queue/folder recomputation consumes the corrected current classification in the later EPIC-006 tickets; this ticket does not implement those independent actions.

## Verification hand-off

On merged `main`:

1. Run `dotnet restore`.
2. Run `dotnet build Pegasus.slnx --configuration Release --no-restore`; expect 0 warnings/errors.
3. Run `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build`; author result: 634/634.
4. Run `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~RetainedMailPersistenceTests|FullyQualifiedName~MailWorkspaceWebTests"`; expect the retained-mail and page-pipeline suites green. Author ran the combined suite at 27/28 with the only failure an over-specific HTML attribute-order assertion; after correcting that assertion, the exact SQL/Web acceptance rerun passed 2/2.
5. In a local DevelopmentOffline browser fixture containing a classified retained message, capture the message detail showing policy/evidence/current attribution, submit a correction, then capture the success notice and immutable before/after history. Do not use a real Outlook mailbox.

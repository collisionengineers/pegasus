# Post-implementation report — TICK-043

## Summary

MAIL-01 now gives retained inbound mail one explicit, mailbox-scoped identity contract. The existing Graph/poll caller keeps immutable provider-item identity separate from RFC message and conversation identity; retained mail requires RFC Message-ID, uses mailbox + RFC identity for intake and read-model idempotency, fails closed on contradictory identity/content, and never builds a thread across mailbox or folder scope. This is local implementation and test evidence only: no Outlook, Graph, Azure, deployment, or external write was performed.

## Changes

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Core/Intake/MailboxIntake.cs` | Requires bounded RFC identity for retained messages and derives the bounded intake receipt token from its SHA-256 while retaining the provider immutable ID separately | Makes duplicate business intake follow the durable mailbox + RFC boundary and rejects missing identity before writes |
| `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` | Finds duplicates by mailbox plus RFC or immutable provider identity, verifies RFC/content consistency, and scopes thread reads to mailbox + folder | Makes redelivery idempotent, contradictions fail closed, and prevents cross-mailbox/folder thread disclosure |
| `src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs` | Adds the unique mailbox + RFC identity index | Places the race-safe duplicate boundary in the database |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260819090641_RetainedMailboxInternetMessageIdentity.cs` and designer/snapshot | Adds the filtered unique index to the existing migration stream | Delivers the persistence constraint without a new store or deployment unit |
| `tests/Pegasus.Core.Tests/Intake/PollApprovedInboxTests.cs` | Covers missing RFC identity and the RFC-derived receipt token | Proves the Core fail-closed contract and real poll caller |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Covers provider-ID change, contradictory identities, database idempotency, and cross-mailbox thread isolation | Proves the real EF boundary and migration |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Adds the canonical inbound mailbox identity behaviour | Keeps product behaviour in the governing FRD |
| `docs/capabilities.md` | Records the locally implemented MAIL-01 evidence and its deployment qualification | Keeps the capability registry accurate without claiming deployment |

## Governing docs

The linked `docs/frd/frd-08-email-mailbox-and-background-processing.md` now states the exact identity dimensions, mailbox + RFC duplicate boundary, contradiction behaviour, and mailbox/folder-scoped thread rule implemented by this change. The existing Core/Infrastructure/Web architecture carries the work, so no ADR was needed. The change preserves the epic constraint that Graph reads feed one Core-owned contract and performs no mailbox mutation.

## Risks / follow-ups

- Existing retained rows with a null RFC identity remain readable; the filtered migration does not fabricate identity for historical rows. New retained messages without RFC identity fail closed.
- This ticket does not implement mailbox search, Case association, classification correction, folder moves, or other Outlook mutations; those remain with their EPIC-006 capabilities.
- Deployment and fresh live-mailbox verification are not claimed. Any future external write still needs exact approval under the repository live-operation rules.

## Verification hand-off

On the merged release candidate, run:

- `dotnet restore --locked-mode`
- `dotnet build --configuration Release --no-restore` — expect 0 warnings and 0 errors
- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` — implementation result: 617/617 passed
- `dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` — implementation result: 96/96 passed
- `dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~RetainedMailPersistenceTests|FullyQualifiedName~ProductionGraphSourceTests"` — implementation result: 27/27 passed
- `git diff --check` — implementation result: passed

Verify the migration applies through the normal integration database fixture and that the focused tests demonstrate same-RFC redelivery, contradiction refusal, missing-RFC refusal, and cross-mailbox thread isolation. No screenshot is required because this ticket changes no UI.

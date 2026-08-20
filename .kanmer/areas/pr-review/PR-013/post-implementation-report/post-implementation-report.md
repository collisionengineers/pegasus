# Post-implementation report — PR-013

## Summary

Fixed approved-mailbox folder refreshes by diffing the loaded EF navigation: retained keys update in place, removed keys are deleted, and only new keys create entities. This prevents duplicate tracked composite keys while preserving the existing transaction, version, replay, audit, and Web-only permission behavior.

## Changes

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxStore.cs` | modified | Diff tracked bindings instead of clear/recreate. |
| `tests/Pegasus.IntegrationTests/AdministrationPolicyPersistenceTests.cs` | modified | Relationally proves unchanged, changed, removed, and added types save atomically. |

## Governing docs

No governing-doc change; this corrects implementation of FRD-08's existing replace behavior.

## Risks / follow-ups

PR-014 is intentionally untouched and still blocks TICK-064.

## Verification hand-off

Release build of Integration project: pass, 0 warnings/errors. Rebuilt focused relational test: 1/1 pass. `git diff --check`: pass. Run the same test after merge; no external writes are required.

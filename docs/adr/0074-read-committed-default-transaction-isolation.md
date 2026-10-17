---
id: ADR-0074
status: accepted
date: 2026-10-15
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: []
tags: [persistence, transactions, concurrency]
---

# ADR-0074: Read committed is the default transaction isolation

## Status

Accepted on 15 October 2026 by operator decision: "Full isolation change:
default transactions to `ReadCommitted`, rely on the concurrency tokens, and
acquire `UPDLOCK, HOLDLOCK` as the first statement where pessimistic locking
is still needed."

On 16 October 2026 the operator ruled where read-committed snapshot is set:
test databases only; production's value is read back read-only; the bootstrap
script is not edited.

## Context

When this record was filed:

- Infrastructure stores opened 133 transactions at `Serializable`, three at
  `ReadCommitted` and eleven with no level named.
- A serializable read of an operation key that is not there range-locks the
  gap, and the insert behind it deadlocked two acts on one another
  (issue 835).
- Azure SQL Database has `READ_COMMITTED_SNAPSHOT` on by default. A LocalDB
  database has it off, and nothing in the repository set it.

## Decision

1. A store transaction is `ReadCommitted` by default.
2. Stores rely on the entities' concurrency tokens.
3. Where pessimistic locking is still needed, `UPDLOCK, HOLDLOCK` is the
   transaction's first statement.
4. `Serializable` is a listed override.
5. Test databases set `READ_COMMITTED_SNAPSHOT ON`. Production's setting is
   read back, not written.

## Consequences

- `PegasusTransactions` in `src/Pegasus.Infrastructure/Persistence/` is the
  one owner of opening a store transaction. `BeginReadCommittedAsync` opens
  the default; the callers of `BeginSerializableAsync` are the list of
  overrides.
- `LocalDbTestDatabase` sets read-committed snapshot on each test database it
  creates, and every database restored from the template carries it.
- The release that ships this records production's
  `is_read_committed_snapshot_on` in [operations](../operations.md).

## Links

- [Runbook — Runtime diagnostics](../runbook.md#runtime-diagnostics)
- [Current architecture](../current-architecture.md)

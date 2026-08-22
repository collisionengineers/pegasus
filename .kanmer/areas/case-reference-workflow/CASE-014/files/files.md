# Files

Committed in `79bf3f86`.

| File | Change | Reuses |
| --- | --- | --- |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs` | The allocated reference gets the `a.`/`ap.` prefix for an audit; no separate `AuditReference` is allocated; no audit folder-creation token is issued | `AuditIdentity.Create`, unchanged |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs` | The Box root is named by the case reference for every case; an audit no longer creates a second folder | — |

## Deliberately untouched

`AuditIdentity.Create` already produced exactly the right string — `a.` for Repairable,
`ap.` for Total Loss. It was being applied to the wrong thing, not computing the wrong
answer.

`StandaloneAuditEvidence.Assessment` already stores the outcome, so nothing new was needed
to know which prefix applies.

A **later** Audit reference on a non-audit case keeps its own folder and its own
`CreateAuditReferenceCustody` work kind. That is a different concept and is out of scope.

The `AuditReference` column stays on the table, now unset for audits. Removing it is a
migration with no behavioural gain and would erase the history of cases already allocated
under the old rule.

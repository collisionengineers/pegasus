# Proof

**Shipped:** PR #486, merge `708706b8` · **Deployed:** ancestor of `4111ad29` → true (Release 16, active revision `…--4111ad291779`).

## The finding

TICK-051's post-implementation report summarised implementation *themes* and did not
inventory every changed file with its rationale, so the DI registration, the retained
projection changes, each governing-document edit and both test files could not be
reconciled file-for-file against PR #486.

## Verified by reconciliation, not by assertion

The two lists were extracted independently and compared.

`git diff --name-only 708706b8^1 708706b8` — the PR's actual final diff:

```
docs/capabilities.md
docs/current-architecture.md
docs/frd/frd-08-email-mailbox-and-background-processing.md
src/Pegasus.Core/Cases/CaseQueries.cs
src/Pegasus.Core/Intake/CaseMatching/AutomaticMailCaseAssociation.cs
src/Pegasus.Core/Intake/CaseMatching/CaseMatchContracts.cs
src/Pegasus.Core/Intake/DurableIntake.cs
src/Pegasus.Infrastructure/DependencyInjection.cs
src/Pegasus.Infrastructure/Persistence/CurrentIntakeAssociations.cs
src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs
src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs
tests/Pegasus.Core.Tests/Intake/CaseMatching/AutomaticMailCaseAssociationTests.cs
tests/Pegasus.IntegrationTests/CaseMatchIntegrationTests.cs
tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs
```

Paths named in TICK-051's `post-implementation-report`: **the same fourteen, exactly** —
no path in the diff missing from the report, and no path in the report absent from the
diff.

The three specifics the finding called out are all present and individually reconcilable:
`src/Pegasus.Infrastructure/DependencyInjection.cs` (the DI registration),
`EfRetainedMailboxMessageStore.cs` / `CurrentIntakeAssociations.cs` (the retained
projection), and all three governing documents.

## Second half of the finding

*"Keep verification counts and local-only/no-external-write claims accurate after the
blocker fixes."* The report distinguishes runs at the initial implementation head from
runs at the replacement blocker head rather than presenting one figure for both, and the
local-only boundary is stated explicitly.

## Not claimed

This is a documentation-accuracy ticket; its proof is the reconciliation above. No runtime
behaviour is claimed by it, and none was changed by it.

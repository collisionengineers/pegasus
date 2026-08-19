# Post-implementation report — TICK-045

*The report. Not the proof — this is the author's **claim**, written before merge; proof is **evidence**, gathered after.*

## Summary

MAIL-03 is functionally carried by the existing MAIL-04 exact-message correction path rather than a second mailbox-specific policy. This branch adds the missing SQL integration evidence that the same Core validation and append-only persistence behave identically and independently for two distinct mailbox identities, including fail-closed stale and unknown message handling. It updates the capability registry to the local-evidence tier without claiming deployment or live mailbox verification.

## Changes

| File | Change | Why |
|---|---|---|
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Added a two-mailbox classification-correction acceptance scenario using the real Core command and EF/SQL transaction. | Proves the shared policy contract, policy/evidence retention, isolated per-message histories, and stale/unknown fail-closed behavior without adding another policy owner. |
| `docs/capabilities.md` | Replaced MAIL-03's allocation-only note with the precise local implementation evidence and explicit deployment qualification. | Keeps the capability schedule accurate without overstating live or deployed behavior. |

No production source, schema, migration, FRD, ADR, current-architecture, or operations file changed: prerequisite MAIL-04 already supplied the required Core owner, transaction, and Web caller.

## Governing docs

The linked `docs/frd/frd-08-email-mailbox-and-background-processing.md` requires one taxonomy/decision owner across the approved mailbox estate, exact-message actions, permanent policy/evidence history, and fail-closed ambiguity. The test drives two distinct mailbox identities through the same registered `CorrectRetainedMailClassification` command and `IRetainedMailClassificationStore` transaction, retains the original policy key/version/predicates, and proves each message receives only its own history. It does not universalise the route-owned automatic predicates protected by ADR-0008.

## Risks / follow-ups

- This is local SQL integration evidence, not deployment or a live Outlook/mailbox check.
- Production currently has one linked mailbox. No live check was performed or claimed in this task. First real second-mailbox evidence remains with [[TICK-036]], [[TICK-037]], or [[TICK-038]] when another mailbox is connected.
- Exact provider/intermediary predicate activation remains [[TICK-035]] scope.
- No Graph, Outlook, Azure, or external system was mutated.

## Simplification pass

Reuse, simplification, efficiency, and altitude were assessed on the complete branch diff and recorded in the ticket plan. The result adds no production abstraction or duplicated taxonomy: one existing Core caller, one existing persistence transaction, one two-mailbox database scenario, and one evidence-status edit. No further behavior-preserving change was identified.

## Verification hand-off

Run on the merged target:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~RetainedMailPersistenceTests"
```

Implementation results on this branch:

- Locked restore and Release build: passed, 0 warnings, 0 errors.
- Core: 635/635 passed.
- Architecture: 97/97 passed.
- Retained-mail persistence: 16/16 passed.
- New focused two-mailbox scenario: 1/1 passed independently.
- `git diff --check`: passed.

Expected acceptance: the two retained messages from distinct mailbox identities are corrected through the same Core command; both retain policy key `shared-mail-policy`, version `9`, and their predicates; each appends exactly one history record; a stale write throws and an unknown exact message returns no decision; total history stays two.

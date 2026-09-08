# Files — INTK-064

## Production changes

- src/Pegasus.Core/Triage/TriageContracts.cs — existing ITriageStore/query
  boundary adds bounded automatic association/recovery contract, separate from
  manual lease-bearing TriageCaseLinkRequest.
- src/Pegasus.Core/Triage/TriageLifecycle.cs — existing Triage lifecycle owner
  orchestrates automatic pairing using current EvaluateIntakeCaseMatch policy;
  preserve manual validation unchanged.
- src/Pegasus.Infrastructure/Persistence/EfTriageStore.cs — existing serializable
  association/history transaction gets SystemWorker-only entry, current candidate
  recheck, manual-override/current-state/lease guards and bounded eligible reads.
- src/Pegasus.Infrastructure/Persistence/CaseMatchEntities.cs — context-bound
  reuse of EfCaseMatchIndex queries for transactional recheck, not a new matcher.
- src/Pegasus.Core/Intake/CaseMatching/EvaluateIntakeCaseMatch.cs — at most name/
  contract clarification for accepted typed source use; no eliminator changes.
- src/Pegasus.Core/Intake/DurableIntake.cs — actual Triage create/replay caller.
- src/Pegasus.Core/Intake/AcceptIntake.cs — formal acceptance/replay pairing.
- src/Pegasus.Worker/IntakeFunctions.cs — existing reconciliation timer caller.
- src/Pegasus.Infrastructure/DependencyInjection.cs — existing owner composition.
- docs/frd/frd-03-triage.md, docs/frd/frd-02-intake-and-source-identity.md and
  docs/current-architecture.md — canonical automatic/manual association behavior.

## Tests and excluded surfaces

Extend existing tests:
- tests/Pegasus.Core.Tests/Triage/TriageReplayTests.cs
- tests/Pegasus.IntegrationTests/QdosTriageCaseAssociationIntegrationTests.cs
- tests/Pegasus.IntegrationTests/TriageFromIntakeIntegrationTests.cs
- tests/Pegasus.IntegrationTests/QdosTriageReplayIntegrationTests.cs
- Existing AzureSqlRuntimeRoleMigrationTests for actual restricted Worker path.

No Web/MCP manual authorization change, domain parser, additional store/schema,
new queue/runtime, live provider/mail, corpus mutation or broad grant. Any
necessary permission migration requires root review and matching bootstrap
census in the same diff. TICK-035 and INTK-063 overlap matcher/acceptance/
composition: implementation must be serialized on their merged/released base.

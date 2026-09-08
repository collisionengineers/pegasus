# Files — INTK-063

## Changed production paths

- src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs — existing store/query
  contracts carry current candidate principal and bounded pending eligibility.
- src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs — one pairing/recovery
  owner and meaningful per-item outcomes; deterministic existing merge replay.
- src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs — registered replay and
  first-arrival pairing use that same owner and known-principal guard.
- src/Pegasus.Core/Intake/AcceptIntake.cs — duplicate acceptance also wakes the
  existing idempotent pairing path; failure remains observable/recoverable.
- src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs — its embedded
  EfImageIntakeCaseCandidates uses current CaseMatchIndex, not original draft;
  bounded eligible recovery query and principal evidence.
- src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs — current
  identity/unique candidate recheck in existing automatic-write transaction;
  preserve manual-history, version and active lease guards.
- src/Pegasus.Infrastructure/Persistence/CaseMatchEntities.cs — only if needed
  for context-bound reuse of the existing candidate query under that transaction.
- src/Pegasus.Worker/IntakeFunctions.cs — existing staged reconciliation timer
  invokes the registered-image recovery and logs its bounded result.
- docs/frd/frd-02-intake-and-source-identity.md and docs/current-architecture.md —
  current identity, deliberate reversal and actual scheduled recovery behavior.

## Tests and protected ownership

- tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeCasePairingTests.cs
- tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs
- tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeLifecycleTests.cs — only
  existing store interface consumer adaptation and preserved lifecycle guards.
- tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs
- tests/Pegasus.IntegrationTests/GroupedImageIntakeConcurrencyTests.cs
- tests/Pegasus.IntegrationTests/StagedArtifactReconciliationFunctionIntegrationTests.cs —
  existing explicit function constructor, actual recovery invocation and all
  existing/new result-field assertions.
- Existing AzureSqlRuntimeRoleMigrationTests for real restricted Worker caller.

Read-only composition context: src/Pegasus.Infrastructure/DependencyInjection.cs
already registers IImageIntakeCasePairing and its existing dependencies. No
change there is required or authorized. Extend the existing owner/interface,
not a separate recovery service; TICK-085 retains its independent parser
registration scope.

No Web route/UI, corpus, cloud, schema or grant change is planned. If a concrete
permission or schema gap appears, report it to root before adding migration and
its paired bootstrap census. TICK-035 is Done/closed with ownership released. INTK-064 still overlaps
acceptance/timer/matcher paths and must follow this ticket's merge/release.
After the composition correction above, TICK-085's write map has no overlap.
Never edit a foreign claimed workspace.

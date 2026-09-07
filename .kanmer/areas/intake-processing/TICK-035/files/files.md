# Files — TICK-035

## Production change map

| Existing path | Bounded change |
| --- | --- |
| src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailRoutePolicy.cs | Generalize/rename this route owner; exact evidenced identities and typed PCH intermediaries; preserve sender/forward consistency and provisional sender helper. No parallel QDOS route. |
| src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs | Generalize/rename this classification owner; keep QDOS predicates and use evidenced selected-profile current request types for additional principals. Reuse standalone Audit evaluation with actual instruction identity. |
| src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs | Pass established principal/profile context to the existing classifier contract. |
| src/Pegasus.Core/Intake/InstructionExtractionPolicySelector.cs | Reuse the fifteen profiles; scope real instruction fragments without combining separate document identities into a fabricated profile. |
| src/Pegasus.Core/Intake/ProcessIntake.cs | Replace fixed extraction binding; require route/profile agreement; use selected extraction and classification in the existing assessment/allocation/matching pipeline. |
| src/Pegasus.Core/Intake/IntakeContracts.cs | Only context/port signature changes required for the same existing pipeline. |
| src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosCaseMatchPolicy.cs | Generalize/rename one match-key owner; retain QDOS-specific grammar, preserve full other-principal references from their typed fields. |
| src/Pegasus.Core/Intake/CaseMatching/CaseMatchContracts.cs | Carry provider identity into shared read/write key normalization. |
| src/Pegasus.Core/Intake/CaseMatching/EvaluateIntakeCaseMatch.cs | Adapt calls to generalized key owner; do not redesign the eliminator. |
| src/Pegasus.Infrastructure/Persistence/CaseMatchEntities.cs | Same match-key normalization in current index projector/query owner. |
| src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs | Required match-owner caller adaptation only. |
| src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs | Required match-owner caller adaptation only. |
| src/Pegasus.Infrastructure/Persistence/EfCaseWorkspaceStore.cs | Required match-owner caller adaptation only. |
| src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs | Required match-owner caller adaptation only. |
| src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs | Required match-owner caller adaptation only. |
| src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs | Rename reference to the same provisional effective-sender owner. |
| src/Pegasus.Infrastructure/DependencyInjection.cs | Replace fixed QDOS compositions with the generalized existing owners; retain all fifteen extraction registrations. Wait for PLAT-028 merge. |
| src/Pegasus.Web/Pages/Administration/Principals/Settings.cshtml.cs | Read activated identity metadata from the one Core owner for the selected customer. Wait for PLAT-028 merge. |
| src/Pegasus.Web/Pages/Administration/Principals/Settings.cshtml | Only metadata label if necessary to correctly show YML's exact address, not a Gmail domain; root owns capture. |
| docs/frd/frd-02-intake-and-source-identity.md | Current route/profile/classification/match behavior and fail-closed boundaries. |
| docs/frd/frd-09-provider-and-intermediary-routes.md | Current evidenced email route activation versus separately credentialed Provider API. |
| docs/current-architecture.md | As-built pipeline composition once wired; no premature live-deployment claim. |

Renamed Core owners stay within existing Intake subdirectories. No new
top-level directory, project, policy framework, package, migration, store,
worker or generic rule editor. Route evidence stays in research/reference;
the active runtime identity list exists exactly once.

## Existing test callers and focused proof

Core: ProcessIntakeTests, DefinitiveIntakeCaseTypeTests,
Intake/Qdos/{QdosMailRoutePolicyTests,QdosMailClassificationPolicyTests,
QdosCaseMatchPolicyTests,QdosInstructionExtractionPolicyTests},
CaseMatching/EvaluateIntakeCaseMatchTests, ReconcileUnidentifiedDestinationsTests,
and InstructionExtractionPolicySelector tests.

Integration: Top15InstructionCorpusTests (reuse hash-bound originals and
expected fields); CaseMatchIntegrationTests; InlineForwardedMailRouteTests;
IntakeWebTestSupport; ProductionCompositionTests; ProviderDomainReferenceIntegrationTests;
QdosAllocationRecoveryTests; QdosEmailCohortTests; QdosExtractionCoverageTests;
QdosIntakeWebTests; QdosMappingExtractionTests;
QdosTriageCaseAssociationIntegrationTests; QdosTriageIntegrationTests;
RetainedMailPersistenceTests; TriageFromIntakeIntegrationTests;
MultiFormatGenuineCorpusWebTests. Mechanical constructor/type rename updates
are allowed; preserve unrelated assertions and do not run all suites merely
because they compile against the changed contract. Add focused actual
destination tests to existing intake integration fixtures rather than a new host.

After PLAT-028 merge, OrganizationDirectoryWebTests consumes activated route
metadata and follows the same single owner. Any Settings HTML change carries
its existing snapshot/catalogue entry under root's scoped capture.

## Preserve / coordinate

Do not edit corpus, provider source originals, operator-notes, migrations,
Worker grants, MailboxIntake, Triage link workflow, engineer workflow, report
stores or PLAT-028's unmerged worktree. INTK-061's merged durability is the
base, not scope to reopen. Root owns all heavy verification and live actions.

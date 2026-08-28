# Files — CASE-031

## Where the change lands

| Path | Why |
| --- | --- |
| src/Pegasus.Core/Intake/IntakeContracts.cs | Add nullable claimant address to InstructionDraft. |
| src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs | Add explicit field labels and draft mapping. |
| src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs | Add bounded nullable draft entity property. |
| src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs | Persist and restore the value across receipt paths. |
| src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs | Preserve it during correction/replay and field reconstruction. |
| src/Pegasus.Infrastructure/Persistence/CaseDataSnapshotFactory.cs | Promote unambiguous evidence through AddExtractedValue. |
| src/Pegasus.Infrastructure/Persistence/Migrations/new claimant-address migration | Add the nullable draft column and update EF snapshots. |
| src/Pegasus.Core/Cases/CaseDataContracts.cs | Extend CaseClaimantData and CaseEditableData. |
| src/Pegasus.Core/Cases/CaseDataOperations.cs | Normalize bounded claimant-address text. |
| src/Pegasus.Infrastructure/Persistence/CaseDataEntities.cs | Add the one canonical field-name entry. |
| src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs | Save, replay and project with existing provenance/version behavior. |
| src/Pegasus.Web/Presentation/InstructionDraftFieldsView.cs and Pages/Shared/_InstructionDraftFields.cshtml | Display/edit draft value and provenance once. |
| src/Pegasus.Web/Pages/Intake/Details.* and Pages/Cases/Create.* | Carry address through correction and Case creation without gating allocation. |
| src/Pegasus.Web/Pages/Cases/Details.cshtml.cs and CaseMutationPageModel.cs | Bind/save/replay through the guarded Case action. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml and _CaseWorkflow.cshtml | Display claimant address beside claimant identity and edit it once. |
| src/Pegasus.Web/Mcp/AssessmentMcpTools.cs | Keep Automation Case read/save parity. |
| src/Pegasus.Core/Eva/EvaApiContracts.cs and CaseEvaApiMapping.cs | Carry canonical claimant address and version API mapping. |
| src/Pegasus.Infrastructure/Eva/EvaApiTransport.cs | Serialize exact ClmAdd. |
| src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs | Validate before image/network work and pass the canonical address. |
| tests/Pegasus.Core.Tests and tests/Pegasus.IntegrationTests focused intake/Case/browser/EVA tests | Prove positive flow, conflicts, persistence, UI, exact JSON and no-call failures. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| AGENTS.md | Core owns policy; schema and permissions ship together; no fabricated data. |
| docs/frd/frd-01-case-identity-and-lifecycle.md | Case snapshots and ordinary guarded edits own claimant data. |
| docs/frd/frd-02-intake-and-source-identity.md | Extraction preserves sources and ambiguity instead of inventing facts. |
| docs/frd/frd-07-eva-and-external-engineering-handoff.md | API submission is once-per-case and must preserve distinct outcomes. |
| normalized eva-api-docs.md from [[DOCS-015]] | ClmAdd is required and maximum 40 characters. |
| src/Pegasus.Core/Intake/InstructionFieldEngine.cs | Reuse candidate/conflict/provenance behavior. |
| src/Pegasus.Core/Intake/InstructionDraftCompleteness.cs | Do not add claimant address to allocation completeness. |
| src/Pegasus.Core/Address/InspectionAddressResolution.cs | Inspection address is a separate concept and must not be reused. |
| src/Pegasus.Core/Eva/CaseEvaMapping.cs and EvaBundleSchema.cs | The ZIP is fixed and explicitly out of scope; these should remain unchanged. |
| src/Pegasus.Infrastructure/Persistence/EvaSubmissionStore.cs | One local gate covers manual and automatic API callers. |

## Ripple effects

- Positional InstructionDraft, CaseEditableData, CaseClaimantData and
  EvaInstructionPayload constructions require updates.
- The draft SQL column affects EF snapshots and raw-SQL fixtures. Existing
  table permissions remain applicable.
- Web manual and Worker automatic API submission converge on
  EvaSubmissionStore.
- Saving the field follows existing Save Case version/history/completeness
  behavior.
- EVA bundle contract tests must remain unchanged and green.

## Out of scope

- EVA ZIP/operator export fields, mapping versions, fixtures, bytes or hashes.
- Fabricated/default claimant addresses.
- Inspection, repairer, sender, principal or third-party address substitution.
- General postal parsing, geocoding or address-directory work.
- Case allocation/completeness changes.
- InstEmail changes, deployment, Principal enablement or further live EVA
  mutations.

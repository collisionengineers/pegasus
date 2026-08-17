# Files — BUG-001

## Primary change surface

| Path/module | Required change or evidence | Risk |
| --- | --- | --- |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | Pass the established QDOS mail context into the extraction/decision path rather than asking extraction to rediscover the principal | Incorrect gate composition could create cases from unclassified or ambiguous mail |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` | Separate email-body principal evidence from instruction-field extraction; remove the requirement that an attachment fragment re-identify QDOS | Over-broad applicability if route/body/classification prerequisites are not explicit |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailRoutePolicy.cs` | Context: establishes accepted QDOS direct route/effective sender; no domain widening planned | Route acceptance alone must not silently become complete instruction proof |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs` | Context: owns QDOS message type/case classification | Classification must remain distinct from principal and field extraction |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | Change only if a small explicit context contract is needed between routing/classification and extraction | Avoid a provider-specific leak into generic contracts |
| `src/Pegasus.Core/Intake/IntakeAllocation.cs` | Verify allocation receives QDOS from the corrected established context/draft; no policy broadening expected | Empty/wrong principal would allocate incorrectly |
| `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs` | Replace legacy same-fragment principal tests with body-principal/document-field separation and fail-closed negatives | Tests must not encode attachment text as principal identity |
| `tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs` | Prove accepted route + QDOS body + valid classification + separate attachment fields reaches `CaseCreated` | Must also prove each missing/ambiguous prerequisite stops safely |
| `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs` | Prove the corrected shape allocates once and queues custody replay-safely | Do not use production data in fixtures |
| `docs/operations.md` / `docs/current-architecture.md` | Refresh only after an authorised deployment and observed result | Planned/source truth must not be reported as deployed truth |

## Live regression records

- Earlier pass: receipt `2c4888d6-4098-4d22-a46a-d976286a27b0`, Case/PO `QDOS26001`, Box remote ID `409001353539`.
- Later failure: receipt `9a91fe16-d62f-4477-a11e-830fd96f672a`, strong QDOS evidence from `EmailBody`, then `needs_sorting`, with no allocation or external work.
- The retained live source is evidence only. Do not commit it or copy personal data into fixtures.

## Governing/context files

| Path | Relevance |
| --- | --- |
| `docs/frd/frd-02-intake-and-source-identity.md` | Definitive authorised intake, fail-closed handling, allocation |
| `docs/frd/frd-05-documents-extraction-and-custody.md` | Field extraction and Box custody after immutable allocation |
| `docs/frd/frd-09-provider-and-intermediary-routes.md` | Keeps route, provider/principal, classification, and association facts distinct |
| `docs/operator-notes.md` | Binding operator truth; user clarification supplements the ticket research |
| `AGENTS.md` | External writes require exact-target approval and proof tiers remain separate |

## Deliberately out of scope

- No attachment-token rule, PDF whitespace repair, OCR feature, or generic parser rewrite.
- No attachment-based QDOS identification fallback without a separately specified requirement.
- No Box, queue, database-schema, migration, or manual production-data fix.
- No production deployment or receipt re-evaluation without explicit exact-target approval.
- No source implementation during the current research/planning phase.

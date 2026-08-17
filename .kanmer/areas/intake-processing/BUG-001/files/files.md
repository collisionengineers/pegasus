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

## Corrected file map — 2026-08-17

This section supersedes the earlier file-map statements that describe email-body QDOS evidence as the principal context.

### Primary change surface

| Path/module | New finding and likely responsibility | Risk |
| --- | --- | --- |
| `src/Pegasus.Core/Intake/IntakeContracts.cs` | `IInstructionExtractionPolicy.Extract` receives only content and time. The boundary needs an explicit, provider-neutral established-principal/route context, or an equivalent route-selected extraction contract. | A provider-specific generic contract would make future routes unsafe; an optional context would preserve the current content-only bypass. |
| `src/Pegasus.Core/Intake/ProcessIntake.cs` | Owns orchestration and already has the accepted `MailRouteEvaluationResult`. It must select/call extraction from that accepted context and prevent content-only QDOS creation on channels without a QDOS principal context. | Do not conflate route identity, classification, case association, or field completeness. Preserve fail-closed route and ambiguous-case exits. |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` | Remove duplicate route evaluation, all content/metadata-based QDOS identity evidence, and the same-fragment QDOS-plus-label applicability gate. Extract fields only after QDOS context is supplied; bump policy version. | Field extraction must still retain conflicts, missing fields, OCR information, and accepted Triage matcher evidence. |
| `src/Pegasus.Core/Intake/IntakeAllocation.cs` | Automatic allocation currently reads principal from `InstructionDraft.SuggestedPrincipalCode`. Enforce route-established principal as the authoritative mailbox allocation input and reject any route/draft mismatch. | Principal and reference are immutable; a mismatch must fail before allocation rather than silently prefer either value. |
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Context for replay/automatic allocation entry points. Verify reevaluation and replay retain the accepted route identity and cannot regress to content-derived principal. | Preserve idempotent evaluation/allocation and completed-work suppression. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Composition currently registers one global QDOS extraction policy. May need adjustment if extraction becomes route-selected rather than globally content-selected. | Do not duplicate Core policy or introduce dormant provider implementations. |

### Test changes and ripple effects

| Path/module | Required evidence | Risk |
| --- | --- | --- |
| `tests/Pegasus.Core.Tests/Intake/Qdos/QdosMailRoutePolicyTests.cs` | Retain exact three-domain, staff-forward prior-sender, malformed/conflicting sender, and widening rejection tests unchanged except for any shared-context fixtures. | These tests describe the correct identity owner and must not be weakened. |
| `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs` | Replace content-identifies-QDOS and same-fragment tests. Prove supplied QDOS context extracts fields across fragments; QDOS text alone never establishes applicability; metadata QDOS strings are not principal evidence. | Direct unit calls must not bypass the context required in production. |
| `tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs` | Add accepted direct and staff-forward routes whose field attachments contain no QDOS token. Add negatives for non-matching/ambiguous effective sender and content-only QDOS on mailbox/manual/automation channels. Prove route/draft mismatch fails closed. | Many current fixtures default to manual upload and implicitly depend on content-only QDOS identification; migrate intentionally rather than mechanically. |
| `tests/Pegasus.Core.Tests/Intake/AllocateDefinitiveIntakeTests.cs` | Prove mailbox allocation uses the persisted accepted route principal and rejects missing/mismatched route identity. Retain staff-create semantics separately. | Avoid changing manual staff acceptance authority while tightening automatic mailbox allocation. |
| `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs` | End-to-end accepted sender/prior sender → field extraction without attachment QDOS → exactly one allocation/link/custody item under replay. Add zero-work negatives for content-only and route/draft mismatch cases. | Fixtures must remain synthetic; do not copy live message content. |
| `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs`, `InlineForwardedMailRouteTests.cs` | Preserve reader reconstruction of one prior sender and persisted effective-sender evidence. | Do not broaden inline-forward parsing or accept partial/conflicting headers. |
| `tests/Pegasus.IntegrationTests/ProviderDomainReferenceIntegrationTests.cs` | Retain proof that policy domains equal the versioned QDOS snapshot. | No reference-package change is required. |
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` | Update constructor/implementation assertions only if the provider-neutral extraction selection contract changes. | Core remains the policy owner; Infrastructure only composes it. |
| Web/integration fixtures using `QdosInstructionExtractionPolicy` | Audit every content-only/manual fixture surfaced by repository search, including multi-format intake, QDOS Web, Triage, sent-evidence, automation-ingress, and test wrapper policies. | Do not blindly turn all manual fixtures into mailbox fixtures; each test must state its authorised principal source. |

### Context files an implementer must read

| Path | What it establishes |
| --- | --- |
| `docs/operator-notes.md` provider/intermediary routing | Staff sender is provenance; one proved prior/original sender drives route identity. |
| `docs/frd/frd-09-provider-and-intermediary-routes.md` | Exact QDOS domain set; route, provider, classification, and association are separate facts. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Receipt is not case creation; principal, route, type, and association ambiguity fail closed. |
| `docs/frd/frd-05-documents-extraction-and-custody.md` | Documents provide instruction data and custody follows immutable allocation. |
| `docs/adr/0008-separate-direct-provider-and-intermediary-email-policies.md` | Durable architecture: proved original sender drives forwarded route; direct/intermediary policies stay separate. |
| `src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v1.json` | Canonical recorded QDOS suffixes: `@qdosassist.co.uk`, `@qdosassists.co.uk`, `@qdoslaw.co.uk`. |

### Deliberately out of scope

- No changes to the three QDOS suffixes, reference package, staff-forward header reconstruction, or route-policy acceptance rules.
- No use of subject, body, filename, attachment branding, document text, OCR, or AI to identify QDOS.
- No generic multi-provider/intermediary policy framework beyond the smallest provider-neutral context needed by the active QDOS route.
- No Box, queue, database migration, schema, PDF-reader, OCR, or classification-rule change unless later planning identifies a separately governed requirement.
- No production deployment, mailbox mutation, receipt reevaluation, allocation, or Box write without exact-target approval.

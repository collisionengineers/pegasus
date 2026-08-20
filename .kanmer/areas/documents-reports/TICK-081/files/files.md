# Files — TICK-081

Surveyed against `origin/dev` `a3c88a7b` before replanning. This inventory distinguishes files that must change for EXT-08 to become true from files that are only prerequisites/context or deployment verification surfaces. Overlapping prerequisite tickets must merge serially into these same paths; TICK-081 must not create parallel implementations.

## Where the change lands

### Governing and current-state documentation

| Path | Required edit and completion risk |
| --- | --- |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Required. Define one shared Core-owned document-generation service for every document/report type; a closed typed template selection is input to that service; type-specific readiness/provenance stays upstream; unknown/unapproved/mismatched selections fail closed. Reconcile the current assessment-specific and future-Audit wording with durable generation, identity/custody/replay/failure behaviour. Do not claim closed future families as active. |
| `docs/capabilities.md` | Required at the implementation/release boundary. EXT-08 must move from allocation language to the exact achieved evidence tier only when the real durable caller exists. Update related rows only where their owning tickets have actually completed; do not turn schedule rows into a second behaviour specification. |
| `docs/adr/0030-<shared-document-generation-service>.md` | Required thin ADR unless the docs phase proves ADR-0025 already records the exact all-document service/template-input choice. Record one Core service/port, one Infrastructure adapter, closed typed template input, Web monolith execution, and no type-specific service/host/deployment. Do not edit accepted ADR-0025/0028 bodies to retrofit the choice. The final filename/title is chosen by `kanmer-docs`, but ADR-0030 is the next free stable ID. |
| `docs/adr/README.md` | Required with ADR-0030: add the accepted decision row derived from its frontmatter. |
| `docs/current-architecture.md` | Required after implementation/deployment. Replace the assessment-only caller map with the actual shared caller, type-specific projection seams, durable report/version store, one adapter/registration, Web entry point, and exact absent callers. |
| `docs/operations.md` | Required after deployment. Record the exact released SHA, successful live document generation/custody/telemetry evidence and limits, or retain an explicit fail-closed qualification. Reconcile stale statements about estimate import, Azure renderer health, and live output. |
| `docs/runbook.md` | Required if focused test class/command names or report operational diagnosis/recovery procedures change—which the shared/durable caller is expected to do. Keep the pinned Chromium install and existing release route; add only the actual shared-caller, persistence/replay, and deployed verification commands. |
| `docs/open-decisions.md` | Edited by [[TICK-082]], not guessed by TICK-081. Resolve the EXT-09 rate-card/formula row only from accepted operator authority; TICK-081 consumes the resulting Core policy. |
| `docs/prd/pegasus-product.md` | No edit expected. Existing product intent already requires accepted data, deterministic reports, Core ownership, and no dormant capability. Change only if a docs review finds a genuinely new product outcome, not to restate mechanics. |

### Core — one policy owner and one shared service

| Path | Required edit and completion risk |
| --- | --- |
| `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` | Required refactor/evolution. Preserve the accepted assessment snapshot, compute-once figures, validation, artifact hash checks, and four outcomes, but move the renderer port/use-case boundary to the shared document-generation contract. Avoid retaining `GenerateAssessmentReportDraft` as a parallel caller after the shared service becomes authoritative. |
| `src/Pegasus.Core/Reports/AssessmentReportProjection.cs` | Required. Project a typed document request/payload into the shared service and carry case type, purpose, normal Case/PO, optional immutable Audit provenance/reference, accepted payload/source hash, calculation/template version, and packaging policy as applicable. Continue reusing the single readiness vocabulary. |
| `src/Pegasus.Core/Reports/<focused shared-contract file>.cs` | Required new focused file if keeping assessment models separate improves altitude. Own the closed document-purpose/template identity, common render envelope, shared `IReportRenderer`-style port, one generation use case, common result/artifact metadata, and failure contract. The plan must choose one stable filename; do not create `Common`, `Helpers`, `Manager`, or V2 wrappers. |
| `src/Pegasus.Core/Reports/<durable generation file>.cs` | Required new focused file unless combined cleanly with the shared contract. Own request/version states, deterministic logical key, immutable internal identity, predecessor/correction link, claim/lease/retry/reconciliation outcomes, and query projection. It must keep Generated distinct from Approved, Issued, Sent, and Received. |
| `src/Pegasus.Core/Assessment/AssessmentPolicy.cs` | Required through [[TICK-082]], [[TICK-094]], and [[TICK-092]] before the caller can be live. Keep one accepted readiness vocabulary and outcome-specific gates; TICK-081 consumes it and edits only if shared-caller integration exposes a real missing common rule. |
| `src/Pegasus.Core/Assessment/AssessmentContracts.cs` | Required through prerequisites where typed Engineer decisions/calculation inputs are finalized. Do not add a second editable report-data model in TICK-081. |
| `src/Pegasus.Core/Assessment/RepairSpecifications.cs` and `src/Pegasus.Core/Assessment/EstimateImport.cs` | Prerequisite-owned edit/context. TICK-093/ENG-002 supply accepted versioned lines and calculation basis; [[TICK-082]] must add the accepted single derivation policy rather than duplicating it in Reports. |
| `src/Pegasus.Core/Documents/DocumentContracts.cs` | Required if generated report and fee-note artifacts need explicit typed semantic roles/content addresses. Reuse `DocumentSource.Generated`, immutable hashes, and custody identities; do not route system generation through a staff edit lease or call every artifact an approved report. |
| `src/Pegasus.Core/Cases/CaseContracts.cs` and `src/Pegasus.Core/Cases/CaseQueries.cs` | Context first; edit only if the existing `CaseType`, normal reference, and `AuditReference` projections cannot supply the shared report request atomically. Do not create another Audit identity implementation. |
| `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` | Conditional but likely required where approval/Sent state must reference an immutable report version rather than a free-form artifact identity/hash. Coordinate with [[TICK-208]]; generation must not absorb approval/sending policy. |
| `src/Pegasus.Core/Cases/OrganizationAdministration.cs` (or one focused provider-report setting contract) | Required by [[TICK-097]] if fee-note packaging remains per-provider. Extend the existing principal administration policy with a closed enum; never accept a per-render packaging override. |

### Infrastructure — projection, one renderer adapter, durable persistence

| Path | Required edit and completion risk |
| --- | --- |
| `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` | Required refactor/rename or replacement-in-place as the sole implementation of the shared port. Accept only the Core-approved typed template identity; map it to embedded resources; preserve one bounded Chromium lifetime, complete Scriban output, hash/page/engine metadata, and no business calculations. Remove the old assessment-port implementation once callers migrate. |
| `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs` | Required. Consume accepted TICK-082 cost derivation and TICK-092 snapshot/hash, include document purpose/Audit provenance, and keep confirmed current custody reads. It must stop hard-coding `Costs: null` only after the accepted formula exists. |
| `src/Pegasus.Infrastructure/Persistence/<report generation entities>.cs` | Required new focused entity file. Persist request/version identity, case, document purpose, accepted payload/template/calculation identity, state/attempt/lease/failure, predecessor, and typed artifact rows without overwriting prior versions. |
| `src/Pegasus.Infrastructure/Persistence/<report generation configuration>.cs` | Required new EF configuration file if the repository convention separates entities/configuration. Define bounded fields, unique logical idempotency key, state/check constraints, immutable predecessor relationship, and indexes used by claim/status queries. |
| `src/Pegasus.Infrastructure/Persistence/<Ef report generation store>.cs` | Required. Implement create-or-read replay, claim/lease expiry, completion/reconciliation, terminal/transient failure, status query, and atomic database-side identity. Coordinate content-store partial failure without duplicating the generic durable-work implementation. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Required. Register report request/version/artifact sets and model configuration following existing conventions. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_<name>.cs` | Required generated migration for the durable report model and any provider packaging setting/version-specific workflow link. Exact timestamp/name is allocated from the implementation branch. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_<name>.Designer.cs` | Required generated companion for the same migration. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | Required generated model snapshot update. |
| `src/Pegasus.Infrastructure/Persistence/EfOrganizationAdministration.cs` and the principal entity/configuration | Required by [[TICK-097]] if per-provider fee-note packaging is stored on the existing Principal. Preserve replacement/correction history and a closed code mapping. Exact entity/config paths must follow the current organization-administration model. |
| Existing `IDocumentContentStore` adapter(s), including `src/Pegasus.Infrastructure/Custody/BoxDocumentContentStore.cs` | Reuse required; edit only if generated artifact addressing cannot be represented by the current immutable content contract. Any Box production write remains separately approval-gated. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Required. Register exactly one shared renderer adapter, one shared Core service, the accepted projections, and durable store/processor. Remove old assessment-only parallel registration. |
| `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` | Conditional. Existing assessment, fee-note, CSS, logo, and Andy resources already suffice for the currently active surface and Audit parity. Edit only for an accepted additional embedded template/signature or renamed logical resource; never embed dormant families. |
| `src/Pegasus.Infrastructure/packages.lock.json` and dependent project locks | Conditional, generated only if package/project references actually change. No new rendering package is presently justified. |

### Web — real shared caller and truthful state

| Path | Required edit and completion risk |
| --- | --- |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` | Required. Invoke the shared durable Core service rather than the assessment-only transient renderer; show exact readiness/generation/failure/replay state; download the retained version; keep operation-key/authorization behaviour and never imply approval/sending. |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` | Required. Replace "Nothing is saved or sent" transient semantics with truthful durable draft/version status while retaining the single readiness rail and disabled/absent rules. |
| `src/Pegasus.Web/Pages/Cases/<shared report status/download surface>` | Conditional. Add only if the existing Assessment/Case Documents pages cannot expose every document type through one understandable case-owned surface. Do not create one page/service per report type. |
| `src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml(.cs)`, `Index.cshtml(.cs)`, and `Replace.cshtml(.cs)` | Required by [[TICK-097]] if the approved provider fee-note packaging option is configured through the existing Principal administration surface. Preserve existing admin authorization and replacement semantics. |
| `src/Pegasus.Web/Program.cs` | Required only as the composition root call remains/changes. It must continue composing the renderer only in Web and must not add a new route host, Worker path, background service deployment, or feature gate that is claimed as delivered while closed. |
| `src/Pegasus.Web/Pegasus.Web.csproj` | Verification-only unless package/reference changes. Keep the Playwright package/base-image versions synchronized through the existing property. |

### Templates and governed assets

| Path | Required edit and completion risk |
| --- | --- |
| `docs/design/assets/report-renderer/templates/assessment_report.scriban` | No behaviour edit expected for the shared-caller refactor or Audit parity; edit only if accepted payload/template mapping requires a type-neutral reference/provenance field actually visible in the approved physical output. Visual parity must be re-proved for any change. |
| `docs/design/assets/report-renderer/templates/assessment_fee_note.scriban` | Conditional for the accepted separate-vs-appended provider packaging implementation; content/wording itself remains governed and must not drift. |
| `docs/design/assets/report-renderer/templates/report.css` | No edit expected. Any change triggers four-outcome and stress visual evidence. |
| `docs/design/brand/logos/logo_no_margin.png`, `docs/design/brand/signatures/**` | No edit unless separately accepted identity evidence changes. Never modify signature bytes as part of service generalisation. |
| `reference/rendererref1/**` | Never edit. Immutable supplied evidence used for mapping and visual comparison only. Generated evidence goes under ignored `artifacts/`. |

### Tests that must change or be added

| Path | Required coverage |
| --- | --- |
| `tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs` | Refactor to prove assessment and fee-note requests use the shared Core service, Core calculations remain single-owner, invalid common/type/template envelopes fail before the adapter, and returned hashes are verified. |
| `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs` | Add accepted payload/source/template/calculation identities, document purpose, Audit/Inspection provenance, EXT-09 cost projection, stale/cross-case/mismatch failures, and same-service resolution. |
| `tests/Pegasus.Core.Tests/Reports/<shared/durable generation tests>.cs` | New. Prove one logical result per Case+type+payload+template/calculation version, concurrent duplicate reconciliation, changed-input successor version, immutable prior versions, lease expiry/retry/terminal failure, and generation not approval/Sent. |
| `tests/Pegasus.Core.Tests/Assessment/**` | Prerequisite-owned tests for EXT-09 formula/version and TICK-094 decisions; TICK-081 consumes their accepted outputs. |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs` | Refactor from a transient fake-render return to the real shared caller/status/download path; prove authorization, fail-closed reasons, idempotent replay, retained version, and no approval/send claim. |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` | Preserve four-outcome, fee-note, hash, resource, and stress coverage through the shared port. Add Inspection/Audit requests resolving the same service/adapter and equivalent physical presentation where RPT-03 is activated. |
| `tests/Pegasus.IntegrationTests/<report persistence tests>.cs` | New SQL Server tests for migration/model, unique idempotency key, claims/leases, artifact/version/provenance persistence, correction lineage, and content-store failure/reconciliation. |
| `tests/Pegasus.IntegrationTests/<provider administration tests>.cs` | Required by [[TICK-097]] for separate/appended configuration, replacement continuity, and missing/invalid fail-closed behaviour. |
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` | Required. Prove one shared Core renderer port/service and exactly one Infrastructure implementation, Web-only composition, no assessment/Audit/type-specific parallel adapter, no renderer libraries in Core, and no standalone deployment boundary. |
| Existing report approval/Sent tests, including `tests/Pegasus.IntegrationTests/CaseReportApprovalWebTests.cs` and Core workflow tests | Conditional through [[TICK-208]] if approval/Sent evidence gains an immutable report-version link. Must prove old version evidence survives correction. |
| Browser/visual evidence under ignored `artifacts/report-renderer/**` | Required proof output, never committed source. Four outcomes, configured fee-note packaging, long lists/photos, Audit/Inspection parity when active, and exact retained SHA/version evidence. |

### Build, CI, release, and infrastructure surfaces

| Path | Required treatment |
| --- | --- |
| `.github/workflows/ci.yml` | No edit expected if tests remain in existing unit/SQL/Browser lanes. Edit only if a genuinely new required lane cannot fit; do not weaken the pinned Chromium or sharding gates. |
| `Directory.Build.props` and project `packages.lock.json` files | No edit expected; verify package/browser pin consistency. Update locks only from an actual dependency change. |
| `infra/modules/platform.bicep` and `infra/main.bicep` | No edit expected: ADR-0028's Web boundary, 1 vCPU/2 GiB, probes, and single replica already exist. Change only from measured capacity/recovery evidence and a reviewed infrastructure scope. |
| `scripts/Build-ReleaseArtifacts.ps1`, `scripts/Test-AzureDeploymentPlan.ps1`, `scripts/Invoke-ProductionSmoke.ps1` | Verification/context first. Edit only if the new durable report health/diagnostic assertion belongs in the existing release smoke without performing a business render or fabricating production case data. |
| `src/Pegasus.Web/Pegasus.Web.csproj` | Preserve `mcr.microsoft.com/playwright/dotnet:v$(PlaywrightVersion)-noble`; the existing image is the runtime. |
| Kanmer `proof.md` | Required only after the exact reviewed result is merged to `main` and deployed/verified. It is a ticket document, not a repository file. |

## Context files

| Path / ticket | What it tells the implementer |
| --- | --- |
| `AGENTS.md` | Core ownership, closed-composition rule, documentation routing, exact evidence tiers, worktree safety, deployment/main authority. |
| EPIC-004 `context.md` | Binding monolith shape, Core policy/identity/custody, rendererref1 evidence status, no separate service and no unauthorised cloud write. |
| `docs/index.md` | Authority chain; current state and required behaviour must not be written into the wrong document. |
| `docs/adr/0025-*.md`, `docs/adr/0028-*.md` | Existing integration and Web execution decisions; no new deployment unit. |
| `docs/frd/frd-11-*.md` | Current four outcomes, Audit parity, closed activation, manual draft caller, correction/finality, human approval and Sent boundary. |
| `docs/engineering.md` | Required evidence tiers and one-Core-owner/simplicity constraints. |
| `docs/runbook.md` | Locked restore/build/test, Chromium, release, live-operation approval and recovery procedures. |
| `docs/open-decisions.md` | EXT-09 rate/formula authority is unresolved and cannot be inferred from imported estimate data. |
| `reference/rendererref1/DESIGN_SPEC.md`, schema, sample JSON/PDFs | Approved supplied assessment/fee-note evidence plus known stale/unaccepted wording; immutable, not runtime policy. |
| `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` | Current policy/calculation/port/result owner to preserve. |
| `src/Pegasus.Core/Reports/AssessmentReportProjection.cs` | Current single readiness projection and reachable case-level operation. |
| `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs` | Real confirmed/custodied source composition and the intentional `Costs: null` blocker. |
| `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` | Sole adapter, fixed two-template invocation, Chromium lifetime, provenance metadata, resource loading. |
| `src/Pegasus.Core/Cases/CaseContracts.cs`, `CaseQueries.cs` | Existing normal/Audit identity and case type; no second Audit reference owner is allowed. |
| `src/Pegasus.Core/Documents/DocumentContracts.cs` and content-store adapters | Generated source/semantic roles and immutable bytes/hash mechanics. |
| `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` and EF workflow store | Existing approval/Sent finality and the current single-artifact association limitation. |
| `src/Pegasus.Core/Assessment/RepairSpecifications.cs`, `EstimateImport.cs` | Accepted versioned repair-spec and import/calculation-basis inputs; formulas are still absent. |
| [[SIMPLI-014]] proof/PIR | Integrated source/Core/adapter/Web/local Chromium evidence and explicit durable-caller exclusions. |
| [[PLAT-007]] research/proof | Deployed Web/Chromium topology and health; not successful live render/custody/retry evidence. |
| [[DOCS-001]] research/files | Durable request/version/artifact/custody/replay gap and intended owner; refresh stale caller/dependency assumptions. |
| [[TICK-082]], [[TICK-094]], [[TICK-092]] | Formula, Engineer decisions, and accepted snapshot/hash prerequisites. |
| [[TICK-096]], [[TICK-097]], [[TICK-098]], [[TICK-206]], [[TICK-216]] | Deterministic renderer, four outcomes/packaging, Audit parity, closed template map, and wording/signature acceptance. |
| [[DOCS-003]], [[DOCS-004]], [[TICK-100]], [[TICK-208]] | Future templates/workflows and post-generation version/Sent boundaries that must reuse the shared service without being prematurely activated. |

## Ripple effects

- **Caller migration:** every existing `GenerateAssessmentReportDraft` and `IAssessmentReportRenderer` caller/fake/test must migrate or deliberately become an internal type-specific projection into the one shared service. No two public generation use cases survive.
- **Persistence:** report versions introduce migration, grants/runtime-role bootstrap review, concurrency/recovery tests, backup/restore compatibility, and case/document query projections.
- **Approval/Sent:** immutable report-version identity may require later workflow links; generation must remain non-final.
- **Principal administration:** fee-note packaging changes principal create/replace/list, persistence, schema, tests, and staff-visible configuration.
- **Audit:** current case/Audit identity data becomes request provenance; no Audit template/resource or adapter is added.
- **Deployment:** the same Web image/revision carries the change. Exact-target Azure write approval is required later; current infra shape should remain unchanged unless measurements prove otherwise.
- **Documentation/evidence:** current architecture and operations update in the deployment task; generated PDFs/logs stay ignored; TICK-081 proof collates exact merged-main and deployed evidence.
- **Ticket sequencing:** add TICK-082 as a real predecessor, finish or explicitly subsume the remaining Preparing dependencies, and assign overlapping source files to one implementation PR at a time.

## Out of scope

- Editing `reference/rendererref1/**`.
- Activating diminution, addendum, valuation evidence, generic letters, Part 35, or any template without its own accepted behaviour and real type-specific projection.
- Adding an Audit-only caller, service, template, renderer, model, host, deployment, comparison specification, or uplift.
- Adding a Worker renderer, queue consumer deployment, Container Apps Job, renderer API/MCP host, fifth project, package, or separate repository.
- Inventing EXT-09 formulas, rate-card ownership, missing wording, engineer qualifications, signatures, provider settings, or production test data.
- Treating generated drafts as approved, issued, sent, received, invoiced, or case-closing.
- Performing any Azure, Box, mailbox, credential, database, release, `main`, or other external write during research.

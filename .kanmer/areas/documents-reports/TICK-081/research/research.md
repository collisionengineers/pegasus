# Research — TICK-081: EXT-08 shared deterministic document-generation caller

## Question

What must be true, in code, governing documentation, deployment estate, evidence, and the Kanmer dependency graph, before EXT-08 can be moved to Done under the operator's rule that every report/document type uses the same function or service in the Pegasus .NET monolith, with any type-specific template supplied to that shared caller?

## Evidence baseline

- Repository source was inspected read-only at current `origin/dev` `a3c88a7bbdb43cf4cbd9303022397f6e028d7bf9`. The primary checkout is intentionally not source authority for this research because it is a user-owned local `dev` checkout at `c41314d9`, one commit ahead and 105 behind `origin/dev`; it was not changed.
- Production/release source is `origin/main` `2325ed4a31d7dad65a00a7ae5ea0c41ca869bfa5`. SIMPLI-014 / PR #415 is reachable from it. TICK-098 / PR #466 is merged to `dev` as `b36c6666` but is not on `main`.
- No code, branch, worktree, database, Azure resource, mailbox, Box location, supplied `reference/` evidence, or deployment state was changed. Git, source, ticket, and documentation inspection was read-only.
- Binding authority read: AGENTS.md; `docs/index.md`; EPIC-004 `context.md`; FRD-11; ADR-0025; ADR-0028; capabilities, boundaries, engineering, runbook, current-architecture, operations, open-decisions; the rendererref1 supplied evidence; and the related Kanmer tickets listed below.

## Settled operator decision

- There is one shared Core-owned document-generation caller/service in the Pegasus .NET monolith for every document/report type.
- A document type may supply a different approved template or typed template selection to that service. It must not create a type-specific caller, service, renderer family, host, queue consumer, deployment unit, or parallel policy owner.
- Audit and Inspection must reach that same service. Their accepted workflow/reference provenance may differ; the physical Audit output reuses the approved Inspection presentation under RPT-03.
- "Same caller" is not permission for an arbitrary file path or free-form template ID. Core must own a closed, typed document-type/template mapping and readiness policy. Missing, unknown, unapproved, mismatched, or ambiguous template/type input fails before Infrastructure rendering.
- This is both required behaviour (FRD-11) and a durable technical contract shape. ADR-0025 and ADR-0028 already require one integrated Core port, one in-process Infrastructure adapter, and the Web Container App execution boundary, but they do not explicitly record the all-document shared-service/template-input rule. Under repository routing, planning should treat a thin ADR-0030 plus FRD-11 behaviour as required unless the docs phase demonstrates that ADR-0025 already records the exact decision without reinterpretation. Accepted ADR bodies are not edited to retrofit it.

## Current application call chain

The current real source path is singular but assessment-specific:

```text
POST /Cases/{id}/Assessment?handler=GenerateReportDraft
  -> GenerateCaseAssessmentReportDraft
  -> IAssessmentReportProjectionSource / EfAssessmentReportProjectionSource
  -> AssessmentReportProjection.Project
  -> GenerateAssessmentReportDraft
  -> IAssessmentReportRenderer.RenderAsync
  -> PlaywrightAssessmentReportRenderer
  -> assessment_report.scriban + assessment_fee_note.scriban
  -> assessment PDF returned to the browser
```

Sources: `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml(.cs)`, `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`, `src/Pegasus.Core/Reports/AssessmentReportRendering.cs`, `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs`, `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs`, `src/Pegasus.Infrastructure/DependencyInjection.cs`, and `src/Pegasus.Web/Program.cs`.

Findings:

1. **There is one registered adapter and one reachable Web operation, but its public contract is not yet the required all-document service.** The port is `IAssessmentReportRenderer`, the Core use case is `GenerateAssessmentReportDraft`, and the case-level operation is `GenerateCaseAssessmentReportDraft`. All names, inputs, and outputs are assessment-specific. The Architecture test proves exactly one implementation of that assessment port; it does not prove that Audit, fee note as an independently packaged document, addendum, diminution, letters, or future document types enter one common caller.

2. **The current Infrastructure method hard-codes two templates inside one assessment render.** `PlaywrightAssessmentReportRenderer.RenderAsync` creates the assessment and fee-note contexts, invokes `assessment_report.scriban` and `assessment_fee_note.scriban`, and returns an `AssessmentReportDraft` pair. This is useful reuse and already means the two current physical artifacts share one engine invocation, but callers cannot supply a closed typed document/template request. The service boundary therefore needs to become document-generic without exposing arbitrary template names or paths.

3. **Core currently owns substantial correct policy.** `AssessmentReportSnapshot.Validate` rejects incomplete references, source hashes, photo custody/hash mismatches, invalid economics, missing total-loss Category S evidence, incomplete physical assessment location, unsupported payload version, and mismatched engineer tuple. Core computes repair and fee arithmetic; Infrastructure formats and renders. This is the owner to preserve and generalise, not a second rules list.

4. **The report projection reuses real accepted sources.** `EfAssessmentReportProjectionSource` composes `IGetCase`, `IGetCaseAssessment`, confirmed current document occurrences, and `IDocumentContentStore`; it does not synthesize photos or provenance. `AssessmentReportProjection` reuses `AssessmentPolicy.EvaluateReadiness` and appends report-specific reasons. A future shared caller must consume type-specific projections/readiness into one render request; it must not make the generic service responsible for discovering or inventing every document type's business inputs.

5. **The current Web action is manual draft generation, not the EXT-08 durable trigger.** It returns only the assessment PDF and explicitly saves, approves, and sends nothing. The operation key only protects the form shape; it does not create a durable generation identity or reconcile request interruption. FRD-11 separates generation from approval, issue, sending, receipt, and correction custody.

## Current data and blocking readiness

6. **Every production projection still passes `Costs: null`.** `EfAssessmentReportProjectionSource` deliberately does so because no accepted EXT-09 formula maps accepted repair-specification lines and a calculation basis into `ReportRepairCosts`. Therefore `AssessmentReportProjection` always emits "Repair cost figures" and no real case reaches the adapter. Unit and Web tests reach it only with constructed costs/fakes.

7. **ENG-002 / PR #455 improved input custody but did not close the formula gap.** Merged `dev` now supports a deterministic Audatex PDF import, a retained source artifact, draft/accepted repair-specification lines, and `RepairCalculationBasis`. ENG-002 remains Verifying and the change is not on production `main`. Its unchecked acceptance item explicitly assigns conversion into report costs to EXT-09.

8. **TICK-082 / EXT-09 is a newly confirmed hard blocker missing from TICK-081's structured links.** It is still Backlog with no research documents. `docs/open-decisions.md` leaves rate-card ownership and WU/rate, sundry, material-band, and VAT derivation formulas unresolved. Until TICK-082 obtains accepted authority and implements the single Core calculation policy, neither the manual draft caller nor any durable shared caller can produce a real accepted report. Planning must add `[[TICK-082]]` as a structured predecessor; it must not bury the dependency in prose.

9. **TICK-093 / ENG-01 is complete and reusable.** It supplies one immutable accepted repair specification with source route/version/hash, Engineer acceptance, and correction lineage. TICK-094 / ENG-02 and TICK-092 / CASE-31 remain Preparing, so the complete typed Engineer-owned decisions and one deterministic accepted document-input snapshot/hash are not yet landed.

10. **Audit provenance exists in the case domain, but there is no Audit render caller.** Core/persistence already expose `CaseType`, immutable `AuditReference`, and `AuditAssessment`; case queries expose the normal Case/PO plus the Audit reference. The current report projection reads only `details.Summary.Reference` into `OurReference` and does not carry report purpose/type or Audit provenance. A shared request can add those typed facts without creating an Audit-specific renderer. Standalone Audit intake evidence is original-report classification evidence, not generated-report input by itself.

## Template and renderer evidence

11. **The runtime resources are deliberately closed.** `Pegasus.Infrastructure.csproj` embeds only `assessment_report.scriban`, `assessment_fee_note.scriban`, `report.css`, the logo, and the complete Andy Patterson signature tuple. `reference/rendererref1/**` is immutable supplied evidence, not runtime policy or an editable task surface.

12. **The four assessment outcomes are implemented and real-Chromium tested locally/CI.** Total loss, Repairable, Cash in lieu, and Contract repair share one Core snapshot and Infrastructure adapter. The renderer tests prove PDF/header/content/hash/template/engine metadata and stress pagination. PR-009 fixed Scriban's 1 MiB output truncation while retaining normal density.

13. **The supplied evidence contains known stale or unaccepted material.** DESIGN_SPEC prose says "three" outcomes in places although the table/schema and operator decision establish four. It also contains unsupported salvage/category/recovery wording and incomplete engineer qualifications. FRD-11's closed activation and Core validation outrank those stale/reference-only statements. TICK-216 is Preparing despite its operator question being resolved; its completion/subsumption must be reconciled before EXT-08 claims every required accepted resource.

14. **Future templates remain closed.** DOCS-003 (diminution) and DOCS-004 (addendum) correctly require separately approved representative templates and workflows, but both instruct future work to reuse the existing Core report identity/readiness/render contract. The shared caller may accept their typed templates only when those capabilities activate; EXT-08 must not add dormant descriptors or placeholder templates merely to anticipate them.

## Durable generation and custody gaps

15. **No durable report aggregate exists.** Current code has transient `RenderedReportArtifact` bytes/hash/page/template/engine metadata and separate workflow concepts for report approval and Sent evidence. There is no report request, report version, payload hash, template selection identity, artifact pair/packaging record, predecessor link, generation state, attempt/lease, failure, or durable content address.

16. **DOCS-001 owns these missing mechanics and is the direct implementation predecessor.** Its research correctly identifies a logical idempotency key of Case + document family/type + accepted payload hash + template/calculation version; immutable assessment/fee-note artifact custody; replay/reconciliation; and append-only correction versions. Its older claim that tests were the only caller is stale after DELIV-012, and its scope currently excludes Audit. Before implementation it must be refreshed to consume the all-document shared-service rule and TICK-082's accepted cost policy instead of creating an assessment-only durable caller.

17. **Existing persistence conventions are reusable.** Case assessment saves use serializable transactions, expected versions, operation-key/request-hash replay, and permanent history. Existing durable intake/custody/lookup work supplies claim/lease/retry/failure conventions. `IDocumentContentStore` supplies immutable bytes/hash verification. The report workflow needs a focused case-owned store and state contract; it should not overload staff `AddCaseDocumentCommand`, current single report-approval fields, or invent a generic job framework.

18. **Fee-note packaging is a required upstream decision already recorded by TICK-097.** Provider configuration chooses either a separate linked immutable fee-note artifact or appended fee-note pages; per-render caller choice is forbidden and missing/ambiguous configuration fails closed. No implementation or dedicated ticket for that setting was found. TICK-097 must implement or explicitly assign this before DOCS-001/TICK-081 completion.

## Architecture and deployed estate

19. **The hosting decision is already made.** ADR-0025 integrates renderer code into the modular monolith behind a Core port; ADR-0028 places the in-process adapter in the existing Web Container App. `Pegasus.Web.csproj` pins the matching Playwright Chromium base image, and `infra/modules/platform.bicep` gives the existing Web container 1 vCPU/2 GiB, one always-warm replica, and startup/liveness/readiness probes. Worker remains unchanged. No new service or deployment unit is justified.

20. **Production has hosting/caller reachability, not successful live document output.** Release 12/13 carries the renderer and Chromium base; the authenticated Report-draft panel is deployed and fails closed. PLAT-007's useful evidence proves topology, image/runtime presence, health, and absence of a standalone renderer. Its checked sentence "A deployed render completes" is not backed by a real live-case output: the same proof admits the path stops at missing repair costs. TICK-081 must require a real deployed render, retained artifact/version, telemetry, retry/restart/duplicate evidence, and exact-SHA proof after the durable caller and EXT-09 land.

21. **Current-state docs drift.** `docs/operations.md` says no estimate import exists; import is now merged on `dev` but not `main`, so the statement is still true for production but needs careful release-scoped wording. It also simultaneously says no Azure deployment/health result is claimed while later release evidence and PLAT-007 say the Chromium Web revision is deployed/healthy. `docs/current-architecture.md` maps the Core caller to `AssessmentReportRendering.cs` but omits `AssessmentReportProjection.cs`, the EF projection source, the Web page entry point, and the explicit fail-closed cost boundary. Completion must refresh both snapshots from the exact deployed SHA.

## Kanmer dependency and ownership estate

| Ticket | Current state | Relevance to TICK-081 |
| --- | --- | --- |
| [[SIMPLI-014]] | Done | One Core assessment port, one Infrastructure adapter, closed rendererref1 resources, real Chromium proof. |
| [[DOCS-002]] / ADR-0028 | Done | Existing Web Container App execution boundary. |
| [[PLAT-007]] | Done with one unchecked residual | Hosting/deployment topology evidence; not live successful output or durable-retry proof. |
| [[TICK-093]] | Done | Canonical accepted repair specification and provenance. |
| [[ENG-002]] | Verifying, merged to dev | Audatex import and calculation-basis input; explicitly leaves report-cost derivation to EXT-09. |
| [[TICK-082]] | Backlog; missing from TICK-081 links | Required EXT-09 rate/formula authority and Core cost derivation. Hard blocker. |
| [[TICK-094]] | Preparing | Accepted typed Engineer decisions/economics. |
| [[TICK-092]] | Preparing | One accepted structured source snapshot and deterministic payload hash. |
| [[TICK-096]] | Preparing | RPT-01 deterministic renderer acceptance; much code is subsumed by SIMPLI-014 but status/proof must be reconciled. |
| [[TICK-097]] | Preparing | RPT-02 four outcomes, fee-note packaging/config, representative acceptance. |
| [[TICK-098]] | Verifying; dev-only docs | RPT-03 Audit physical parity/provenance; does not prove an Audit caller. |
| [[TICK-206]] | Preparing | Closed template-to-capability map; implementation largely subsumed but ticket is not closed. |
| [[TICK-216]] | Preparing | Approved wording/signature boundary; implementation partly subsumed, ticket is not closed. |
| [[DOCS-001]] | Preparing | Durable shared caller/job/result/custody implementation owner and direct predecessor. |
| [[DOCS-003]], [[DOCS-004]] | Backlog | Future typed templates/workflows; must reuse shared caller but stay closed now. |
| [[TICK-208]], [[TICK-100]] | Preparing | Version-specific Sent evidence and addendum lineage after the base durable report version exists. |

The existing structured `blockedBy` set includes SIMPLI-014, DOCS-001, PLAT-007, TICK-092/093/094/096/097/206/216, but omits TICK-082. Done predecessors should be treated as satisfied evidence, while Preparing/Backlog predecessors must actually finish or be explicitly and honestly subsumed; a green document gate is not capability readiness.

## Completion implications

- TICK-081 should remain an acceptance envelope over one shared implementation, not create a second renderer/caller alongside DOCS-001.
- The eventual plan must first repair/confirm the dependency graph, including TICK-082; refresh DOCS-001/TICK-092/TICK-094/TICK-096/TICK-097 research against current `dev`; and decide which ticket owns the shared-service refactor so one PR edits each overlapping file.
- The shared Core service should accept a closed typed request containing document purpose/type, approved template identity/version, accepted payload/source identity/hash, business reference/provenance, and packaging policy. Type-specific projection/readiness happens before that service; the same service validates the common envelope, calls the one Infrastructure renderer, verifies artifact hashes, and hands results to one durable result/custody workflow.
- Audit and Inspection tests must resolve the same Core service and Infrastructure adapter. A template difference, where later accepted, is request data; service type/registration and deployment stay identical.
- Completion requires source, caller, durable persistence, local/CI Chromium, architecture, concurrency/replay/failure, deployed exact-SHA, retained artifact/reference, telemetry, and current-state documentation evidence. A DI registration, reachable disabled button, container health, or test-only synthetic costs is insufficient.
- No cloud write is authorised by research. A later exact production deployment still requires explicit approval for exact targets; a `dev` to `main` promotion requires immediate `MERGE AUTH GRANTED`.

## Open questions

No new operator-only question about caller topology remains: the shared caller/service and template-as-input direction is explicit.

The following are prerequisite-owned, not safe assumptions for TICK-081 planning:

- EXT-09 rate-card ownership and derivation formulas — TICK-082 / `docs/open-decisions.md`.
- Exact completion of typed Engineer decisions and accepted snapshot/hash — TICK-094 and TICK-092.
- Provider fee-note packaging configuration implementation — TICK-097.
- Any future document family's template/content/workflow — its own capability ticket.

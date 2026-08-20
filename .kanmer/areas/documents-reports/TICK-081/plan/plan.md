# Plan — TICK-081: Activate one deterministic document-generation service

## Approach

Treat TICK-081 as the activation and acceptance envelope for one implementation owned by [[DOCS-001]], not as a second renderer branch. Before that implementation starts, its research/plan must be refreshed from TICK-081's current research and files inventory. It will evolve the assessment-specific public use case and port into one Core-owned document-generation service and one Infrastructure renderer adapter. Every activated generated document/report type enters that same service; a type supplies only a closed, typed purpose/template selection and its accepted projection. Audit and Inspection therefore share the service and approved Inspection presentation while retaining their distinct reference provenance. TICK-081 reaches Done only after the prerequisite policy tickets, that single implementation, review, release, deployment, and exact proof are complete.

This beats four alternatives: retaining assessment-specific public services would violate the operator decision; adding an Audit or future-type service would duplicate policy; exposing arbitrary template names or paths would fail open; and keeping the old zero-diff acceptance plan would leave the current assessment-only contract and transient browser return unchanged. A Worker, renderer API, queue deployment, new project, or workspace integration is also rejected because ADR-0025 and ADR-0028 already select the monolith's existing Web process.

## Governing docs

- **Modifies — `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`.** Under the operator's 2026-08-20 direction, the implementing docs phase must state that every activated generated document type uses one shared Core service; type-specific projections and approved templates are typed inputs; unknown, mismatched, missing, dormant, or ambiguous selections fail before rendering. It must preserve generation versus approval/Sent boundaries, immutable versions/custody, Audit reference provenance, and Audit/Inspection physical-output parity.
- **Meets — `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md`.** The implementation keeps business policy and the public generation contract in Core and one in-process renderer adapter in Infrastructure. It does not create another renderer system.
- **Meets — `docs/adr/0028-run-integrated-renderer-in-web-container-app.md`.** Rendering stays in the existing Web Container App and existing image; no Worker renderer, new deployment unit, queue consumer, API, MCP host, or fifth project is introduced.
- **New ADR — next free ID (currently ADR-0030).** Before code, run `kanmer-docs` to record the durable technical decision that all generated document types enter one Core-owned service and one Infrastructure adapter through a closed typed template map. Link the resulting ADR into TICK-081 and [[DOCS-001]]. Do not retrofit this decision into accepted ADR-0025 or ADR-0028.
- **Schedule/current state.** Update `docs/capabilities.md` only in the PR that makes EXT-08 true. Refresh `docs/current-architecture.md` and `docs/operations.md` in the authorised deployment/release task, not before deployment.

## Steps

1. **Hold activation behind the real predecessor graph.** Keep TICK-081 in Preparing while blockers remain. [[TICK-082]] is now a structured blocker and must settle the rate-card owner and WU/rate, sundry, material-band, and VAT rules, then implement one Core calculation policy. [[TICK-092]] and [[TICK-094]] must supply the accepted immutable input snapshot and Engineer-owned decisions. Accept merged evidence from [[TICK-093]]/ENG-002 and reconcile [[TICK-096]], [[TICK-097]], [[TICK-206]], and [[TICK-216]] rather than reimplementing them.

2. **Record the shared-service contract before implementation.** Use `kanmer-docs` to create the thin shared-caller ADR and update FRD-11 as described above. Link both governing docs to [[DOCS-001]] and TICK-081. Keep future diminution, addendum, valuation, letter, and other inactive templates closed until their own accepted FRD/template evidence exists.

3. **Replan [[DOCS-001]] as the sole implementation owner.** Refresh its research, files, plan, checklist, and links against this ticket. Assign all overlapping Core, Infrastructure, Web, persistence, migration, provider-configuration, and test edits to that one branch/PR. Do not take TICK-081 or another report ticket for the same files concurrently.

4. **Implement one closed Core entry point in [[DOCS-001]].** Replace the assessment-specific public generation use case/port with one clearly named Core service and request contract for all activated generated documents. The request carries a closed document purpose/type, the Core-selected approved template identity, accepted projection/version/hash, Case identity, normal/Audit reference provenance, and configuration needed for packaging. Type-specific projection code may prepare accepted data, but it must call this same service. Core owns the single type-to-template/readiness map and rejects missing, unknown, dormant, mismatched, stale, ambiguous, or uncustodied input before Infrastructure. Do not leave a public assessment or Audit generation service beside it.

5. **Adapt the existing renderer and projections without a second policy owner.** Refactor the existing Playwright adapter into the sole implementation of the shared Core port, retaining the pinned Chromium engine, embedded resource loading, CSS/assets, pagination fixes, and deterministic metadata. Inspection and Audit select the same approved assessment presentation; Audit supplies its immutable Audit reference/provenance through the request and gets no Audit-only template. Provider configuration from [[TICK-097]] decides separate versus appended fee-note packaging; per-request choice and missing/ambiguous configuration fail closed. Future types can add accepted projections/templates to the one map later, not alternate callers.

6. **Make generation durable and recoverable.** In [[DOCS-001]], persist an immutable request/version and generated artifact set with source/payload/template/calculation identities, content hashes, provenance, predecessor/correction lineage, state, attempt/lease/failure data, and the configured fee-note relationship. Reuse the case transaction/idempotency and `IDocumentContentStore` conventions. The logical identity is Case + activated document type/family + accepted payload hash + template/calculation version. Replay returns the same completed version; interruption can reconcile; a correction appends a new version. Generation never implies approval, sending, receipt, invoicing, or closure.

7. **Wire the existing Web composition and staff-visible state.** Register exactly one shared service and one renderer adapter in the existing composition root. Route the current assessment operation and Audit/Inspection generation paths through it; expose durable pending/completed/failed status, retained download(s), and actionable fail-closed reasons without adding a type-specific controller/page service. Extend existing provider administration only as required for the settled fee-note packaging configuration. Generate and review the EF migration/snapshot required by the durable store and configuration; do not hand-edit generated migration metadata.

8. **Verify and simplify the implementation PR.** Add focused Core tests for the closed map, readiness, provenance, idempotency, correction, and all failure paths; Infrastructure/Browser tests for the one adapter, four assessment outcomes, both fee-note packaging modes, Audit/Inspection presentation parity, hashes, long content, and renderer failure; persistence/Web tests for custody, replay/recovery, status/download, and configuration; and architecture tests proving exactly one Core port/service, one Infrastructure implementation, Web-only composition, and no type-specific parallel adapter. Run the four simplification lenses over the branch diff, apply behavior-preserving findings, and record dated dispositions in the implementing ticket's plan.

9. **Review, merge, promote, and deploy through existing controls.** Independently review the implementing PR against its plan, governing docs, and single-caller invariant; require green CI before merging to `dev`. Promote an exact reviewed `dev` SHA to `main` only after explicit `MERGE AUTH GRANTED`. Obtain separate explicit approval naming the exact Azure subscription, resource group, resources, and operation immediately before any deployment write. Deploy through the existing Web Container App, then refresh current-state docs and retain exact revision/image/SHA, health, telemetry, artifact, retry/restart, and failure evidence.

10. **Produce TICK-081 acceptance proof and close only on observed evidence.** On merged `main` and the deployed exact revision, write `proof.md` identifying the single Core service and sole Infrastructure adapter, and show at least Inspection and Audit entering that same service with the same physical presentation and their correct distinct provenance. Include dependency PRs/SHAs, retained request/version/artifact identities and hashes, representative four-outcome PDFs, fee-note packaging, fail-closed cases, replay/recovery/correction, architecture assertions, deployment telemetry, and current-state documentation. If any activated type bypasses the service or any prerequisite remains assumed, leave TICK-081 open and return the gap to its owning ticket.

## Verification

From the implementing worktree, run the locked canonical commands exactly:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
```

The post-implementation report records command results, focused behavior evidence, migration review, simplification dispositions, and CI links. TICK-081 `proof.md` is written only after merge to `main` and deployment. It must distinguish source registration, local composed execution, merged-main identity, and deployed caller/artifact evidence; a green build, healthy Chromium process, or registration alone does not prove a real document used the caller.

## Risks / open questions

- **Cost policy is unresolved.** [[TICK-082]] owns the authoritative formulas; TICK-081 and [[DOCS-001]] must not infer them from Audatex or supplied examples.
- **A generic service could become a policy catch-all.** Keep document-specific projection/readiness with its existing Core owner and centralise only generation identity, closed template selection, rendering, custody, and recovery.
- **Template mappings could be duplicated.** Core owns one typed map; Infrastructure resolves embedded resources only from that selection, and architecture/tests detect a second list or adapter.
- **Ticket overlap could lose work.** [[DOCS-001]] is the only implementation branch for the shared caller and durable store; dependencies land first and overlapping report tickets are not taken concurrently.
- **Future scope could leak in.** No placeholder template, dormant selector, or speculative projection is added without its own accepted governing behavior and representative evidence.
- **Production evidence needs authority.** No cloud or release write occurs without the exact approvals required by AGENTS.md and the runbook.
- **No operator question remains for planning.** The shared-caller, closed typed-template input, and Audit/Inspection parity decisions are settled; prerequisite product decisions remain owned by their linked tickets.

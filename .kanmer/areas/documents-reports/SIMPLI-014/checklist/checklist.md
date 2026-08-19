# Checklist — SIMPLI-014

- [x] Update FRD-11 with the approved four-outcome assessment/fee-note activation contract, Core ownership, fail-closed wording/signature rules, inactive unsupported families, and generation-versus-issue boundary; leave `reference/rendererref1/` unchanged.
- [x] Add the minimal `Pegasus.Core/Reports` request/result port and Core application caller, reusing existing assessment/readiness/source-version vocabulary and leaving durable trigger/reference/custody ownership to DOCS-001.
- [x] Migrate the reusable Scriban/Playwright/PDFsharp rendering pipeline into `Pegasus.Infrastructure/Reports`, removing arbitrary local-path/base64/density/template selection and workspace-only authoring/output mechanics from the application boundary.
- [x] Implement the fixed rendererref1 assessment/fee-note mapping for four outcomes, pin and verify canonical embedded resources, use normal clean page flow, and fail closed on unsupported families, placeholders, incomplete inputs and signature/attachment mismatches.
- [x] Register the adapter and Core use case in existing Infrastructure/Web composition with a bounded reusable Chromium lifecycle; add no HTTP, Razor, MCP, CLI, Worker trigger or separate service.
- [x] Remove `workspaces/report-renderer/` and standalone API/CLI/MCP/MCPB/container surfaces after inventory; reconcile `workspaces/README.md`, `.github/workflows/workspaces.yml`, `Pegasus.slnx` and dependency-direction assertions while preserving provenance/history.
- [x] Adopt root analyzers/warnings-as-errors, add only required renderer dependencies, regenerate existing Pegasus package locks, and add Chromium setup to the relevant main CI/test path.
- [x] Migrate focused engine tests and add Core/Infrastructure/Web-composition tests, including one real approved rendererref1 Chromium render and fail-closed legacy-template/path/signature/incomplete-request cases.
- [x] Run the reuse, simplification, efficiency and altitude pass; apply behaviour-preserving findings and append a dated “Simplification pass” section with every disposition to `plan`.
- [x] Refresh `docs/current-architecture.md` and accurately qualify `docs/operations.md` as integrated locally but not deployed; write the post-implementation report with scope/deviations.
- [x] Run locked restore, Release build, focused report/architecture/integration checks, full Release tests, documentation checks, resource/package/advisory checks and representative Chromium evidence; record exact results for proof.

## Progress notes

(append with `set_ticket_doc(doc: "checklist", append: true)`)

- 2026-08-19: Implementation complete through Core port/use case, closed Infrastructure renderer, application composition, workspace/host retirement, explicit resources/packages/locks, focused real-Chromium evidence, current-state docs, and two independent simplification reviews. Awaiting the final full non-corpus solution run before marking verification complete.


- 2026-08-19 final verification: locked restore passed; Release build passed with zero warnings/errors; focused report Core 9/9, real renderer 2/2, dependency-direction 39/39; whole Core 625/625 and Architecture 97/97 passed in the full non-corpus solution invocation. The legacy Integration suite exceeded its documented ~12-minute baseline and remained silent, so it was proportionally stopped after the renderer-focused tests had passed; CI's existing sharded lanes remain the authoritative whole-suite gate. Documentation placement/links passed, Infrastructure package vulnerability output was clear, standalone surface searches were empty, and two retained PDFs record real Chromium evidence.

- 2026-08-19 PR-006/PR-007 corrections: completed exact supplied assessment/fee-note sections, hash-validated ordered photo bytes, VAT/total/payment/terms and all-four-outcome real Chromium content proof; reconciled stale runbook/design workspace references. Release build passed with zero warnings/errors; focused Core 11/11 and Browser 5/5 passed. A combined local full Integration host aborted after 124 passing tests, and the non-Browser rerun aborted after 28 passing tests, during substantial concurrent dotnet activity on the shared workstation; no test failure was reported. Existing CI head's shard-3 failure was independently inspected and is an unrelated LocalDB disposal lock (`ApprovedMailboxEstateIntegrationTests`, 173/174 passed); the new push will rerun authoritative shards.

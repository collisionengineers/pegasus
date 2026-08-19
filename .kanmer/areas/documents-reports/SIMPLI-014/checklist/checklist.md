# Checklist — SIMPLI-014

- [ ] Update FRD-11 with the approved four-outcome assessment/fee-note activation contract, Core ownership, fail-closed wording/signature rules, inactive unsupported families, and generation-versus-issue boundary; leave `reference/rendererref1/` unchanged.
- [ ] Add the minimal `Pegasus.Core/Reports` request/result port and Core application caller, reusing existing assessment/readiness/source-version vocabulary and leaving durable trigger/reference/custody ownership to DOCS-001.
- [ ] Migrate the reusable Scriban/Playwright/PDFsharp rendering pipeline into `Pegasus.Infrastructure/Reports`, removing arbitrary local-path/base64/density/template selection and workspace-only authoring/output mechanics from the application boundary.
- [ ] Implement the fixed rendererref1 assessment/fee-note mapping for four outcomes, pin and verify canonical embedded resources, use normal clean page flow, and fail closed on unsupported families, placeholders, incomplete inputs and signature/attachment mismatches.
- [ ] Register the adapter and Core use case in existing Infrastructure/Web composition with a bounded reusable Chromium lifecycle; add no HTTP, Razor, MCP, CLI, Worker trigger or separate service.
- [ ] Remove `workspaces/report-renderer/` and standalone API/CLI/MCP/MCPB/container surfaces after inventory; reconcile `workspaces/README.md`, `.github/workflows/workspaces.yml`, `Pegasus.slnx` and dependency-direction assertions while preserving provenance/history.
- [ ] Adopt root analyzers/warnings-as-errors, add only required renderer dependencies, regenerate existing Pegasus package locks, and add Chromium setup to the relevant main CI/test path.
- [ ] Migrate focused engine tests and add Core/Infrastructure/Web-composition tests, including one real approved rendererref1 Chromium render and fail-closed legacy-template/path/signature/incomplete-request cases.
- [ ] Run the reuse, simplification, efficiency and altitude pass; apply behaviour-preserving findings and append a dated “Simplification pass” section with every disposition to `plan`.
- [ ] Refresh `docs/current-architecture.md` and accurately qualify `docs/operations.md` as integrated locally but not deployed; write the post-implementation report with scope/deviations.
- [ ] Run locked restore, Release build, focused report/architecture/integration checks, full Release tests, documentation checks, resource/package/advisory checks and representative Chromium evidence; record exact results for proof.

## Progress notes

(append with `set_ticket_doc(doc: "checklist", append: true)`)

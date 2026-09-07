# Decision index

Architecture Decision Records capture durable **technical/architectural** product
decisions only. The conventions for writing them — stable IDs, YAML frontmatter,
one decision per ADR, supersede-don't-renumber — live in
[`AGENTS.md`](../../AGENTS.md#adr-conventions). Documentation rules, product
intent, and feature behaviour are **not** ADRs; see the PRD/FRD taxonomy in
[`AGENTS.md`](../../AGENTS.md) and the [documentation index](../index.md).

Every ADR carries frontmatter (`id`, `status`, `date`, `supersedes`,
`superseded_by`, `related_capabilities`, `related_frd`, `tags`). The **current
architecture is the set below with `status: accepted`**. An accepted ADR with
a `superseded_by` list retains only the unaffected clauses; its Status section
names the precise partial scope. Published bodies are
immutable; a changed decision is recorded by a new, superseding ADR — IDs are
never renumbered or reused.

## Decisions

| ID | Title | Status | Superseded-by | Owner capability |
| --- | --- | --- | --- | --- |
| [ADR-0001](0001-hybrid-pdf-extraction.md) | Hybrid PDF extraction | accepted | — | — |
| [ADR-0002](0002-dotnet-modular-monolith-on-azure.md) | .NET modular monolith on Azure App Service | accepted | ADR-0004, ADR-0007, ADR-0015, ADR-0030, ADR-0032 | — |
| [ADR-0003](0003-pdfpig-for-first-qdos-slice.md) | PdfPig for the first QDOS embedded-text slice | accepted | — | — |
| [ADR-0004](0004-provider-api-and-staff-mcp-authentication.md) | Provider API and staff MCP authentication | accepted | ADR-0011 | — |
| [ADR-0005](0005-multiformat-intake-assets.md) | Multi-format intake and review assets | accepted | — | — |
| [ADR-0006](0006-provider-neutral-intake-with-contained-qdos-policy.md) | Provider-neutral intake with a contained QDOS policy | accepted | — | — |
| [ADR-0007](0007-direct-terminal-azure-deployment.md) | Direct authorised-terminal Azure deployment | accepted | ADR-0014, ADR-0015, ADR-0037 | — |
| [ADR-0008](0008-separate-direct-provider-and-intermediary-email-policies.md) | Separate direct-provider and intermediary email policies | accepted | — | — |
| [ADR-0009](0009-adopt-pegasus-monorepo-workspaces.md) | Adopt Pegasus monorepo source workspaces | accepted | — | — |
| [ADR-0010](0010-adopt-single-context-domain-documentation.md) | Adopt single-context domain documentation | superseded | — | — |
| [ADR-0011](0011-restrict-mcp-to-automation-actor.md) | Restrict MCP to a vendor-neutral Automation Actor | accepted | — | — |
| [ADR-0012](0012-conservative-mot-mileage-estimation.md) | Conservative MOT mileage estimation | superseded | — | — |
| [ADR-0013](0013-qdos-alpha-implementation-contract.md) | QDOS alpha implementation contract | superseded | ADR-0029 | — |
| [ADR-0014](0014-local-to-production-deployment.md) | Local-to-production deployment only | accepted | — | — |
| [ADR-0015](0015-host-web-on-container-apps-consumption.md) | Host Pegasus Web on Azure Container Apps Consumption | accepted | — | — |
| [ADR-0016](0016-standalone-desktop-email-evaluator.md) | Standalone local desktop email evaluator | accepted | — | — |
| [ADR-0018](0018-provider-inspection-mode-database-setting.md) | Provider-determined inspection mode as a database setting | accepted | — | — |
| [ADR-0019](0019-in-process-onnx-vrm-recognition.md) | In-process ONNX VRM recognition engine | accepted | — | — |
| [ADR-0020](0020-accepted-qdos-case-association-predicates.md) | Accepted QDOS automatic case-association predicates | superseded | — | — |
| [ADR-0021](0021-automation-actor-direct-write-assessment-contract.md) | Automation Actor direct-write assessment contract and the Send to AI transport slice | superseded | ADR-0031 | — |
| [ADR-0022](0022-approved-mailbox-identity-and-enablement-database-setting.md) | Approved-mailbox identity and enablement as an administrator-editable database setting | superseded | ADR-0024 | — |
| [ADR-0023](0023-restructure-repository-documentation-and-reference-evidence.md) | Restructure repository documentation and reference evidence | superseded | — | — |
| [ADR-0024](0024-stable-approved-mailbox-identity-and-explicit-baseline.md) | Stable approved-mailbox identity and per-mailbox fresh start | accepted | — | — |
| [ADR-0025](0025-integrate-renderer-and-extractor-into-the-application.md) | Integrate the report renderer and document extractor into the application, not into standalone packages | accepted | — | RPT-01, RPT-02, RPT-03, RPT-04, RPT-05, INT-10, INT-11, INT-12 |
| [ADR-0026](0026-enable-automation-mcp-by-explicit-deployment-configuration.md) | Enable Automation MCP by explicit deployment configuration | accepted | — | MCP-01, MCP-02, MCP-03, MCP-04, MCP-06 |
| [ADR-0027](0027-authorization-code-for-external-mcp-connectors.md) | Authorization code with PKCE for external MCP connectors | accepted | — | MCP-01, MCP-02, MCP-03, MCP-04, MCP-06 |
| [ADR-0028](0028-run-integrated-renderer-in-web-container-app.md) | Run the integrated report renderer in the Web Container App | accepted | — | EXT-08, RPT-01, RPT-02 |
| [ADR-0029](0029-image-initiated-case-projection.md) | Image-initiated Case projection | accepted | — | INT-17, INT-28 |
| [ADR-0030](0030-non-additive-schema-changes-before-cutover.md) | Non-additive schema changes before cutover | accepted | — | — |
| [ADR-0031](0031-automation-actor-contract-without-eva-export-tools.md) | Automation Actor contract without EVA export tools | accepted | — | MCP-06, AI-09 |
| [ADR-0032](0032-near-real-time-durable-intake-triggering.md) | Near-real-time durable intake triggering | superseded | ADR-0033 | INT-33 |
| [ADR-0033](0033-warm-unified-work-queue-for-five-second-intake.md) | Warm unified work queue for five-second intake | accepted | — | INT-33 |
| [ADR-0034](0034-per-principal-eva-api-submission-settings.md) | Per-Principal EVA API submission settings | accepted | — | EXT-04 |
| [ADR-0035](0035-ai-job-ledger.md) | AI job ledger | accepted | — | AI-10, AI-09, MCP-06, MCP-01 |
| [ADR-0036](0036-outbound-mail-via-approved-mailbox.md) | Outbound mail via the approved mailbox | accepted | — | — |
| [ADR-0037](0037-linux-authorised-release-workstation.md) | Linux authorised release workstation | accepted | — | OPS-10, OPS-24 |

ADR-0017 was never issued (a numbering collision while filing 0018/0019); the gap
is intentional and the number is not reused.

Acceptance of a decision is design authority within its scope. No ADR proves
implementation, a real caller, deployment, live verification, or operator
acceptance unless separately named evidence records that exact state.

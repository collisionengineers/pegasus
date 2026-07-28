# Decision index

New durable repository decisions use `NNNN-purpose.md` files in this directory.

`docs/decisions/` is the single canonical authority for durable repository
decisions. It contains both current decisions and preserved historical ADRs.
Supersede decisions explicitly; never rewrite published history to hide a changed
choice.

## Architecture decision records

| Decision | Status | Summary |
| --- | --- | --- |
| [ADR-0001: Hybrid PDF extraction](ADR-0001-hybrid-pdf-extraction.md) | Accepted | Hybrid PDF extraction boundary; the embedded engine is selected by ADR-0003 and scan qualification is refined by ADR-0005. |
| [ADR-0002: .NET modular monolith on Azure App Service](ADR-0002-dotnet-modular-monolith-on-azure.md) | Accepted, partially superseded | Modular-monolith, runtime, data, regional, and cost decisions remain; ADR-0004 supersedes the provider API/MCP authentication model and ADR-0009 supersedes the deployment mechanism. |
| [ADR-0003: PdfPig for the first QDOS embedded-text slice](ADR-0003-pdfpig-for-first-qdos-slice.md) | Accepted for the first local QDOS slice | PdfPig selection still requires genuine-corpus cohort and holdout evidence before production use. |
| [ADR-0004: Provider API and staff MCP authentication](ADR-0004-provider-api-and-staff-mcp-authentication.md) | Accepted | Provider API is `Next`/`unallocated`; staff MCP is `0.1.0-alpha.1` but intake-only until `Next`/`unallocated` email work. |
| [ADR-0005: Multi-format intake and review assets](ADR-0005-multiformat-intake-assets.md) | Accepted for the local `0.1.0-alpha.1` slice | Multi-format assets; every visible DOCX placement is retained as an occurrence. |
| [ADR-0006: Provider-neutral intake with a contained QDOS policy](ADR-0006-provider-neutral-intake-with-contained-qdos-policy.md) | Accepted for the pre-release local intake slice | Supersedes ADR-0005 decision 1 only; decision 0011 supersedes its single-policy selection and no-provider-registry/table limits while preserving provider-neutral transport, provenance, storage, and fail-closed boundaries. |
| [ADR-0007: Repository-local Codex planning plugin boundaries](ADR-0007-repository-local-codex-planning-plugin-boundaries.md) | Superseded by ADR-0008 | Historical workflow-plugin decision. |
| [ADR-0008: Focused repository workflow plugins](ADR-0008-focused-repository-workflow-plugins.md) | Superseded by [0010](0010-adopt-azure-workflow.md) | Historical focused workflow-plugin decision; 0010 is superseded by 0012. |
| [ADR-0009: Direct authorised-terminal Azure deployment](ADR-0009-direct-terminal-azure-deployment.md) | Accepted | Supersedes ADR-0002's deployment mechanism only; no GitHub Actions/OIDC deployment. |

## Repository decisions

| Decision | Status | Summary |
| --- | --- | --- |
| [0010: Adopt Azure Workflow repository standard](0010-adopt-azure-workflow.md) | Superseded by [0012](0012-adopt-tool-neutral-repository-workflow.md) | Retained repository-workflow history. |
| [0011: Separate direct-provider and intermediary email policies](0011-separate-direct-provider-and-intermediary-email-policies.md) | Accepted | Separates direct-provider and intermediary email policy while preserving ADR-0006's provider-neutral boundaries. |
| [0012: Adopt a tool-neutral repository workflow](0012-adopt-tool-neutral-repository-workflow.md) | Accepted | Current tool-neutral repository workflow decision. |
| [0013: Adopt Pegasus monorepo source workspaces](0013-adopt-pegasus-monorepo-workspaces.md) | Accepted | Adopts independently buildable, non-caller source workspaces without changing the production runtime boundary. |

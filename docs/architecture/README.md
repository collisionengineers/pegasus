# Pegasus decision index

The canonical current architecture is [docs/architecture.md](../architecture.md).
This directory retains accepted and historical ADRs plus their detailed context.

This directory records stable technical decisions and the intended system shape.
It is not the current implementation inventory: dated caller evidence lives in
[the implementation handoff](../agent-notes/current-implementation-handoff.md),
and live Azure facts live in [the Azure inventory](../azure/current-inventory.md).
Business requirements remain under `docs/operator-notes/` and are not edited here.

## System shape

```mermaid
flowchart LR
    Staff[Staff] --> Web[Pegasus.Web\nRazor Pages and HTTP]
    Provider[Provider API - Next / unallocated] --> Web
    Mcp[Staff MCP - 0.1.0-alpha.1] --> Web
    Web --> Core[Pegasus.Core\nuse cases and policy]
    Worker[Pegasus.Worker\nplanned trigger host] --> Core
    Core --> Infrastructure[Pegasus.Infrastructure\nSQL and external adapters]
    Infrastructure --> Sql[(Azure SQL)]
    Infrastructure --> Blob[Transient Blob and queues]
    Infrastructure --> Box[Box source custody]
```

The four production projects are `Pegasus.Core`,
`Pegasus.Infrastructure`, `Pegasus.Web`, and
`Pegasus.Worker`. Web and Worker are the two composition roots; Core owns
business policy and must not depend on Azure, EF Core, Graph, Box, or Web.
Infrastructure implements Core ports. SQL owns workflow, action history, and
source/file relationships; Box owns long-term original files; Blob and queues
are transient processing infrastructure.

### Current evidence versus target

The sole proven mutating intake path is the Development-only
`/Intake/Upload` -> `ProcessIntake` path. The dashboard, `/Intake/Queue`, and
`/Intake/Review` are query callers; review download calls
`IIntakeArtifactStore`. The Worker has no trigger or Core caller. Mailbox,
Box, Blob, OCR service, provider API, staff MCP, EVA adapter, live telemetry,
and Azure deployment are planned or absent, not merely unverified. See the
dated handoff for source evidence and limitations.

The intended Azure environments are isolated local development, Azure
development/integration, and production. The release design is an authorised
terminal using committed Bicep and `azd`; it requires an explicit migration
before application deployment. It is documented, not runnable or production
ready. [ADR-0009](decisions/ADR-0009-direct-terminal-azure-deployment.md) and
[the Azure release plan](../azure/README.md) own the route and its gaps.

## Architecture decisions

| Decision | Status | Summary |
| --- | --- | --- |
| [ADR-0001](decisions/ADR-0001-hybrid-pdf-extraction.md) | Accepted | Hybrid PDF extraction boundary. |
| [ADR-0002](decisions/ADR-0002-dotnet-modular-monolith-on-azure.md) | Accepted, partially superseded | Modular-monolith and runtime decisions remain; API/MCP is superseded by ADR-0004 and release mechanism by ADR-0009. |
| [ADR-0003](decisions/ADR-0003-pdfpig-for-first-qdos-slice.md) | Accepted for the local slice | PdfPig selection still needs genuine-corpus cohort and holdout evidence before production use. |
| [ADR-0004](decisions/ADR-0004-provider-api-and-staff-mcp-authentication.md) | Accepted | Provider API is `Next`/`unallocated`; staff MCP is `0.1.0-alpha.1` but intake-only until `Next`/`unallocated` email work. |
| [ADR-0005](decisions/ADR-0005-multiformat-intake-assets.md) | Accepted for the local slice | Multi-format assets; each visible DOCX placement is retained as an occurrence. |
| [ADR-0006](decisions/ADR-0006-provider-neutral-intake-with-contained-qdos-policy.md) | Accepted for the pre-release local slice | Provider-neutral intake with a contained QDOS policy. |
| [ADR-0007](decisions/ADR-0007-repository-local-codex-planning-plugin-boundaries.md) | Superseded by ADR-0008 | Historical workflow-plugin decision. |
| [ADR-0008](decisions/ADR-0008-focused-repository-workflow-plugins.md) | Superseded by [0010](../decisions/0010-adopt-azure-workflow.md) | Historical focused repository workflow plugins. |
| [ADR-0009](decisions/ADR-0009-direct-terminal-azure-deployment.md) | Accepted | Direct authorised-terminal Azure release; no GitHub Actions/OIDC deployment. |

## Architecture work still required

1. Prove SQL migrations and reference allocation against SQL Server/Azure SQL,
   including concurrency and duplicate delivery.
2. Replace ignored local retention with caller-backed Blob staging and Box source
   custody before deployed intake.
3. Implement target OCR, Azure integrations, provider API, and staff MCP through
   their real callers and shared Core use cases.
4. Build and prove the separate migration, package, identity, and release path
   recorded by ADR-0009; do not infer it from the current Bicep or `azure.yaml`.

The complete product gap is maintained in `docs/product/qdos-alpha-gap.md`.

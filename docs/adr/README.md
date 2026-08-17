# Decision index

Architecture Decision Records capture durable **technical/architectural** product
decisions only. The conventions for writing them — stable IDs, YAML frontmatter,
one decision per ADR, supersede-don't-renumber — live in
[`AGENTS.md`](../../AGENTS.md#adr-conventions). Documentation rules, product
intent, and feature behaviour are **not** ADRs; see the PRD/FRD taxonomy in
[`AGENTS.md`](../../AGENTS.md) and the [documentation index](../index.md).

Every ADR carries frontmatter (`id`, `status`, `date`, `supersedes`,
`superseded_by`, `related_capabilities`, `related_frd`, `tags`). The **current
architecture is the set below with `status: accepted`**. Published bodies are
immutable; a changed decision is recorded by a new, superseding ADR — IDs are
never renumbered or reused.

## Current architecture decisions (`status: accepted`)

| ADR | Title | Related FRD |
| --- | --- | --- |
| [0001](0001-hybrid-pdf-extraction.md) | Hybrid PDF extraction | FRD-05 |
| [0002](0002-dotnet-modular-monolith-on-azure.md) | .NET modular monolith on Azure | — |
| [0003](0003-pdfpig-for-first-qdos-slice.md) | PdfPig for the first QDOS embedded-text slice | FRD-05 |
| [0004](0004-provider-api-and-staff-mcp-authentication.md) | Provider API and staff MCP authentication | FRD-09, FRD-10 |
| [0005](0005-multiformat-intake-assets.md) | Multi-format intake and review assets | FRD-02, FRD-05 |
| [0006](0006-provider-neutral-intake-with-contained-qdos-policy.md) | Provider-neutral intake with contained QDOS policy | FRD-02 |
| [0007](0007-direct-terminal-azure-deployment.md) | Direct authorised-terminal Azure deployment | — |
| [0008](0008-separate-direct-provider-and-intermediary-email-policies.md) | Separate direct-provider and intermediary email policies | FRD-08, FRD-09 |
| [0009](0009-adopt-pegasus-monorepo-workspaces.md) | Adopt Pegasus monorepo source workspaces | — |
| [0011](0011-restrict-mcp-to-automation-actor.md) | Restrict MCP to a vendor-neutral Automation Actor | FRD-10 |
| [0013](0013-qdos-alpha-implementation-contract.md) | QDOS alpha implementation contract | FRD-01/02/06/07/08/12 |
| [0014](0014-local-to-production-deployment.md) | Local-to-production deployment only | — |
| [0015](0015-host-web-on-container-apps-consumption.md) | Host Pegasus Web on Azure Container Apps Consumption | — |
| [0016](0016-standalone-desktop-email-evaluator.md) | Standalone local desktop email evaluator | FRD-08 |
| [0018](0018-provider-inspection-mode-database-setting.md) | Provider-determined inspection mode as a database setting | FRD-02, FRD-06 |
| [0019](0019-in-process-onnx-vrm-recognition.md) | In-process ONNX VRM recognition engine | FRD-06 |
| [0021](0021-automation-actor-direct-write-assessment-contract.md) | Automation Actor direct-write assessment contract | FRD-10, FRD-11 |
| [0024](0024-stable-approved-mailbox-identity-and-explicit-baseline.md) | Stable approved-mailbox identity and per-mailbox fresh start | FRD-08 |
| [0025](0025-integrate-renderer-and-extractor-into-the-application.md) | Integrate the report renderer and document extractor into the application, not into standalone packages | FRD-02, FRD-05, FRD-11 |

## Superseded and relocated

| ADR | Title | Now owned by |
| --- | --- | --- |
| [0010](0010-adopt-single-context-domain-documentation.md) | Adopt single-context domain documentation | `AGENTS.md` / [`docs/index.md`](../index.md) — governance is not an ADR |
| [0012](0012-conservative-mot-mileage-estimation.md) | Conservative MOT mileage estimation | [FRD-06](../frd/frd-06-vehicle-and-engineering-evidence.md) |
| [0020](0020-accepted-qdos-case-association-predicates.md) | Accepted QDOS automatic case-association predicates | [FRD-09](../frd/frd-09-provider-and-intermediary-routes.md) |
| [0022](0022-approved-mailbox-identity-and-enablement-database-setting.md) | Approved-mailbox identity and enablement as a database setting | [ADR-0024](0024-stable-approved-mailbox-identity-and-explicit-baseline.md) — estate decision carried forward; behaviour in [FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md) |
| [0023](0023-restructure-repository-documentation-and-reference-evidence.md) | Restructure repository documentation and reference evidence | `AGENTS.md` / [`docs/index.md`](../index.md) — governance is not an ADR |

ADR-0017 was never issued (a numbering collision while filing 0018/0019); the gap
is intentional and the number is not reused.

Acceptance of a decision is design authority within its scope. No ADR proves
implementation, a real caller, deployment, live verification, or operator
acceptance unless separately named evidence records that exact state.

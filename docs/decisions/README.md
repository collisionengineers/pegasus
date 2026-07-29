# Decision index

`docs/decisions/` is the canonical durable-decision authority. Published ADR
bodies, including their decision clauses, rationale, and dated provenance, are
immutable. Reviewed navigation, current status, and supersession metadata is
maintained in this index without changing a decision's meaning; a changed choice
is recorded by a dated addendum or superseding decision.

Acceptance of a decision is design authority within its scope. It does not prove
implementation, a real caller, deployment, live verification, or operator
acceptance.

## Architecture decision records

| Decision | Current status and qualification |
| --- | --- |
| [ADR-0001: Hybrid PDF extraction](ADR-0001-hybrid-pdf-extraction.md) | Accepted. ADR-0003 selects the embedded engine and ADR-0005 refines scan qualification; neither supersedes hybrid extraction, uncertainty/review routing, or provider-boundary rationale. |
| [ADR-0002: .NET modular monolith on Azure](ADR-0002-dotnet-modular-monolith-on-azure.md) | Accepted target architecture, partially superseded by ADR-0004 for API/MCP authentication, ADR-0009 for deployment mechanism, and Decision 0013 where the older repository shape implied no source workspaces. It is not caller or deployment proof. |
| [ADR-0003: PdfPig for the first QDOS slice](ADR-0003-pdfpig-for-first-qdos-slice.md) | Accepted for the first local embedded-text slice. Its immutable body retains the historical literal `docs/evaluation/qdos-pdf-engine-benchmark.md`; that path no longer exists and is not a live link. Use the [retained benchmark evidence](../changes/2026-07-27-qdos-alpha-reference-corpora.md#embedded-pdf-benchmark-identity) for live navigation. The benchmark selects an engine; it does not prove field accuracy, representative-corpus suitability, production readiness, or acceptance. |
| [ADR-0004: Provider API and staff MCP authentication](ADR-0004-provider-api-and-staff-mcp-authentication.md) | Accepted security design. ADR-0014 supersedes only its **Maturity and activation** paragraph's old release labels and intake-only alpha-MCP wording: provider API is `0.4.0`, broader classified-email/MCP work is `0.3.0`, and staff MCP remains `0.1.0-alpha.1` with the exact `MCP-01`–`MCP-04` caller matrix. The remaining security boundary is accepted; no endpoint, OAuth server, scope matrix, or caller is proved by the ADR or allocation. |
| [ADR-0005: Multi-format intake assets](ADR-0005-multiformat-intake-assets.md) | Accepted local-slice policy. Every visible placement/occurrence is retained; hashes correlate equal bytes but do not delete placements. Current format support remains caller-proved evidence. |
| [ADR-0006: Provider-neutral intake with contained QDOS policy](ADR-0006-provider-neutral-intake-with-contained-qdos-policy.md) | Accepted pre-release policy. Decision 0011 supersedes its single-policy selection and no-provider-registry/table limits. ADR-0014 supersedes only **Decision items 1 and 8** and the named opening **Limits and deferred-capability impact** exclusions for QDOS-alpha implementation. Provider-neutral transport, provenance, storage, migration, and fail-closed boundaries remain. |
| [ADR-0007: Repository-local planning plugin boundaries](ADR-0007-repository-local-codex-planning-plugin-boundaries.md) | Superseded historical record. Decision 0012 is the current tool-neutral workflow authority. |
| [ADR-0008: Focused repository workflow plugins](ADR-0008-focused-repository-workflow-plugins.md) | Superseded historical record. Decision 0010 succeeded it and Decision 0012 superseded 0010. |
| [ADR-0009: Direct authorised-terminal Azure deployment](ADR-0009-direct-terminal-azure-deployment.md) | Accepted target deployment mechanism, superseding only ADR-0002's mechanism. It authorizes no command and proves no deployment. |
| [ADR-0014: QDOS alpha implementation contract](ADR-0014-qdos-alpha-implementation-contract.md) | Accepted checkpoint 1 addendum. It activates the clause-specific QDOS implementation and Razor/Worker/MCP caller contract under issue #3 while retaining the separate `DOC-CON-052` evaluator delivery and post-alpha repository-policy deferral. It changes no capability allocation and proves no implementation, caller, deployment, or acceptance state. |

## Repository decisions

| Decision | Current status and qualification |
| --- | --- |
| [0010: Adopt Azure Workflow](0010-adopt-azure-workflow.md) | Superseded by [0012](0012-adopt-tool-neutral-repository-workflow.md). Body retained as reviewed onboarding provenance. |
| [0011: Separate direct-provider and intermediary email policies](0011-separate-direct-provider-and-intermediary-email-policies.md) | Accepted route-policy authority. Design/alpha target only until registry, selectors, policies, Worker, case model, and callers are exercised. |
| [0012: Adopt a tool-neutral repository workflow](0012-adopt-tool-neutral-repository-workflow.md) | Accepted current repository-workflow authority. Tools execute accepted work; they do not own product rules or authorization. |
| [0013: Adopt Pegasus monorepo source workspaces](0013-adopt-pegasus-monorepo-workspaces.md) | Accepted. Workspaces are independently buildable source imports and never application callers, dynamic dependencies, deployment units, or business-policy owners without a separately accepted integration contract and caller proof. |

New durable decisions use `NNNN-purpose.md`. Reviewed navigation, current
status, and supersession metadata is maintained here; published bodies remain
unchanged.

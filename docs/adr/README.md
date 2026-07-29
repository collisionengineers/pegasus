# Decision index

`docs/adr/` is the canonical durable-decision authority. Published ADR
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
| [ADR-0001: Hybrid PDF extraction](0001-hybrid-pdf-extraction.md) | Accepted. ADR-0003 selects the embedded engine and ADR-0005 refines scan qualification; neither supersedes hybrid extraction, uncertainty/review routing, or provider-boundary rationale. |
| [ADR-0002: .NET modular monolith on Azure](0002-dotnet-modular-monolith-on-azure.md) | Accepted target architecture, partially superseded by ADR-0004 for API/MCP authentication, ADR-0007 for deployment mechanism, and ADR-0009 where the older repository shape implied no source workspaces. It is not caller or deployment proof. |
| [ADR-0003: PdfPig for the first QDOS slice](0003-pdfpig-for-first-qdos-slice.md) | Accepted for the first local embedded-text slice. Its immutable body retains the historical literal `docs/evaluation/qdos-pdf-engine-benchmark.md`; that path no longer exists and is not a live link. Use the [retained benchmark evidence](../changes/2026-07-27-qdos-alpha-reference-corpora.md#embedded-pdf-benchmark-identity) for live navigation. The benchmark selects an engine; it does not prove field accuracy, representative-corpus suitability, production readiness, or acceptance. |
| [ADR-0004: Provider API and staff MCP authentication](0004-provider-api-and-staff-mcp-authentication.md) | Accepted security design. Provider API is allocated to `0.4.0`; broader classified-email/MCP work is allocated to `0.3.0`; staff MCP remains `0.1.0-alpha.1` intake/case/document scope. No endpoint, OAuth server, scope matrix, or caller is proved by the ADR or allocation. |
| [ADR-0005: Multi-format intake assets](0005-multiformat-intake-assets.md) | Accepted local-slice policy. Every visible placement/occurrence is retained; hashes correlate equal bytes but do not delete placements. Current format support remains caller-proved evidence. |
| [ADR-0006: Provider-neutral intake with contained QDOS policy](0006-provider-neutral-intake-with-contained-qdos-policy.md) | Accepted pre-release policy, partially superseded by ADR-0008 for separate direct-provider/intermediary policies and registry limits. Provider-neutral transport, provenance, storage, migration, and fail-closed boundaries remain. |
| [ADR-0007: Direct authorised-terminal Azure deployment](0007-direct-terminal-azure-deployment.md) | Accepted target deployment mechanism, superseding only ADR-0002's mechanism. It authorizes no command and proves no deployment. |

## Repository decisions

| Decision | Current status and qualification |
| --- | --- |
| [ADR-0008: Separate direct-provider and intermediary email policies](0008-separate-direct-provider-and-intermediary-email-policies.md) | Accepted route-policy authority. Design/alpha target only until registry, selectors, policies, Worker, case model, and callers are exercised. |
| [ADR-0009: Adopt Pegasus monorepo source workspaces](0009-adopt-pegasus-monorepo-workspaces.md) | Accepted. Workspaces are independently buildable source imports and never application callers, dynamic dependencies, deployment units, or business-policy owners without a separately accepted integration contract and caller proof. |
| [ADR-0010: Adopt single-context domain documentation](0010-adopt-single-context-domain-documentation.md) | Accepted. Root `CONTEXT.md` is the domain glossary and `docs/adr/` is the sole root durable-decision store; existing source roles and workspace-local decisions remain unchanged. |

New durable decisions use `NNNN-purpose.md`. Reviewed navigation, current
status, and supersession metadata is maintained here; published bodies remain
unchanged.

---
id: ADR-0009
status: accepted
date: 2026-07-27
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: []
tags: [architecture, workspaces]
---
# ADR-0009: Adopt Pegasus monorepo source workspaces

- Date: 2026-07-27
- Status: accepted

## Context

Pegasus remains a .NET modular monolith with one Core business-policy owner,
one Infrastructure adapter project, and Web and Worker composition roots. Four
related repositories contain durable document-extraction, deterministic-report,
AI Centre, and Agent Skills source that should be maintained with Pegasus, but
none has a current Pegasus production caller. Adding those sources as runtime
projects would create dormant capability and duplicate domain ownership.

ADR-0002 describes the repository as exactly four production projects and three
test projects. That runtime boundary remains correct, but it does not provide a
place for independently buildable source assets which are deliberately outside
the deployed solution.

## Decision

Adopt **Pegasus** as the active product and repository identity. Keep these as
the only production projects:

- `Pegasus.Core`;
- `Pegasus.Infrastructure`;
- `Pegasus.Web`; and
- `Pegasus.Worker`.

Keep the three corresponding Pegasus test projects in `Pegasus.slnx`. Add a
`workspaces/` source boundary containing:

- `document-extraction/`, imported from `collisiondocnetconverter`;
- `report-renderer/`, imported from `collisionrenderer`; and
- `ai-centre/`, imported from Collision AI Centre.

Repository-specific engineering workflow remains owned by
`docs/engineering.md`; root `AGENTS.md` routes work into that policy.
Reusable procedures are installed under `.agents/skills/` and must operate
within the repository policy rather than override it. The
`workspaces/ai-centre/skills/` tree contains application-facing AI-agent skill
packages, not skills for developing Pegasus. Those packages remain outside the
application caller, runtime-project, deployment-unit, and business-policy
boundaries described below.

A workspace is durable source, not a runtime, deployment unit, application
module, package activation, or caller. Each workspace retains its independent
build, test, lock and solution boundaries. `Pegasus.slnx` must not reference a
workspace. No production project may reference workspace source until a later
capability-specific ADR/change record defines the Core contract, proves the
actual caller and recovery path, and accepts the dependency.

`Pegasus.Core` remains the only owner of case truth, business policy, repair and
valuation acceptance, report decisions, and AI proposal review. Future
Infrastructure document extraction adapts the imported library behind
`IIntakeSourceReader`. Future rendering consumes a Core-owned render contract;
report policy does not move into Infrastructure or the renderer. Future AI
workers may lease Core-owned, capability-scoped work and return proposals or
visible failures, but they may never write accepted case state directly.

Collision AI Centre owns future agent harnesses, runtime evaluations, retrieval,
model selection and separately governed fine-tuning. Pegasus embeds no
Claude-specific transport and activates no direct model API in this decision.
The future desktop workbench remains deferred until the Web capability is
complete.

This decision supersedes ADR-0002 only where ADR-0002 implies that the repository
may contain no top-level source owner beyond the four production and three test
projects. ADR-0002's runtime, dependency direction, one Core, one database,
one migration stream and four-project production boundary remain accepted.

## Consequences

Workspace health is validated independently in CI without implying product
activation. Architecture tests keep every production `ProjectReference` out of
`workspaces/` and prevent runtime embedding of workspace source, workbooks,
Python or PowerShell.

Imported suite-specific guidance is removed or replaced by workspace-local
instructions. AI Centre documentation consumes `Pegasus.Core` and the report
renderer rather than planning duplicate case-domain or renderer owners.

A later activation may choose not to use an imported source asset. Source
incorporation therefore preserves options without making a vendor, transport,
deployment or data-migration commitment.

---
id: ADR-0031
status: accepted
date: 2026-08-25
supersedes: [ADR-0021]
superseded_by: []
related_capabilities: [MCP-06, AI-09]
related_frd: [frd-07, frd-10, frd-11]
tags: [mcp, automation, ai, eva]
---

# ADR-0031: Automation Actor contract without EVA export tools

## Status

Accepted. Supersedes ADR-0021; ADR-0026 and ADR-0027 still refine production
enablement and external connector authentication.

## Context

ADR-0021 included separate Automation MCP tools for generating an EVA bundle
and reading its hand-off status. FRD-07 now defines authenticated staff Export
as the single current act that creates the EVA package and sends the case to an
Engineer through the existing manual EVA route. Keeping another tool surface
would duplicate that act.

## Decision

The Automation Actor keeps its existing attributed direct-write assessment and
case-detail tools, safeguards, scopes, history, operation keys and Send to AI
pointer transport. It does not expose `pegasus_eva_bundle_generate` or
`pegasus_eva_handoff_status`, and no replacement automation Export tool is
introduced. EVA package generation belongs to the staff Export boundary in
FRD-07.

## Consequences

- The active MCP inventory has no separate EVA generate/status route.
- Automation values remain unconfirmed until the existing staff review rules
  accept them, and automation still cannot confirm findings, approve reports or
  dispatch externally.
- Future EVA API or direct estimating-system integrations require their own
  current product and architecture authority; this decision adds no placeholder
  route for them.

## Links

- [FRD-07](../frd/frd-07-eva-and-external-engineering-handoff.md)
- [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md)
- [FRD-11](../frd/frd-11-reports-correspondence-and-reviewed-proposals.md)
- [ADR-0021](0021-automation-actor-direct-write-assessment-contract.md)
- [ADR-0026](0026-enable-automation-mcp-by-explicit-deployment-configuration.md)
- [ADR-0027](0027-authorization-code-for-external-mcp-connectors.md)

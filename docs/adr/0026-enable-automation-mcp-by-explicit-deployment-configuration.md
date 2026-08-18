---
id: ADR-0026
status: accepted
date: 2026-08-18
supersedes: []
superseded_by: []
related_capabilities: [MCP-01, MCP-02, MCP-03, MCP-04, MCP-06]
related_frd: [frd-10, frd-11]
tags: [mcp, automation, deployment]
---

# ADR-0026: Enable Automation MCP by explicit deployment configuration

## Status

Accepted.

## Context

The Automation MCP ingress is already an HTTPS Web boundary with OAuth
client-credentials authentication, permanent attributable history, per-area
scopes, rate limiting, and an Administrator kill switch. ADR-0021 made its
composition gate DevelopmentOffline-only. A production configuration attempt
on 2026-08-18 proved that the restriction is implemented as a startup guard:
the configured revision did not become ready and was rolled back closed.

The operator has clarified that automation-client selection and tool access are
controlled outside Pegasus by the connecting MCP client. Pegasus must expose
the already-approved endpoint and tools, retain its normal authentication,
audit, and fail-closed safeguards, and allow an explicitly approved deployment
to activate the composition gate.

## Decision

This record amends ADR-0021 decision 1 and its final consequence only: the
DevelopmentOffline-only composition gate for `Features:AutomationMcp` no
longer applies. Every other clause of ADR-0021 — the direct-write inventory,
scopes, actor rights, permanent history, kill switch, and the Send to AI
transport slice — stands unchanged.

`Features:AutomationMcp` remains off by default but may compose the existing
Automation MCP ingress in a Production runtime profile when all required
Automation MCP configuration is present. The feature flag remains the
deployment kill switch; removing it keeps the OAuth and MCP routes absent.

The production deployment uses the existing HTTPS Container App ingress and a
Key Vault-backed OAuth client secret. Pegasus continues to validate tokens and
the existing per-area scopes, enforce the Administrator kill switch and rate
limit, invoke only the implemented Core use cases, and record permanent
history. It does not create a new Pegasus-side tool-permission policy or take
ownership of the external MCP client's client-selection or tool-access policy.

## Consequences

- The `DevelopmentOffline` limitation for Automation MCP in ADR-0021 is
  amended; its direct-write inventory and all other safeguards remain in
  force under ADR-0021 itself.
- An approved production configuration can enable the existing ingress without
  adding a new transport, deployment unit, or business-policy implementation.
- Provisioning renders `Features__AutomationMcp=true` on the Web app whenever
  the Automation MCP secret URI is supplied; the deployment-level switch off is
  a provision without those settings, and the immediate switch off is the
  Administrator kill switch (client registration disabled).
- A missing or invalid required setting still fails startup rather than
  exposing a partially configured endpoint.

## Links

- [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md)
- [ADR-0021](0021-automation-actor-direct-write-assessment-contract.md)

# Interfaces and automation

## Outcome

Provider APIs, staff MCP, background processing, and individually approved AI
assistance expose the same Core-owned policies as the staff Web application
without creating alternate business engines or premature integrations.

## Settled requirements

- Provider API credentials and access are principal-scoped, revocable, and
  limited to accepted submission/status/reference contracts.
- Staff MCP uses the same authenticated authorization and Core use cases as the
  Web application; tools do not own policy or credentials.
- Background automation records versioned decisions, stable identity,
  idempotency, correction paths, and attributable failures.
- Rule-based deterministic behavior is preferred where it meets the need. AI
  assistance activates only for a named outcome with approved data/model/vendor
  boundaries, evaluation cohort, review, rollback, and cost evidence.
- No dormant model, vendor, endpoint, tool, or alternate workflow is built for
  a deferred capability.

The stable `API-*`, `MCP-*`, and `AI-*` outcomes and allocations live in the
[capability inventory](../capabilities.md).

## Current state and activation

There is no provider API, staff MCP product caller, production Worker trigger,
or AI caller. Activation requires a decision-complete change record and the
actual entry point, caller, permission, failure, recovery, and acceptance proof.

The former [operator-assistance plan](../../history/plans/later-delivery/ai-and-automation/operator-assistance.md)
and interface/integration packs remain historical evidence only.

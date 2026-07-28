# Interfaces and automation

## Outcome

Provider APIs, staff MCP, background processing and individually approved AI
assistance expose the same Core-owned policies as the staff Web application
without creating alternate business engines or premature integrations.

## Settled requirements

- Provider API credentials and access are principal-scoped, revocable and
  limited to accepted submission/status/reference contracts.
- Staff MCP uses authenticated authorization and the same Core use cases as the
  Web application; tools do not own policy or credentials.
- Background automation records versioned decisions, stable identity,
  idempotency, correction paths and attributable failures.
- Deterministic behavior is preferred where it meets the need. AI assistance
  activates only for a named capability with approved evidence, review,
  rollback, transport/security and cost proof.
- No dormant model, vendor, endpoint, tool or alternate workflow is built for a
  deferred capability.

## AI ownership and proposal contract

`AI-07` remains the post-EVA-replacement staff-selected `AI Assessor` assignment
outcome. It does not own a button, queue, model or transport.

`AI-08` owns a case-grounded query-response proposal in approved Collision
Engineers house style/letterhead. A named Engineer reviews, edits if needed and
approves it before sending.

`AI-09` defines vendor-neutral **Send to AI**:

1. a staff action creates one durable, idempotent, capability-scoped work
   request bound to the immutable case/revision and selected evidence manifest;
2. a separately activated scoped worker may lease it through MCP and return
   only a proposal, evidence or visible failure;
3. duplicate, expired, cancelled or stale-case work cannot mutate accepted case
   data; and
4. a named Engineer accepts, amends or rejects the proposal through the owning
   Core use case.

The request/proposal/review contract is `Later`/unallocated. Collision AI Centre
owns future agent harnesses, runtime evaluations, retrieval, model selection,
skills and separately governed fine-tuning. Pegasus Core remains the sole owner
of business policy and accepted case truth.

## Transport activation experiment

A later activation must compare:

- attended Claude Code, Cowork or Desktop chat consuming scoped MCP work;
- supported scheduled Claude Desktop automation polling the queue; and
- a future Collision AI Centre harness polling the queue.

The experiment proves actual client/tool support, OAuth and actor identity,
attended versus unattended operation, lease/cancel/recovery, proposal return
and cost. An incompatible Claude surface is discarded rather than weakening the
queue contract. No direct Anthropic or other model API is part of the current
scope or an assumed fallback.

## Current state and activation

There is no provider API, staff MCP product caller, production Worker trigger,
AI queue, AI worker or model caller. Imported AI source and the allocation of
`MCP-01`–`MCP-04` do not activate `AI-09`. Activation requires the actual entry
point, caller, permission, failure, recovery and acceptance proof.

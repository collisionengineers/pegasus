---
id: ADR-0035
status: accepted
date: 2026-08-28
supersedes: []
superseded_by: []
related_capabilities: [AI-10, AI-09, MCP-06, MCP-01]
related_frd: [frd-10, frd-11]
tags: [ai, mcp, automation, ledger]
---

# ADR-0035: AI job ledger

## Status

Accepted, 2026-08-28. Refines ADR-0011/ADR-0031 by adding one scope and one
store to the Automation Actor boundary; it changes neither the actor identity
nor the existing tool contract. It supersedes the "shared AI usage ledger"
exclusion recorded in the AI assistance row of `docs/boundaries.md`.

## Context

The operator has directed (EPIC-011 decisions D5 and D6) that AI work is a
catalogue of named jobs — an estimate drafted to a target, an Unidentified
item's proposed destination, a drafted query reply, a scheduled pass over the
Unidentified queue — visible to staff as one list on Operations and worked by
external AI clients that Pegasus does not host. Pegasus already has two
durable work records, and neither fits.

**The external-work outbox** (`ExternalWorkItems`, `IExternalWorkStore`) is
worker-dispatched: the Pegasus Worker claims a row and runs a handler chosen
by kind. Its dispatcher throws `UnknownExternalWorkKindException` for a kind
it has no handler for, and that row poisons. An AI job has no in-process
handler by design — the work is done outside Pegasus — so every AI kind would
be an unknown kind.

**The Send to AI hand-off record** (`AiWorkRequests`, AI-09) is push
semantics: Pegasus posts a pointer to a channel connector, tracks one
in-flight request per case, and composes only in the DevelopmentOffline
profile. It has no claim, no lease, no kind other than the assessment pointer,
and no route by which an external scheduler could create work. It did
establish the patterns that matter here: an operation key on every mutation,
an expected version on every transition, the Administrator kill switch, and
Automation Actor attribution of every write.

## Decision

Pegasus keeps a **durable, pull-based AI job ledger** — the `AiJobs` store —
owned by `Pegasus.Core`.

1. **Pull, not push.** Pegasus never dispatches an AI job. An external AI
   client, authenticated as the Automation Actor, lists queued jobs and
   claims one; the claim is a bounded lease held by that client's name.
2. **One dedicated scope.** The ledger tools are granted only by a new
   `automation.jobs` scope with its own consent description. The existing
   scopes are unchanged; a token without `automation.jobs` cannot see the
   ledger.
3. **Kinds are a Core catalogue.** The permitted job kinds are a closed
   `Pegasus.Core` list (FRD-11 owns the catalogue). An unknown kind is refused
   at creation, not persisted and poisoned later.
4. **Creation has two callers.** Staff create jobs from the Web application;
   external schedulers create jobs through the Actor's `create` tool. Pegasus
   runs no timer for AI work (D5).
5. **Results are pointers or drafts, never applied.** A completed job points
   at a draft the client wrote through the existing attributed Actor tools,
   or carries a proposal for staff to confirm through the existing staff
   action. The ledger never mutates case, Unidentified, or correspondence
   state itself.
6. **The existing patterns are reused, not copied.** Operation-key replay,
   expected-version transitions, the ADR-0021/0026 kill switch, and
   Automation Actor attribution govern the ledger exactly as they govern
   AI-09 and the assessment toolset.

The AI-09 hand-off record and its channel transport remain as they are; this
decision adds a second, distinct record and does not migrate or retire the
first.

## Consequences

- A lease that expires returns the job to `Queued` for another claim; the
  expired claim is recorded, not erased.
- The Administrator kill switch refuses claims and progress for the whole
  ledger; queued jobs wait, taken jobs expire back to `Queued` when their
  lease ends.
- Nothing an AI client returns reaches accepted case truth without a staff
  act: an estimate stays a draft until an Engineer accepts it, a proposed
  Unidentified destination is applied through the existing resolution
  action, a drafted reply is text offered to the composer.
- Operations and Administration read one ledger for the AI Job List and the
  active/failed counts; there is no second AI usage record to reconcile.
- `docs/boundaries.md`'s exclusion of a shared AI usage ledger no longer
  holds; the row is amended under UIIMP-007 to cite this record.
- Wave-3 implementation carries the `AiJobs` migration, its grants, the Core
  ledger, and the Actor tools together; a Web-only or tool-only slice is not
  the decided shape.

## Options considered

**Add AI kinds to `ExternalWorkItems`.** One store fewer, but the outbox is a
Worker dispatch queue: every AI kind would need an in-process handler or would
poison, and a lease held by an external client is not a Worker claim.

**Extend `AiWorkRequests` with kinds and claims.** Closest in intent, but it
would turn a push record with one request per case into a pull queue while
keeping a DevelopmentOffline-only transport; the two lifecycles do not share
a state machine, and the channel connector would remain a required dependency
for work that no longer uses it.

**No Pegasus ledger — clients keep their own.** Rejected: staff need one
visible list with attribution, expiry and a kill switch, and the operator
withdrew the earlier exclusion for exactly that reason.

## Links

- [FRD-11 — AI Job List](../frd/frd-11-reports-correspondence-and-reviewed-proposals.md#ai-job-list)
- [FRD-10 — AI job and estimate tools](../frd/frd-10-mcp-automation-and-actor-boundary.md#ai-job-and-estimate-tools)
- [ADR-0011](0011-restrict-mcp-to-automation-actor.md)
- [ADR-0026](0026-enable-automation-mcp-by-explicit-deployment-configuration.md)
- [ADR-0027](0027-authorization-code-for-external-mcp-connectors.md)
- [ADR-0031](0031-automation-actor-contract-without-eva-export-tools.md)
- [Boundaries — AI assistance](../boundaries.md)

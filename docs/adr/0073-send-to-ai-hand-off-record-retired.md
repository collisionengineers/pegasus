---
id: ADR-0073
status: accepted
date: 2026-10-15
supersedes: [ADR-0035]
superseded_by: []
related_capabilities: [AI-10, AI-09]
related_frd: [frd-27]
tags: [ai, ledger, send-to-ai]
---

# ADR-0073: The Send to AI hand-off record is retired

## Status

Accepted on 15 October 2026 by operator decision: "New ADR superseding
ADR-0035's AI-09 clause; FRD-27:3 and the ADR index corrected; ADR bodies stay
as records".

This record replaces, in part:

- [ADR-0035](0035-ai-job-ledger.md): the closing paragraph of its Decision,
  which says the AI-09 hand-off record and its channel transport "remain as
  they are" and that the decision "does not migrate or retire the first".
  Decisions 1 to 6, the Context that describes the hand-off record, the
  Consequences and the Options considered stand, and its body stays as filed.

[FRD-27](../frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md)
owns the current AI job behaviour.

## Context

[ADR-0035](0035-ai-job-ledger.md) added the AI job ledger beside the Send to
AI hand-off record (`AiWorkRequests`, AI-09) and left the hand-off record and
its channel transport as they were. The hand-off is gone:

- [Capabilities](../capabilities.md#retired-identities) retires AI-09 on
  28 September 2026 by operator decision: "the AI Job List (`AI-10`) is the
  only route".
- The migration `20260929090000_RetireUnusedTables` drops the `AiWorkRequests`
  table and the connector columns of `SendToAiControl`; its comment records
  that "the AI Job List is the only Send to AI route" (#843).
- [Current architecture](../current-architecture.md) says the AiJobs pull
  ledger is the one AI integration boundary and the push hand-off to a channel
  is retired.

## Decision

The AI job ledger is the only Send to AI route. The Send to AI hand-off record
(`AiWorkRequests`, AI-09) and its channel transport are retired. This replaces
the closing paragraph of ADR-0035's Decision and adds no other rule.

## Consequences

- ADR-0035 lists this record in `superseded_by` and its index row names the
  AI-09 hand-off record as the replaced scope.
- FRD-27 no longer lists AI-09 among its owner capabilities.

## Links

- [FRD-27 — AI Job List](../frd/frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#ai-job-list)
- [ADR-0035](0035-ai-job-ledger.md)
- [Capabilities — Retired identities](../capabilities.md#retired-identities)

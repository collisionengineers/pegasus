---
id: ADR-0032
status: superseded
date: 2026-08-25
supersedes: []
superseded_by: [ADR-0033]
related_capabilities: [INT-33]
related_frd: [FRD-02, FRD-08]
tags: [intake, email, queues, reliability]
---
# ADR-0032: Near-real-time durable intake triggering

- Status: Superseded by [ADR-0033](0033-warm-unified-work-queue-for-five-second-intake.md). This historical decision partially superseded ADR-0002's polling and timer-first outbox triggering only.
- Date: 2026-08-25
- Owners: Alex and the Pegasus development team

## Context

Pegasus already commits original source custody and a stable processing-work
record before acknowledging intake, then lets the Worker perform identification,
classification, extraction, allocation, and case creation. The normal path was
scheduled by short mailbox and SQL-dispatch polling intervals. Production
measurement showed that shortening those intervals materially increased
Function execution cost without removing the visible delay.

The durable records, identifier-only queues, idempotent claims, and single Core
policy owner remain correct. The scheduling mechanism is the obsolete boundary.

## Decision

Use immediate best-effort publication after the durable intake or downstream
work commit as the normal queue trigger. Retain a slower database reconciliation
sweep solely to recover work whose publication or delivery was missed.

Use Microsoft Graph basic change notifications to wake approved-mailbox delta
processing. The existing Web deployment validates and enqueues the wake
identifier; it does not read or process mail. The Worker remains the sole owner
of mailbox cursors and all intake processing. Subscription state uses the
existing SQL deployment and its client-state secret uses protected application
configuration.

Keep consumption scale-to-zero as the default. Always-ready capacity is an
operational choice only after deployed measurement proves callback cold start is
the remaining reason the functional latency target cannot be met.

This decision partially supersedes ADR-0002 only where it selected polling as
the normal mailbox trigger and a timer as the normal SQL-outbox publisher.
ADR-0002's modular-monolith, hosting, durability, queue, Worker, and ownership
decisions remain accepted.

## Consequences

- Ordinary work no longer waits for a polling interval after its durable commit.
- A callback stays short and safe even when document processing is slow.
- Duplicate notification and queue delivery remain normal idempotent conditions.
- Slow reconciliation and mailbox polling remain required recovery mechanisms,
  not competing business implementations.
- The Web deployment needs only the least-privilege ability to publish validated
  identifiers; mailbox read authority remains with the Worker.
- Subscription maintenance and lifecycle recovery become deployed operational
  responsibilities.

## Options considered

- **Keep shortening polling:** rejected because measured cost rose without a
  corresponding latency improvement.
- **Combine callback, mailbox read, extraction, and case creation:** rejected
  because it would hold an external request across untrusted work and duplicate
  the Worker's durable processing boundary.
- **Keep an always-ready Function immediately:** rejected pending evidence that
  cold start, rather than the existing timer and source-reader delays, prevents
  the target.

## Links

- [FRD-02](../frd/frd-02-intake-and-source-identity.md)
- [FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md)
- [ADR-0002](0002-dotnet-modular-monolith-on-azure.md)

---
id: ADR-0042
status: accepted
date: 2026-09-08
supersedes: [ADR-0036]
superseded_by: []
related_capabilities: []
related_frd: [FRD-08]
tags: [security, persistence]
---
# ADR-0042: Staff-send operation journal and Sent evidence

## Status

Accepted contract recorded from current FRD-08; this document replaces
only the conflicting mechanism below, not the unrelated clauses of ADR-0036.
This proposed patch does not itself constitute a deployment or new authorization.

## Context

The earlier decision and current functional contract describe different
persistence behavior. Keep one current technical explanation and preserve the
earlier rationale as history.

## Decision

Persist staff-send operation state for draft, attachment/upload and submission
recovery in the existing application persistence boundary. The operation owns
attempts and recovery correlation; the retained approved-mailbox Sent item owns
evidence of sending. Provider acceptance is Submitted until that matching
evidence is observed. A journal entry is not a second authoritative Sent record.

Apply the existing FRD-08 idempotency, unknown-outcome and staff-initiation
contract; this clarification adds no automatic retry or autonomous chaser.

## Consequences

The earlier blanket no-new-record wording does not prohibit required durable
operation state. All outward actions retain the existing approved identity,
scope and human initiation constraints. No new store, process or dispatch
authority is introduced by documenting this distinction.

## Links

- [FRD-08](../frd/frd-08-email-mailbox-and-background-processing.md)
- [ADR-0036](0036-outbound-mail-via-approved-mailbox.md)

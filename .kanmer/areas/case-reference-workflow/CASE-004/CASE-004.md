---
id: CASE-004
type: ticket
title: Deliver case notes as a separate future capability
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - design
  - future-capability
  - case-notes
links:
  - PLAT-001
  - CASE-002
  - CASE-017
docs_todo: true
archived: true
created: '2026-08-19T10:59:18.139Z'
updated: '2026-08-25T06:38:23.512Z'
---

## Disposition

Superseded by [[CASE-017]], which delivered the Notes timeline for operator-authored and system entries and is recorded as Done in production. The pre-delivery statements below are retained only as historical context.

## What

Deliver case notes as a real future capability, separate from post-report queries and system-generated case history.

## Why

The Collision Engineers operator confirmed on 2026-08-19 that the inactive Case Details notes surface represents intended product scope. Notes are staff-authored internal case records; they are not incoming queries raised to Engineers and must not be merged with the query workflow owned by [[CASE-002]].

## Current boundary

The existing inactive notes interface remains non-persistent until this capability's authority and behavior are accepted. Future scope does not authorize placeholder notes, fabricated content, or a hidden write path.

## Scope

- Allocate a capability ID and canonical PRD/FRD owner.
- Define who may create and view a note, required/optional fields, note types if any, actor/time attribution, and ordering.
- Decide whether notes are append-only or may be corrected; any correction must preserve the original, reason, actor, time, and superseding version.
- Define visibility boundaries and whether a note may be marked for any later external correspondence without treating internal text as automatically sendable.
- Keep notes distinct from immutable system history, email correspondence, post-report queries, tasks, and report amendments.
- Wire the existing Case Details surface through one Core-owned policy and durable store.
- Expose every accepted user-facing note operation through MCP with equivalent authorization, versioning, attribution, confirmation, and recovery behavior.
- Provide search/filter behavior only if explicitly accepted; do not add speculative taxonomy.

## Verification

- [ ] Capability ID and governing PRD/FRD behavior are accepted.
- [ ] Notes are distinct from history, correspondence, queries, tasks, and addenda.
- [ ] Authorization, attribution, ordering, correction, and audit-history rules are proven.
- [ ] UI and MCP use the same Core contract and fail closed.
- [ ] No inactive or placeholder surface writes data before activation.

## Decision record

Operator decision, 2026-08-19: case notes are a real future capability.


## Outcome

Archived on 2026-08-25 because the intended capability is now owned by the delivered [[CASE-017]].

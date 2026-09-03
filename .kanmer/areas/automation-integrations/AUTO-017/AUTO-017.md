---
id: AUTO-017
type: ticket
title: Support Provider API updates to an existing Case
status: backlog
area: automation-integrations
order: 50
assignee: ''
profile: feature
labels:
  - provider-api
  - deferred
  - API-01
groups:
  - EPIC-011
links:
  - TICK-058
  - TICK-060
refs:
  - docs/frd/frd-09-provider-and-intermediary-routes.md
archived: false
created: '2026-09-02T18:34:39.767Z'
updated: '2026-09-03T15:15:26.964Z'
---

## What

Define and implement an explicitly authorised Provider API operation for adding a later provider instruction or material to an existing instructed Case.

## Why

API-01 is create-only. When a new Provider API submission definitively matches an existing Case, the current contract rejects it rather than mutating that Case or allocating a duplicate. Updating existing Cases through the API needs its own authorization, request contract, concurrency rules, attribution, validation, and recovery evidence.

## Approach

- Preserve API-01 as create-only and consume no existing-Case mutation path implicitly.
- Settle the allowed update operations, immutable-field boundaries, authorization, idempotency, optimistic concurrency, failure behavior, and permanent history before implementation.
- Reuse the existing Principal credential boundary and Core Case mutation policies where they fit; introduce no parallel policy owner.

## Verification

- A Principal can update only an authorised existing Case belonging to that Principal.
- Principal and allocated Case/PO identity remain immutable.
- Replay is idempotent, stale/conflicting mutation fails closed, and permanent attribution is retained.
- API-01 matching rejection remains distinct from this explicit update contract.

## Outcome

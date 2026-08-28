---
id: KANMER-005
type: ticket
title: Enforce exclusive editing leases between staff and Automation Actors
status: preparing
area: automation-integrations
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-08-28T09:54:44.547Z'
labels:
  - bug
  - lease
  - concurrency
links: []
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0011-restrict-mcp-to-automation-actor.md
  - docs/adr/0031-automation-actor-contract-without-eva-export-tools.md
archived: false
created: '2026-08-18T15:17:05.786Z'
updated: '2026-08-28T10:13:44.130Z'
---

## What

Make editing leases mutually exclusive across human and Automation Actor sessions. While an Automation Actor holds an active lease, the GUI must prevent a staff user from claiming that lease or editing the protected item; the same rule must apply in the opposite direction.

## Why

Observed failure: an Automation Actor held the editing lease, but a staff user could enter edit mode and take the lease. The actor's edits still succeeded, yet its attempt to end the lease was rejected because the staff user had become the recorded holder. This permits concurrent writes and leaves lease ownership inconsistent with the actor performing the edit.

## Approach

- Enforce lease ownership atomically at claim and write boundaries for every actor type.
- Reject a competing claim while an unexpired lease exists, without replacing its owner.
- Ensure only the current holder can edit, renew, or release the lease.
- Cover the human-holds/AI-competes and AI-holds/human-competes cases.

## Verification

- [ ] With an Automation Actor lease active, a staff user cannot claim the lease or edit the item.
- [ ] With a staff lease active, an Automation Actor cannot claim the lease or edit the item.
- [ ] The active holder can save edits and release its lease successfully after a competing claim attempt.
- [ ] Lease ownership remains unchanged after rejected claim or write attempts.

## Outcome

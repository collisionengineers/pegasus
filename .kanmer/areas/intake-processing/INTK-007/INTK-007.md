---
id: INTK-007
type: ticket
title: Replace Needs sorting with referenced Unidentified work
status: review
area: intake-processing
assignee: Codex
profile: feature
stageEntered:
  preparing: '2026-08-19T09:52:38.657Z'
  review: '2026-08-19T12:05:55.914Z'
taken_at: '2026-08-19T11:47:27.623Z'
branch: intk-007-unidentified-intake
worktree: .worktrees/intk-007
labels:
  - unidentified
  - queues
  - intake
  - cross-cutting
  - reference-allocation
groups:
  - EPIC-007
links:
  - TICK-044
  - TICK-057
  - TICK-064
  - INTK-005
  - INTK-006
  - PLAT-003
blocks:
  - TICK-057
  - TICK-064
  - PLAT-003
refs:
  - docs/operator-notes.md
  - docs/prd/pegasus-product.md
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-03-triage.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/frd/frd-09-provider-and-intermediary-routes.md
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/frd/frd-12-operator-experience.md
  - docs/capabilities.md
  - docs/current-architecture.md
  - docs/runbook.md
  - docs/design/README.md
commits:
  - abd8a923
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/424'
archived: false
created: '2026-08-19T09:46:52.709Z'
updated: '2026-08-19T12:05:55.914Z'
---

## What

Replace the broad `Needs sorting` destination with an operator-facing **Unidentified** queue for received source material whose identity, meaning, ownership, or operational destination cannot be established.

Every Unidentified item receives an immutable internal reference allocated sequentially as `U1`, `U2`, `U3`, and so on. This is a tracking reference only: it is never a Case/PO, Audit reference, principal identity, or evidence that case-allocation gates passed.

## Why

Unreadable or unclassifiable emails, documents, images, attachments, and other received material need an honest destination and a stable handle for staff follow-up. The existing `Needs sorting` label is too broad and does not give an item its own durable internal reference or clearly state why Pegasus could not identify it.

This is a wide replacement of `Needs sorting`, not a mail-only label change. It affects intake decisions, queues, counts, filters, status pages, automation tools, history, tests, and documentation wherever that term currently represents unidentified material.

## Required behaviour

- Allocate each newly Unidentified source or inseparable source group the next never-reused `U<n>` reference.
- Preserve original files, filenames, source identities, receipt identities, and group membership.
- Record a required reason using one canonical taxonomy plus safe explanatory detail, including unreadable/corrupt/unsupported content, no usable identification, conflicting identification, ambiguous ownership/destination, and technical processing failure where custody succeeded.
- Show the U-reference and reason in Queues, receipt/upload status, detail pages, search, history, and relevant automation queries.
- Staff can resolve an Unidentified item/group into the correct supported destination without changing or reusing its U-reference; history permanently records the resolution.
- Case/PO and Audit allocation remain fail closed and use their existing independent sequences.
- Preserve the settled distinction between Unidentified, Triage, Blocked intake, and Audit. Unidentified replaces only the old broad `Needs sorting` meaning.
- Reconcile specialist flows such as grouped vehicle images: evidence that qualifies for the mandatory existing-case association or Image-Only case outcome must not be stranded merely because one member lacks readable identity.

## Verification

- U-references allocate atomically, monotonically, and without reuse under concurrency, retry, replay, failure, or later resolution; the format expands naturally beyond `U99999`.
- All existing `Needs sorting` producers and consumers are inventoried and either migrated to Unidentified or explicitly mapped to another settled destination.
- Every Unidentified item/group has durable custody, a U-reference, a reason, visible next action, and permanent history.
- No U-reference is accepted where a Case/PO, Audit reference, or principal identity is required.
- Existing records migrate without loss, duplication, reference reuse, or silent semantic change.
- UI and API/MCP vocabulary contain no stale operator-facing `Needs sorting` wording after the replacement.

## Governing-document impact

This changes settled product vocabulary and behaviour currently spread across operator truth and several FRDs. Update the protected business/governing documentation with explicit operator confirmation before implementation; do not encode the replacement only in UI strings.

## Outcome

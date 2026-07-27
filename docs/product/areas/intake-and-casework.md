# Intake and casework

## Outcome

Authorized intake becomes a reviewable source-backed draft and, only after all
gates pass, a case with an immutable principal and reference. Staff then manage
the complete QDOS Inspection, standalone Audit, Inspection + Audit, Triage,
work, report-evidence, reopening, matching, and terminal-history paths.

## Settled requirements

- Email, document, image-led, and manual receipt retain stable source identity,
  origin, occurrences, and original custody.
- Processing, source limits, principal identity, mandatory fields, and
  standalone Audit evidence fail closed before case creation/reference
  allocation.
- `Needs sorting`, `Blocked intake`, and pre-case `Triage` remain distinct.
- A principal/reference never changes or returns to the pool after allocation.
  Wrong-principal work closes as `Created in error` and links a replacement.
- Cases are never deleted. Reopening needs a reason and normal destination
  gates; `Created in error` never reopens.
- Matching/linking is evidence-backed and reversible with a reason while both
  source origins remain permanently attributable.
- Direct-provider and intermediary email routes have separate rules. Either can
  identify the same provider, but the applicable route owns its provider,
  instruction-type, and case-association evidence and precedence.
- A staff-forwarded message retains the Collision Engineers forward as
  provenance while the proved original sender drives route identification.
- Concurrent editing cannot silently overwrite newer case data.

The stable `EVAL-*`, `INT-*`, `MAIL-*`, `TRI-*`, and `CASE-*` outcomes and
allocations live in the [capability inventory](../capabilities.md). Detailed
operator intake, case-type, lifecycle, address, and term authority is indexed
under [operator notes](../../operator-notes/README.md).

## Current state and activation

The only mutating caller is the Development-only manual intake Web route. It
creates a reviewable receipt/draft, not a case or reference; the Worker has no
trigger. Each next slice requires one change record with real callers, policy
owner, failure/recovery behavior, evidence cohort where applicable, and exact
product/architecture/operations updates.

The former [V1 casework pack](../../history/plans/remainder-delivery/casework/)
and [mailbox dossier](../../history/plans/mailbox-categorisation-and-email-matching/)
are historical planning evidence, not active plans.

# FRD-12: Operator experience
> Owner capabilities: UI · Migrated from docs/requirements.md · UI behaviour: docs/design.md

## Operator experience

The selected alpha direction is Operations-first. The UI must provide:

- an authenticated office-wide dashboard with Europe/London day boundaries and
  Monday-to-Monday weeks;
- actionable receiving, requests, Triage, case, query, and exception queues;
- intake-evidence filters with exact options `All`, `Instructions`, and
  `Images`;
- clear counts that link to their exact filtered work and do not render stale
  zero placeholders;
- list/detail journeys for intake, source evidence, Triage, cases, documents,
  history, and exports;
- supporting-detail navigation from Intake or Case detail that neither commits nor discards the current form and returns to the same detail context, evidence selection, position, and unsaved edits;
- administration for authorised accounts, roles, access, organisations,
  principals, configuration, and mailboxes;
- exact state labels mapped to Core decisions;
- loading, empty, current, stale, unavailable, partial, failed, validation,
  conflict, and access-denied states;
- keyboard, pointer, screen-reader, 200% zoom, forced-colour, and reduced-motion
  support;
- responsive use without hiding required evidence or actions.

Every actionable search result is a full-row keyboard-focusable link or button with visible action affordance. At constrained desktop width, a long Case/PO or Image Intake Reference moves to a labelled second line instead of overlapping the received timestamp. Inbox and intake rows always show received date above received time, and show the precise processing outcome—such as `Case created`, `Image intake registered`, `Associated with Case`, `Needs sorting`, or `Blocked intake`—rather than a generic `New`. One semantic action or state has one consistent icon across Pegasus; no decorative or generated replacement icon is used.

### Dashboard freshness and reconciliation

Every count and query exposes its last successful update time and current
refresh state. `0`, loading, current, stale-with-last-good-time, partial,
unavailable, and failed are distinct outcomes. A refresh never replaces a
last-good value with a false zero, merges partial data into an apparently
complete result, or implies that an external action succeeded.

Manual refresh reruns the same exact filtered query; it does not change policy
or create a business transition. Its caller, start/end time, sources, and
success/partial/failure result remain auditable in content-safe telemetry.
Reconciliation that accepts, rejects, links, or changes an external business
fact instead enters permanent business history with the responsible actor,
source/version, before/after values, time, and reason where required.

`New cases today` counts every instructed Case created in the current Europe/London calendar day, including a Case later closed that day. It excludes pre-Case Image intakes, Triage, `Needs sorting`, and `Blocked intake`. It is separate from `Due today`, `Sent to Engineer`, and `Reports sent`.

`Due by` and overdue/chaser work remain a separate operational view from `New cases today`. The case list and persistent case identity area expose due/overdue state, while the case workspace keeps the missing-material reason, next chase, last recorded outcome, and next permitted action together. Triage has no due/chaser presentation.

The UI never infers state from colour alone, never uses decorative glyphs as
unlabeled controls, and never presents draft, queued, attempted, allocated, or
configured work as completed, delivered, deployed, or accepted.

The durable interaction, visual, component, and source/runtime rules are owned
by [design](../design.md).

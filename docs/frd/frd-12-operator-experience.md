# FRD-12: Operator experience
> Owner capabilities: UI · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

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

Every actionable search result is a full-row keyboard-focusable link or button with visible action affordance. At constrained desktop width, a long Case/PO, Image Intake Reference, or U-reference moves to a labelled second line instead of overlapping the received timestamp. Inbox and intake rows always show received date above received time, and show the precise processing outcome—such as `Case created`, `Image intake registered`, `Associated with Case`, `Unidentified`, or `Blocked intake`—rather than a generic `New`. One semantic action or state has one consistent icon across Pegasus; no decorative or generated replacement icon is used.

### Upload

Selected files render one row per file — name, size, and a per-file state
that is a spinner while the submission is in flight and a tick once the
response confirms the file is durably stored; a failed file states its
failure on the same row. Every row enters the in-flight state together, since
a single submission stores the whole batch and no finer per-file signal
exists during it — no row is ticked ahead of what the response actually
proved. No mechanics narration ("receipt", "submission group", or similar
internal vocabulary) appears on the Upload or status surfaces.

Once a file's processing resolves, the status surface shows a confirmation
outcome rather than a passive label: what already happened automatically
(reported, with a link to open it and, where relevant, the existing reversal
path — never re-offered as a choice), or the staff decision that is
genuinely open (attach to a possible match with a free choice of
destination, or create a case from what was uploaded). The exact decision
table is owned by [FRD-02](frd-02-intake-and-source-identity.md#upload-confirmation-surface).
A grouped upload shows this per file; members of the same group can resolve
independently and are never collapsed into one group-wide outcome.

### Queues: tabs and filters

`/Triage` (nav label "Queues") is one page with five tabs, each carrying its
own count: Not ready, Review, Held, Triage, and Unidentified. There is no
separate top-level Unidentified navigation entry; `/Unidentified` is a
permanent redirect to the Unidentified tab, kept for existing links and
bookmarks rather than left dead.

The Not ready tab filters by case origin — `All`, `Instruction-initiated`, or
`Image-initiated` — the two origins settled for the Image-initiated Case
lifecycle: a formal instructed Case, or an ImageIntake-backed projection still
awaiting one. Each origin's rows keep their own shape (a Case row carries
Registration/Claimant/Principal/Received; an Image-initiated row carries its
VRM reference, registration, lifecycle status, and received date) rather than
being forced into one column set.

The Unidentified tab filters by media kind — `All`, `Images`, or `E-mails` —
derived from the underlying retained receipt's source channel and content
type, not a separate stored field. Every row states, in one line, what is
going on: the U-reference, the kind, an operator-meaningful handle (the
original filename, or the e-mail subject and sender — never an internal
identifier), the received date and time, and the canonical reason. The row
links to the item's detail page and nothing else.

Detail for an open item shows the same kind/received/reason facts, the
retained file or message it concerns by its operator-meaningful handle, one
link to the underlying retained material, chronological history, and the
resolution form. Exact U-reference search returns both open and resolved
items as a distinct result type and never treats U<n> as a Case, Audit, or
Image Intake reference. Resolution is staff-authorised, antiforgery
protected, version-checked, idempotent by operation key, and requires a
supported destination and reason. A stale version is a non-destructive
conflict; a replay shows the original result. The permanent U-reference and
origin remain visible after resolution.

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

`New cases today` counts every instructed Case created in the current Europe/London calendar day, including a Case later closed that day. It excludes Image-initiated Cases, Triage, Unidentified, and `Blocked intake`. The Unidentified count is the exact count of open Unidentified items and links to that queue. These are separate from `Due today`, `Sent to Engineer`, and `Reports sent`.

`Due by` and overdue/chaser work remain a separate operational view from `New cases today`. The case list and persistent case identity area expose due/overdue state, while the case workspace keeps the missing-material reason, next chase, last recorded outcome, and next permitted action together. Triage has no due/chaser presentation.

The UI never infers state from colour alone, never uses decorative glyphs as
unlabeled controls, and never presents draft, queued, attempted, allocated, or
configured work as completed, delivered, deployed, or accepted.

Image-initiated Cases are searchable using their VRM reference or registration
and use the named states Awaiting instruction, Merged into Instruction-initiated
Case, and Staff-closed. Details show preserved filenames/group evidence, custody,
and chronological merge/closure history. Staff closure is a reasoned action and
terminal records are read-only; it is not a generic Close control.

Retained vehicle images are viewable in Pegasus (CASE-006): the
Image-initiated Case page and a case's evidence view render each record's
registered images as a gallery of lazy-loaded thumbnail previews that expand
to the full-size image when activated, with the original filename as the
accessible name. Images are served only by an authorised staff endpoint that
returns the stored image media type inline; material that is not a true image
media type is never rendered inline and stays on the forced-download route.

The durable interaction, visual, component, and source/runtime rules are owned
by [design](../design/README.md).

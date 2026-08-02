# UI specification

Status: **Planned `0.1.0-alpha.1` specification with Operations-first selected for the shell and landing strategy. Detailed raster styling remains subject to this specification and the design system.**

## Shared shell and hierarchy

1. Authenticated identity/role, navigation and sign out.
2. Surface title, the exact queue/filter, freshness and a safe primary action.
3. Operational table, workbench or record.
4. Named workflow/evidence/lease/exception state and consequential action.
5. Provenance, external identity, permanent business history and limitation.

The Planned `0.1.0-alpha.1` routes are Operations, Intake, Triage, Cases and authorised Administration. Each comparison direction uses the same focused-flow set. Production email allocated `Next / 0.3.0` appears only after its gates; every deferred `Next` or `Later` capability carries its exact target in the [capability inventory](../../docs/capabilities.md#capabilities). Deferred capabilities have no alpha placeholder route or control; [traceability](traceability-matrix.md) mirrors those allocations.

The Development/local email evaluator is separately owned and has no QDOS-alpha
route, navigation, control, `unchecked`/`checked` workbench, review-report
mechanic, or UI acceptance checkpoint. This does not remove the shared mail
policy, production-intake surfaces, Graph replay/live adapters, or the genuine
evidence required to activate them.

## Contracts

| Component | Required contract |
|---|---|
| Shell/access | Sign-in and disabled/stale-role/denied outcomes; permitted-route visibility plus server authorisation. |
| Metric/queue | Label, value or unavailable state, last-good time, current refresh state, and exact destination filter. `0`, loading, current, stale, partial, unavailable, and failed remain distinct. Operations includes exact `Blocked intake`, Due today, New cases today, and day/week Sent to Engineer and Reports sent. |
| Inbox/intake row | Received date above time; exact processing outcome rather than generic `New`; long Case/PO or Image Intake Reference moves to a labelled second line at constrained desktop width and never overlaps the timestamp. |
| Intake workbench | Persistent source identity; `All`/`Instructions`/`Images` evidence filter; evidence/candidate; fact versus suggestion versus confirmed value; provenance/missing/conflict; acceptance path and no-case failure consequence. |
| Search result | One full-row keyboard-focusable link or button with visible action affordance; all result text contributes to its accessible name without obscuring its identity fields. |
| Field provenance | Every editable or source-derived Case datum shows its current origin marker. Direct values identify staff entry, extraction, AI, provider API, or external vehicle/estimate origin; derived values identify accepted inputs and calculation. Origin and status remain distinct. |
| Supporting detail navigation | Opening source evidence or other supporting detail preserves list/detail position, the current Intake or Case-detail context, and every unsaved edit; returning never silently discards or replaces proposed values. |
| Request-scoped in-house upload | Authenticated staff create a temporary token bound to one request/operation and server-enforced expiry. The isolated public edge exposes bound upload fields and an immediate request-local result only; expiry, revocation, cross-request isolation, limits, custody, retry, abuse, and non-disclosing failures are explicit. |
| State action | Permitted transition, prerequisite, consequence, required reason, recovery and history link; never generic Close. |
| Readiness blocker | Every unmet requirement names its exact field or material, source/provenance, reason, and permitted resolution. The UI has no opaque aggregate blocker; actions enable only from their explicit current prerequisites and no unrelated save resets state. |
| Identity header | Read-only Case/PO/principal, registration, type/secondary Audit identity, workflow state, `Due by`/overdue state, and EVA proxy limitation. |
| Due/chaser panel | Missing-material reason, next chase, last recorded channel/outcome, optional note, and next permitted action together. Copy/preparation is not sent or delivered; Triage has no such panel. |
| Inspection address | Explicit mode choice between physical vehicle/repairer address and exact `Image Based Assessment`; address fields appear only for the physical mode and never imply attendance. |
| Engineering findings | Separate Roadworthiness and Assessment controls; accepted and superseded versions; correction reason/history; reopen requirement; no inferred fee/invoice mutation. |
| Evidence/document panel | Original/source/version/logical removal/closed lock; Box/external state; issued report versions; exact Outlook evidence with separate discovery/link/sent times. |
| Evidence image preview | Loading and source-preserving enlarged-image states are explicit; opening or closing a preview preserves Case context and does not alter source, category, advisory, or report-image selection. |
| Email quick preview | At allocated mailbox-workspace activation, keyboard and pointer intent exposes an accessible preview that neither clips/obscures adjacent controls nor changes message or Case state; focus departure dismisses it. It shows sender, subject, timestamp, excerpt, classification, association and attachment names, but no mutation controls. |
| Mailbox refresh | No automatic refresh while an operator is reading or acting. Manual refresh retains active list context and an open message where available. If it leaves the active scope or becomes unavailable, keep detail visible with explicit no-longer-in-this-view state and return-to-list action. |
| Email-management workspace | Planned `Next / 0.3.0`: land on the incoming Inbox across all approved mailboxes, newest received message first. Mailbox, folder, queue and search filters narrow that view, remain visible and are preserved on return from message or Case detail; a fresh visit resets to the default all-Inboxes view. The workspace provides manual refresh, last successful update time and distinct stale/unavailable state, preserving active filters, page and open message where still available. Sent and read-only Deleted Items search are explicit folder scopes. General search includes retained message bodies, attachment filenames and searchable attachment content; unavailable content is explicit. Search remains in the current mailbox/folder scope unless explicitly broadened, and results are individual messages rather than collapsed conversations. Each result identifies body, attachment-name or attachment-content hits and names a matching attachment. Inbox and search-result lists use accessible pagination, not infinite scrolling. Inbox rows include a short body excerpt beneath sender and subject, and display read/unread state without changing it. Opened messages preserve list context and show full message, retained attachments and a chronological independently openable thread limited to retained messages in approved mailbox/folder scope. Show classification, queue, processing outcome and Case association before actions. Classification, linking and folder-move actions exist only in message detail and only for one exact message; no bulk actions. A folder move follows a saved classification as a separate confirmation to the policy-designated folder only; reclassification to a new designated folder requires another separate confirmation; failure preserves classification, remains visible and allows staff-initiated retry; success removes the message from Inbox without duplication and preserves it in destination-folder/search scope. Linking uses Case search, target summary, reason and confirmation; no `View in Outlook` action. Selecting a Case opens it in the same tab; Back restores the message detail and list context. Each Case has one newest-first chronological history of its associated received and Sent correspondence, with explicit oldest-first ordering; this workspace remains the cross-mailbox browsing and reconciliation surface. |
| Report-image selection | The Engineer report-generation section, not Case evidence, selects and orders report images. It requires a human-confirmed readable registration for the first overview, excludes reflections, and distinguishes an advisory from a human decision. |
| Lease/conflict | Holder/expiry/recovery, read-only alternative, current conflict and preserved proposed values. |
| History | Business mutation/accepted evidence/export/material business failure only; no routine views, polling, retry, lease heartbeat or telemetry. |
| Reason dialog | Named requirement/consequence, labelled reason, confirmation/cancel, initial focus, focus containment, Escape where safe and focus return to the invoking control. |
## Presentation responsibilities

Product requirements own business gates and outcomes; this specification owns
how they are presented and operated. Lists expose identity, state, freshness,
filter, provenance, and permitted action. Detail pages expose source evidence,
accepted facts, missing/conflicting values, history, leases, external status,
and reasoned transitions without duplicating Core policy. The shell and
dashboard own navigation and exact queue metrics; administration surfaces own
authorised configuration journeys; error, empty, loading, denied, stale,
partial, conflict, and unavailable states are explicit.

## Focused flows

**Intake:** source -> `All`/`Instructions`/`Images` evidence filter ->
evidence/candidate -> safe processing plus deterministic Principal and Case type
creates exactly one Case/reference. Incomplete ordinary data, images, or
applicable progression requirements yield **Not ready**; **Review** follows
only when the explicit route policy permits it. `Blocked intake` with a
required reason creates no Case/reference when an identity-critical gate fails;
fail-closed `Needs sorting` remains pre-Case. Resolve/retry re-enters the same
path and may create exactly one Case/reference only after it establishes the
identity-critical facts. Manual image/instruction link and reasoned reversal
retain original origins.

Opening evidence or supporting detail from Intake preserves the active `All`/`Instructions`/`Images` filter, selected record, scroll/list-detail position, and every unsaved candidate edit. Return restores the originating Intake or Case-detail context without reloading over proposed values.

The request-scoped in-house upload route is a distinct public edge of that
intake flow. Authenticated staff create a temporary token bound to one request,
its allowed operation, and a server-enforced expiry; staff can revoke it. The
isolated unauthenticated surface uploads only to that request and returns an
immediate structured result. Expired, revoked, cross-request, type/count/size
limit, custody, retry, and abuse outcomes reveal no case, reference, request
history, other upload, token-management function, or external account. Success
proves request-local custody only, not case creation, Box custody, EVA handoff,
report generation, or external delivery.

**Triage:** distinct inbox classification/label plus dedicated pre-case
list/detail; never a case state. Missing registration goes to `Needs sorting`;
Open/Awaiting information/Finding recorded/Completed/Cancelled; two
independently optional findings, with at least one required before Finding
recorded/Completed: Roadworthiness = Roadworthy/Unroadworthy and Assessment =
Repairable/Total loss. A case's `has Triage` is Boolean/reference-only. Triage
findings are reference-only and do not affect Case/PO/reference, workflow,
professional findings, final outcome, Engineer report, Audit suffix/allocation,
fee, invoice, or any other case decision. Exact reply-chain evidence;
reasoned pre-send replacement and post-send superseding finding/new response;
optional assignee; reasoned case link. Reopen returns to Open and preserves the
prior finding/reply. No due/chaser UI.

**Case:** read-only until an explicit edit lease. The persistent header keeps
Case/PO, principal, registration, type/secondary Audit identity, state,
`Due by`/overdue, and EVA proxy limitation visible. The work area keeps the
missing-material reason, next chase, last recorded channel/outcome, optional
note, and next action together; due/chaser work is separate from `New cases today`.
Overview, data, provenance, documents/images, vehicle/MOT, tasks/reminders,
request-scoped in-house upload token, EVA export, report evidence, and history remain
focused sections.

Inspection address is one explicit choice: physical vehicle/repairer address
with address fields, or exact `Image Based Assessment` without fabricated
address fields. Ordinary-image VRM and vehicle/MOT results show suggestion,
confirmed, unknown/no-result, stale, unavailable, and failed distinctions with
source/version/age; refresh never overwrites confirmed or last-good data.

Image readiness display is a future surface: the advisory (registration overview, damage close-up, and the applicable reflection criterion, refreshed whenever current Case images change, with no Case-state, eligibility, or chase effect) is owned by [AI-05, `Later / 1.0.0`](../../docs/capabilities.md#capabilities) and has no `0.1.0-alpha.1` surface.

Roadworthiness and Assessment are separate professional findings. A correction
shows the retained earlier version and reasoned superseding version; a closed
case requires reasoned reopen before revision. Issued report/addendum versions
and exact Sent evidence remain distinct; report sent enters post-report work
and does not close the case. A Box PDF, upload, export, or queue result is not
delivery evidence, and correction never implies a fee/invoice change.

Named actions cover Not ready, Review, Held, terminal outcomes, archive/reopen.
Held preserves the chase interval; Created in error offers only linked
replacement and never Reopen.

**Administration:** account creation/disable/access review/roles, principal
successor cutover, configuration and mailbox allowlist. No generic rules editor
or cloud/credential operation.

The complete per-scope query, mutation, Intake, Triage and Case state contract
is the [requirements state
matrix](requirements.md#complete-state-matrix); this specification does not
compress or replace it.

## Freshness and reconciliation

Every query keeps the last successful value/time visible when a later refresh
is stale, partial, unavailable, or failed. Manual refresh reruns the same
filter; it never substitutes zero, marks an external action complete, or
changes a business fact. Show start/completion feedback and a safe retry.

Routine refresh audit belongs to content-safe telemetry. When staff accept,
reject, link, or change an external fact during reconciliation, show the
source/version, prior and new value, actor, time, outcome, and required reason
in permanent history.
## UI-07 exact search and filters

Case/PO, Image Intake Reference, registration, claimant, claim number, principal, state, Engineer, received/instruction dates and range, and origin.

## Exceptions and necessary copy

Use guidance only where the operator must understand a consequence:

- “Blocked intake — no case has been created. A reason is required.”
- “No case or reference was created; review the missing or conflicting evidence.”
- “Created in error cannot be reopened. Create and link the replacement case.”

Illustrative text must not fabricate operational input. Loading, empty, stale/partial, retryable error, denied/unauthenticated, validation, conflict, external-unknown and reopened behavior follows the full state matrix. Permanent consequences remain visible without hover or colour alone.

## Accessibility and acceptance

Use skip link, labelled navigation, semantic tables/captions/header/sort state, keyboard queue selection, pane/tab relationships, associated error summary, restrained live announcements, visible focus and safe modal focus handling. At 1280+ use dense panes; at 1024–1279 and 200% zoom, turn secondary panes into labelled tabs/drawers/ordered sections while identity/state/actions remain first. Mobile is `Not planned`.

When implemented:

- each visible trace row and state needs authenticated Web-caller and named Core-owner evidence;
- keyboard, screen-reader, focus/error, forced-colours, reduced-motion, 1280+ desktop, constrained desktop and 200%-zoom inspection must be recorded;
- operator review uses approved genuine local immutable material only; generated imagery or synthetic operational material cannot prove acceptance; and
- every UI capability allocated after `0.1.0-alpha.1` re-enters inventory, specification, alternatives, independent review, explicit approval, visual generation and manual visual review before its exact target can be implemented.

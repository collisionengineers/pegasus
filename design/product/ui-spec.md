# UI specification

Status: **Planned `0.1.0-alpha.1` specification with Operations-first selected for the shell and landing strategy. Detailed raster styling remains subject to this specification and the design system.**

## Shared shell and hierarchy

1. Authenticated identity/role, navigation and sign out.
2. Surface title, the exact queue/filter, freshness and a safe primary action.
3. Operational table, workbench or record.
4. Named workflow/evidence/lease/exception state and consequential action.
5. Provenance, external identity, permanent business history and limitation.

The Planned `0.1.0-alpha.1` routes are Operations, Intake, Triage, Cases and authorised Administration. Each candidate direction uses the same focused-flow set. `Next`/`unallocated` email appears only after its gates; `Later`/`unallocated` has no placeholder route or control.

## Contracts

| Component | Required contract |
|---|---|
| Shell/access | Sign-in and disabled/stale-role/denied outcomes; permitted-route visibility plus server authorisation. |
| Metric/queue | Label/value/unavailable/freshness; exact destination filter; zero differs from failed/stale; Operations includes exact `Blocked intake`, Due today and day/week Sent to Engineer and Reports sent. |
| Intake workbench | Persistent source identity; evidence/candidate; fact versus suggestion versus confirmed value; provenance/missing/conflict; acceptance path and no-case failure consequence. |
| State action | Permitted transition, prerequisite, consequence, required reason, recovery and history link; never generic Close. |
| Identity header | Read-only Case/PO/principal, registration, type/secondary Audit identity, workflow state, due and EVA proxy limit. |
| Evidence/document panel | Original/source/version/logical removal/closed lock; Box/external state; exact Outlook evidence with separate discovery/link/sent times. |
| Lease/conflict | Holder/expiry/recovery, read-only alternative, current conflict and preserved proposed values. |
| History | Business mutation/accepted evidence/export/material business failure only; no routine views, polling, retry, lease heartbeat or telemetry. |
| Reason dialog | Named requirement/consequence, labelled reason, confirmation/cancel, initial focus, focus containment, Escape where safe and focus return to the invoking control. |

## Focused flows

**Intake:** source -> evidence/candidate -> definitive automatic incomplete **Not ready**; staff-resolved incomplete **Not ready**; explicit staff confirmation of instruction and image completeness to **Review**; staff-resolved complete **Review** only through that confirmation; `Blocked intake` with required reason and no case/reference while blocked; or fail-closed `Needs sorting`. Resolve/retry re-enters the same fail-closed intake path and may create exactly one case/reference only if its ordinary gates then pass. Manual image/instruction link and reasoned reversal retain origin. Completeness/review gates govern later Engineer assignment, not automatic definitive Review creation.

**Triage:** dedicated pre-case list/detail. Missing registration goes to `Needs sorting`; Open/Awaiting information/Finding recorded/Completed/Cancelled; Roadworthy/Unroadworthy finding/replacement; exact reply-chain evidence; correction/new response; optional assignee; reasoned case link. No due/chaser UI.

**Case:** read-only until an explicit edit lease. Overview, data, provenance, documents/images, vehicle/MOT, inspection address/Image Based Assessment, tasks/reminders, chasers/file request, EVA export, report evidence and history. Named actions cover Not ready, Review, Held, terminal outcomes, archive/reopen. Held preserves interval; Created in error only offers linked replacement and never Reopen.

**Administration:** account creation/disable/access review/roles, principal successor cutover, configuration and mailbox allowlist. No generic rules editor or cloud/credential operation.

The complete per-scope query, mutation, Intake, Triage and Case state contract is the [requirements state matrix](requirements.md#complete-state-matrix); this specification does not compress or replace it.

## UI-07 exact search and filters

Case/PO, registration, claimant, claim number, principal, state, Engineer, received/instruction dates and range, and origin.

## Exceptions and necessary copy

Use guidance only where the operator must understand a consequence:

- “Blocked intake — no case has been created. A reason is required.”
- “No case or reference was created; review the missing or conflicting evidence.”
- “Created in error cannot be reopened. Create and link the replacement case.”

Illustrative text must not fabricate operational input. Loading, empty, stale/partial, retryable error, denied/unauthenticated, validation, conflict, external-unknown and reopened behavior follows the full state matrix. Permanent consequences remain visible without hover or colour alone.

## Accessibility and acceptance

Use skip link, labelled navigation, semantic tables/captions/header/sort state, keyboard queue selection, pane/tab relationships, associated error summary, restrained live announcements, visible focus and safe modal focus handling. At 1280+ use dense panes; at 1024–1279 and 200% zoom, turn secondary panes into labelled tabs/drawers/ordered sections while identity/state/actions remain first. Mobile is Never.

When implemented:

- each visible trace row and state needs authenticated Web-caller and named Core-owner evidence;
- keyboard, screen-reader, focus/error, forced-colours, reduced-motion, 1280+ desktop, constrained desktop and 200%-zoom inspection must be recorded;
- operator review uses approved genuine local immutable material only; generated imagery or synthetic operational material cannot prove acceptance; and
- every `Next`/`unallocated` and `Later`/`unallocated` UI change re-enters inventory, specification, alternatives, independent review, explicit approval, visual generation and manual visual review.

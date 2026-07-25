# Operator experience requirements

## People and context

- Approximately eight users in a small office.
- Desktop-first; no first-MVP mobile workflow was requested.
- Users need immediate acknowledgement and clear exception visibility more than decorative analytics.
- Accounts use application-managed usernames and passwords.

## Primary surfaces

### Case intake dashboard

- Queue counts: Not ready, Review, Held.
- Inbox categories: Receiving work, Queries, Other, Needs sorting, and a manual Blocked intake filter. `Triage` is reached through its separate business workflow, never displayed as another inbox category.
- Time view: Due today; `In today` for cases created since Europe/London midnight; Sent to Engineer today/this week; Reports sent today/this week; and seven-day chasers. Week totals use Monday-to-Monday Europe/London boundaries. `In today` is case-created activity and must not be conflated with due work.
- Filters distinguish instructions from images.
- Refresh age and failure state must be visible; zero and unavailable must not look identical.
- Not ready is incomplete work being chased. Review is complete work awaiting a required approval. Held is a reasoned manual case pause that blocks progression and chasers while the due date remains visible.
- Blocked intake is pre-case. Staff select it with a required reason; the view retains the source, warning, and retry action but shows no case/reference.
- `Sent to Engineer` counts each case once. The first-MVP proxy is the first successful EVA JSON/image export generation and must be labelled so it does not imply EVA or Engineer receipt. `Reports sent` counts every report with an explicitly associated exact Outlook Sent item from the shared approved-mailbox allowlist; automatic matching is deferred.

### Triage workspace

- Provide separate Triage navigation/list/detail rather than an inbox category or case page state.
- Require vehicle registration to create an active Triage; otherwise keep the source in `Needs sorting`.
- Present `Open`, `Awaiting information`, `Finding recorded`, `Completed` and `Cancelled`, the binary `Roadworthy`/`Unroadworthy` finding, optional assignee, no due date and no chaser controls.
- Completion waits for exact allowlisted Outlook reply-chain evidence and offers no subject, registration or manual-item-selection fallback. Show missing/ambiguous/technical evidence outcomes without inventing completion.
- Require reasons for finding replacement, reopen, unlink and relink; show superseded findings/replies and always reopen to `Open`. Keep the optional later-case link visibly separate from the Triage identity.

### Needs sorting workbench

- List genuine incoming items without first requiring a case.
- Show transport metadata, attachments, PDF/document preview, image thumbnails, and evidence together.
- Mark extracted values as suggestions until confirmed.
- Keep instruction completeness and image completeness independently reviewable/filterable.
- Make all three intake outcomes reachable from the real review surface: `Blocked intake` creates no case, accepting incomplete work creates a `Not ready` case, and accepting complete work creates a `Review` case. The configurable completeness gate applies to Engineer assignment, not case creation.
- Use registration as the identifier for images before a principal reference exists.
- Preserve unknown, contradictory, unsupported, and transient-failure outcomes instead of forcing a case classification.
- Allow staff to place any inbox item in Blocked intake with a reason. Missing VRM and an unclear or absent original report for a standalone Audit are expected examples.

### Case workspace

- Keep case reference, registration, principal, work type, due date, status, and assigned engineer visible.
- Expose documents, images, report, and permanent action history without separate disconnected applications.
- Show reopened state and the full retained lifecycle.
- Show related `a.` or `ap.` references without replacing the parent inspection reference.
- Offer Box folder access and manual chaser text copying.
- Closing uses one of four named business outcomes: post-report completion, provider cancellation, Collision Engineers rejection, or `Created in error`; there is no delete action.
- Principal and reference are read-only immediately after allocation. If the principal was wrong, staff use a reasoned `Created in error` action; the original becomes terminal and links to a newly allocated replacement. `Created in error` cannot reopen.
- Reopen requires a reason and an otherwise-valid nonterminal destination; normal gates apply and `Held` remains a separate action rather than a reopen destination.
- Report sending has no pre-send review gate. When automatic evidence is absent/ambiguous, any staff role records `Report sent` by selecting the exact approved-mailbox Sent item and entering a reason. Show Outlook `sentDateTime` as the authoritative report time and discovery/link times separately. Allow reasoned unlink/relink, recompute dependent activity totals, retain prior history, and keep a confirmed event final if Outlook later moves/deletes the item.

## Required states

Every production surface needs designed states for loading, empty, stale, partial data, transient integration failure, unauthorized action, validation conflict, duplicate/idempotent intake, blocked intake, held, terminal, reopened, and successful completion. Counts and status colours must never be the only accessible signal.

## Visual principles

- Warm off-white application ground with white panels and hairline borders.
- Warm charcoal navigation, near-black text, CE red for primary/urgent accents.
- Amber for incomplete/pending, restrained navy for review, green only for confirmed completion.
- System-sans UI; 14-16px body; sharp 2-3px corners; shadows rare.
- Lucide-style line icons in production. No emoji or decorative icon mix.
- Keyboard-visible focus, 44px minimum interactive targets where practical, AA contrast, reduced motion, and screen-reader labels.
- Do not narrate Azure, OCR, AI, queues, or implementation mechanics to operators. Describe evidence and actions in business language.

## Deferred-capability impact

The [UI planning impact register](README.md#deferred-capability-impact) applies. These requirements preserve stable case, Triage, source, document and external-message identities and named Core actions; they do not authorise broader email management, automatic matching/sending, WhatsApp automation, EVA API/replacement, finance workflows, deferred case types, guided capture, AI/vision, external accounts, malware UI or later infrastructure. Add a surface only after its owning product/contract decision and real caller exist; no placeholder navigation or generic form is permitted now.

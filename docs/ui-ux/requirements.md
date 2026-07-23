# Operator experience requirements

## People and context

- Approximately eight users in a small office.
- Desktop-first; no first-MVP mobile workflow was requested.
- Users need immediate acknowledgement and clear exception visibility more than decorative analytics.
- Accounts use application-managed usernames and passwords.

## Primary surfaces

### Case intake dashboard

- Queue counts: Not ready, Review, Held.
- Inbox categories: Receiving work, Queries, Other, Needs sorting.
- Time view: Due today, Submitted today, Cleared this week, and seven-day chasers.
- Filters distinguish instructions from images.
- Refresh age and failure state must be visible; zero and unavailable must not look identical.

### Needs sorting workbench

- List genuine incoming items without first requiring a case.
- Show transport metadata, attachments, PDF/document preview, image thumbnails, and evidence together.
- Mark extracted values as suggestions until confirmed.
- Keep instruction completeness and image completeness independently reviewable/filterable.
- Use registration as the identifier for images before a principal reference exists.
- Preserve unknown, contradictory, unsupported, and transient-failure outcomes instead of forcing a case classification.

### Case workspace

- Keep case reference, registration, principal, work type, due date, status, and assigned engineer visible.
- Expose documents, images, report, and audit trail without separate disconnected applications.
- Show reopened state and the full retained lifecycle.
- Show related `a.` or `ap.` references without replacing the parent inspection reference.
- Offer Box folder access and manual chaser text copying.
- Closing uses one of the named business outcomes; there is no delete action.

## Required states

Every production surface needs designed states for loading, empty, stale, partial data, transient integration failure, unauthorized action, validation conflict, duplicate/idempotent intake, held, terminal, reopened, and successful completion. Counts and status colours must never be the only accessible signal.

## Visual principles

- Warm off-white application ground with white panels and hairline borders.
- Warm charcoal navigation, near-black text, CE red for primary/urgent accents.
- Amber for incomplete/pending, restrained navy for review, green only for confirmed completion.
- System-sans UI; 14-16px body; sharp 2-3px corners; shadows rare.
- Lucide-style line icons in production. No emoji or decorative icon mix.
- Keyboard-visible focus, 44px minimum interactive targets where practical, AA contrast, reduced motion, and screen-reader labels.
- Do not narrate Azure, OCR, AI, queues, or implementation mechanics to operators. Describe evidence and actions in business language.

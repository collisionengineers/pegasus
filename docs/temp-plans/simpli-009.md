# SIMPLI-008 / SIMPLI-009 — queued intake ownership and staff status

## Ownership

- Kanmer tickets: `SIMPLI-008`, `SIMPLI-009`
- Branch: `task/simpli-009`
- Worktree: `C:/Users/PC/Documents/GitHub/pegasus-worktrees/simpli-009`
- Delivery: one combined branch and PR, explicitly selected by the user.

## Supporting documents

- `.kanmer/areas/simplify/SIMPLI-008/{research,impact,plan,checklist,proof}.md`
- `.kanmer/areas/simplify/SIMPLI-009/{research,impact,plan,checklist,proof}.md`

## Scope

Make Web a durable staging-only caller, make Worker the sole queued-intake processor, and add an authenticated staged-receipt status page showing Received, Processing, Complete, or Failed. Remove the inline intake APIs, Web processor registration, and request-local SQL completion polling. No live-data migration or legacy-row repair is required; databases contain disposable test data.

## Ordered implementation

1. Collapse submission onto `ReceiveIntake.ExecuteAsync`, always persisting `Pending` work.
2. Delete inline processing contracts and persistence transitions.
3. Add processor-specific transient versus terminal/unexpected classification.
4. Enforce Worker-only composition and remove Web's unused queue-sender role.
5. Add a bounded queued-status query and `/Upload/Status/{id}` staff page.
6. Redirect successful Upload POSTs to the status page.
7. Separate Web submission from Worker drain in tests and cover recovery, faults, permissions, and status destinations.
8. Refresh FRD, design, current architecture, and source-level operations statements.

## Acceptance

- Web cannot resolve or invoke `ProcessQueuedIntake`.
- Successful receipt means staged bytes plus a `Pending` work item exist.
- Worker dispatches and processes the identifier-only queue message.
- Duplicate delivery, crash after staging, expired leases, poison handling, expected failure, transient retry, and unexpected terminal failure are proven.
- Staff can see the staged receipt's state and reach its resulting case or retained receipt.
- Restore, Release build, focused tests, full tests, negative symbol searches, and documentation checks pass.

## Boundaries

No deployment or cloud write. No mailbox/Sent polling, external-work, Box, report-renderer, or case-policy changes. No new queue, runtime, schema compatibility layer, applied-migration edit, or ADR.

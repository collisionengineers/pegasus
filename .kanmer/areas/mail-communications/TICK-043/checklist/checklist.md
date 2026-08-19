# Checklist — MAIL-01

- [x] Revalidate prerequisites and the exact existing Core/Infrastructure/Web helpers to reuse.
- [x] Implement the minimal Core contract/policy with fail-closed validation.
- [x] Implement the mailbox-scoped persistence/projection/adapter boundary with idempotency and durable evidence.
- [x] Wire the real caller without duplicating business rules.
- [x] Add focused acceptance tests for duplicate delivery, post-move provider ID, missing/contradictory identity and cross-mailbox isolation.
- [x] Run `dotnet restore` and `dotnet build --configuration Release`.
- [x] Run focused tests and the relevant full suite.
- [x] Run and record the four-lens simplification pass.
- [x] Update governing/current-state documentation only to the evidence tier actually reached.
- [x] Write the post-implementation report with commands, results, residual risks and deployment qualification.

## Progress notes

- 2026-08-19: Reused `PollApprovedInbox`, `IRetainedMailboxMessageStore`, `EfRetainedMailboxMessageStore`, the Graph immutable-ID request convention, and the existing retained-mail integration fixtures. No second policy owner or UI path was added.
- 2026-08-19: The retained path now requires RFC Message-ID, derives its bounded intake occurrence token from that RFC identity, preserves the provider immutable ID separately, enforces mailbox+RFC uniqueness, refuses contradictory identity/content, and scopes thread reads by mailbox and folder.
- 2026-08-19: Locked restore passed. Final Release build passed with 0 warnings/errors; full Core passed 617/617; Architecture passed 96/96; focused Production Graph plus retained-mail integration passed 27/27; the final retained-mail run after simplification passed 12/12. `git diff --check` passed.
- 2026-08-19: Four-lens simplification found one duplicated identity lookup; it was consolidated without behavioural change. Documentation claims local implementation only and does not claim deployment or live mailbox verification.

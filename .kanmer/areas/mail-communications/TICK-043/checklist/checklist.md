# Checklist — MAIL-01

- [x] Revalidate prerequisites and the exact existing Core/Infrastructure/Web helpers to reuse.
- [x] Implement the minimal Core contract/policy with fail-closed validation.
- [x] Implement the mailbox-scoped persistence/projection/adapter boundary with idempotency and durable evidence.
- [x] Wire the real caller without duplicating business rules.
- [x] Add focused acceptance tests for duplicate delivery, post-move provider ID, missing/contradictory identity and cross-mailbox isolation.
- [x] Run `dotnet restore` and `dotnet build --configuration Release`.
- [ ] Run focused tests and the relevant full suite.
- [ ] Run and record the four-lens simplification pass.
- [x] Update governing/current-state documentation only to the evidence tier actually reached.
- [ ] Write the post-implementation report with commands, results, residual risks and deployment qualification.

## Progress notes

- 2026-08-19: Reused `PollApprovedInbox`, `IRetainedMailboxMessageStore`, `EfRetainedMailboxMessageStore`, the Graph immutable-ID request convention, and the existing retained-mail integration fixtures. No second policy owner or UI path was added.
- 2026-08-19: The retained path now requires RFC Message-ID, derives its bounded intake occurrence token from that RFC identity, preserves the provider immutable ID separately, enforces mailbox+RFC uniqueness, refuses contradictory identity/content, and scopes thread reads by mailbox and folder.
- 2026-08-19: Locked restore and Release build passed with 0 warnings/errors. Initial focused Core (20/20) and integration (27/27) runs passed; after strengthening the intake token, integration remained 27/27 and one Core assertion exposed only an expected token-length typo, now corrected for rerun.

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

## PR-004 blocking-fix notes

- 2026-08-19: Unified receipt hashing, store lookup/comparison, and SQL uniqueness on one trimmed, NFKC-normalized, invariant-uppercase RFC Message-ID key. The raw transport value remains unchanged evidence.
- 2026-08-19: Added real poll/EF tests proving case/whitespace-equivalent RFC variants create one staged receipt, work item and retained row, while distinct canonical RFC identities create two of each.
- 2026-08-19: Blocking-fix verification passed: Release/Integration build 0 warnings/errors; focused Core 21/21; new real poll/EF regressions 2/2; full Core 618/618; retained-mail integration 14/14; diff check clean.

## PR-005 blocking-fix notes

- 2026-08-19: Bounded the canonical RFC output after trim/NFKC/uppercase; expansion past 500 now becomes malformed-message handling before any receipt or database write.
- 2026-08-19: Real poll/EF evidence now uses genuinely Unicode-equivalent Kelvin-sign/ASCII identities and asserts the first raw transport value remains verbatim beside the canonical key.
- 2026-08-19: Verification passed: Release build 0 warnings/errors; focused Core 22/22; Unicode/distinct poll/EF 2/2; full Core 619/619; retained-mail integration 14/14; diff check clean.

## PR-008 blocking-fix notes

- 2026-08-19: Added the committed `20260819093019_RetainedMailboxInternetMessageIdentity` migration to the schema inventory assertion.
- 2026-08-19: Both restart theory variants now give the independent later message its own RFC Message-ID while preserving terminal missing/changed-source assertions.
- 2026-08-19: Verification passed: Integration build 0 warnings/errors; three previously failing cases 3/3; full affected integration classes 23/23; diff check clean.

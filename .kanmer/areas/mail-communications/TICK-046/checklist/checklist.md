# Checklist — MAIL-04

- [x] Revalidate prerequisites and the exact existing Core/Infrastructure/Web helpers to reuse.
- [x] Implement the minimal Core contract/policy with fail-closed validation.
- [x] Implement the mailbox-scoped persistence/projection/adapter boundary with idempotency and durable evidence.
- [x] Wire the real caller without duplicating business rules.
- [x] Add focused acceptance tests for before/after history, evidence/policy version, stale concurrency, duplicate delivery and re-evaluation protection.
- [x] Run `dotnet restore` and `dotnet build --configuration Release`.
- [x] Run focused tests and the relevant full suite.
- [x] Run and record the four-lens simplification pass.
- [x] Update governing/current-state documentation only to the evidence tier actually reached.
- [x] Write the post-implementation report with commands, results, residual risks and deployment qualification.

## Progress notes

- 2026-08-19: Reused `MailClassificationResult`, `MailTaxonomy`, the retained-mail store/read model, receipt versioned-envelope serializer, the exact-message Razor page, and existing EF concurrency conventions.
- 2026-08-19: Added one Core correction use case and port; Infrastructure performs a single optimistic transaction that updates the accepted current decision and appends an immutable before/after record.
- 2026-08-19: Added migration/backfill for decision actor/time/version and least-privilege Web grants. No mailbox or cloud write was performed.
- 2026-08-19: Release build 0 warnings/errors; focused Core 18/18; full Core 634/634; focused SQL/Web acceptance 2/2 (and combined retained-mail/Web suite previously 27/28 with the sole failure an over-specific HTML attribute-order assertion, corrected and rerun green).
- 2026-08-19: Governing FRD already states the delivered contract, so no normative documentation wording or current/deployed-state claim was changed.

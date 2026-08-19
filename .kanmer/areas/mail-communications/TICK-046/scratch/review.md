# Independent review — 2026-08-19

## Changes

PR #418 adds one Core correction use case and dossier/history contracts; extends the retained-mail EF store with exact-message reads and an optimistic append-only correction transaction; adds decision attribution/version/concurrency and correction-history schema/backfill/runtime grants; protects accepted corrections from automated receipt replay; wires the existing Razor message detail as the real caller; and adds Core, SQL persistence and Web rendering tests.

## Comments

- **Blocking:** the model-bound classification parser accepts undefined numeric enum values (or throws while indexing the taxonomy), so a forged `received:999`/`sent:999` request does not fail closed as a validation result. `Other` name/reasoning bounds are only HTML attributes and are not independently enforced by Core, allowing crafted oversized values to reach bounded columns. This contradicts the plan/FRD requirement to validate invalid categories before any write.
- **Non-blocking:** `MessageModel`'s XML remarks still claim the page is read-only and has no handler; update while touching the boundary if convenient.

## Disposition

- Blocking validation gap filed as [[PR-010]], which blocks this ticket.
- Stale XML remark is non-blocking and may be fixed in the same PR; it does not justify separate scope.

## Verdict

**Needs changes.** Reviewed the complete ticket/group context, open questions, plan/checklist/report, governing FRD, full non-generated diff, migration/backfill/grants, Core ownership, optimistic concurrency, replay protection, exact-message UI, and focused tests. The report otherwise honestly matches the diff, the simplification record is credible, and no unauthorized Outlook/cloud mutation is present. PR #418 must not merge until [[PR-010]] is implemented and re-reviewed. CI was still running when this verdict was recorded.

## Re-review — 2026-08-19 (`fe66e4bd`)

### Changes checked

PR-010 adds canonical enum/bounds validation beside `MailCategory`, invokes it at the Core correction boundary, rejects hostile model-bound values in Web, adds zero-write real page-pipeline coverage, and corrects the stale page remark.

### Comments and disposition

- **Fixed in PR:** [[PR-010]] is substantively satisfied. Undefined Received/Sent families and oversized Other fields fail closed; focused reviewer runs passed Core 19/19 and hostile Web/SQL 1/1.
- **Blocking:** SQL shard 2 failed `IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema` because its committed migration fixture omits `20260819104953_MailClassificationCorrectionHistory`. Filed [[PR-012]] and linked it as blocking. This is branch-related, not a transient runner failure.

### Verdict

**Needs changes.** The implementation and PR-010 fix pass substantive re-review, and all other completed CI checks are green, but the required canonical migration/schema regression fails. Update the existing fixture, rerun the focused SQL test and full CI, then re-review before merge.

## Final re-review — 2026-08-19 (`581fee7f`)

### Changes and dispositions

- [[PR-010]] remains satisfied: canonical validation is Core-owned; hostile undefined/oversized submissions fail closed and produce zero writes.
- [[PR-012]] is fixed in PR: the exact canonical committed-migration fixture now includes `20260819104953_MailClassificationCorrectionHistory`; no assertion was weakened.
- The post-implementation report and simplification record accurately cover the complete diff and review responses.

### CI

Fresh run `32246448063` passed changes, documentation, reference-data, unit, browser, SQL integration shards 1/2/3, and SQL integration coverage. Infrastructure was skipped by path rules as expected.

### Verdict

**Pass.** Independently reviewed the full ticket/group/docs, governing FRD, complete PR diff, Core policy ownership, append-only correction evidence/history, optimistic concurrency, replay protection, migration/backfill/grants, exact-message UI and tests. No unresolved blocker or unauthorized live Outlook/cloud mutation remains. Merge to `dev` and advance [[TICK-046]] to Verifying.

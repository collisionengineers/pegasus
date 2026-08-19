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

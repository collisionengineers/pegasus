# Independent review — PR #411 — 2026-08-19

## Changes

- `docs/frd/frd-08-email-mailbox-and-background-processing.md` adds named subtype spellings, classification criteria/methods, operational destinations, and logical Outlook folder types.
- `MailClassificationContracts.cs` registers the added named subtypes.
- `MailOperationalDestinationPolicy.cs` adds a pure, versioned Core mapping.
- The QDOS classifier now emits only the explicit `pre-instruction-emails/triage-request` subtype for its existing accepted Triage predicate; it does not alter Triage workflow semantics.
- Core taxonomy, mapping, and QDOS tests were updated/added; capabilities and current-architecture snapshots were refreshed.

## Comments

1. **Blocking:** the new policy and FRD table map many known classifications to the generic `Other` operational destination. This conflicts with the operator's latest decision recorded in TICK-057: known categories/subtypes must remain distinct operational views, and `Other` is reserved for the reasoned novel-classification escape hatch. Affected cases include General, non-query Billing, Non-client-related, In-progress, non-Triage Pre-instruction, Internal CC, and Sent classifications.
2. **Non-blocking / confirmed:** `Ambiguous` and `Unclassified` correctly fail closed to `NeedsSorting`.
3. **Non-blocking / confirmed:** the QDOS change only names the already-classified Triage request and does not change Triage workflow behavior.
4. **Non-blocking / confirmed:** Outlook folder recommendation/mutation remains separated under MAIL-23.

## Disposition

- Comment 1: filed as [[MAIL-001]], which blocks [[TICK-044]].
- Comments 2–4: won't-do-because the existing implementation is correct and in scope.

## Verdict

**Needs changes.** Checked the ticket plan, files map, open questions, post-implementation report, EPIC-006 context, linked FRD, TICK-057's operator-decision plan, PR #411 diff, and GitHub checks. The implementation/report accurately describe the diff and the simplification pass is explicit, but the central destination behavior contradicts the later operator decision. PR #411 was not merged and TICK-044 remains in Review.

# Independent re-review — PR #411 — 2026-08-19

## Changes

Commit `702148f2` replaces the contradictory generic-Other mapping with a typed result: every known category/subtype maps to Receiving work, Queries, Triage, or `DetailedClassification` while retaining the exact validated `MailCategory`; only a reasoned novel `MailCategory.Other` maps to `Other`. FRD-08, capabilities, current architecture, the plan correction, post-implementation report, and tests were updated consistently.

## Comments

1. **Blocking finding resolved:** known classifications no longer map to generic `Other`; exhaustive tests assert this invariant.
2. **Confirmed:** `Ambiguous` and `Unclassified` fail closed to Needs sorting with no category.
3. **Confirmed:** only `pre-instruction-emails/triage-request` maps to Triage; no Triage workflow semantics changed.
4. **Confirmed:** the exact category remains available for downstream UI-14 and MAIL-23 consumers without duplicating the taxonomy.
5. **CI note:** the first SQL shard-2 attempt encountered an unrelated transient SQL deadlock in `QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate`, outside this diff. The failed-job rerun passed; the complete required CI matrix is green.

## Disposition

- Previous comment 1: fixed in PR by `702148f2`; [[MAIL-001]] is resolved by the same correction.
- No new comments.

## Verdict

**Pass.** Independently checked updated diff, plan, open questions, post-implementation report, governing FRD, TICK-057 operator decision, exhaustive Core tests, QDOS semantic boundary, simplification re-check, and the final green GitHub CI matrix. PR #411 is approved for merge to `dev`.

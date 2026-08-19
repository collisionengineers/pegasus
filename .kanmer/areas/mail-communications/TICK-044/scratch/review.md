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

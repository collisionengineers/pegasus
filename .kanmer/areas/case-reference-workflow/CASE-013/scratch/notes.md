## 2026-08-22 — this ticket's symptom has a second cause, now fixed separately

QDOS26010 (created 02:01Z on 22 Aug, after this fix deployed in release 17) is
still `NotReady`. That is **not** evidence the fix failed.

The promotion to Review happens inside
`EfQueuedCustodyProcessor.CompleteCaseCustodyAsync`. Custody for that case
**failed** — `create_case_custody | failed | custody_unexpected_failure` — so the
promotion code never ran at all, and the completeness flags this ticket sets
were never consulted.

The custody failure has its own cause and its own fix: the Worker runtime role
was never granted the case-document tables ([[DOCS-008]], PR #510). Until that
migration is applied, no case can reach Review however complete it is.

So the acceptance condition for this ticket cannot be evaluated on any case
created so far. It needs one instruction through the pipeline *after* the
DOCS-008 migration lands, which is the same case that closes [[DOCS-006]],
[[DOCS-007]], [[CASE-014]], [[CASE-017]], [[INTK-029]] and [[INTK-030]].

---
outcome: needs-changes
pr: https://github.com/collisionengineers/pegasus/pull/659
head: ed0dc6ad2b00d1299b404f06596ee0ed499ec250
reviewers: gpt-5.6-terra (xhigh, independent read); Claude Opus (dispositions, verification)
date: 2026-09-04
---

# Review attestation — CASE-032 — needs changes

Reviewed PR https://github.com/collisionengineers/pegasus/pull/659 at head
`ed0dc6ad2b00d1299b404f06596ee0ed499ec250`. Not merged.

The full record, with every finding, disposition, command and exit code, is in
this ticket's `reference` document ("Review record — CASE-032").

Two should-fix findings must be applied before this PR can merge:

1. `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:557,574,576` — the new
   `Custody`, `Reference` and `Provider` quick-detail pairs pass `string.Empty`
   when absent, and `Pages/Cases/Index.cshtml:216,235` renders every pair
   unconditionally, so an absent value draws a labelled blank row instead of
   nothing. Add each pair only when its source value is non-null, as
   `BlockedRow` (`:609-618`) already does for `E-mail`. Do not add a
   placeholder word; leave the `"Unassigned"` fallback at `:429` alone.
2. `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs:254` — the assignee
   assertion is vacuous: `development-offline-administrator` is rendered by the
   authenticated shell on every page. Assert the assignee inside the seeded
   Triage row (or as the contiguous `provider · assignee` meta) so the fourth
   half is actually proved. Weaken no existing assertion.

One reviewer finding was rejected: moving the new field *captions* into
`OperatorLabels` — that file owns value vocabulary, not captions, and every
quick-detail caption in `Pages/Cases/Index.cshtml.cs` is already a literal at
its use site. Reasoning is recorded in the review record.

Everything else passed: scope, no migration needed, Core ownership of the
custody vocabulary, the cardinality-safe single-statement left join, the
honest simplification pass, and a clean local verification at this head
(restore 0, Release build 0, Core.Tests 0 / 1225 passed, ArchitectureTests
0 / 100 passed, `TriageQueuesWebTests` 0 / 9 passed).

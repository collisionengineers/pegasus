## Backfill plan (VERIFY2, 2026-08-20)

No implementation is planned. EVAL-02 was already implemented under ADR-0016 before this ticket was worked, in `scripts/email-eval-desktop/`, not in the Pegasus web app. The plan is the verification itself (see `research.md`):

1. Correct the mapping — confirm via `docs/capabilities.md`'s own row and ADR-0016 that EVAL-02 belongs to the standalone desktop evaluator, not the in-app mail classification panel.
2. Confirm the taxonomy is exactly 8 Received + 4 Sent categories, structurally enforced.
3. Confirm reasoning is required and validated before filing.
4. Confirm test coverage exists for both.

Simplification pass: n/a — docs-only backfill, no diff.

## Fix plan (implementation, 2026-08-20) — supersedes "no implementation is planned" above

The VERIFY2 correction above named the fix correctly: re-source the catalog from the Core-owned taxonomy rather than resurrecting the deleted txt (one list per concept — the ADR's own Context already said Core owns classification policy). Implemented exactly that:

1. `CategoryCatalog.Load()` now enumerates `ReceivedMailFamily`/`SentMailFamily` and calls `MailTaxonomy.CategoryName` per value — the exact same 8+4 category set as before (proven by the unchanged `CatalogParsesAllTwelveCategoriesWithoutReplyFolder` assertion list), now with zero file I/O and no possibility of drifting from Core's settled taxonomy.
2. Removed the now-dead repository-root discovery from `Program.cs` and the test file, since nothing needs it once the catalog is code-sourced. This also incidentally fixed the separately-broken fixture path (the `RepositoryRoot()` helper the tests used for `CopyFixture` was on the same deleted-marker probe) — replaced with in-memory MIME fixtures, matching the existing `IntakeTestEvidence.CreateEmail` convention elsewhere in the repo, so the tests no longer depend on any external file at all.
3. Did not touch `EmailEvaluationWorkflow`'s reasoning/filing/persistence code — EVAL-02's actual capability (reasoning required, JSONL log) was already correct and untouched by this fix, confirmed still green by the full 9-test run.

### Simplification pass (2026-08-20)

Delivered together with TICK-007's simplification pass (same branch/PR) — see TICK-007's plan doc for the full four-lens write-up. Specific to this ticket: removing `FindRepositoryRoot` and `RepositoryRoot()` entirely (rather than leaving them unused, or keeping a `repositoryRoot` parameter nothing reads) is the simplification finding, applied. No unapplied findings.

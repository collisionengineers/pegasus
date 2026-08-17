**Author is the reviewer.** This is not an independent review (same agent implemented TICK-009).

## Changes

- `tests/Pegasus.IntegrationTests/QdosEmailCohortTests.cs` — volume roots now prefer existing `emailevals/*` + `extraction-corpus/QDOS` and otherwise fall back to a flat `corpus/*.eml` dump; labelled facts use a new skip attribute; worktrees locate the primary checkout corpus via the `gitdir:` pointer. Corpus is read-only. Artifacts still write under `artifacts/evaluation/qdos-classification/`.
- `docs/operations.md` — one dated 2026-08-17 volume-cohort bullet (counts only: 256 EML, 75 accepted / 167 no-match / 13 needs-sorting / 1 unreadable; 14 classified / 61 unclassified / 0 ambiguous). Explicitly not holdout, deploy, live, or acceptance.
- `docs/capabilities.md` — MAIL-21 activation note now points at that operations observation and still names labelled holdout, deployment, and live verification as separate states.

No Core policy, Worker, Graph, schema, or Inbox change.

## Comments

- **non-blocking** — Reviewer is the author. Repository workflow prefers an agent that did not implement; recorded here rather than pretended otherwise.
- **non-blocking** — Plan cites ADR-0008 for the cohort requirement but `refs` only contain FRD-08. Honest: this slice is local volume evidence, not the full ADR-0008 activation cohort (positive/negative/ambiguous/retry/version-pinning as a route-activation gate).
- **non-blocking** — `DiscoverCorpusRoot` assumes a standard `.git/worktrees/<name>` layout. Unusual gitdir placements fall through to skip. Acceptable.
- **non-blocking** — If a machine has labelled/emailevals trees *and* extra flat EML at the corpus root, the flat files are ignored. Matches the plan's anti-double-count rule.

## Disposition

- Author-reviewer — won't-do-because the user asked for this review now; independence is disclosed.
- ADR-0008 not in refs — won't-do-because the plan already scoped this as local volume only; filing a ticket would invent ADR activation work the ticket parked.
- gitdir layout / flat-vs-tree — won't-do-because both are documented mitigations, not defects.

## Verdict

**Pass** (self-review), pending green `repository-check` on PR 391.

Checked: PIR file list vs `gh pr diff 391` (match); plan Governing docs vs FRD-08 (no behaviour change; no unauthorised FRD/ADR edit); files.md ripple (no policy/UI/CI-corpus requirement added); open-questions all ticked above Parked; no unplanned extras.

**CI note:** `repository-check` sql-integration (2) failed on `QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate` (SQL deadlock). That class is not in this diff; the MAIL-21 change is Corpus-category and is filtered out of sql-integration (`Category!=Corpus`). This is the known flaky deadlock lane tracked by DELIVE-001. Re-running the failed jobs rather than treating it as a MAIL-21 defect.

sql-integration (2) rerun passed (the deadlock did not reproduce). Merged PR 391 into `dev`. Moved TICK-009 to verifying. Next: kanmer-verify.

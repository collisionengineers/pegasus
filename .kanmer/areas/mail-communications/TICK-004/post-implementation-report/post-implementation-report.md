## Backfill post-implementation report (VERIFY2, 2026-08-20)

No implementation occurred under this ticket. EVAL-02 was already implemented under ADR-0016 in the standalone `scripts/email-eval-desktop/` tool, matching the capability text exactly (8 Received + 4 Sent taxonomy, Reply as context, required reasoning, JSONL adjudication log). Corrected an earlier hypothesis (in this run's capability-survey document) that the in-app `MailClassificationSelection` panel might satisfy this — `docs/capabilities.md`'s own EVAL-02 row and ADR-0016 both make clear this is a separately owned, non-production prerequisite tool.

## Fix (implementation, 2026-08-20) — supersedes "No implementation occurred" above

Implementation did occur, in the same branch/PR as TICK-007: `CategoryCatalog.Load()` is now parameterless and sources the 8 Received + 4 Sent categories from Core's `MailTaxonomy` instead of the deleted `docs/reference/CollisionSPikeCurrenttree.txt`. `Program.cs`'s repo-root probe (which existed only to feed that file lookup) was removed. The test suite's own parallel dependency on the same deleted marker file — and a second, independently-broken fixture path — was replaced with in-memory MimeKit-built `.eml` fixtures, matching the convention already used in `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs`.

Verified starting state and fix: `dotnet test` on a clean `origin/dev` worktree failed 7/8 (`Repository root not found.`); after the fix, `dotnet build` (0 warnings/errors) and `dotnet test` both pass, 9/9 (8 original + TICK-007's 1 new test). See TICK-004's `files.md`/`plan.md` for the detail and TICK-007's `plan.md` for the shared simplification-pass write-up.

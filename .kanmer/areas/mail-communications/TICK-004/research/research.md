# Research — EVAL-02 taxonomy selection + required reasoning (retrospective backfill, VERIFY2 lane, 2026-08-20)

**Read-only verification backfill.** Verdict: **PARTIAL — ticket stays at preparing.** The owning surface exists and enforces the core behaviour, but it is broken against the current repo and lacks the Reply limb.

## Ownership (settled by FRD text)

`docs/frd/frd-08-email-mailbox-and-background-processing.md#qdos-alpha-evaluation-boundary` states the evaluator is "a separately delivered evidence harness and is not a QDOS-alpha product surface". EVAL-02 is owned by the **standalone desktop evaluator** (ADR-0016, `docs/adr/0016-standalone-desktop-email-evaluator.md`, on origin/main), *not* by the shipped Mail classification UI. The Mail UI (MAIL-21/22, `src/Pegasus.Web/Pages/Mail/Message.cshtml` + `MailClassificationSelection`) happens to also implement taxonomy selection with a required `CorrectionReason` — but that is MAIL-22's delivery, not EVAL-02's.

## What exists (verified on origin/dev; also present on origin/main = 2325ed4a ancestor path)

- `scripts/email-eval-desktop/` (8 files incl. tests) — WinForms evaluator.
- Required reasoning enforced: `EmailEvaluationWorkflow.cs:135-150` rejects empty reasoning.
- Taxonomy enforced: `CategoryCatalog.cs` requires exactly 8 Received + 4 Sent categories, hard `InvalidDataException` otherwise.
- Tests: `scripts/email-eval-desktop/tests/DesktopEvaluatorTests.cs`.

## The gaps (why this is not done)

1. **The evaluator can no longer start.** `CategoryCatalog.Load` reads `docs/reference/CollisionSPikeCurrenttree.txt` and throws `FileNotFoundException` if missing — and that file was **deleted** from the repo (commit `4e084ca2` "Delete superseded planning material; migrate surviving content first"). Verified: the path does not exist on origin/dev or origin/main. The tool as shipped fails at load.
2. **No Reply limb.** The capability text is "Received/Sent/**Reply** taxonomy". The evaluator catalog is Received(8)+Sent(4) only; `EmailEvaluationWorkflow.cs` contains no reply handling. (Core's shipped taxonomy models reply as an `IsReplyContext` flag on `MailCategory` — `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs` — a second, diverging list-owner situation.)
3. **One list per concept violated by drift.** The canonical taxonomy now lives in Core (`ReceivedMailFamily`/`SentMailFamily`, MAIL-21/22); the evaluator's file-parsed catalog is a stale second copy keyed to a deleted file. Remediation should point the evaluator at the Core taxonomy (or a retained export of it), not resurrect the deleted txt.
4. No evidence any evaluation campaign has ever been run with it.

## What implementation needs

- Re-source the evaluator's catalog from the Core-owned taxonomy (single list owner), restoring startability.
- Decide the Reply representation (separate limb vs `IsReplyContext` mirror) consistently with Core.
- Acceptance evidence: one real evaluation session producing a reasoned classification record.

Premises verified read-only: file presence/absence via `git ls-tree`/`git show` on origin/dev and origin/main; deletion commit located via `git log --diff-filter=D`. Assumed: no other copy of the taxonomy txt exists outside the repo.

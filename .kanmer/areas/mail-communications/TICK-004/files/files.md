- `scripts/email-eval-desktop/CategoryCatalog.cs` — 12-category taxonomy (existing, unchanged)
- `scripts/email-eval-desktop/EmailEvaluationWorkflow.cs` — required-reasoning validation (existing, unchanged)
- `scripts/email-eval-desktop/MainForm.cs` — reasoning text box wiring (existing, unchanged)
- `scripts/email-eval-desktop/EvaluationWorkspace.cs` — JSONL adjudication log (existing, unchanged)
- `scripts/email-eval-desktop/tests/DesktopEvaluatorTests.cs` — coverage (existing, unchanged)
- `docs/adr/0016-standalone-desktop-email-evaluator.md` — the accepted design this implements (existing, unchanged)

No files changed by this ticket — verification-only backfill against already-shipped code.

## Fix (implementation, 2026-08-20) — the "no files changed" statement above is superseded

Confirmed the gap directly before fixing it: `dotnet test` on `scripts/email-eval-desktop/tests/Pegasus.EmailEvaluation.Desktop.Tests.csproj` in a clean worktree from `origin/dev` failed 7 of 8 tests with `System.InvalidOperationException: Repository root not found.` — both `CategoryCatalog.Load`'s marker check and the test helper's own repo-root probe keyed off `docs/reference/CollisionSPikeCurrenttree.txt`, which is gone (confirmed by TICK-004's own prior research). A second, independent breakage was also found and fixed in the same pass: the test fixture path the tests copied from (`docs/reference/imp-docs/.../email-mistags/acknowledgement/2/Thank you for your email.eml`) no longer exists either — that subtree was renamed to `miscategorised-emails` with different contents at some point, unrelated to the taxonomy-file deletion but with the same practical effect (broken tests).

Files changed:

- `scripts/email-eval-desktop/CategoryCatalog.cs` — `Load()` no longer reads any file. It now builds the 8 Received + 4 Sent categories directly from Core's own `MailTaxonomy` (`ReceivedMailFamily`/`SentMailFamily` enums + `MailTaxonomy.CategoryName`, `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs`) — one list per concept, Core owns it, no local copy of the taxonomy strings. Signature changed from `Load(string repositoryRoot)` to `Load()`.
- `scripts/email-eval-desktop/Program.cs` — dropped the now-unnecessary `FindRepositoryRoot` repo-root probe entirely (it existed only to hand a path into `CategoryCatalog.Load`).
- `scripts/email-eval-desktop/tests/DesktopEvaluatorTests.cs` — removed `RepositoryRoot()` and the file-copying `CopyFixture` helper (both depended on the same now-gone repo-root marker and a fixture path that no longer exists); replaced with an in-memory `.eml` builder (`WriteFixture`, MimeKit `MimeMessage`/`MailboxAddress`/`TextPart`) following the exact same convention already used in `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs`'s `IntakeTestEvidence.CreateEmail`. All `CategoryCatalog.Load(RepositoryRoot())` call sites updated to `CategoryCatalog.Load()`.
- `docs/adr/0016-standalone-desktop-email-evaluator.md` — Context sentence corrected to name Core's `MailTaxonomy` as the taxonomy source instead of the deleted file; the decision itself is unchanged.

Verified: `dotnet build` of both the desktop exe project and its test project (0 warnings, 0 errors), then `dotnet test` — 9/9 passing (8 original + 1 new, from TICK-007's work delivered in the same branch/PR per the orchestrator's instruction).

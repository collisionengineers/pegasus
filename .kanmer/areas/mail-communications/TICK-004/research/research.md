## Backfill research (VERIFY2, 2026-08-20)

**Important correction to an earlier hypothesis in this run's capability survey:** EVAL-02 is NOT satisfied by the in-app `MailClassificationSelection` panel (`src/Pegasus.Web/Presentation/MailClassificationSelection.cs`, MAIL-21/22 work). `docs/capabilities.md`'s own EVAL-02 row states the canonical owner is the "QDOS-alpha evaluation boundary" and explicitly notes: *"Separately owned prerequisite; not QDOS delivery."* Per ADR-0016 (accepted 2026-07-29), EVAL-01 through EVAL-05 are entirely about a standalone, non-production, Windows-only WinForms desktop tool at `scripts/email-eval-desktop/`, deliberately excluded from `Pegasus.slnx` and never referenced by Pegasus.Web or Pegasus.Worker. This is a completely different artifact from the shipped mail classification panel.

**Capability text (`docs/capabilities.md:76`):** "Reviewer selects from the detailed Received/Sent/Reply taxonomy and records required reasoning."

**Code evidence, `scripts/email-eval-desktop/`:**
- Taxonomy: `CategoryCatalog.cs` loads exactly 12 categories (8 `Received`, 4 `Sent`) from `docs/reference/CollisionSPikeCurrenttree.txt`, throwing `InvalidDataException` if the count doesn't match — a hard structural guarantee, not just a convention.
- Reply as taxonomy context, not a third family: ADR-0016 states explicitly "Reply remains context on its underlying Received or Sent category and does not create a standalone folder" — this is the accepted design answer to "Reply", not a gap.
- Required reasoning: `EmailEvaluationWorkflow.cs:135,149-150` — `TryFileAsync(..., string reasoning, ...)` trims the input and rejects empty reasoning before filing. `MainForm.cs:18,94,129` wires a `reasoningText` `TextBox` into the same call.
- Persistence: `EvaluationWorkspace.cs` copies the reviewed `.eml` into the local `emailevallocal` tree and appends a JSONL adjudication record including the reason (`tests/DesktopEvaluatorTests.cs:99-118` `FilingCopiesSourceAndEscapesReasonInOneJsonLine` proves the reason is escaped correctly into one JSON line).
- Tests: `scripts/email-eval-desktop/tests/DesktopEvaluatorTests.cs` — 9 `[Fact]` tests covering folder selection, reasoning validation, custom `Other` category naming, and idempotent re-filing.

**File presence at 2325ed4a (production `main` ancestor):** confirmed present via `git show 2325ed4a:scripts/email-eval-desktop/EmailEvaluationWorkflow.cs`. This is not meaningful as "production deployment" evidence, though — the tool is by design never deployed to any Azure environment; it is a local reviewer utility restored and run independently. No `deployment` value applies (leaving it unset is the honest state, per the runbook's own rule: "no local result is relabelled deployed, live verified, or accepted").

**Verdict:** EVAL-02 is fully implemented and tested against its own capability text, in the correct (standalone desktop) artifact.

## Plan (implementation, 2026-08-20) — supersedes the earlier "stays at preparing" placeholder

The earlier research/plan pair correctly named the gap (the `Suggestion` display slot exists and is always null) and correctly guessed the fix's shape, but misidentified the vehicle: it pointed at the injected-but-unused `IInstructionExtractionPolicy`. That interface extracts instruction *fields* (claimant name, claim number, ...) against an `EstablishedPrincipalContext` the evaluator doesn't have — a different concern. The correct one-owner is `IMailClassificationPolicy` (`src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs`), the same interface MAIL-21/22 shipped, implemented by `QdosMailClassificationPolicy`. It exposes exactly what the ticket asks for: `Category` (with `Subtype`), `Predicates` (matched/unmatched with `Detail` evidence text), `PolicyKey`, `PolicyVersion`.

A second, better-than-expected finding: `EmailEvaluationWorkflow`'s injected `sourceReader` (`IIntakeSourceReader`) was *also* dead — never called. `IMailClassificationPolicy.Classify` takes exactly the `IntakeSourceReadResult` that reader produces, so no new reader dependency was needed — just actually calling the one already wired.

### Steps taken

1. **Reuse, not copy.** Added `IMailClassificationPolicy classificationPolicy` to `EmailEvaluationWorkflow`'s constructor; Program.cs supplies `new QdosMailClassificationPolicy()` — the one Core-owned policy, no rule table duplicated locally.
2. In `LoadCurrentAsync`, after reading the file once into `byte[] content` (reused for both display and classification — was two reads before, now one): build an `IntakeSource` (`FileName`, `"message/rfc822"`, `content`, `timeProvider.GetUtcNow()`, a fixed non-staff `Actor` string, `IntakeSourceIdentity(ManualUpload, SHA256-hex-of-content)` — the same `ManualUpload` channel and hash-of-content-as-token convention `DownloadIntakeSource.cs` uses for its receipt token), run it through the existing `sourceReader`, then `classificationPolicy.Classify(readResult)`.
3. Format the `MailClassificationResult` into the existing `suggestion` field: Classified → `"{Received|Sent} / {name}[/{subtype}]"` + matched-predicate evidence lines + `policy {key} v{version}`; Ambiguous → the candidate list, same evidence/policy suffix; Unclassified → `null` (unchanged "No category" fallback — nothing to suggest, not a bug).
4. Classification wrapped in its own `IntakeExceptionPolicy.IsRecoverable` guard, separate from the display-read guard: a rule-evaluation fault degrades to no suggestion, never blocks the message the reviewer is looking at or their ability to file it. Read-only, advisory, beside the human review — matches ADR-0016 and the ticket's own framing.
5. `MainForm.cs`: grew the suggestion row and switched the label to a fixed, wrapping one so multi-line evidence is visible instead of clipped by the old single-line 38px row.
6. Tests: one new deterministic fixture-based test proving a known rule-matching email renders the exact category/subtype/evidence/policy-version string; extended the existing filing test to check the JSONL log's `suggestedCategory` field is written through untouched (that field already existed — `EvaluationWorkspace.Commit` already accepted and logged `suggestion`, it was just always null before).

### Existing code reused (no new abstractions)

- `IMailClassificationPolicy` / `QdosMailClassificationPolicy` — Core's one classification-policy owner, unchanged.
- `IIntakeSourceReader` (`MimeKitPdfPigOpenXmlIntakeSourceReader`) — already injected, now actually called.
- `EvaluationWorkspace.Commit`'s existing `suggestedCategory` JSONL field — no schema change.
- `DownloadIntakeSource.cs`'s hash-of-content receipt-token convention, reused for the advisory `IntakeSourceIdentity`.

### Simplification pass (2026-08-20)

Ran the four lenses (reuse, simplification, efficiency, altitude) over this branch's diff (both TICK-007 and TICK-004 changes, delivered together per the orchestrator's instruction):

- **Reuse**: confirmed no second classification-policy implementation was introduced; the desktop tool calls Core's existing `IMailClassificationPolicy`, exactly the "one Core owner" rule. Confirmed the fixture-eml builder (`WriteFixture` in the test file) follows the exact convention already used in `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs`'s `IntakeTestEvidence.CreateEmail`, rather than inventing a new one.
- **Simplification**: reading the file once into `content` and reusing the same bytes for both the display parse and the classification read removed a second disk read that a first draft would otherwise have needed. Dropped `Program.cs`'s `FindRepositoryRoot` entirely (dead once `CategoryCatalog.Load()` no longer needs a repository-root argument) rather than leaving it unused.
- **Efficiency**: no material efficiency concern — this runs once per reviewer folder-navigation action on a local desktop tool, not a hot path.
- **Altitude**: `FormatSuggestion`/`FormatCategory` kept as private static methods on the workflow rather than a new class/service — two small pure functions, one caller, no seam for a second caller yet (no abstraction without one).

No unapplied findings.

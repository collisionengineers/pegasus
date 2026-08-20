## Research (VERIFY2, 2026-08-20) — genuinely PARTIAL, stopping here

**Correction to an earlier hypothesis (this run's capability-survey doc):** EVAL-05 is NOT satisfied by the in-app `MailClassificationSelection` panel. Per `docs/capabilities.md`'s own EVAL-05 row and ADR-0016, this belongs to the standalone `scripts/email-eval-desktop/` desktop evaluator (see [[TICK-004]]'s research for the shared background).

**Capability text (`docs/capabilities.md:79`):** "Display the rule-generated category and evidence beside the human review once rules exist" — explicitly conditional on rules existing.

**What exists:** the desktop evaluator has a display slot for a rule-generated suggestion — `EmailEvaluationWorkflow.EvaluationSnapshot.Suggestion` (`EmailEvaluationWorkflow.cs:16,55`: `"Suggested: {suggestion}"`), rendered in `MainForm.cs`, and the workflow is constructed with an `IInstructionExtractionPolicy extractionPolicy` dependency (`EmailEvaluationWorkflow.cs:26,39,46`) evidently intended to supply that suggestion.

**What is genuinely missing:** `suggestion` is assigned `null` at every call site in `EmailEvaluationWorkflow.cs` (lines 66, 120, 195, 231) and is never assigned from `extractionPolicy` or any other rule engine in `LoadCurrentAsync` (`:191-220`) — the field that is meant to carry evidence is coded but never populated. The UI always shows `"Suggested: No category"` (confirmed by `tests/DesktopEvaluatorTests.cs:46`, which asserts exactly that string). The injected `extractionPolicy` is unused for this purpose.

**Is this excused by "once rules exist"?** No, not cleanly. QDOS-direct classification predicates now exist elsewhere in Pegasus.Core (ADR-0020, MAIL-21/22, `MailClassificationSelection`) — rules of the relevant kind DO exist in the application today. But EVAL-05's own scope is specifically the standalone desktop evaluator surfacing "the rule-generated category and evidence" beside the human review inside that tool, and nothing wires the desktop tool to any rule source (Core's classification predicates or otherwise). The gap is real: the display mechanism is present but inert.

**This matches the ticket's own pre-existing `checklist.md`** (migrated from TICK-008), which already has the exact unchecked item: "Record the rule-generated category and evidence beside the human review." — unaltered.

**Verdict: genuinely PARTIAL.** Per this run's operating instructions, the ticket stays at `preparing` with the gap named here, rather than being walked further. What activation would need: wire `EmailEvaluationWorkflow.LoadCurrentAsync` to call a rule/predicate source (most naturally the same `IInstructionExtractionPolicy`/QDOS predicate surface already injected but unused) and populate `suggestion` with its result plus supporting evidence text before the reviewer sees the message.

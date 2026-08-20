# Research — EVAL-05 rule-generated category beside human review (retrospective backfill, VERIFY2 lane, 2026-08-20)

**Read-only verification backfill.** Verdict: **NOT built in its owning surface — ticket stays at preparing.**

## Ownership

Same as EVAL-02 ([[TICK-004]]): the FRD's "QDOS-alpha evaluation boundary" section assigns EVAL-05 to the **standalone desktop evaluator** (ADR-0016), explicitly *not* the shipped Mail UI. Capability text: "Display the rule-generated category and evidence beside the human review **once rules exist**".

## What exists

- The evaluator has the display slot wired: `EvaluationSnapshot.Suggestion` (`scripts/email-eval-desktop/EmailEvaluationWorkflow.cs:16-18,53-59`) renders as "Suggested: …" in the UI.
- But `suggestion` is **only ever assigned `null`** (lines 66, 120, 195, 231) — the UI always shows "Suggested: No category". No rule engine feeds it.

## Adjacent but not this capability

The shipped Mail message page *does* display rule-generated classification evidence beside the human correction form — `src/Pegasus.Web/Pages/Mail/Message.cshtml:78-99` ("Classification evidence": Policy/PolicyVersion/Reason/Predicates from `MailClassificationResult`, `src/Pegasus.Core/Intake/Classification/MailClassificationContracts.cs:230-244`) directly above the "Correct classification" form (`:101-131`). That satisfies EVAL-05's *words* but on the wrong surface — it is MAIL-21/22 (QDOS) delivery, and the FRD forbids counting QDOS surfaces as evaluator evidence.

## The gap

1. No rule engine exists in the evaluator; the suggestion slot is permanently null. The capability's own condition "once rules exist" is unmet — arguably EVAL-05 is not yet *due*, but it is certainly not *done*.
2. Any implementation should reuse the Core mail classification policy (`MailClassificationResult` with Predicates/PolicyKey/PolicyVersion) as the rule source rather than inventing a second rules engine (one Core owner) — the evaluator would call shared Core policy and populate `Suggestion` from it.
3. Blocked in practice by the same startability defect as EVAL-02 (deleted taxonomy source file — see [[TICK-004]] research).

## What implementation needs

- Wire `EvaluationSnapshot.Suggestion` to the shared Core mail classification policy output, including evidence (predicates + policy version) beside the human decision.
- Depends on [[TICK-004]]'s catalog re-sourcing fix.
- Acceptance evidence: an evaluation session showing a rule-suggested category with evidence rendered beside a recorded human review.

Premises verified read-only: all code quotes from origin/dev via `git show`; null-assignment claim from full read of `EmailEvaluationWorkflow.cs`.

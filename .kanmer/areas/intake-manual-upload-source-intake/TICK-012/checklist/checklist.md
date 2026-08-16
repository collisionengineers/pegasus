# Checklist — TICK-012 (INT-25)

- [x] Distil contract/caller/failure into proof.md "What was verified"
- [x] Run focused Core intake suites: AllocateDefinitiveIntakeTests, DefinitiveIntakeCaseTypeTests, Qdos/QdosMailClassificationPolicyTests, CaseMatching/EvaluateIntakeCaseMatchTests
- [x] Run focused integration recovery suites: QdosAllocationRecoveryTests, IntakeAllocationConsumerTests, CaseAcceptanceReplayTests
- [x] Write proof.md with real `dotnet test` output (local caller-proof tier) + honest "Not covered" (live/deployed journey, QDOS-only breadth, OCR-literal Audit bound)
- [~] Record activation-criteria decision + the two scope open-questions; move ticket to `review` (not `done`) — moved to `review`; operator activation decision + follow-up filing pending user input

## Progress notes

- 2026-08-13: Mechanism found already implemented, wired and test-covered (see research.md). No source change made. Release build 0 errors; Core intake 64 passed; integration recovery/replay 22 passed (LocalDB) → 86 local caller-proof tests green. Live/deployed tier deferred (requires-live-approval). Moved to `review` for operator activation decision; did not self-advance to `done`.

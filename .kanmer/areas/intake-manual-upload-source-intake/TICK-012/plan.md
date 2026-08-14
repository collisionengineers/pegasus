# Plan — TICK-012 (INT-25): Automatic case creation from definitive authorised intake

Written FROM research.md and impact.md. Both establish that the capability's
mechanism is implemented, wired through the real durable caller, and covered by
Core + integration tests. This is therefore an accept-and-prove plan, not a
build plan.

## Approach

Do not change the intake/allocation path — it is invariant-bearing and proven.
Instead satisfy the ticket's own Verification: (1) record the exact feature
contract, caller, failure behaviour and required tests (already captured in
research.md/impact.md, distilled here), and (2) settle the activation-criteria
decision. Take the conservative, in-policy stance: **accept the local
caller-proof activation tier** as evidence by running the focused intake test
suites, and **explicitly defer the live/deployment (tier-5) acceptance** as
`requires-live-approval` rather than attempting any live/deployed journey. This
beats the alternatives — "re-implement/refactor" risks the replay guarantees for
no gain; "claim fully done incl. live" would over-claim against NOW.md's own
warning that no enabled Worker caller has run against the deployed estate.

## Steps

1. Distil the contract into a one-screen record in proof.md's "What was
   verified": Core owner `AllocateIntake.AttemptAutomaticAsync`; caller chain
   timer→`intake-work`→`ProcessQueuedIntake`→allocation; reference minting
   `QDOSyyNNN` + `a.`/`ap.`; definitive-Audit triple condition; unique-match
   bypass; bounded-failure-separate-from-decision; frozen-command staff retry.
2. Run the focused Core intake test suites (the local caller-proof evidence):
   `AllocateDefinitiveIntakeTests`, `DefinitiveIntakeCaseTypeTests`,
   `Qdos/QdosMailClassificationPolicyTests`,
   `CaseMatching/EvaluateIntakeCaseMatchTests`.
3. Run the focused integration recovery suites:
   `QdosAllocationRecoveryTests`, `IntakeAllocationConsumerTests`,
   `CaseAcceptanceReplayTests` (real-SQL durable caller evidence).
4. Capture the real `dotnet test` output into proof.md as the local activation
   tier. Record honestly in proof.md's "Not covered": the live/deployed
   Worker-caller journey (requires-live-approval), QDOS-only breadth, and the
   OCR-literal Audit bound.
5. Record the activation-criteria decision and the two open scope questions
   (breadth / OCR follow-ups; whether the operator wants a live journey before
   final acceptance) for the user, then move the ticket to `review` — do NOT
   self-advance to `done`; the acceptance is the operator's to confirm.

## Verification

proof.md is produced from step 2–4: the pasted `dotnet test` summaries (pass
counts + suite names) for the six suites above are the evidence that the
mechanism behaves to contract at the local caller-proof tier. Behaviours to
confirm in the output: automatic allocation uses the persisted typed case type;
a failed automatic attempt is durable and not retried in the background; the
definitive-Audit triple condition and negation exclusion hold; a unique existing
match bypasses allocation exactly once; the processing decision is separated
from a failed allocation. No live/estate command is run.

## Risks / open questions

- **Activation-acceptance is the operator's call** (open question in research):
  if the operator requires a live deployed case-creation journey, that stays
  pending under approval and the ticket holds at `review` rather than `done`.
- **Test-run cost**: the solution is large and CI is slow; mitigate by filtering
  to the named test classes rather than a full `dotnet test`.
- **Scope discipline**: if a real defect surfaces while reading, file a separate
  `bugs` ticket — do not fix here.
- **Blocks DOC-01**: confirm the final reference forms are what DOC-01's
  `SafeName` accepts before DOC-01's plan closes.

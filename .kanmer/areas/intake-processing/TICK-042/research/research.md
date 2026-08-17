# Research — TICK-042: INT-28 automatic image/instruction matching

## Question

Does `dev` still need implementation for automatic association of image-led and instruction-led records, including both pairing directions and fail-closed eligibility?

## Findings

- `docs/frd/frd-02-intake-and-source-identity.md` requires automatic association only for one eligible pre-report Case with a confirmed registration and no contradictory evidence; all other cases remain a reasoned staff decision.
- The forward path is in `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs`. After a confident scan, it identifies an exact confirmed-registration match or one sole eligible one-character-missing candidate, registers the confirmed identity, and calls the one-shot automatic association. Multiple candidates, missing origin data, storage/read failures, or recoverable write failures abstain without changing the intake decision.
- `src/Pegasus.Core/ImageIntake/ImageIntakeCasePairing.cs` provides the reverse path. `src/Pegasus.Core/Intake/AcceptIntake.cs` calls it only after a new Case acceptance. It scans unassociated Image intakes, permits only one exact eligible candidate matching the newly accepted Case, and isolates each recoverable pairing failure.
- `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs` documents that the Image-intake registration is immutable and that a Case is eligible only before report delivery. The reverse path deliberately does not apply scan-time near-match completion rules.
- `src/Pegasus.Infrastructure/Persistence/EfIntakeMutationStore.cs` owns the durable, idempotent automatic association write; it yields to existing associations and staff edits rather than overwriting history.
- `tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs` and `tests/Pegasus.Core.Tests/ImageIntake/ImageIntakeCasePairingTests.cs` cover confident and ambiguous forward paths, idempotency, exact-only reverse pairing, and per-item failure isolation. The focused ImageIntake Core test run passed 78/78 on 2026-08-17; the wider integration subset timed out after 120 seconds without a final result.
- The implementation and reverse-pairing acceptance are already present on `dev` in `f7d99b18` and `ef3eb4c7`.

## Implications

INT-28 is already implemented on `dev`. This ticket must not broaden matching criteria, make a near-match valid after registration, or introduce automatic association where ambiguity or contradictory evidence exists. Work is limited to reconciling its record with the shipped implementation and retaining targeted regression evidence unless a concrete defect is found.

## Open questions

- None. The bounded integration run is verification follow-up, not a product or design question.

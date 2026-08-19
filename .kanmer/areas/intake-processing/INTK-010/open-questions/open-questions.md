## Resolved in research/plan, not asked

- INT-28 interaction with the confirmation offer — the ticket's Constraints section already answers this; research.md restates it precisely against the verified automation code path (`DurableIntake.cs:721-760`) rather than re-asking it.
- Whether "offer to create an Image-initiated case" means a staff-triggered creation button — resolved by evidence: Image-initiated Case creation is inherently automatic and VRM-keyed (INTK-008's own scope text), so the "offer" on the images branch is a report of the automatically-created case, not a button; the instruction-document branch is the genuine staff-triggered offer. See research.md's design-decision section.

## Genuinely open (none blocking)

None identified. If the simplification pass or implementation surfaces a case not covered by the seven-branch table in plan.md step 5, it will be added here before the ticket leaves Preparing... [table already covers Received/Processing/Complete×{CaseId, ImageIntakeRegistered, Unidentified, Ambiguous, CanBecomeCase, refusal}/Failed, which is exhaustive over `IntakeDecision`'s seven values plus `QueuedIntakeStatusKind`'s four, so no gap is currently expected].

## Parked (explicitly deferred)

None.

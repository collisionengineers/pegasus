## Independent review — PR #454 (orchestrator, 2026-08-20)

Verdict: **pass**, merge on green CI.

- The ordinal-0 ambiguity is fixed at its owner: `GroupedIntakeMemberToken.ParentTokenCandidates` owns the token shape in one place (ordinal-0 member carries the parent token verbatim; later ordinals suffix `:{n}`, n≥1), and `FindForMemberSourceAsync` resolves through the candidate set instead of a lossy strip. Strict suffix validation (positive, unsigned, no whitespace) prevents false parses.
- Tests cover the exact ambiguity plus round-trip token shapes in Core and the store path in GroupedIntakeWebTests.

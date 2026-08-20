# Checklist — PR-044

- [x] Add bounded fresh-context Pending→Uncertain cancellation handoff.
- [x] Preserve the original caller cancellation after the durable handoff.
- [x] Prove cancellation during provider move recovers by same key and blocks new keys until resolved.
- [x] Prove cancellation during Success save recovers by same key and never duplicates the move.
- [x] Run focused/proportional verification and four simplification lenses.
- [x] Update PR-044/TICK-049 reports and traceability, push, and leave Review.

# Checklist — PR-044

- [ ] Add bounded fresh-context Pending→Uncertain cancellation handoff.
- [ ] Preserve the original caller cancellation after the durable handoff.
- [ ] Prove cancellation during provider move recovers by same key and blocks new keys until resolved.
- [ ] Prove cancellation during Success save recovers by same key and never duplicates the move.
- [ ] Run focused/proportional verification and four simplification lenses.
- [ ] Update PR-044/TICK-049 reports and traceability, push, and leave Review.

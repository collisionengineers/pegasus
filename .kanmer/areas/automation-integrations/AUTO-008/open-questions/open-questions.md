# Open questions — AUTO-008

- [x] Measure durable acceptance → dispatch, dispatch → processing claim, inner `ProcessIntake`, and post-evaluation terminal tail separately. — Research decision.
- [x] Report median, p95, and worst case for healthy runs; report retry paths separately. — Research decision.
- [x] Use representative repository-provided fixtures and immutable local copies only. — Repository safety rule.
- [x] Treat the 15-second dispatcher cadence as a hypothesis, not a finding of root cause. — Static inspection limitation.

## Parked (explicitly deferred)

- [ ] Compare against the older TypeScript runtime. Deferred until an approved predecessor source or executable is supplied; none is tracked in this repository.
- [ ] Measure production latency. Deferred because production observation/deployment scope is not part of this local research request.

# Open questions — INTK-003

No operator decision is required by this ticket's research.

## Parked (explicitly deferred)

- [ ] **Stale-dispatch age and recovery cadence.** Deferred to blocking [[INTK-041]], which owns the near-real-time durability contract. INTK-003 planning must consume that settled value; it must not choose a persistence-local default.
- [ ] **Separate stale-publication telemetry count.** Deferred to [[INTK-041]]'s observability contract. Keep one recovery result/count unless that contract requires operators to distinguish lease expiry from lost publication.

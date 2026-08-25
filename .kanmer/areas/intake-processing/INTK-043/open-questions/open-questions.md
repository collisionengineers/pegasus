# Open questions — INTK-043

- [x] Which routes share the optimization? E-mail and manual upload both continue through the existing Core-owned ProcessQueuedIntake to ProcessIntake route.
- [x] May planning assume the source reader owns the observed interval? No. Instrument and baseline the broader queued-processing stages first; select the change only from evidence.
- [x] What counts toward the target? Ordinary healthy intake from receipt/wake through truthful terminal/case state; report retries, cold starts and deliberately large inputs separately.
- [x] Which safety behavior may be traded for speed? None: durability, integrity, traversal bounds, fail-closed allocation, idempotency and truthful Processing remain binding.
- [x] When is the representative baseline taken? After blocking [[INTK-041]] and [[INTK-042]] land, so old timer/dispatch delay is not labelled reader cost.

## Parked (explicitly deferred)

- [ ] Production p95 proof is deferred to approved deployment/observation work in [[DELIV-021]]; INTK-043 supplies instrumentation, a repeatable pre-release baseline and the measured code change without cloud writes.

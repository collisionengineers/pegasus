# Open questions — INTK-043

- [x] Is five seconds measured only after Pegasus receipt? No. Report both Outlook-arrival-to-custody and Pegasus-receipt-to-custody; the former is best effort because Outlook/Graph and Box are external.
- [x] Does completion include custody? Yes. Confirmed Box custody is the requested completion boundary.
- [x] Which payloads count? Every currently supported input cohort up to documented limits.
- [x] What warm capacity starts the remediation? One function-specific 2 GB always-ready instance, then 4 GB only on measured CPU evidence.
- [x] Is a second worker/queue path permitted? No. Normal work uses the one typed queue and one Core-owned processor; retry/recovery remains the sole later message.

## Parked (explicitly deferred)

- Provider replacement is not planned. If Outlook/Graph or Box remains the sole reason the best-effort total target misses, record the evidence for a later product decision.

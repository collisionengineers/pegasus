# Open questions

No operator-only question remains. The operator activated integration, approved the initial four assessment variants and shared Audit/Inspection physical report template, and confirmed the deployment ownership boundary.

- [x] **Does EXT-08 perform its own Azure write?** — No. Resolved by the operator on 2026-08-19: TICK-081 is a non-mutating acceptance envelope. [[PLAT-007]] alone owns the exact-target Azure deployment, runtime health, Chromium, telemetry, capacity, and recovery proof. TICK-081 consumes that evidence and must not request or perform a duplicate deployment.

## Parked (explicitly deferred)

- None.

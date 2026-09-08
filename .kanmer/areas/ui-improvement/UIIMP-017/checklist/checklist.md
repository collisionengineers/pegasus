# Checklist — UIIMP-017

- [x] Existing Health metrics render all five instants through OfficeTime.
- [x] Default selector and catalogue name the real populated mailbox state.
- [x] Existing route and direct predicate tests prove the correction.
- [x] Root focused capture/update/verify/catalogue pass with failures retained; independent review next.

Root initial build FAILED on CA1859 after 50.17s; no tests/captures ran.
Corrected build PASS 44.19s; initial 2-test run 1 PASS / 1 FAIL on exact
service-cell whitespace, with the correct London metrics value already passing.
Only the expected space was corrected after inspecting captured HTML. Both
failures remain in the report. Final unique-TRX run passed 2/2; build20.79s,
scoped snapshot update3/verify3 and catalogue60/67/0 passed. No source markup
or timestamp assertion was weakened. Independent review next; no CI/live/
manual visual/deployment claim.

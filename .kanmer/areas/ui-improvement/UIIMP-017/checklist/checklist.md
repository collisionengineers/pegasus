# Checklist — UIIMP-017

- [x] Existing Health metrics render all five instants through OfficeTime.
- [x] Default selector and catalogue name the real populated mailbox state.
- [ ] Existing route and direct predicate tests prove the correction.
- [ ] Root focused capture/update/verify/catalogue pass with failures retained; independent review next.

Root initial build FAILED on CA1859 after 50.17s; no tests/captures ran.
Corrected build PASS 44.19s; initial 2-test run 1 PASS / 1 FAIL on exact
service-cell whitespace, with the correct London metrics value already passing.
Only the expected space was corrected after inspecting captured HTML. Both
failures remain in the report; tests/generated evidence await root's unique-TRX
correction run. No source markup or timestamp assertion was weakened.

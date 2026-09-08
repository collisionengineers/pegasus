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

## Closeout — UIIMP-017

- [x] PR merge verified (`gh pr view --json state,mergedAt`).
- [x] Whole final PASS proof read; PR URL and merge date present.
- [x] Done accepted by root after exact merged verification.
- [ ] Retain and hash-check three TRXs and associated author/merged captures.
- [ ] Outcome and reachable merge/delivery recorded in ticket body.
- [ ] Remove only the two validated owned worktrees from the shared checkout.
- [ ] Delete only local and remote UIIMP-017-health-display without force.
- [ ] Verify exact refs/registrations absent; broad prune is unnecessary after normal removal.
- [ ] Release the claim last.

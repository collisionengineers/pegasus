# Open questions — CASE-040

- [x] What durable rule identifies the fallback A Patterson account for the
  default Sign-off Engineer? Operator answer 2026-09-03: an
  Administrator-maintained "Default sign-off Engineer" designation on one
  flagged account, exposed on PLAT-068's `SignOffEngineerProfile`; CASE-040's
  Core resolver reads it. No username or account ID is hard-coded. The
  plan's default stands.

- [ ] Operator: does the first Send to EVA move the case from `Review` to
  `With Engineer`? FRD-07 says twice that neither route changes the Case state
  or version (lines 63 and 131, reconciled by DELIV-041 after D36) and the plan
  follows it, leaving `StartCaseWork` as the only transition. D44 as recorded
  on [[PLAT-070]] says "Review to With Engineer happens through Send to EVA",
  and the mockup does exactly that (`Pegasus_UI_v2_src/src/20-case.js:190`).
  Raised by the 2026-09-03 plan review; the answer changes CASE-040's Core
  action, so it is needed before implementation.

## Parked (explicitly deferred)

None.

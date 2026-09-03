# Open questions — CASE-040

- [ ] Operator: what durable rule identifies the fallback A Patterson account
  for the default Sign-off Engineer — a reserved username `a.patterson`, an
  Administrator-maintained "default Sign-off Engineer" setting on a flagged
  account (PLAT-068), or another immutable account identity? D31 says Andy is
  the default but not how the account is designated; the repository has only
  the fixed report tuple and the mockup's username is not a persisted rule.
  Plan default (2026-09-02, plan/plan.md §Resolved implementation decisions):
  an Administrator-maintained Default designation on one flagged account,
  exposed on PLAT-068's `SignOffEngineerProfile`; no username or account ID
  is hard-coded (FRD-01/FRD-04). Confirming that default ticks this item;
  a different answer changes only the Core resolver and the PLAT-068
  dependency line.

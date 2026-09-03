# Open questions — PLAT-068 (2026-09-02)

- [x] Are the three signature PNGs seeded by the migration or uploaded by an
  Administrator? Operator answer 2026-09-03: an Administrator sets the
  sign-off status and uploads the signature through the new control; no
  migration seed and no name-to-account mapping in the repository.
- [x] Is an account offered as sign-off when the flag is Yes but
  qualifications or the signature are missing? Resolved 2026-09-03 by the
  controller from FRD-11 as fixed in PR #647: eligible = enabled + Engineer
  role + flag + signature on file; qualifications are optional (Neil signs
  without a qualification line until his are recorded).
- [x] Does the Sign-off Engineer setting carry the printed signatory name?
  Resolved 2026-09-03 by the controller: yes — one nullable column in the
  same migration, required when the flag is Yes; no general account Name
  field. DOCS-017 renders the tuple from it.

Scope addition (operator, 2026-09-03): the setting also carries one
"Default sign-off Engineer" designation that an Administrator sets on
exactly one flagged account; CASE-040's default rule reads it.

## Parked (explicitly deferred)

None.

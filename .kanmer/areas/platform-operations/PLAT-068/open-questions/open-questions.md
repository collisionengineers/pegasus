# Open questions — PLAT-068 (2026-09-02)

- [ ] Are the three PNGs under `docs/design/brand/signatures/` loaded by an
  Administrator through the new upload control (no migration seed), or must
  the migration seed them onto named production accounts? The repository
  holds no mapping from those names to production account IDs. Plan
  default: no seed; an Administrator uploads through the control (FRD-04
  says the initial accounts are application data, never hard-coded).
- [ ] Is an account offered as sign-off when the flag is Yes but
  qualifications or the signature are missing? D31 says "flagged"; the
  mockup (`05-state.js` `signoffEngineers()`) requires a signature on file.
  Plan default: eligibility = enabled + Engineer role + flag, one Core
  function (`SignOffEngineerEligibility.IsEligible`); requiring a stored
  signature changes only that function.
- [ ] The report tuple prints a name ("A Patterson"), but a staff account
  holds only a username (`a.patterson`); `ActorDisplayNames` resolves staff
  to the username and the mockup's account `Name` column and create-dialog
  field have no implementation. Does the Sign-off Engineer setting carry the
  printed signatory name (one more nullable column in the same migration,
  required when the flag is Yes), or does the account gain a general Name
  under a separate ticket? Plan default: expose `UserName` as `DisplayName`
  in `SignOffEngineerProfile` and add no name field; the answer decides
  whether [[DOCS-017]] can render the tuple without a hard-coded mapping.

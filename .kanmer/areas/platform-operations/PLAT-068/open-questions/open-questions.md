# Open questions — PLAT-068 (2026-09-02)

- [ ] Are the three PNGs under `docs/design/brand/signatures/` loaded by an
  Administrator through the new upload control (no migration seed), or must
  the migration seed them onto named production accounts? The repository
  holds no mapping from those names to production account IDs.
- [ ] Is an account offered as sign-off when the flag is Yes but
  qualifications or the signature are missing? D31 says "flagged"; the mockup
  (`05-state.js` `signoffEngineers()`) requires a signature on file.

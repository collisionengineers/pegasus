# Checklist — PR-061

- [ ] Validate current Review state under the existing locked Export transaction.
- [ ] Add deterministic SQL regression for a demotion committed before Export obtains the lock.
- [ ] Confirm no new abstraction, schema, compatibility or retry path.
- [ ] Run Release build and focused integration evidence.
- [ ] Write report, commit, push and update PR #539.

2026-08-25 simplification: reused the existing lock/transaction and exception; no new type, schema or compatibility path. Release build and focused SQL test passed.

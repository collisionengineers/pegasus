# Checklist — PR-061

- [x] Validate current Review state under the existing locked Export transaction.
- [x] Add deterministic SQL regression for a demotion committed before Export obtains the lock.
- [x] Confirm no new abstraction, schema, compatibility or retry path.
- [x] Run Release build and focused integration evidence.
- [x] Write report, commit, push and update PR #539.

2026-08-25 simplification: reused the existing lock/transaction and exception; no new type, schema or compatibility path. Release build and focused SQL test passed.

# Checklist — PR-038

- [x] Add the database-enforced single active claim per retained message.
- [x] Prove concurrent different keys cannot both invoke the mover.
- [x] Prove matching replay and a new key after terminal failure remain valid.
- [x] Record focused verification, simplification, commit `fc3b651e` and PR #477 traceability.

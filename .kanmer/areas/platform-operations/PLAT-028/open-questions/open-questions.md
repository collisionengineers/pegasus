# Open questions — PLAT-028

- [x] Archive PLAT-024 and consolidate Organizations and Principals here. — Operator decision, 2026-08-21.
- [x] Put provider-key controls on the Principal within the consolidated Organization detail. — Operator decision.
- [x] Preserve create and immutable replacement as separate actions; do not turn Principal identity into editable fields. — Product invariant.
- [x] Show generated/reset secrets once in the immediate POST response only; do not store them in TempData, session, URL, logs, or the database. — Security requirement.
- [x] Pause blocks new submissions while prior-result reads continue; revoke invalidates authentication. — Operator decision.
- [x] Use existing page/header/table/form/status primitives and remove explanatory empty-state panels. — Design authority.

## Parked (explicitly deferred)

- [ ] Support multiple simultaneous credentials per Principal. Deferred until a second concrete caller exists.
- [ ] Perform live provider issuance. Deferred to separately approved activation.

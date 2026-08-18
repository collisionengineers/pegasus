# Checklist — DELIV-004

- [x] Add the explicit no-closed-gate delivery rule to `AGENTS.md`.
- [x] Compare the wording with `docs/engineering.md` and leave its detailed
  anti-dormancy rule unchanged: no contradiction found.
- [x] Inspect the documentation-only diff for unauthorized scope.
- [x] Record the targeted policy-search output for proof.

## Progress notes

- 2026-08-18: Added the explicit safety-rail rule in `AGENTS.md`; verified
  that `docs/engineering.md` already prohibits disabled flags and other
  dormant implementation shapes. `git diff --check` passed and the diff
  changes only `AGENTS.md`.

# Proof — DELIV-004 (verified on merged `main` `f1e116c6`, 2026-08-18)

- `grep -n -i "closed composition|disabled flag|feature gate" AGENTS.md docs/engineering.md` on `main`:
  - `AGENTS.md:184: - A closed composition or feature gate is a disabled flag, not a partially …` (the explicit rule);
  - `docs/engineering.md:118: as dormant registration, an unused endpoint, a disabled flag, or dark …` (retained detailed prohibition).
- `git diff --check ea908247^ ea908247` → clean; `git show --stat ea908247` → `AGENTS.md | 4 ++++` only.
- Shipped to `main` in release 9 (PR #398 merged 2026-08-18T09:24:59Z into dev; promoted with `f1e116c6`). Applied in practice the same day: AUTO-001 activated the Automation MCP gate in production rather than shipping it dark.

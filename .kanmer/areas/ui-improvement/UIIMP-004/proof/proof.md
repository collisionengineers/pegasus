---
ticket: UIIMP-004
merged_main: 1ec65dc894f121f4bb5b31ae82c818a401d08beb
pr: 562
proof_type: command-log
date: 2026-08-27
---

# Proof — UIIMP-004

Written on merged `main` (`1ec65dc8`). Deployment n/a — the Test UI is
documentation and test tooling only.

- PR #562 merged as `1ec65dc8` after the nine `fix(mail)` commits were
  reverted (`a1b8e9b2`); `git diff origin/dev...HEAD -- src global.json
  .github` was empty at merge, so no product code shipped from this ticket.
- Reduced branch verified locally: Release build 0 warnings / 0 errors;
  `Test-UiCatalogue.ps1` — 52 routed sources, 57 prototypes, 0 broken local
  references; `Test-UiModes.ps1` pass; `git diff --check` pass.
- CI run on `2e8a0361`: unit, browser, all three SQL shards and coverage pass.
- Independent reviews: tooling approved at `44d16f46`; reduced branch
  approved at `db2c3757`. Codex's 18 quality findings are dispositioned
  "defer" to [[UIIMP-005]] in the plan.

Verdict: **PASS**.

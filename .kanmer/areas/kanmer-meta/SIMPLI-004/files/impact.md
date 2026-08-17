# Impact — Retire NOW.md

## Files changed
- `NOW.md` — deleted (after fact relocation).
- `docs/operations.md` — absorb the un-duplicated production facts; **update
  Worker state to the 2026-08-12 "enabled" truth (meaning change — confirm
  with user first)**.
- `docs/open-decisions.md` — take ownership of the `## Path` sequence +
  "Explicitly NOT on the path"; update the delegating line at :25; record the
  embedded operator decisions.
- `docs/index.md:9`, `docs/engineering.md:6,16`, `docs/capabilities.md:6`,
  `docs/runbook.md:1089`, `README.md:68` — remove/redirect NOW.md references.
- `AGENTS.md` — claim mechanism becomes Kanmer (shared edit with [[SIMPLI-002]]).
- Code comments: `src/Pegasus.Infrastructure/Persistence/EfApprovedInboxPollStore.cs:116`,
  `tests/Pegasus.PerformanceTests/CapacitySoakTests.cs:86` — drop "queued in
  NOW.md" wording (comment-only, no behaviour change).

## Guards
- `docs/operator-notes.md` is protected — not touched.
- Relocation is a Stage-B gate: no `NOW.md` deletion until every fact in the
  root-plan table has a confirmed home.
- `Test-DocumentationLinks.ps1` catches any dangling `NOW.md` link left behind.

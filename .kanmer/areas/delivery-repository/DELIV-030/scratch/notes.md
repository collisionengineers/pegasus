2026-08-28 docs follow-ups collected from EPIC-011 reviews (carry into this ticket or the design README correction):
- `docs/design/README.md` §Status chips: `.status--neutral` is `#57534e` on `#f3f2f0` (prototype literal kept by PLAT-029), not `--muted` on `--surface-3`.
- `docs/design/README.md`: PLAT-029 reviewed divergences — account dialog keeps a "Change password" link; Add dialog omits "Create upload request" until a Case picker exists (wave 4); utility search has no placeholder; freshness wording "Current · HH:MM"; `.shortcut-hint` uses an opaque `#2d3336` ground for contrast.
- `docs/current-architecture.md` §routes: `/Cases` (queues, formerly `/Triage`), `/Search` (formerly `/Cases`), `/Triage`/`/Unidentified` 301 stubs, `/VehicleImages` list removed (detail retained), rail-count filter now sums six figures from three queries.
- `docs/open-decisions.md`: PLAT-048 engineering choices — 15-minute StaleAfter reused for poll health rows; 24-hour EVA recent-failure window; Engineer report attributes by current assignee.

2026-09-02 — Scope note from EPIC-012: the current-architecture and operations refresh must cover the single-scroll Case record, the retired `/Cases/{id}/Assessment` route (301), the EngineerNotes and staff sign-off stores, the storage-location column, the MarketResearch AI job kind and the D29–D43 decisions. Runs after the EPIC-012 snapshot/walk chore.

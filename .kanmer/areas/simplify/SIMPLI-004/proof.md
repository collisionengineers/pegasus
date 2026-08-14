# Proof — Retire NOW.md (PR #374)

- `NOW.md` **deleted**; the claimable unit is now a Kanmer ticket (`take_ticket`)
  per the rewritten AGENTS.md workflow.
- Durable facts relocated first (no loss): the current **Worker-enabled** state
  (live-verified 2026-08-13 via `az functionapp config appsettings list` — all
  nine `AzureWebJobs.*.Disabled` = false) + the post-release-8 deployment/
  migrations + the "nothing live-verified beyond smoke" caveat → `operations.md`;
  the QDOS cutover **Path** (owned by `open-decisions.md:25` before) →
  `open-decisions.md`.
- All NOW.md references reworded in README/engineering/capabilities/runbook/
  docs-index + two code comments to point at the Kanmer board.
- `runbook.md` reconciled to `operations.md` on the Worker state (review fix).

Verified: `git grep 'NOW.md'` over checked docs → no live links; link checker
green (118 files); independent review confirmed facts preserved and the
operations.md/FRD-08 meaning changes match accepted decisions.

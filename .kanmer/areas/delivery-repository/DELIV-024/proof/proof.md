# Proof

- PR #555 merged to `dev` as `1a8fda3e244c993c24c52f731e8c5027dcc4d4dc` after independent docs-only review and green CI.
- That exact documentation SHA was atomically promoted to `main` and `dev` under operator merge authority.
- `docs/current-architecture.md` now records immediate publication by the committing caller and one-minute Worker recovery for interrupted Pending work.
- `docs/operations.md` records release 32's full exact source SHA, image digest, manifest hash, revision, unchanged migration head, activation, schedule and strict smoke evidence.
- Operator speed and displayed-state acceptance remain explicitly unclaimed.
- Markdown placement, documentation link, and diff checks passed.

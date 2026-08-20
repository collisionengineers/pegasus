# Files — TICK-186

No repository (tracked) files are added or changed. All output is a local,
gitignored artifact — no PR is expected for this ticket.

## Created (untracked, under `artifacts/`, gitignored via `.gitignore:21` `/artifacts/`)

- `artifacts/evaluation/extraction-cohort/20260820/manifest.csv` — the
  cohort/holdout manifest: one row per corpus `.eml` file
  (`sha256,filename,size_bytes,hash_sort_index,split`).
- `artifacts/evaluation/extraction-cohort/20260820/_build_manifest.py` — the
  self-contained script that produced the manifest from `corpus/*.eml`;
  rerunning it reproduces byte-identical output (verified).
- `artifacts/evaluation/extraction-cohort/20260820/README.md` — methodology,
  findings, and the holdout-protection statement.

## Read-only

- `corpus/*.eml` (256 files) — hashed and stat'd only; never modified,
  renamed, copied, or moved.
- `docs/capabilities.md`, `docs/frd/frd-02-intake-and-source-identity.md`,
  `docs/operations.md`, `docs/runbook.md`,
  `tests/Pegasus.IntegrationTests/QdosEmailCohortTests.cs`,
  `artifacts/evaluation/qdos-classification/*`,
  `artifacts/evaluation/*inventory.json` — read for context and to confirm
  the existing artifact-output and split-methodology conventions.

## Kanmer

- `TICK-186` refs: added `docs/frd/frd-02-intake-and-source-identity.md`
  (governing doc, INT-21's owner) via `link_doc`.

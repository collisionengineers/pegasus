# Proof — extraction cohort and untouched holdout assembled (command-log)

2026-08-20. Local-only evaluation artifacts (corpus/ is ignored and immutable; nothing tracked changed, so there is no PR — the manifest is the deliverable).

- Input: 256 corpus `.eml` files (the only source format present at corpus/ top level on this workstation).
- Split: SHA-256 content digest per file, sorted ascending, cut at floor(0.8×256)=204 → **204 cohort / 52 holdout**. Deterministic and seed-free; rerun produced a byte-identical manifest. None of the 4 duplicate-content groups straddles the cut.
- Manifest: `artifacts/evaluation/extraction-cohort/20260820/manifest.csv` (`sha256,filename,size_bytes,hash_sort_index,split`) with `_build_manifest.py` (reproducible builder) and `README.md` (methodology, findings, holdout-protection statement).
- Holdout protection: the manifest's `split` column is the fence; extraction-development work filters to `split=="cohort"`; the holdout is touched once, later, for confirmation only.
- Full command log: this ticket's `scratch/notes.md`.

Residual (parked, recorded in research): TICK-217's migrated acceptance items (per-field thresholds, zero-false-case confirmation, operator decision) need a human-labelled ground-truth cohort not present on this machine plus an operator ruling.

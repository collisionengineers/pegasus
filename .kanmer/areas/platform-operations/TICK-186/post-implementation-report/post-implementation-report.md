# Post-implementation report — TICK-186

## What was built

A deterministic extraction-development cohort / untouched-holdout split over
the 256 `.eml` files actually present in this machine's local, gitignored
`corpus/` directory, plus the reproducible script and manifest that fence the
holdout. Local-only evidence artifact under `artifacts/evaluation/
extraction-cohort/20260820/` — `artifacts/` is gitignored, so there is no
repository diff and no PR for this ticket.

## Result

- 256 total `.eml` files (no PDFs/`.doc`/`.msg` at the corpus top level; two
  stray non-corpus tooling files excluded).
- Split: SHA-256 content digest, ascending sort, cut at `floor(0.8×256)=204`.
- **204 cohort** (extraction-development) / **52 holdout** (untouched).
- No duplicate-content group straddles the split boundary (checked; 4 groups
  found, all single-sided).
- Rebuild command reproduces a byte-identical `manifest.csv` (verified).

## Deviation from the checklist's migrated TICK-217 items

The ticket's checklist doc carries TICK-217's migrated acceptance steps
(review cohort evidence against fixed per-field thresholds, confirm zero
false case creation, record accepted thresholds/operator decision). None of
those are checked. They require a human-reviewed, labelled ground-truth
cohort (the `extraction-corpus/QDOS/{audits,inspections,inspection-and-audit,
triage}` tree, absent on this machine) and an operator acceptance decision —
neither exists here. Rather than fabricate labels or guess thresholds, this
ticket's actual scope stopped at assembling the cohort/holdout split itself,
which is what its own body text (re-check the current source/evidence state,
plan first, no invented authority) asks for. This is recorded verbatim in
`research.md` and `plan.md`.

## Governing docs touched

None changed. `docs/frd/frd-02-intake-and-source-identity.md` linked as this
ticket's governing doc (INT-21's canonical owner per `docs/capabilities.md`).

## Simplification pass

n/a — no application/repository code changed; local evidence-artifact
assembly only (recorded in `plan.md`).

## Tests

None applicable — no code changed.

## Next

- The migrated TICK-217 checklist items remain open and parked, pending a
  labelled corpus tree and an operator threshold decision.
- No code currently reads `manifest.csv` to actually withhold the holdout
  during development; wiring that is follow-on work once extraction-dev work
  starts.

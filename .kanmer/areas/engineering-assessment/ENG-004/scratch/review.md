## Independent review — PR #437 (orchestrator, 2026-08-20)

Verdict: **pass**. All CI green.

- Three-part fix at the right altitude, all in the shared engine + one policy: (1) label matches only at line start / after a column separator / after 2+ spaces — kills mid-cell "Make" hits; (2) values truncate at the first flattened-column boundary (tab, pipe, double-space, whitespace-preceded colon) — "AUDI NSF : Footbrake : SATISFACTORY" → "AUDI NSF" before validation; (3) an optional per-field `AcceptsValue` validator (the seam the plan named), with make/model rejecting wheel-position/MOT vocabulary and non-name charsets.
- Tests pin the exact production line shape, the boundary truncation, the mid-line single-space non-label, and — importantly — that a real instruction fragment now wins over an appended MOT table *without* a conflict null-out. That also pre-fixes part of INTK-017's "most fields blank" mechanism.
- Trade-off accepted and noted: an in-value double space (rare extraction artifact) now truncates; deterministic precision beats recall here, and the validator would reject junk anyway.

# Plan — UIIMP-007

Docs-only. Diff estimate: ~200 lines added / ~40 changed across four owned
files; no code, no tests, no ADR.

## Premises (read-only checks, 2026-08-28)

- `docs/capabilities.md` has 232 capability rows today (mechanical count:
  Now 135, Next 29, Later 39, Not planned 29; `1.0.0` = 12, `0.7.0` = 0
  after the EXT-04 bring-forward), while the summary still says 231 /
  132 / 29 / 41 / 29 and the release table says `1.0.0` 13, `1.1.0` 6,
  `0.7.0` 1. [[DELIV-018]] records that drift. This ticket changes the
  rows it owns and states the correct sums after the change.
- `docs/index.md` has no row naming Queues or Dashboard; the design row
  exists ("What are the UI rules?").
- No capability ID covers the Engineer Report except `MI-01` (per-Engineer
  throughput, query rate/types) — reallocate it rather than add a new ID.
- Inbound links to FRD-12 use only `#operator-experience` (34) and
  `#dashboard-freshness-and-reconciliation` (2): both headings are kept.

## Steps

1. FRD-12: rewrite to the FRD template (Purpose / Behaviour / States /
   Edge cases / Acceptance / Links) carrying every existing normative rule
   plus the EPIC-011 §1 routes and IA (Work Centre, Cases, Search, Case
   workspace, Assessment, Operations, Administration, workspace tabs,
   command palette, keyboard map, breakpoints, removed routes, redirects,
   display labels citing FRD-01). Behaviour only; visuals cite
   `docs/design/README.md`. Reuses the existing heading slugs.
2. capabilities.md: add `UI-16`; move `AI-10`, `EXT-09`, `EXT-10`, `MI-01`
   to `Now / 0.1.0-alpha.1`; D7 notes on `ENG-01`, `EXT-13`, `EXT-01`;
   recompute the allocation summary, release table and the ordered release
   sequence; bump the provenance count.
3. boundaries.md: AI-assistance row — shared AI job ledger moves in scope
   under ADR-0035 (AUTO-009), referenced in prose, no link. Automated
   correspondence row untouched (MAIL-024).
4. index.md: design row names the shell contract; FRD-12 named as the
   route/IA behaviour owner.
5. `pwsh ./scripts/Test-DocumentationLinks.ps1` must pass.

## Out of scope

`docs/open-decisions.md` (referenced, not edited), FRD-01 label mapping
(PLAT-047), ADR-0035 (AUTO-009), FRD-08 / correspondence row (MAIL-024).

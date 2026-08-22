# Checklist — INTK-025

- [x] `GuardedPrefixes` mechanism; `TP ` literal out of the engine (grep = 0)
- [x] `WithReportFacts` (report-named fragments only; letter outranks; run-together columns cut; digit-guarded Speedo)
- [x] `WithCircumstances` (line-final "circumstances?" anchor — the reader can wrap the prompt; terminators Damage Area / Pre-existing / TP / If-you-need)
- [x] Version 3 → 4
- [x] Unit facts 35/35; corpus mapping + coverage 8/8 (circumstances pinned for the four engineer letters; audit letters carry no prompt — mapped)
- [x] Simplification pass (below)
- [x] Full build + Core + intake suites green
- [x] PR to dev merged on green CI — shipped and deployed to production

## Simplification pass — 2026-08-21

- One `with`-projection applies the third-party guard to every definition
  instead of twelve edits; the guard is a single interpolated lookbehind in
  the engine built from policy data. Applied.
- `WithSubjectFacts` takes the derived base list rather than a second
  content copy; the fragment pipeline has one builder. Applied.
- Report and circumstances synthesis reuse the existing
  synthesized-labelled-lines pattern — no engine change for either rule.
- Speedo: approved rule with no value-bearing corpus instance; implemented
  digit-guarded, positively tested only synthetically (recorded exception to
  the corpus-only methodology).

## Progress notes

2026-08-21: a shell-heredoc edit corrupted `\b` in the prompt regex into a
literal backspace byte (0x08) — invisible in every text view; found by byte
scan after the corpus run went dark. All touched files byte-audited clean.
Editing rule reaffirmed: source edits go through script files, never inline
heredocs.

2026-08-22: the last two boxes were stale — the PR merged and the rules have
been on production since release 16. Ticked against deployed evidence, and the
extraction is now observed on a live instruction (see proof).

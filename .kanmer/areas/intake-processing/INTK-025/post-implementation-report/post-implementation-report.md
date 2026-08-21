# Post-implementation report — INTK-025

Delivered as planned on `task/intk-025-qdos-report-rules` (PR #501).
Deviations and finds:

- **Corpus find:** audit letters ("AUDIT REPORT NOTIFICATION") carry the
  Damage Area block but **no circumstances prompt** — only engineer letters
  do. Pinned per mapped file; empty circumstances on audit instructions is
  correct behaviour, not a miss.
- **Prompt anchor loosened** to the line-final "circumstances?" because the
  reader can wrap the prompt across physical lines.
- **EREF8's make/model resolved** (VAUXHALL ASTRA GS TURBO) — verified
  against the letter as the client's own car (the TP row is a DACIA and
  stays guarded); the resolution came from INTK-023's prefix subsumption and
  is now pinned rather than silently null.
- **Speedo** implemented digit-guarded with no value-bearing corpus instance
  — recorded exception to the corpus-only methodology.
- **Tooling incident recorded** (checklist): a shell-heredoc edit turned the
  prompt regex's `\b` into an invisible backspace byte; found by byte scan,
  all touched files audited clean, editing rule reaffirmed (script files
  only) and saved to session memory.
- Verification: engine grammar-free (grep 0); Core 874/874; corpus mapping +
  coverage + acceptance + intake suites 25/25; build 0/0. Live proof at the
  next release: a fresh audit instruction lands make/model from its report,
  an engineer instruction lands its circumstances paragraph.

Self-reviewed; subagents barred by operator directive (deviation noted).

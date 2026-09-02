# Plan — DELIV-040 review remediation

## Objective

Apply the operator dispositions on PR #643 without changing application code.
Reuse the existing branch, worktree and PR; update the governing documents,
EPIC-011 contract, affected Kanmer tickets, and both external work-pack plan
sets. Stop at a refreshed PR in Review for an independent delta review.

## Repository changes

- Clarify the dark-feature convention in `AGENTS.md`, engineering guidance and
  the design guide: an implemented backend behind a closed gate is not
  delivered; a ticketed backend-less control may be shown disabled and inert.
- Defer all of D18 to a new Documents & Reports ticket and restore the current
  accepted signature-tuple/readiness contract in FRD-04, FRD-11, FRD-12,
  capabilities, open decisions and the design guide.
- Permit full manual estimates and direct edits/overrides of draft, accepted,
  current and imported normalized estimates. Retain the raw artifact/hash and
  record attributed before/after history.
- Withdraw the proposed upload limits. Keep the current 10 MiB policy truthful
  and defer any replacement to INTK-052 requirements, Azure, performance and
  cost research.
- Define one upload-link session: first successful file starts a fixed,
  non-sliding 15-minute TTL; retry and replacement are allowed until explicit
  finalization or expiry; closure accepts no later bytes.
- Apply chase/completeness changes after a warning and confirmation; open work
  is recalculated at next evaluation. Add no migration/version-retention rule
  for hypothetical live cases.
- Keep raw PDF/XML `pegasus_estimate_import` separate from structured
  AI-draft `pegasus_estimate_save`; only the latter requires a taken Estimate
  job.
- Change the optional target to 0–80%, remove keyboard-map scope unless
  explicitly authorized, remove canonical-source claims for the external HTML,
  and correct the capability census to 205.

## Board and work-pack changes

- Update EPIC-011 D7, D16, D17, D18, D20, D23 and D24.
- Amend UIIMP-012, TICK-082, ENG-033, AUTO-016, PLAT-062, INTK-050,
  INTK-052, TICK-081, TICK-097, DOCS-001 and archived TICK-216.
- Create DOCS-017 for the complete deferred D18 scope and INTK-055 for
  retryable upload-link sessions.
- Synchronize README, decisions, ticket map, UI reconciliation, acceptance
  matrix, and both Codex and Claude orchestration plan/ledger pairs. Do not
  modify historical run records or source HTML/DOCX/PDF evidence.

## Verification

Run the documentation link/placement/catalogue checks, locked restore, Release
build and non-Corpus tests. Parse both YAML ledgers, verify their ticket ids
against Kanmer, and sweep for superseded D18, upload-cap, 0–100, immutable
normalized-estimate, migration, blanket AI-job and keyboard-map claims.

## Simplification pass — 2026-09-02

n/a — docs-only. Prefer deletion of superseded clauses and reuse the existing
FRD owners and ticket boundaries.

## Stop condition

Commit and push the remediation to the existing PR #643, resolve every review
thread with a public disposition, write the refreshed post-implementation
report, and move Implementing → Review. Do not merge, deploy, verify, clean the
worktree, or start implementation of any deferred ticket.

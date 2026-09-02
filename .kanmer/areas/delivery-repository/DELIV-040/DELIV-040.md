---
id: DELIV-040
type: ticket
title: Record the 2026-09-01 operator interface decisions in the governing documents
status: implementing
area: delivery-repository
assignee: claude-code/20260901T215000Z-claude-controller/implementer-a1
profile: chore
stageEntered:
  preparing: '2026-09-02T00:56:48.183Z'
taken_at: '2026-09-02T01:27:49.169Z'
branch: task/deliv-040-governing-docs
worktree: ../pegasus-worktrees/deliv-040-governing-docs
claim_expires_at: '2026-09-02T01:57:49.169Z'
claim_controller: claude-code/20260901T215000Z-claude-controller/implementer-a1
lease_id: fe0dd564-63e7-456c-8ec1-1e3c86ff095d
lease_revision: 1
lease_workspace: >-
  worktree:c:\users\pguser\documents\github\pegasus-worktrees\deliv-040-governing-docs
lease_phase: implementing
lease_heartbeat_at: '2026-09-02T01:27:49.169Z'
labels:
  - docs
  - governing-docs
  - work-pack
  - phase-0
groups:
  - EPIC-011
links:
  - KANMER-009
  - UIIMP-012
  - INTK-050
  - PLAT-062
  - TICK-082
  - ENG-031
  - PLAT-064
blocks:
  - ENG-033
  - INTK-054
  - TICK-082
  - PLAT-059
  - PLAT-064
  - ENG-031
  - PLAT-062
  - INTK-052
  - MAIL-030
  - MAIL-031
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-03-triage.md
  - docs/frd/frd-04-parties-accounts-and-access.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/frd/frd-12-operator-experience.md
  - docs/capabilities.md
  - docs/boundaries.md
  - docs/open-decisions.md
  - docs/design/README.md
commits:
  - 48cb2816
  - '33811713'
  - d0527096
  - d161ae1e
  - 399f1ac8
  - 4525afcc
  - 436b38dc
  - 4089599e
  - bd140c7c
  - e670d4fd
  - 1441550f
  - 25c14574a9e34c77e977f8a8eb203c2fe85dc13e
deployment: n/a
archived: false
created: '2026-09-01T21:54:35.752Z'
updated: '2026-09-02T02:50:56.917Z'
---

## What

Edit the existing canonical documents so they record the operator decisions D15–D28 (EPIC-011 `decisions/2026-09-01-work-pack.md`, "Interface decisions confirmed binding on 2026-09-01"). No new Markdown file; `docs/operator-notes.md` is not touched (checked: no conflicting statement).

## Why

The decisions are binding but the documents conflict with or are silent on them: FRD-11 defers image curation (UI-15) and accepts only exact signature tuples; `docs/design/README.md` D7 draws Glass's/Audatex as disabled seams; FRD-11 says no accepted rate formula exists beside its own VAT arithmetic; FRD-04 has no administrator reset; FRD-08 carries a provisional freshness; the upload limits disagree across documents. A feature ticket that depends on a decision leaves Backlog only after this ticket merges.

## Approach

One PR to `dev` under the `governing_docs` lock, edits per decision:

| Decision | Files and sections |
| --- | --- |
| D17 rate cards, VAT on the whole subtotal, no comparison/savings, betterment and guide codes evidence only | FRD-06 § Canonical repair specifications and § Professional engineering findings; FRD-11 figures table, the "not yet derivable" paragraph and the readiness line; FRD-12 § Assessment and § Administration; FRD-04 Administrator column; `capabilities.md` EXT-09 and RPT-02; `open-decisions.md` rate-card row; design README Assessment fields and admin nav |
| D18 any Engineer issues a report with typed identity only | FRD-11 signature-tuple paragraph and readiness; design README signature assets (retained, inactive); `capabilities.md` RPT-02; FRD-04 Engineer role |
| D22 mail freshness fixed at 15 minutes, no backfill, delete is a move to Deleted Items | `open-decisions.md` freshness and retention rows resolved; FRD-08 freshness and workspace sections; `capabilities.md` UI-10 |
| D23 completeness as a versioned required/not-required set; chase interval 1–365 days default 7, Europe/London, Held preserves | FRD-01 policy lines and the seven-day clause; `capabilities.md` CASE-18; runbook release-validation bullet; design README workflow configuration |
| D24 AI target estimate optional 0–100 %, no default, guidance only | FRD-11 Estimate job row; design README Send to Claude dialog; `open-decisions.md` |
| D20 uploads 100 MB per file, about 200 MB per request, provider envelope 30 MB, public link one success, staff upload only with durable custody, one grouped decision plus per-file details | engineering tier 10; FRD-05 limits; FRD-12 § Upload (resolves [[INTK-050]]) and public link; FRD-02 upload confirmation and INT-31; `capabilities.md` INT-31 |
| D16 whole-page pointer drop and MCP raw-artifact import | FRD-12 Assessment line and acceptance evidence (narrow pointer-only exception); design README (dialog removed) and keyboard contract; FRD-10 tool table; FRD-06 provenance and replay; `capabilities.md` EXT-12 and MCP-06 |
| D25 Triage History with append-only notes; Files without upload | FRD-03; FRD-12 Triage section; design README Triage |
| D26 direct Case creation with an attributable instruction receipt | FRD-02 § Ways intake starts and INT-26; FRD-12 Add dialog |
| D19 report images (Close-up, Overview, non-destructive crops, immutable issued snapshot) | FRD-06, FRD-11 (the UI-15 deferral paragraph), FRD-12; `capabilities.md` |
| D21 excluded capabilities are absent, not disabled; launch controls removed; D27 capacity tier not run; D15 canonical visual source | `capabilities.md` OPS-20, ENG-01, EXT-13; `boundaries.md` new rows; design README § Absent versus disabled (D7 amended) |
| D28 administrator password reset | FRD-04 § Staff role access (temporary password, forced change, permanent history, no email); `capabilities.md` ACC-03 or a new id; design README Accounts area |

OCR (`prebuilt-layout`, no raw response retained, measured threshold) stays with [[TICK-041]] and [[PLAT-065]] (ADR-0037, FRD-05, FRD-07, boundaries).

## Verification

- [ ] Every row's files carry the decision; `docs/open-decisions.md` rows that the decisions settle are resolved.
- [ ] `scripts/Test-DocumentationLinks.ps1` and `scripts/Test-MarkdownPlacement.ps1` pass; no new Markdown file.
- [ ] Simplification pass: n/a — docs-only.
- [ ] Independent review confirms no unauthorized scope.

## Outcome

---
id: INTK-050
type: ticket
title: >-
  FRD-12 and FRD-02 still require per-file grouped upload outcomes; INTK-011
  replaced them with one submission-level decision card
status: backlog
area: intake-processing
order: 500
assignee: ''
profile: chore
labels:
  - docs
  - frd
  - intake
  - stale-contract
groups:
  - EPIC-011
links:
  - INTK-047
  - INTK-046
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-29T17:45:41.473Z'
updated: '2026-09-03T15:15:27.920Z'
---

## What

Two governing documents describe upload behaviour the application stopped
having:

- `docs/frd/frd-12-operator-experience.md:358-359` — the Upload section requires
  grouped members to carry per-file outcomes and says they "are never collapsed
  into one group-wide outcome".
- `docs/frd/frd-02-intake-and-source-identity.md:264-267` — the same requirement,
  stated independently.

`Pages/UploadGroupStatus.cshtml` has presented a **single submission-level
decision card** since INTK-011.

## Which side is wrong

**The documents.** Two independent reviewers reached the same verdict: the
per-file wording is stale, and the submission-level card is the intended
behaviour that INTK-011 deliberately shipped. This is a documentation
reconciliation, **not** a code change — do not "fix" the page back to per-file
outcomes on the strength of the FRD text.

Confirm that reading before editing. If the operator actually wants per-file
outcomes, this becomes a behaviour ticket instead and needs their decision
first — say so and stop rather than guessing.

## How it surfaced

[[INTK-047]] found the conflict while porting the upload pages and correctly did
**not** touch it — the discrepancy predates the ticket and belongs to neither
INTK-047's files nor its brief (AGENTS.md rule 2). Its cross-model reviewer then
independently confirmed the conflict exists in **both** FRDs, not just FRD-12,
and located the second occurrence.

## Approach

- Reconcile both FRDs to the shipped submission-level behaviour, in the same
  change — one concept, one correction, both places. Leaving one stale
  reintroduces the same contradiction from the other direction.
- Check whether `docs/design/README.md` carries the same per-file claim before
  assuming two files is the whole set.
- Governance note: this edits existing canonical FRDs. It creates no new
  Markdown file, so the New Markdown placement rule is satisfied.
- Markdown convention: H1 on line 1, a blank line before every heading, hard
  wrap near 78 columns.

## Verification

- [ ] No governing document still requires per-file grouped upload outcomes.
- [ ] The corrected wording matches what `UploadGroupStatus.cshtml` actually
      renders, quoted with a `file:line`.
- [ ] `scripts/Test-DocumentationLinks.ps1` passes.
- [ ] No behaviour changed by this ticket.

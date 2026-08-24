---
id: DOCS-013
type: ticket
title: Record the operator export as an artefact distinct from the EVA hand-off
status: backlog
area: documents-reports
assignee: ''
profile: chore
labels:
  - qdos26015
  - eva
  - export
  - governance
links: []
blocks:
  - ENG-014
  - ENG-015
refs:
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
archived: false
created: '2026-08-24T08:19:34.397Z'
updated: '2026-08-24T08:19:42.975Z'
---

## What

Amend FRD-07 so it describes two artefacts, not one: the gated EVA hand-off
(manifest mandated) and the [[CASE-019]] operator export (JSON and images only).

## Why

FRD-07 predates the operator export and describes only the hand-off. Its line 45
reads:

> The container format is intentionally unspecified: its selection must evaluate
> whether a single archive is the clearest usable representation **without
> changing the exact package contents, manifest**, or manual-handoff boundary.

The packaging ticket changes exactly that, for the export. Without this
amendment the code and the governing doc disagree, and the next reader cannot
tell which artefact the manifest requirement binds.

FRD-07 also lists `Reference` as a key but has never said *which* reference —
that choice has only ever existed in code, which is how the export came to carry
the Pegasus case reference instead of the provider's.

## Approach

- Add the operator export to `docs/frd/frd-07-eva-and-external-engineering-handoff.md`
  as a distinct artefact: JSON plus images, no manifest, not a hand-off, records
  no revision and no `First sent to Engineer` proxy.
- State explicitly that the manifest requirement binds the hand-off only.
- State which reference the `Reference` key carries.
- `docs/operator-notes.md` is **protected**. Line 505 describes the hand-off, not
  the export, so no meaning changes — confirm that by reading it before editing,
  and stop for user resolution if it turns out otherwise.

Docs-only: the simplification pass records "n/a — docs-only".

## Verification

- [ ] FRD-07 names both artefacts and no longer reads as forbidding the export change
- [ ] `docs/operator-notes.md` meaning unchanged
- [ ] `docs/index.md` authority chain still resolves

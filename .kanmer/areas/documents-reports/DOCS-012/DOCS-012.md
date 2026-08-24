---
id: DOCS-012
type: ticket
title: 'Show case evidence on the Evidence tab, not the document custody ledger'
status: backlog
area: documents-reports
assignee: ''
profile: fix
labels:
  - found-during-qa
  - ui
  - design
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-23T15:19:38.272Z'
updated: '2026-08-24T09:56:57.069Z'
---

## What the operator saw

> *"**Issue 2** — Document custody box in evidence is unneeded detail. We only
> need to see the files that are considered case evidence. If they show here,
> they should be on box."*

## Answered by the operator, 2026-08-24

> *"1. Include button for box folder. Per file 'custody state' is dev speak
> leaking into UI. Just show whats been stored. Keep it simple.*
>
> *2. Changes go in notes (created by system same as other automatic notes)*
>
> *3. The controls need rework:*
> - *Theres an export control that isnt needed.*
> - *Removal can just be a delete/trash icon next to each file/image*
> - *"retain document" as a control makes zero sense. Its already stored so how
>   can it be retained?*
> - *Semantic role shouldn't be user configurable"*

## What this means, control by control

| Today | Becomes |
| --- | --- |
| Box folder link | **Keep.** A button. |
| Per-version `Custody` column | **Gone.** Internal vocabulary. That a file is listed *is* the statement that it is stored. |
| `Revision state` column | **Gone.** Superseded by the notes rule below. |
| `EVA eligibility` cell, *"Eligible unless staff confirms third-party vehicle evidence"* | **Gone.** A how-it-works sentence in a table cell, banned outright. |
| Selective export by version | **Gone.** The whole-case Export on the case header is a different action and stays. |
| `Remove occurrence` form | **A delete control per row.** |
| `Retain document` upload | **Gone.** The reasoning is exact: it is already stored. |
| Confirm third-party vehicle evidence | **Open — see below. My earlier reading of this was wrong.** |

## Correction: the third-party control is not a semantic-role control

I originally mapped *"semantic role shouldn't be user configurable"* onto the
third-party confirmation and wrote it down as removed. **That was wrong**, and
research caught it.

That control sets no role. It sets `ThirdPartyVehicleConfirmedAtUtc`, and
`EvaHandoffStore` includes a Case image in the EVA bundle only while that value
is `null`. The single writer is this staff confirmation; **nothing sets it
automatically.**

So removing the control removes the only way to keep a third-party vehicle's
photograph out of the bundle sent to the engineer. `docs/design/README.md` also
requires Case evidence to show *"staff-confirmed third-party exclusions"*.

**Recommendation:** delete the banned `EVA eligibility` explanatory column, keep
the exclusion capability, and let the operator say whether the control itself
should go — knowing that if it does, third-party images start reaching EVA.

If *"semantic role shouldn't be user configurable"* meant some other control,
the research found no second candidate; the roles are assigned by custody, not
chosen.

## The notes rule, and the trap under it

Document changes are recorded as **case notes created by the system, in the same
shape as the other automatic notes** — not as a revision column.

The Notes tab renders `CaseWorkflowEvents`. There is a **second, write-only
table, `CaseHistory`, that nothing operator-facing reads** — and the existing
`custody_confirmed` / `custody_failed` writes go there, which is why document
custody events have labels and never appear. Writing to the wrong table already
shipped once as a production defect in Release 22: the page said the note was
added and the tab stayed at zero.

No document operation writes a visible note today. Logical removal writes nothing
but the version flags.

## Also found

- The current row loop is a **cartesian product** of document × occurrence ×
  version, so a document with two occurrences and three versions renders six
  rows. Fixed by joining on the occurrence's version.
- `OperatorLabels.DocumentRole` and `DocumentOrigin` already exist with **zero
  callers** — the rewritten table uses them instead of printing raw enum names.
- The delete icon needs a **seventeenth Lucide glyph**. The registry is a closed
  sixteen with checksums, and hand-drawn or substituted glyphs are banned, so the
  authentic `trash-2` must be fetched and both checksums amended.
- Blast radius per handler is mapped in the plan. `IAddCaseDocument` keeps two
  other production callers, so only the handler goes — which breaks an
  architecture test that pins the port to the page model's constructor.

## Sequencing

Lands before [[DOCS-011]]. [[CASE-022]] owns the upload-request section of the
same file.

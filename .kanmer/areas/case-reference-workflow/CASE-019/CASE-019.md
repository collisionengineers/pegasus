---
id: CASE-019
type: ticket
title: Make Export download the EVA-format case bundle
status: backlog
area: case-reference-workflow
assignee: ''
profile: fix
labels:
  - qdos26011
  - operator-requested
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-22T19:42:25.356Z'
updated: '2026-08-22T19:42:25.356Z'
---

## Why — operator direction (2026-08-22, QDOS26011)

> "The case is not able to be exported. The export button seems to just go to the evidence page. The evidence shows in a selection of tickboxes, but these are unclickable."

> "What export should do: download a zip of the images, and a json that matches the exact EVA import requirements and format that has been shown in the repository."

## What already exists

`EvaBundleSchema.CreateOfflineReplay` produces precisely that archive and does not need writing:

```
EVA-QDOS26011.json     the 13 EVA fields, fixed order
Images/000 <name>.jpg  each retained photograph
provenance.json        per-field value, status, source, source version
manifest.sha256        SHA-256 of every entry
```

This ticket wires the Export control to it.

## Why it does nothing today — four separate faults

1. **The link cannot be generated.** `Details.cshtml` emits `asp-route-id`, but the Export page's route is `/Cases/{caseId:guid}/Documents/Export`. `caseId` is never supplied and is not an ambient value of `/Cases/{id:guid}`, so no `href` is produced and the control is inert.
2. **The tickboxes are `disabled` unless an edit lease is held** (`_CaseDocuments.cshtml`), and the submit button is not rendered at all without one. Exporting is a read, and must not require edit authority.
3. **No photograph is eligible** — see [[DOCS-009]].
4. **The bundle refuses without an accepted mapping**, which is unset in production — see [[PLAT-037]].

## Decisions taken (operator, 2026-08-22)

- **Missing inspection date defaults to today**, recorded in provenance as a system default, mirroring the existing `SystemDefault:Receipt date` treatment of `instruction_date`.
- **A blank field does not block the download.** All 13 keys are always present in exact order; a field the case genuinely lacks is emitted empty and named on the Export control beforehand. QDOS26011's only remaining blank is VAT status, which has no provider default and is neither extracted nor staff-entered.

## Scope

- Export is a case action available in Review, needing casework rights and no edit lease.
- It selects every eligible retained photograph itself; the operator does not tick boxes to get a case export.
- It is an operator download, not an external hand-off: it does not create an `EvaHandoffRevision`, does not write an `EvaFirstHandoffProxy`, and leaves the gated hand-off panel untouched.
- Keep the existing selective document export reachable for the case where an operator wants specific versions.

## How to verify

Pressing Export on QDOS26011 downloads `EVA-QDOS26011.zip` containing eight photographs, a 13-key JSON carrying the extracted claimant, claim number, registration, model, incident date and circumstances, the lookup-derived mileage, today's inspection date, and an empty VAT status; `manifest.sha256` matches every entry.

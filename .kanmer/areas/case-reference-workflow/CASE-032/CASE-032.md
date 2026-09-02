---
id: CASE-032
type: ticket
title: 'Queue-row projections: image-intake custody and Triage reference/provider'
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - backend
  - queues
  - rule-14
  - wiring
groups:
  - EPIC-011
links:
  - CASE-025
blocks:
  - CASE-025
  - CASE-042
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-01-case-identity-and-lifecycle.md
archived: false
created: '2026-08-29T13:04:56.182Z'
updated: '2026-09-02T20:33:37.483Z'
---

## What

Add the two queue-row projections that `.kanmer/groups/EPIC-011/context.md`
§1.4 requires and that no Core summary carries, then render them in the rows
[[CASE-025]] shipped:

1. **Image-initiated row — `files·custody`.** §1.4 (context.md:44) requires
   "image-initiated: ref·reg, files·custody". Add the custody half to
   `ImageIntakeSummary` and render it beside the file count.
2. **Triage row — `ref` and `provider`.** §1.4 requires "triage: ref·reg,
   provider·assignee". Add the Triage reference and the provider to
   `TriageSummary` and render `ref·reg` as the title and `provider·assignee`
   as the meta.

## Why

CASE-025's `## What` names "per-kind rows" ported to §1.4, and two of the four
named row halves have no implementation at all — not merely no caller. The
GPT-5.6 adjudication of 2026-08-29 reversed CASE-025 out of Done on this, and
found **no board ticket supplies them**: [[INTK-046]]'s What/Owns cover only
`Pages/Triage/Details`, `Pages/Unidentified/Details`, `Pages/Intake/**` and
`Pages/ImageIntake/Details` — no Core queue projection — and both Core
summaries are unchanged on merged `dev` at `b92cb9a7`. [[INTK-037]] scopes
Triage *detail* identities, not the queue row.

Evidence recorded by the audit:

- `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:543-559` renders
  `Join(ImageIntakeReference, NormalizedVehicleRegistration)` and
  `"{fileCount} retained image…"` and nothing else; `git grep -i custody` over
  both owned Cases files returns no hits.
- `src/Pegasus.Core/ImageIntake/ImageIntakeContracts.cs:100-109` —
  `ImageIntakeSummary` carries Id, OriginReceiptId, ImageIntakeReference,
  NormalizedVehicleRegistration, AssociatedCaseId/Reference, RegisteredAtUtc,
  State, ClosureReason. No custody field.
- `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs:560-575` titles the Triage row
  with `item.NormalizedVehicleRegistration` alone and metas `assignee` alone.
- `src/Pegasus.Core/Triage/TriageContracts.cs:271-278` — `TriageSummary` is
  `(Guid Id, string NormalizedVehicleRegistration, TriageState State,
  Guid? AssigneeId, Guid? LinkedCaseId, DateTimeOffset CreatedAtUtc,
  long Version)`. No reference, no provider.

## Approach

- Extend the two existing Core summaries and their EF projections; add no new
  query type and no second list-shaping path.
- Reuse the existing reference and provider vocabulary — the Triage reference
  and the provider name already have owners in Core; name them in the plan
  rather than introducing a display string in the Web layer.
- Render through the row builders CASE-025 already ships in
  `Pages/Cases/Index.cshtml.cs`; the row shapes stay as ported.
- Keep the projection query count unchanged — fold the new fields into the
  existing queue reads, not a per-row lookup.

## Verification

- [ ] The image-initiated row renders `files·custody` from a queried custody
      value, not a computed placeholder.
- [ ] The Triage row renders `ref·reg` and `provider·assignee` with all four
      halves from `TriageSummary`.
- [ ] Queue page query count is unchanged (no N+1 introduced).
- [ ] `TriageQueuesWebTests` asserts each new half against seeded data.

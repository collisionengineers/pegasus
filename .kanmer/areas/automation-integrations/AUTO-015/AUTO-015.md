---
id: AUTO-015
type: ticket
title: >-
  pegasus_assessment_update can overwrite or clear any assessment field
  unconfirmed, including assessment.values.engineer
status: backlog
area: automation-integrations
order: 30
assignee: ''
profile: fix
labels:
  - backend
  - mcp
  - automation
  - one-owner
  - live-gate-open
groups:
  - EPIC-011
links:
  - ENG-027
  - AUTO-011
refs:
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
archived: false
created: '2026-08-29T17:02:51.859Z'
updated: '2026-09-03T15:15:26.924Z'
---

## What

`src/Pegasus.Web/Mcp/AssessmentMcpTools.cs:323,337` — `pegasus_assessment_update`
accepts `Dictionary<string, string?>? fields` where "a null value clears the
field", with **no path allowlist**. An Automation Actor can therefore overwrite
or clear any assessment field, `assessment.values.engineer` included, and the
write is unconfirmed because `AssessmentPolicy` only confirms on the staff
branch.

## Why this matters now

[[ENG-027]] made `EfValuationStore` the owner of the valuation-driven write to
`assessment.values.engineer`. After that ticket the field has **two writers**:

- `EfCaseAssessmentStore` via `ISaveAssessment`, reachable from this MCP tool
- `EfValuationStore`, driven by the `CaseValuations` rows

They can silently diverge, and nothing re-syncs them until the next valuation
write. Three production consumers already read the field —
`src/Pegasus.Core/AiWork/AiJobOperations.cs:304`,
`src/Pegasus.Core/Reports/AssessmentReportProjection.cs:194`, and
`src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs:165` — so a divergence
reaches an AI job, a report projection, and the Assessment page.

**This path is live, not gated.** `docs/operations.md` records
`Features:AutomationMcp` as enabled in production since release 9 (2026-08-18),
so decision D21's "gate is open in the deployed estate" row applies: this is a
real production writer, not a closed seam.

## How it was found

An independent cross-model reviewer raised it while verifying ENG-027's round-3
remediation. It is **pre-existing** — ENG-027 did not introduce it — and sits
outside that lane's owned files, so it was correctly not absorbed there
(AGENTS.md rule 2). ENG-027's plan qualifies its "one owner" claim accordingly
rather than asserting an ownership it does not have.

## Approach

Search before building — read how the staff branch confirms, and reuse it.

- Decide the intended contract first: should an Automation Actor be able to write
  `assessment.values.*` at all, and if so must the write be confirmed or marked
  unconfirmed-by-automation? `docs/frd/frd-10-mcp-automation-and-actor-boundary.md`
  is the authority on the actor boundary; do not invent policy beyond it.
- The rule belongs in `Pegasus.Core` — a second implementation of a business rule
  in the Web layer is a stop condition.
- If a path allowlist is the answer, it is **one list**, in one place, not a copy
  per caller.
- Consider whether the valuation-owned field should simply be refused to this
  tool, which is the smaller change if the contract allows it.

## Verification

- [ ] An Automation Actor cannot leave `assessment.values.engineer` diverged from
      the `CaseValuations` rows, or the divergence is impossible by construction.
- [ ] The refusal (or the confirmation semantics) is exercised by a test that
      fails on the pre-fix code.
- [ ] The rule lives in `Pegasus.Core` with one owner, and the MCP tool calls it.
- [ ] No existing assertion is weakened to accommodate the change.

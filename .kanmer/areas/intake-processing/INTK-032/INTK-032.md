---
id: INTK-032
type: ticket
title: Fall back safely when a third-party report format cannot be read
status: backlog
area: intake-processing
assignee: ''
profile: feature
labels:
  - qdos26009
  - extraction
  - audits
links:
  - INTK-031
  - INTK-056
  - TICK-041
  - PLAT-065
  - CASE-038
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-21T23:30:27.932Z'
updated: '2026-09-02T20:34:07.216Z'
---

## Why — operator direction (2026-08-22)

> "add a new ticket accounting for situations where we cannot extract details from the third party engineer report e.g. we've not had that format in before — will plan in more detail the outcome on this specific ticket at a later time."

The original report accompanying an audit instruction is written by a different engineering firm each time. [[INTK-031]] builds the issuer corpus so known layouts are recognised; this ticket owns what happens for the ones that are **not**.

## Why it matters more than it looks

The audit reference prefix depends on whether the report says Repairable or Total Loss. If that fact cannot be read, the case cannot be given its reference — so an unreadable report is not a cosmetic gap, it blocks allocation. Fail closed rather than guessing a prefix that is immutable once allocated.

## Operator direction (2026-09-02) — the fallback is Document Intelligence

> "We should implement a fallback of using document intelligence. This report uses a 'Vehicle Details' table with: Status Repairable, Legal Status Roadworthy."

Recorded outcome for an unrecognised or unreadable third-party report format: send the report through the provider-neutral Azure Document Intelligence port that [[TICK-041]] defines (`prebuilt-layout`, pinned GA version, response hash retained) and that [[PLAT-065]] provisions and activates, and read the outcome from the layout result's **table cells** — a Vehicle Details row whose label is `Status` and whose value states Repairable or Total Loss — rather than from free text. Low confidence, no status cell, or a cell stating both still fail closed to staff review; nothing here invents a prefix. The bounded text-rule fix for the report that triggered this (Unidentified U45, 2026-09-02) is [[INTK-056]]; this ticket is the path for the formats that fix cannot reach.

Sequencing: blocked on TICK-041's accepted ADR and port and on PLAT-065's live activation; until then the existing behaviour (abstain → Unidentified) stands.

## Scope for now

The detailed design of the fallback route (which read result feeds the table lookup, how the issuer registry from [[INTK-031]] narrows the search, what the operator sees) is planned on this ticket when TICK-041's port exists. What this ticket records now is the requirement: an unrecognised report format must produce a clear, actionable operator state rather than a silent partial extraction or an invented value, and the fallback mechanism is Document Intelligence layout extraction.

## How to verify

- [ ] A report of an unknown format with a readable Vehicle Details `Status` cell classifies through the Document Intelligence path with the response version/hash and confidence retained as evidence.
- [ ] Low confidence, no status cell, or both outcomes in the cell fail closed to staff review with an honest operator state.
- [ ] Embedded-text reports that the text rules already read never incur a Document Intelligence call.
- [ ] No local/test profile calls Azure.

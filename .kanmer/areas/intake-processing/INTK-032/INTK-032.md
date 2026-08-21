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
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-21T23:30:27.932Z'
updated: '2026-08-21T23:30:27.932Z'
---

## Why — operator direction (2026-08-22)

> "add a new ticket accounting for situations where we cannot extract details from the third party engineer report e.g. we've not had that format in before — will plan in more detail the outcome on this specific ticket at a later time."

The original report accompanying an audit instruction is written by a different engineering firm each time. [[INTK-031]] builds the issuer corpus so known layouts are recognised; this ticket owns what happens for the ones that are **not**.

## Why it matters more than it looks

The audit reference prefix depends on whether the report says Repairable or Total Loss. If that fact cannot be read, the case cannot be given its reference — so an unreadable report is not a cosmetic gap, it blocks allocation. Fail closed rather than guessing a prefix that is immutable once allocated.

## Scope for now

The outcome design is **deliberately deferred** — the operator will plan the exact behaviour on this ticket later. What this ticket records now is the requirement: an unrecognised report format must produce a clear, actionable operator state rather than a silent partial extraction or an invented value.

Related: [[INTK-031]] (issuer corpus and identification).

## How to verify

To be defined with the operator before implementation starts.

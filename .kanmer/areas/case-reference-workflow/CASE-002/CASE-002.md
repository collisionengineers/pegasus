---
id: CASE-002
type: ticket
title: Allocate and design engineer queries and case notes
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - design
links: []
docs_todo: true
archived: false
created: '2026-08-18T09:39:12.311Z'
updated: '2026-08-18T09:39:12.311Z'
---

## What

Create capability inventory entries and design the engineer-queries workflow and case-notes capability shown as unbound markup in the Case Details screen.

## Why

The Claude Design prototypes show an engineer queries panel and case notes on the Case Details screen. [[PLAT-001]] shipped these as inactive unbound markup because neither is allocated in `docs/capabilities.md`. The open-questions doc records: "Engineer queries are not allocated: raising, replying to and resolving a query is its own workflow, not a panel." Case notes are similarly unallocated — the Case tabs stayed Overview / Evidence / History because "the app has no notes capability and renaming the tab would promise one."

## Approach

- Add capability IDs for engineer queries (raising, replying, resolving) and case notes to `docs/capabilities.md`.
- Design the query workflow as its own lifecycle (not a panel): states, actors, response proof, due/chaser interaction, closure.
- Design case notes as a separate capability with its own FRD.
- Wire the unbound markup once the capabilities are accepted.

## Verification

- [ ] Capability IDs exist in `docs/capabilities.md` with allocations and owner FRDs.
- [ ] The unbound markup carries the capability IDs in Razor comments.
- [ ] `dotnet build --configuration Release` passes.

## Outcome

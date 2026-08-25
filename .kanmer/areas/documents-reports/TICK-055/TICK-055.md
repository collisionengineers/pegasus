---
id: TICK-055
type: ticket
title: >-
  CASE-23 — Post-report query and dispute work on the existing case with
  retained report/reply-chain evidence and an explicit lifec…
status: backlog
area: documents-reports
assignee: ''
profile: feature
labels:
  - capability
  - CASE-23
  - next
links:
  - PLAT-001
  - TICK-105
  - TICK-208
  - CASE-002
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
archived: false
created: '2026-08-12T15:05:19.337Z'
updated: '2026-08-25T06:46:28.489Z'
---

## What

Plan and research **CASE-23**: handle post-report queries and disputes on the existing case, retaining the exact report and reply-chain evidence through an explicit lifecycle.

## Why

This capability is allocated to **Next / 0.4.0** in `docs/capabilities.md`. The operator corrected the workflow on 2026-08-19: a query is raised **to** the responsible Engineer after a report has been sent. The Engineer receives and answers it; the Engineer does not originate the external query.

## Required boundary

- Require an already-sent report and bind the query to the exact immutable report version and Sent evidence.
- Retain the correspondent or source evidence, responsible Engineer, received time, original query, every response, actor and time, and reply/send evidence.
- Define assignment and reassignment, query type, due/chaser interaction, response, resolution, reopening or follow-up, and correction history.
- Permit intake from retained correspondence and any accepted staff-recording route without fabricating an external origin.
- Keep post-report queries separate from staff-authored case notes and immutable system history.
- Expose the accepted workflow through the UI and MCP using the same Core policy, authorization, attribution, versioning, confirmation, and recovery behavior.

## Verification

- [ ] An incoming query can be attached only to an already-sent exact report version and its source evidence.
- [ ] An Engineer cannot be recorded as the originating external querist, but can receive, respond to, resolve, and handle an authorized follow-up.
- [ ] The original query, responses, actors, times, and Sent evidence remain retained.
- [ ] UI and MCP callers use one Core workflow and equivalent safeguards.
- [ ] MI measures consume accepted post-report query events only.

## Notes

- Source: `docs/capabilities.md` — CASE-23.
- Merged from [[CASE-002]]; its operator correction and lifecycle detail are retained here.

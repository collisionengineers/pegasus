---
id: CASE-002
type: ticket
title: Design post-report queries raised to Engineers and separate case notes
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - design
  - future-capability
  - post-report
links:
  - PLAT-001
  - TICK-105
  - TICK-208
docs_todo: true
archived: false
created: '2026-08-18T09:39:12.311Z'
updated: '2026-08-19T10:58:00.593Z'
---

## What

Allocate and design:

1. the post-report query workflow, where a query is raised **to** the responsible Engineer after a report has been sent; and
2. case notes as a separate capability.

## Why

The Collision Engineers operator corrected the prototype interpretation on 2026-08-19: Engineers do not raise queries. Queries arise only after a report has been sent and are directed to the Engineer responsible for that report.

[[PLAT-001]] shipped inactive query and notes markup because neither capability is allocated. The inactive surface must not imply that an Engineer originates a query.

## Query workflow boundary

- A sent report and its exact immutable version/Sent evidence are prerequisites.
- An incoming query is raised to the responsible Engineer and linked to the exact case, report version, correspondent/source evidence, and received time.
- The Engineer receives, responds to, and resolves the query; they do not create the originating query.
- Define states, assignment/reassignment, query type, due/chaser behavior, response evidence, reply/send evidence, resolution, reopening/follow-up, and correction history.
- Define intake from retained correspondence and any permitted staff recording route without fabricating an external origin.
- Preserve the original query, every response, actor/time, and Sent evidence.
- Expose the same accepted workflow through the user interface and MCP with equivalent authorization, confirmation, attribution, versioning, and recovery.

## Case-notes boundary

Case notes remain a separate capability and lifecycle. Do not merge free-form notes with externally raised post-report queries.

## Current boundary

The inactive interface stores nothing and must not label an Engineer as the query originator until the capability is accepted and wired.

## Verification

- [ ] Separate capability IDs and canonical behavioral owners exist for post-report queries and case notes.
- [ ] Query behavior requires an already-sent exact report version and records the external/staff-recorded source.
- [ ] Tests prove Engineers cannot originate the query but can receive, respond, resolve, and handle an authorized follow-up.
- [ ] UI and MCP use the same Core workflow and evidence.
- [ ] MI query measures consume accepted post-report query events only.

## Decision record

Operator correction, 2026-08-19: queries are raised to Engineers after a report is sent; Engineers do not raise queries.

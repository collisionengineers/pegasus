---
id: PLAT-035
type: ticket
title: Fail the build when a runtime role writes a table it has no grant on
status: backlog
area: platform-operations
assignee: ''
profile: chore
labels:
  - testing
  - least-privilege
  - regression-class
links: []
docs_todo: true
archived: false
created: '2026-08-22T04:52:30.443Z'
updated: '2026-08-25T06:41:31.634Z'
---

## Why

Three production outages, one cause, no test that could see it.

| Migration | Table | What broke |
| --- | --- | --- |
| `20260814092852_AddWorkerCaseCreationGrants` | case-creation tables | Worker could not create cases |
| `20260821095500_GrantWorkerVehicleLookupRequests` | `VehicleLookupRequests` | Zero automatic lookups enqueued ([[CASE-010]]) |
| `20260822044425_GrantWorkerCaseDocuments` | `CaseDocuments`, `DocumentVersions`, `DocumentOccurrences` | Custody reported Failed over evidence that was in Box ([[DOCS-008]]) |

Each time the pattern is identical, and the second migration's own comment names
the blind spot exactly:

> "Local/LocalDB tests run full-privilege and never exercise the least-privilege
> role, so this only ever failed against the deployed estate."

So the whole test suite is green while the deployed estate is broken, and the
defect is found by an operator looking at a case page. The grant matrix in
`20260729199000_RuntimeRoleReconciliation` is the one list of what each runtime
role may touch; nothing checks it against what the code actually does.

## What this needs to answer

The hard part is attribution, not assertion: given an EF entity write, which
runtime role performs it? Web and Worker share `Pegasus.Infrastructure`, and
several stores are composed by both. Candidate approaches, to be weighed in
research rather than picked here:

- static analysis over each composition root's registered stores;
- a run of the existing integration suite against a database where the test
  connection uses the least-privilege role rather than `dbo`;
- asserting the grant matrix against a declared per-store role attribute.

The second is the most faithful — it reproduces the deployed condition instead
of modelling it — and is likely also the cheapest, since the fixtures exist.

## How to verify

Revert any one of the three migrations above on a branch. The suite must go red,
naming the table and the role.

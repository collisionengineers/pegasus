---
id: DOCS-008
type: ticket
title: Custody reports Failed although the evidence reached Box
status: done
area: documents-reports
order: 1060
assignee: claude-code
profile: fix
stageEntered:
  review: '2026-08-22T04:53:07.269Z'
  verifying: '2026-08-22T05:54:47.458Z'
  done: '2026-08-22T06:02:10.573Z'
labels:
  - regression
  - qdos26009
  - release-17
  - custody
links: []
docs_todo: true
deployment: production
archived: false
created: '2026-08-21T23:30:27.801Z'
updated: '2026-09-01T14:44:32.659Z'
---

## Why

QDOS26009's case page shows *"Case custody — Case evidence — Failed — Case evidence could not be stored."* The operator checked Box and **the evidence is there**.

## Cause — settled 2026-08-22, read live from the production database

The Worker runtime role holds **no permission at all** on the three tables the
custody path writes, beyond the DELETE deny every table carries:

```
pegasus_worker_runtime_role  CaseDocuments        DELETE  DENY
pegasus_worker_runtime_role  DocumentOccurrences  DELETE  DENY
pegasus_worker_runtime_role  DocumentVersions     DELETE  DENY
```

They are granted to the **Web** role only (`20260729199000_RuntimeRoleReconciliation`
lines 110, 125, 126). The Worker block at line 166 never listed them, because
when that baseline was written only Web created case documents. `fef817b8`
(DOCS-007, release 17) moved document registration into the Worker, and it has
been denied ever since.

So the uploads reach Box, the record write is refused, the whole custody
transaction rolls back, and the operator is told the evidence could not be
stored while looking straight at it in Box.

| Observation | Explained by |
| --- | --- |
| The files really are in Box | Uploads run before the record write |
| `custody_unexpected_failure` | `SqlException` 229 inside `DbUpdateException` — unclassified |
| `AttemptCount = 1`, never retried | Unclassified ⇒ terminal, not transient |
| `CaseDocuments` = 0 for both cases | The INSERT is what is denied |
| `create_image_case_custody` completed 29 min earlier | Image custody writes `ImageIntakes`, which the Worker *is* granted — Box access was never at fault |

This is the third instance of one defect class; see [[PLAT-035]].

## Corrections to this ticket's earlier reasoning

Two things written here while diagnosing were wrong and are left visible rather
than quietly edited away:

- *"The prime suspect is `RecordRetainedCaseFilesAsync` … very likely a
  regression I introduced."* The code that DOCS-007 added is correct. It was the
  first caller to need a grant nobody had given the Worker.
- *"Application Insights holds zero telemetry."* It does not. Every check that
  produced that conclusion ran inside a window where the workspace's **0.1 GB
  daily cap** had already stopped ingestion. The Worker has been reporting
  continuously for the whole retained week. Full evidence in [[PLAT-034]].

## Fix

`20260822044425_GrantWorkerCaseDocuments` grants the Worker the Web role's own
permission strings for those three tables. No application code changed. PR #510.

## How to verify

After the migration applies to production, re-read `sys.database_permissions`
for the Worker role, then put one real instruction through the pipeline: custody
reaches `confirmed` and the case's `CaseDocuments` row count is non-zero.

That single case also closes [[DOCS-006]], [[DOCS-007]], [[CASE-013]],
[[CASE-014]], [[CASE-017]], [[INTK-029]] and [[INTK-030]] — every one of them has
been waiting on custody succeeding.

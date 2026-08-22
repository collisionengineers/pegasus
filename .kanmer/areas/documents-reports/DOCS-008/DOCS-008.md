---
id: DOCS-008
type: ticket
title: Custody reports Failed although the evidence reached Box
status: implementing
area: documents-reports
assignee: claude-code
profile: fix
taken_at: '2026-08-22T04:40:54.844Z'
branch: task/docs-008-grant-worker-documents
worktree: ../pegasus-worktrees/docs-008-grants
labels:
  - regression
  - qdos26009
  - release-17
  - custody
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-21T23:30:27.801Z'
updated: '2026-08-22T04:40:54.844Z'
---

## Why

QDOS26009's case page shows *"Case custody — Case evidence — Failed — Case evidence could not be stored."* The operator checked Box and **the evidence is there**.

## Evidence read from production (2026-08-22)

```
ExternalWorkItems: create_case_custody | failed | attempts 1
  FailureCode:   custody_unexpected_failure
  FailureReason: Case evidence could not be stored.
Cases: CustodyState=failed, CustodyRootRemoteId=NULL, CustodySourceRemoteId=NULL,
       CustodyConfirmedAtUtc=NULL, AuditCustodyRemoteId=NULL
CaseDocuments for this case: 0
CaseWorkflowEvents: only vehicle_lookup_current — no custody event at all
```

`custody_unexpected_failure` is the **fallback** bucket in `EfQueuedCustodyProcessor.GetFailureCode` — not `FileNotFound`, `InvalidData`, `Unauthorized`, lease-lost, cancelled, or `HttpRequestException`/`IOException`. So an unclassified exception type reached it.

## The shape of the fault

The uploads succeeded (Box has the files) but nothing was committed. That places the failure **after** the Box writes and inside `CompleteCaseCustodyAsync`'s transaction, which rolls back everything: the custody confirmation, the Review transition and the document records.

The prime suspect is `RecordRetainedCaseFilesAsync`, added by [[DOCS-007]] in Release 17 and called at `EfQueuedCustodyProcessor.cs:580` — **this is very likely a regression I introduced.**

Ruled out so far by reading production data:
- ordinal collision — ordinals are globally unique per case (source 1, attachments 2..n+1, photographs n+2..)
- column length — every `ContentHash` is 64 chars, filenames ≤ 68, media types ≤ 15
- a competing `CaseDocuments` row — there are none, and the only other insert site allocates the next free ordinal

## Diagnosis is currently blind

Application Insights holds **zero** telemetry for the last 12 hours, so the actual exception could not be read. See the separate telemetry ticket — it blocked this investigation and will block the next one.

## How to verify

A reproduction test at the production shape — source + 2 attachments + the surviving photographs — that fails before the fix and passes after. Then a real instruction through the live pipeline reaching `CustodyState=confirmed` with its document records written.

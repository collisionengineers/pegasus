# Research — narrowed to the audit path, exception still unread

## What is proven

**The uploads all succeeded.** The operator confirmed the evidence is in Box, so this is not
a partial upload and not a Box outage. Everything after the uploads rolled back:

```
create_case_custody | failed | attempts 1 | custody_unexpected_failure
Cases: CustodyState=failed, CustodyRootRemoteId=NULL, CustodySourceRemoteId=NULL
CaseDocuments: 0        CaseWorkflowEvents: no custody event
CaseHistory: custody_failed | "Case evidence could not be stored."
Work item: CaseRootCreationToken and AuditFolderCreationToken both predeclared
```

`custody_unexpected_failure` is the fallback bucket — not `FileNotFound`, `InvalidData`,
`Unauthorized`, lease-lost, cancelled, `HttpRequestException` or `IOException`.

## Ruled out by experiment, not by argument

| Hypothesis | Test | Result |
| --- | --- | --- |
| The record write breaks with more than one attachment | new `AcceptedCaseRecordsEveryAttachmentWhenMoreThanOneArrives` (two PDFs, production file names) | **passes** |
| The record write breaks on embedded photographs | existing corpus test run locally | **passes** |
| A schema constraint rejects the new rows | production data: hashes all 64 chars, names ≤ 68, ordinals globally unique, no competing `CaseDocuments` row, no constraint on the custody remote-id columns | dead |

The two-attachment test is kept — coverage the suite did not have.

## The remaining difference, and what has now been done about it

**No custody test has ever run an audit case.** Every fixture in
`CustodyOutboxIntegrationTests` accepts with `CaseType.Inspection`. QDOS26009 is
`Type='audit'`, and the audit path diverged at `EfQueuedCustodyProcessor`:

```csharp
var rootReference = isAuditCase ? casePayload.AuditReference : casePayload.CaseReference;
```

The Box root was created as **`a.QDOS26009`** while `GetExistingCaseRootAsync` looks a root
up by `CaseReference`. `CompleteCaseCustodyAsync` additionally wrote `AuditCustodyRemoteId`
and `AuditCustodyConfirmedAtUtc`, neither exercised anywhere.

**[[CASE-014]] has since removed that divergence entirely.** An audit's reference now
carries its own `a.`/`ap.` prefix, the Box root is named by the case reference for every
case, and an audit no longer creates a second folder. That may be the fix; it is not yet
proved to be.

## Why this stopped short of the cause

An audit reproduction needs `StandaloneAuditEvidence` seeded before allocation, and the
fixture failed twice more inside `EfCaseAcceptanceStore.ResolveStandaloneAuditEvidenceAsync`.
Building it correctly is several more layers of plumbing and it is guesswork without the
exception.

**The exception is unreadable because the estate emits no telemetry** — [[PLAT-034]], now
fixed in the same branch but not yet proved from a deployed run. Fixing the instrument
before continuing to guess is the right order.

Worth carrying into the fix: `FailureCode`/`FailureReason` are deliberately operator-safe
and **nothing anywhere retains the exception type**. Had the type been recorded, this would
have taken minutes.

## Next, in order

1. Deploy [[PLAT-034]] and confirm telemetry lands.
2. Re-run the failed `create_case_custody` work item; read the actual exception.
3. Confirm whether [[CASE-014]] already fixed it, and either way add the audit custody
   coverage the suite is missing.

## Incidental finding

`StandaloneAuditEvidence.Assessment` already stores `'repairable'` — which is what let
[[CASE-014]] be implemented immediately rather than waiting on [[INTK-031]].

# Research — narrowed to the audit path, exception still unread

## What is proven

**The uploads all succeeded.** Operator confirmed the evidence is in Box, so this is not a
partial upload and not a Box outage. Everything after the uploads rolled back:

```
create_case_custody | failed | attempts 1 | custody_unexpected_failure
Cases: CustodyState=failed, CustodyRootRemoteId=NULL, CustodySourceRemoteId=NULL
CaseDocuments: 0        CaseWorkflowEvents: no custody event
CaseHistory: custody_failed | "Case evidence could not be stored."
Work item: CaseRootCreationToken and AuditFolderCreationToken both predeclared
```

## What was ruled out by experiment, not by argument

Three reproduction attempts were written and run against the real code:

| Hypothesis | Test | Result |
| --- | --- | --- |
| The record write breaks with more than one attachment | new `AcceptedCaseRecordsEveryAttachmentWhenMoreThanOneArrives` (two PDFs, production file names) | **passes** — hypothesis dead |
| The record write breaks on embedded photographs | existing `AcceptedCaseRetainsEmbeddedPhotographsBesideTheSource` (corpus) run locally | **passes** — hypothesis dead |
| A schema constraint rejects the new rows | production data checked: hashes all 64 chars, names ≤ 68, ordinals globally unique (source 1, attachments 2–3, photographs 4+), no competing `CaseDocuments` row, no unique index or check constraint on the custody remote-id columns | dead |

The two-attachment test is kept — it is coverage the suite did not have.

## The one structural difference left, and it is a real coverage hole

**No custody test has ever run an audit case.** Every fixture in
`CustodyOutboxIntegrationTests` accepts with `CaseType.Inspection`. QDOS26009 is
`Type='audit'`, and the audit path differs at `EfQueuedCustodyProcessor.cs:141-144`:

```csharp
var isAuditCase = string.Equals(casePayload.CaseType, "audit", StringComparison.Ordinal);
var rootReference = isAuditCase
    ? casePayload.AuditReference ?? throw new InvalidDataException(...)
    : casePayload.CaseReference;
```

So an audit's Box root is named **`a.QDOS26009`**, not `QDOS26009` — and
`CompleteCaseCustodyAsync` additionally writes `AuditCustodyRemoteId` and
`AuditCustodyConfirmedAtUtc`, neither of which any test exercises.

`CustodyNames.SafeName("a.QDOS26009")` was checked and returns the name unchanged, so the
dot itself is not rejected.

A note for [[CASE-014]]: `GetExistingCaseRootAsync` is called with **`CaseReference`** while
`CreateCaseRootAsync` is called with **`rootReference`**. For an audit those are different
strings. That inconsistency may be harmless today because the two calls serve different
work kinds, but it is exactly the kind of split the single-reference change removes.

## Why this stopped here

An audit reproduction needs `StandaloneAuditEvidence` seeded before allocation, and the
fixture failed twice more inside `EfCaseAcceptanceStore.ResolveStandaloneAuditEvidenceAsync`
("The retained Audit evidence is incomplete or invalid"). Building it correctly is several
more layers of fixture plumbing, and it is guesswork without the exception.

**The exception is unreadable because the estate emits no telemetry** — see [[PLAT-034]].
That ticket is the unblocker, and continuing to guess here is worse engineering than fixing
the instrument first.

Worth noting for the fix: `FailureCode`/`FailureReason` are deliberately operator-safe, and
**nothing anywhere retains the exception type**. Had the type been recorded, this
investigation would have taken minutes. That is a defect in its own right and belongs with
this ticket's fix.

## Next step, in order

1. [[PLAT-034]] — make telemetry land.
2. Re-run the failed `create_case_custody` work item and read the actual exception.
3. Fix the cause, and add the audit custody coverage the suite is missing.

## Incidental finding

`StandaloneAuditEvidence.Assessment` already stores `'repairable'`. The Repairable /
Total Loss concept [[CASE-014]] and [[INTK-031]] need is therefore **already in the model**,
which should shorten both.

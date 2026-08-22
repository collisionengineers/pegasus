# Research

## What production showed

```
Cases: Reference='QDOS26009'  Type='audit'  AuditReference='a.QDOS26009'
```

One audit, two identities. The operator says there is only ever one.

## Where each came from

`EfCaseAcceptanceStore` mints the reference and then derives a second string from it:

```csharp
var reference = $"{principal.Code}{year % 100:00}{allocatedSequence:000}";
var auditReference = standaloneAuditAssessment is { } assessment
    ? AuditIdentity.Create(reference, assessment)
    : null;
```

`AuditIdentity.Create` was already correct — `a.` for `Repairable`, `ap.` for `TotalLoss`.
It was being applied to a *second* identity rather than to the reference itself.

## The sequencing question, and its answer

A reference is immutable after allocation, so the prefix must be knowable at allocation.
It is:

- `EfCaseAcceptanceStore` **refuses** to allocate a standalone Audit without
  `StandaloneAuditEvidenceId` ("A standalone Audit requires retained original-report
  evidence");
- `StandaloneAuditEvidence.Assessment` already stores `repairable` / total loss, and
  `Cases.StandaloneAuditAssessment` carries it too.

Operator confirmed the intent directly: *"Yes, you already know this, its why we are
extracting the third party report for the audit."* So the fact is in hand before the
reference exists, and no revision is ever needed.

## A divergence found while reading, not on the ticket

`EfQueuedCustodyProcessor` named an audit's Box root from the **audit** reference:

```csharp
var rootReference = isAuditCase ? casePayload.AuditReference : casePayload.CaseReference;
```

while `GetExistingCaseRootAsync` looks a root up by **case** reference. For an audit those
are different strings. No test covers audit custody at all — every fixture in
`CustodyOutboxIntegrationTests` uses `CaseType.Inspection` — so nothing caught it. That
divergence is a live suspect in [[DOCS-008]], and collapsing the two identities removes it.

## Decisions

- The prefix goes on the case's own reference; no second identity is allocated.
- The `AuditReference` **column** stays, unset for audits. Dropping it is a migration with
  no behavioural gain that would erase how already-allocated cases were named.
- Existing cases are not rewritten — the immutability invariant applies to them too.
- A **later** Audit reference on a non-audit case is a different concept and keeps its own
  folder and work kind.

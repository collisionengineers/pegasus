# Plan — INTK-048: resolve manually linked Unidentified receipts

## Approach

Keep the fix in `ReconcileUnidentifiedDestinations`, the existing Core owner of
the supersession rule. A receipt with an effective `CurrentCaseId` has reached
a real Case even if its original processing decision remains eligible for
Unidentified. Recognize that association first and reuse
`CurrentCaseReference`. This is smaller and safer than resolving in the web
action or rewriting the receipt decision because it preserves one business-rule
owner and lets the existing worker repair historical rows.

## Governing docs

- **Meets `docs/frd/frd-02-intake-and-source-identity.md`:** resolves an open
  U-item once its origin reaches a formal Case, records that destination in
  history through the existing resolver, and leaves receipts with no real
  destination open.
- The governing document already states the required behavior and is not
  modified.

## Steps

1. Adjust `ResolveForReceiptAsync` so original-decision eligibility blocks
   reconciliation only when `CurrentCaseId` is absent. Map any effective current
   Case to `InstructionCase` with `CurrentCaseReference`.
2. Add a Core regression test for an eligible receipt carrying an active manual
   Case association, while retaining the no-destination guard.
3. Add an integration regression that writes the manual association through
   `ILinkIntake`, runs the sweep, and proves association, Case event,
   resolution history, and replay safety.
4. Run the focused tests and canonical locked restore, Release build, and
   non-Corpus suite.
5. Run the required simplification pass over this ticket's diff, applying only
   behavior-preserving findings and recording every disposition.

## Verification

Before review, run:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~ReconcileUnidentifiedDestinationsTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~UnidentifiedReconciliationTests"
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
```

Post-merge proof should repeat the focused reconciliation tests at the merged
SHA. Production verification, after a separately approved release, checks U38
and U39 resolve to QDOS26030 without changing their active manual associations
or Case workflow events.

## Risks / open questions

- Reordering the guard must not force-close genuinely unidentified material.
  Mitigation: the effective Case check is the only new bypass, and existing
  no-destination coverage remains.
- The SQL integration setup may be unavailable locally. A non-PASS is recorded
  honestly and blocks review rather than being replaced by weaker coverage.
- No open questions.

## Simplification pass — 2026-08-28

Independent review covered reuse/duplication, simplification, efficiency, and
abstraction altitude over the branch's own diff.

- No production simplification findings. The change reuses
  `CurrentCaseId`/`CurrentCaseReference`, keeps the cheap eligibility guard,
  and remains in the existing Core reconciliation owner.
- Separate test-claim gap fixed: the SQL integration test now verifies the
  manual association and `intake_case_linked` event after reconciliation, not
  only before it.
- No unapplied findings.

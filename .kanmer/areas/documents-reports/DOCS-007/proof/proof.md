# Proof

**Shipped:** PR #505, commits `fef817b8` and `f0d8b6eb` ·
**Deployed:** Release 17 (`71911734`), carried to Release 22 (`191ddf33`), the
serving revision.

## Registration asserted end to end

`AnAutomaticAuditReachesReviewWithOneIdentityAndItsDocuments` (PR #515) drives
a real audit through `ProcessIntake` → acceptance → `IProcessQueuedCustody`
against real SQL, and asserts that after custody completes the case has case
**documents**, not just bytes in custody storage:

```csharp
Assert.True(documents > 0, $"Custody completed but registered {documents} case documents.");
```

That is the exact gap this ticket named: `RetainInstructionAttachmentsAsync`
uploaded every attachment and photograph to Box, but `CompleteCaseCustodyAsync`
recorded only the **source** version against the case, so the files existed and
the records did not. Pegasus could not show them, and the Evidence gallery
served the Azure blob copies instead.

Both halves of the operator's decision — *Box is the record, drop the local
copy* — are in place: registration through the existing `IAddCaseDocument` route
(`EfDocumentCustodyStore`, which composes `BoxDocumentContentStore` in
Production), and the read switched so the Evidence tab serves those documents
through `IDownloadCaseDocument`. The switch is additive and ordered: a case
accepted before the records existed still renders from its retained intake
asset, so nothing stopped rendering the day this shipped.

```
dotnet test … ~CustodyOutboxIntegrationTests (Release)   21 passed, 0 failed
CI on PR #515                                            10 checks green
```

## What this ticket cost, and what it exposed

Moving registration into the Worker made this the first caller to write
`CaseDocuments`, `DocumentVersions` and `DocumentOccurrences` from that host —
which had never been granted those tables. Custody failed on every case created
after release 17 ([[DOCS-008]]), which is why production shows:

```
CaseDocuments for QDOS26009: 0
CaseDocuments for QDOS26010: 0
```

Zero because every write was denied, not because registration was skipped. The
grant landed in release 20 and the permission is verified live as the Worker
principal itself.

## Evidence tier

**End-to-end against real SQL** for the registration. Not yet observed on a live
case — one instruction after the grant, or an operator pressing **Retry
custody**, produces non-zero rows and the Evidence tab serving from Box.

Extraction and retention of the photographs that get registered *is* observed in
production on QDOS26010: 20 embedded images across four pages, recorded under
[[DOCS-006]].

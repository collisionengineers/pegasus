# Post-implementation report

PR [#530](https://github.com/collisionengineers/pegasus/pull/530) → `dev`,
branch `task/plat-041-box-round-trips`, four commits off `origin/dev` `a6acc782`.

## Result

| | Fixed | Per image | 5 images |
| --- | ---: | ---: | ---: |
| Before | 0 | 9 | **45** |
| One-at-a-time read (Evidence tab, document download, report) | 0 | 4 | 20 |
| Export and hand-off | 3 | 1 | **8** |

The 3 fixed are the case-folder resolve (a listing of the custody root) and the
case-folder listing (ancestry + listing). Counted, not estimated — asserted in
`BoxDocumentContentStoreTests` against a request-counting in-memory Box.

The ticket's four items, in its own impact order: (1) the folder is resolved
once per export via an explicit batch route, nothing memoised between
operations; (2) the redundant metadata GET is gone from the read; (3) ancestry
is not re-walked once the folder is known; (4) the images are fetched
concurrently.

## What shipped differently from the plan

Two changes came out of the simplification pass, both recorded in full under the
plan's dated `## Simplification pass` heading.

1. **The read's metadata check was deleted, not re-pointed at the listing.**
   The plan had it read `IsExpectedRevision` off the item the listing already
   returned — apparently free. The pass found that rests on Box honouring
   `fields=…,size,parent` on `/folders/{id}/items`, that `IsExpectedRevision`
   refuses a null `Size` or `ParentId` deliberately and under test
   (`BoxManagedRevisionTests`), and that **no production path has ever read
   either field off a listing**. That is DOCS-010's exact failure shape — a
   field Box silently omits breaking every managed read. Deleting the check
   from the read is what the ticket asked for in the first place ("the SHA-256
   check is the real guarantee") and removes the premise entirely. The **write**
   path keeps `VerifyFileMetadataAsync` unchanged.

2. **The SQL predicate push was reverted.** It was one of the ticket's two named
   cheap wins and I took it, then removed it: it copies six Core-owned
   eligibility rules into an Infrastructure LINQ expression — a fourth copy of
   the jpeg/png list, which already disagrees with
   `EfAssessmentReportProjectionSource.PhotoMediaTypes` — for a smaller row
   count on a query of a few dozen rows. `EvaHandoffPolicy` re-decides all six,
   so it bought nothing measurable against 18 s of Box.

Also beyond the plan: the hand-off loop was converted alongside the export's
(same file, same defect, and it gives the new port method a second real caller
in the same diff), and the duplicated 14-argument `EvaBundleImage` construction
was written once.

The ticket's other named cheap win — `EfVehicleWorkflowStore.cs:470`'s
`Cases.AnyAsync` — was **not** taken. It is ~5 ms of an 18 000 ms problem and
removing it changes `IVehicleEvidenceQueries.GetAsync` from returning `null` to
returning an empty `CaseVehicleEvidence` for a case that does not exist, on a
port with four callers.

## The archive bytes

`EvaBundleSchema` is untouched; ENG-014 owns the format. Selection is still
decided by `EvaHandoffPolicy.SelectEligibleImages`, rows still arrive ordered by
occurrence ordinal, the index-zip preserves that order, and every image is still
verified against its recorded SHA-256 before it reaches the bundle.

Directly tested: a five-image batch returns byte-identical content, in the same
order, to five one-at-a-time reads. **Not** done: a literal byte diff of an
archive produced before and against after on the same case. The argument above
plus that test is what I have; it is not a cross-commit byte comparison.

## Verification

| Suite | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release` | succeeded, 0 warnings, 0 errors |
| `Pegasus.Core.Tests` | **937 passed**, 0 failed |
| `Pegasus.ArchitectureTests` | **99 passed**, 0 failed |
| `BoxDocumentContentStoreTests` + `BoxManagedRevisionTests` | **14 passed**, 0 failed |
| Full `Pegasus.IntegrationTests` (`Category!=Corpus&Category!=Browser`) | **not completed locally** |

The full integration suite could not be completed on this workstation. Three
other agents' suites (`case-021-observed-images` ×2, `eng-014`) were running
against the same LocalDB instance throughout. Of the 12 failures seen, **11 were
`SqlException: Connection Timeout Expired` in the post-login phase and 1 was a
`RegexMatchTimeoutException` in `InstructionFieldExtraction`** — a timeout, in a
file this branch does not touch. Zero assertion failures. The failing tests
included `MailboxIntakeIntegrationTests`, `QdosMappingExtractionTests` and
`RetainedMailPersistenceTests`, none of which this branch touches. I did not
kill the other worktrees' runs. **CI is the authoritative run for that suite.**

## Not done, deliberately

- `EfAssessmentReportProjectionSource.cs:80-107` — a third identical per-image
  loop. Takes the 9 → 4 per-read win for free; converting it is a follow-up.
- `EfDocumentCustodyStore.cs:589` — streams into a size-bounded `ZipArchive`;
  batching would buffer the whole export in memory.
- `BoxCaseCustody.VerifyFileAsync:1128-1148` still does the metadata GET and the
  ancestry walk. It is the custody-binding verification, stricter on purpose,
  and not on the export path.
- **The next latency ticket**: `ResolveCaseFolderAsync` still finds the case
  folder by listing the *entire* custody root, paged at 1000, filtered
  client-side. It is now the largest fixed cost of an export and grows forever
  with the case count. The durable id is already in `Cases.CustodyRootRemoteId`.
- No live Box call was made. Confirming a real listing's field shape needs the
  Box app credentials out of Key Vault — a credential read requiring approval.
  Nothing in this change depends on the answer.
- No docs changed. `docs/operations.md` mentions `VerifyFileMetadataAsync` only
  in the release 24/25 history, which remains true of those releases.

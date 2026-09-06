---
kind: review-attestation
pr: "none (controller override: no PR; worktree head review)"
head_sha: "6c8b945bd4e9ab65baf996a4025afa3cafb77f3d"
verdict: needs-changes
reviewer: "pegasus-reviewer (INTK-060 C07b, attempt 1)"
independent: true
plan_hash: "pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md @ 6c8b945bd"
ticket_updated: "INTK-060 scratch/c07-notes version 513847ac84cfa9f8"
board_sha: "n/a (controller override: Kanmer writes limited to scratch/review-c07b)"
expected_reviewers: []
threads_snapshot: []
findings:
  - id: C07B-R-1
    severity: major
    disposition: open
    summary: "Accepted-count accounting is idempotent only via the receipt, and the code has a branch that writes no receipt: a replay then double-counts the link's files/bytes and can set Exhausted early."
  - id: C07B-R-2
    severity: major
    disposition: open
    summary: "A Pending hand-over writes a receipt, so every later submission of that key is refused as Replay; RetainIncomingArtifact reconciles only Unknown and nothing sweeps Pending, so the occurrence and its DocumentVersion stay Pending forever and render 'Storing' indefinitely."
  - id: C07B-R-3
    severity: major
    disposition: open
    summary: "A thrown hand-over records nothing, leaving the arrival 'pending' rather than 'unknown'; the next attempt takes the Pending branch and re-offers bytes custody may already hold."
  - id: C07B-R-4
    severity: major
    disposition: open
    summary: "Plan item 7's 'Pending never renders upload success' is violated: a Pending disposition renders 'Your document was received and retained securely.' ASSUMPTION 6 does not address the rendered claim."
  - id: C07B-R-5
    severity: minor
    disposition: open
    summary: "RecordAcceptedAsync bumps the workflow version and clears the edit lease in a second transaction without re-asserting ArchivedCaseGuard.RequireMutable."
  - id: C07B-R-6
    severity: minor
    disposition: open
    summary: "The page's exception filter was not widened for the new custody hand-over; adapter transport failures (HttpRequestException, TaskCanceledException) become a 500 instead of the plain retry message."
  - id: C07B-R-7
    severity: minor
    disposition: open
    summary: "The post-implementation report has no '## Simplification pass' section, so the repository's third review question is unanswered by the record."
  - id: C07B-R-8
    severity: minor
    disposition: open
    summary: "The refusal proof asserts ThrowsAnyAsync<Exception>, which cannot distinguish A's authorization refusal from an incidental validation error; and 'inactive link' is never seeded independently of 'revoked'."
  - id: C07B-R-9
    severity: note
    disposition: accepted-risk
    summary: "EfPublicUploadRetentionStore.FindAsync is a global SingleOrDefaultAsync on OperationKey with no matching index, so every hand-over scans PublicUploadOccurrences. Base-slice behaviour, now on the hot path. Reason: accepted for this slice; the table is empty-to-small in v1 and the index is A-owned."
  - id: C07B-R-10
    severity: note
    disposition: accepted-risk
    summary: "DocumentVersionEntity.BoxFileId/BoxVersionId has two writers (A04's adapter and RecordAsync); the write is idempotent but 'one owner per rule' is blurred. Base-slice, reviewed in C07a as C07-R-2. Reason: no behaviour defect at this head."
  - id: C07B-R-11
    severity: note
    disposition: rejected-with-reason
    summary: "The controller's compile-only accommodation 6c8b945bd is correct and minimal. Reason: it removes exactly the removed constructor argument, leaves contentStore in use at DocumentCustodyDurabilityTests.cs:378 so no unused-local warning, changes no assertion, and the solution builds 0 Warning(s) 0 Error(s). No change requested."
---

# C07b review — the public upload retention caller

**Verdict: needs-changes.** Head `6c8b945bd`. Four majors, four minors, three
notes. Ownership: **PASS**. Grant claim (item 7): **verified**.

The slice does the main thing it was asked to do, and does it well. The public
upload path no longer claims a custody it does not have: `EfDocumentRequestStore`
creates no `CaseDocumentEntity`, no `DocumentVersionEntity`, no
`DocumentOccurrenceEntity`, assigns no `CustodyStatus` and never touches
`IDocumentContentStore` (verified by grep over the file: the only remaining
document-table references are three reads and the Confirmed-only Box identity
update). The plan's stop condition "an accepted upload lacks confirmed custody"
is answered at the store. The fail-closed path is real — absent retention
returns `Unavailable` before a row is read or written, proved with zero
session/occurrence/receipt/document rows. The arrival is committed `pending`
before the hand-over and the double records the state it observed at that
moment, which is stronger evidence than an assertion after the fact.

What blocks it is the seam either side of the hand-over: the receipt is doing
two jobs it cannot do, and a thrown hand-over is recorded as the wrong state.

## Slice under review

`2b6b5ed37..b5b5338a4` (the implementation commit `87eebffe1` plus the
controller's revert `b5b5338a4`) plus the controller's one-line compile
accommodation `6c8b945bd`. The merge `6bb5453ba` brings `Operations/Index`,
`Triage/Details`, `OperatorLabels` and `MultiFormatGenuineCorpusWebTests` from
the C branch; those are not this slice and were not reviewed.

## Ownership — PASS

| File | Owner | Verdict |
| --- | --- | --- |
| `src/Pegasus.Core/Documents/RequestUploadPolicy.cs` | C (C07 files) | in scope |
| `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs` | C (C07 files) | in scope |
| `src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs` | C (`file-ownership.csv:1142`; `C-intake.md:430`, `:1132` "C owns … presentation/handlers") | in scope, enumerated under a different C slice |
| `tests/…/IncomingArtifactCustodyTests.cs`, `PublicUploadSessionTests.cs` | C (proposed C files) | in scope |
| `tests/…/CustodyOutboxIntegrationTests.cs` | C (C07 files) | in scope, deviation recorded |
| `tests/…/PublicUploadRetentionWebTests.cs` | new C test | dispatch-named, not enumerated |
| `tests/…/DocumentCustodyDurabilityTests.cs` | A (explicit user ruling) | byte-identical to `2b6b5ed37` at `b5b5338a4`; `6c8b945bd` is the controller's own one-line compile accommodation |

No A-owned DI registration, migration, entity configuration, snapshot or
`src/Pegasus.Core/Custody/*` file is touched. `CustodyContracts.cs` is
unchanged — `ICaseArtifactCustody` and `CaseArtifactCustodyRequest` are frozen
and consumed as-is.

## Findings

### C07B-R-1 (major) — the receipt is the only replay guard on the link's limits accounting, and there is a branch that writes no receipt

`src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs:315-329`,
`:481-500`, `:517-531`.

The replay decision comes from `uploadPolicy.Authorize(…, priorReceipt?.ContentHash)`
(`RequestUploadPolicy.cs:593-598`): with no receipt row there is no
`existingOperationContentHash`, so no `Replay` and no `OperationConflict`, and
the attempt proceeds to a full accept. `RecordAcceptedAsync` deliberately
writes **no** receipt when custody created no `DocumentOccurrenceEntity` for
the version (`:481-500` — the branch ASSUMPTION 5 introduces), yet it still
runs the counters unconditionally at `:517-521`:

```csharp
link.AcceptedFileCount = checked(link.AcceptedFileCount + 1);
link.AcceptedByteCount = checked(link.AcceptedByteCount + arrival.ContentLength);
link.Version = checked(link.Version + 1);
if (link.AcceptedFileCount >= uploadLimits.MaximumFileCount
    || link.AcceptedByteCount >= uploadLimits.MaximumRequestBytes)
{
    link.Status = RequestUploadStatus.Exhausted;
}
```

So in that branch a second submission of the same operation key re-enters,
correctly calls custody once (`RetainIncomingArtifact.FindAsync` returns the
Confirmed retention and short-circuits), and then increments the link's
accepted file and byte totals a **second** time — and can drive
`RequestUploadStatus.Exhausted` before the sender has sent the files they were
promised. The report's claim that "replay safety does not rest on the receipt"
is true of custody and false of the limits accounting; ASSUMPTION 5 is
therefore honest about the FK and about `ReceiptId` being null, but not honest
about what else the receipt was load-bearing for.

**Fix.** Make the accounting idempotent on the committed
`PublicUploadOccurrenceEntity`, not on receipt existence: in one transaction,
read the occurrence's stored `CustodyState`, and count only on the transition
into an accepted state. That is the row that is guaranteed to exist.

### C07B-R-2 (major) — a Pending arrival is a permanent dead end, and the receipt is what makes it one

`EfDocumentRequestStore.cs:270-283`, `:481-500`, `:315-329`;
`src/Pegasus.Core/Intake/RetainIncomingArtifact.cs:133-145`.

When custody answers `Pending`, the double (and A04, per the report's own
statement of what A04 will do) creates the document occurrence, so
`RecordAcceptedAsync` **does** write the receipt. Every later submission of
that operation key is then refused in `AuthorizeAndRecordArrivalAsync` as
`Replay`, and `RetainIncomingArtifact` is never called again for it. But the
command's only Pending recovery is to re-offer the bytes:

```csharp
if (existing.IsConfirmed) { return existing; }
if (existing.State == IncomingArtifactCustodyState.Unknown)
{
    return await ReconcileAsync(actor, existing, cancellationToken);
}
// Pending falls through to custody.RetainAsync — but nothing reaches here again.
```

`ReconcileAsync` covers `Unknown` only. Nothing else in the tree reads
`PublicUploadOccurrenceEntity` (three references, all in this file) and nothing
sweeps `DocumentCustodyStatus.Pending` (one reference in the whole of `src/`,
the label at `src/Pegasus.Web/Presentation/OperatorLabels.cs:515`). So the
caller closes the one door the command left open: the occurrence stays
`pending`, the version stays `DocumentCustodyStatus.Pending`, and the Case
documents tab renders "Storing" forever. This is exactly the review question
"does anything downstream depend on the receipt existing?" — yes: the receipt's
existence *blocks* the Pending from ever being retried.

**Fix (pick one).** Write the receipt only for a Confirmed disposition (and
move the accounting onto the occurrence per R-1); or give Pending a
reconciliation caller; or, if it is genuinely A04's sweep to own, record it as
an explicit residual with a linked follow-up instead of leaving it unstated.

### C07B-R-3 (major) — a thrown hand-over leaves the arrival `pending`, not `unknown`

`EfDocumentRequestStore.cs:246-268`; `RetainIncomingArtifact.cs:147-163`.

The hand-over has no exception handling in either the caller or the command. A
timeout, a lost connection or an adapter throw — precisely the case
`IncomingArtifactCustodyState.Unknown` is documented for
(`RetainIncomingArtifact.cs:20-27`: "a timeout, a lost connection, a restart
mid-call") — never reaches `store.RecordAsync`, so the occurrence keeps the
`pending` it was committed with. Nothing false is rendered (no receipt, no
counts, window shut, `NotRetained`-equivalent page message via the page's
catch), so this is not a false-success defect. The damage is on the retry: a
`pending` existing takes the re-offer branch, so bytes custody may already hold
are sent again — the duplicate-creation path the command's own remarks say
never to take. The dispatch asked for "no orphaned Pending on a thrown
exception without a recorded Failed/Unknown"; this is that gap.

**Fix.** Wrap the hand-over in a recoverable-exception catch, record the
occurrence `unknown` through `IIncomingArtifactRetentionStore.RecordAsync`,
then return `NotRetained`. (Doing it inside `RetainIncomingArtifact` around
`custody.RetainAsync` is the better home, since it is the command that owns
what "retained" means.)

### C07B-R-4 (major) — a Pending hand-over renders upload success

`src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs:107-110`;
`EfDocumentRequestStore.cs:270-283`.

`Accepted` and `Replay` both render "Your document was received and retained
securely." The store maps a `Pending` custody disposition to `Accepted`. The
mapping is the controller's explicit instruction and is not itself the defect;
the sentence is. Plan item 7 says in terms that Pending "never renders upload
success", and "retained securely" is a claim about custody that custody has not
made. ASSUMPTION 6 reconciles the *decision* and the *stored status* honestly
("`CustodyStatus` stays `Pending`, no Box identity is written, the fixed window
does not open") and then does not address the one place the claim reaches the
sender.

**Fix.** Give the page something to distinguish the two — a `Pending`-aware
result flag, or a distinct Case-free sentence such as "Your document was
received and is being stored." Nothing should say "retained securely" before
custody said so.

### C07B-R-5 (minor) — the second transaction re-asserts no archived/terminal guard

`EfDocumentRequestStore.cs:344-355` (guarded) vs `:524` (`CaseMutationGuard.Complete(workflow)`
with no guard). A Case archived or moved terminal during the hand-over has its
workflow version bumped and its edit lease cleared by an upload it would now
refuse. This was one transaction before the split. There is a defensible answer
(custody already holds the bytes, so the record must land), but it is not
recorded. **Fix.** Re-assert `ArchivedCaseGuard.RequireMutable` in
`RecordAcceptedAsync`, or state the choice in the method's remarks.

### C07B-R-6 (minor) — the page's exception filter was not widened for the hand-over

`Request.cshtml.cs:150-160` catches `ArgumentException`,
`InvalidOperationException`, `IOException`, `UnauthorizedAccessException` and
`DbUpdateException`. The hand-over puts a remote custody adapter on this path;
its transport failures (`HttpRequestException`, `TaskCanceledException`,
`SocketException`) are none of those and become an unhandled 500 instead of the
plain retry message. **Fix.** Add the transport types, or route the hand-over
through `IntakeExceptionPolicy.IsRecoverable`, which the codebase already uses
for exactly this classification.

### C07B-R-7 (minor) — no `## Simplification pass` in the report

The C07a report carried one; `c07b-report.md` does not, so the repository's
third review question has no record to check. I ran the pass myself over the
slice diff and found nothing to remove: every private member of
`EfDocumentRequestStore` has a live caller (counted references, all ≥ 2), the
legacy `catch (DbUpdateException)` orphan-rollback block is gone with the
content write it existed for, `IDocumentContentStore` keeps ten other
production references, and `DocumentContentRollback` keeps two
(`EfDocumentCustodyStore.cs:78`, `EfMarketResearchAiJobCompletionStore.cs:174`),
so neither is orphaned by leaving the constructor. **Fix.** Add the section
with honest dispositions.

### C07B-R-8 (minor) — the authorization proof is looser than it reads

`tests/Pegasus.IntegrationTests/PublicUploadRetentionWebTests.cs:315-336`. The
`Refuses` helper asserts `ThrowsAnyAsync<Exception>`, which passes on any
incidental `ArgumentException` from the command's own validation rather than on
A's authorization refusal — and for the holding case the helper injects a fake
`IntakeReceiptId` specifically to get past that validation, which shows how
close the two are. Separately, "inactive link" is never seeded on its own: the
`Status != Active` branch is only reached through the Revoked seed, which also
sets `RevokedAtUtc`. **Fix.** Assert `StaffAuthorizationException` (and
`InvalidOperationException` for holding) by type, and seed one link with a
non-Active status and no revocation.

### Notes (no change requested)

- **C07B-R-9** `EfPublicUploadRetentionStore.FindAsync` (`:794-834`) is a
  global `SingleOrDefaultAsync` on `OperationKey`, while the only index is
  `(SessionId, OperationKey)` — a scan per hand-over. The doc comment's stated
  reason for link-scoping (the session row being "rebuilt beneath" the link) is
  also the one scenario that would make that `Single` throw; it cannot happen
  while `PublicUploadSessions` keeps its unique index on `RequestUploadLinkId`
  and `DENY DELETE`. Base-slice, accepted.
- **C07B-R-10** `DocumentVersionEntity.BoxFileId/BoxVersionId` has two writers,
  A04's adapter and `RecordAsync` (`:855-870`). Idempotent (`?? version.BoxFileId`),
  reviewed in C07a as C07-R-2. Accepted.
- **C07B-R-11** The accommodation `6c8b945bd` is right. It removes exactly the
  removed `contentStore` argument; the local stays in use at
  `DocumentCustodyDurabilityTests.cs:378` (`contentStore.Addresses.Count`), so
  there is no unused-local warning and the build is 0/0; no assertion changed,
  so the A-owned semantic retargeting still travels as A's handoff. It does
  re-touch a file the user ruled A-owned, but as a recorded controller act, not
  an implementer deviation.

## What the dispatch asked, point by point

1. **The caller.** Correct on every listed property except the two accounting
   consequences above. Session get-or-create with one row per link (unique index
   on `RequestUploadLinkId`), `AcceptsBytes` refusing a finalized/expired
   session as `Unavailable` with no Case disclosure, `LimitsVersionMismatch` on
   a session whose recorded version is not the accepted one, occurrence
   committed `pending` before the hand-over, actor
   `ActionActor.RequestLink(arrival.LinkId)`, `CaseId` = `link.CaseId` read
   inside the authorizing transaction, `IntakeReceiptId: null`,
   Confirmed/Pending → `Accepted`, Failed/Unknown → `NotRetained` (a retryable
   refusal), a version-less Confirmed also → `NotRetained`, `Start` only on
   Confirmed and idempotent (`RequestUploadPolicy.cs:431-435`), counters only
   on an accepted disposition, and `Unavailable` with zero rows when
   `RetainIncomingArtifact` is unregistered. Replay calls custody exactly once
   **when a receipt exists** — see R-1 and R-2 for when it does not.
2. **ASSUMPTION 5.** The FK claim is true: `CustodyModelConfiguration.cs:103`
   binds `RequestUploadReceiptEntity.OccurrenceId` to `DocumentOccurrenceEntity`
   and `CustodyEntities.cs:84` makes it a non-nullable `Guid`, so a
   `PublicUploadOccurrenceEntity.Id` genuinely cannot go there. Downstream, the
   only reader of `RequestUploadReceipts` outside this store is
   `EfOperationsStore.cs:252` (`lastReceiptAtUtc`), and `UploadToRequestResult.ReceiptId`
   has no consumer in `src/` — so the Case documents tab, `IGetRequestUpload`,
   revocation and exhaustion do **not** depend on the receipt, exactly as
   claimed. Where the assumption is not honest is the other direction: replay
   and limits accounting **do** rest on it (R-1), and its presence blocks Pending
   recovery (R-2).
3. **Transaction boundaries.** Committed before the hand-over: the session row
   (window shut) and the occurrence as `pending` — and nothing else; the Case
   workflow version is deliberately not bumped for a mere arrival. On a custody
   exception: occurrence stays `pending`, no receipt, no counters, session
   untouched — nothing false, but the wrong recorded state (R-3). On a crash
   between the hand-over and `RecordAcceptedAsync`: converges, because a retry
   finds no receipt, re-enters, gets the same Confirmed retention from
   `FindAsync` without a second custody call, and completes the record —
   correct, and the same mechanism as R-1's defect.
4. **The test double.** It genuinely enforces A's rule rather than assuming it
   (`PublicUploadRetentionWebTests.cs:596-641`): re-reads the link row and
   refuses a non-`RequestLink` actor, a `RequestLink` naming a different link, a
   `CaseId` that is not the link's, a null Case (holding), and a link that is
   not Active / is revoked / is expired against the injected clock. On
   acceptance the double, not the caller, creates the document, version and
   document occurrence, and it records the occurrence's custody state at the
   moment of the hand-over. Every path in the dispatch is covered:
   `CustodyRefusesEveryAuthorityThatIsNotThisExactActiveLink` (six wrong
   authorities, then asserts no occurrence and no document behind any of them),
   `AConfirmedHandOverOpensTheFixedWindowAndRecordsTheBoxIdentities`,
   `APendingHandOverIsAcceptedWithNoRemoteIdentityAndNoOpenWindow`,
   `ARefusedOrUncertainHandOverIsNeverAcceptedAndNeverCounted` (Failed and
   Unknown), `ReplayOfTheSameOperationKeyReturnsTheSameDocumentAndCallsCustodyOnce`,
   and `WithoutTheRetentionCommandTheSubmissionRefusesAndWritesNothing`. Caveat
   at R-8. Not covered, because the code does not do it: a thrown hand-over
   (R-3) and a Pending that later confirms (R-2).
5. **`Request.cshtml.cs` disclosure.** PASS. "The document could not be
   retained. Try again using the same upload operation." names no Case, no
   reference, no principal, no file identity, and is byte-identical to the
   sentence the page's exception path already used, so a refusal and an
   exception are indistinguishable to a prober. `Unavailable` still returns
   `NotFound()`.
6. **`CustodyOutboxIntegrationTests.cs`.** PASS. Five lines: a
   `baseFactory` local and `PublicUploadRetentionWebTests.WithRetention(baseFactory)`,
   with a comment saying why. The test performs a real accepted upload, so
   without the registration the fail-closed `Unavailable` would have failed it
   for the wrong subject. Minimal and legitimate; the deviation is recorded.
7. **C07-R-1.** **Verified closed for the Web accept path.** The web-role grant
   statement in
   `src/Pegasus.Infrastructure/Persistence/Migrations/20260906054658_V1PlatformFoundation.cs`
   contains both `GRANT SELECT,INSERT,UPDATE ON OBJECT::[dbo].[PublicUploadSessions]`
   and `... [dbo].[PublicUploadOccurrences] TO [pegasus_web_runtime_role]`, and
   both tables appear in the `DENY DELETE` cursor list. `[pegasus_worker_runtime_role]`
   holds nothing on either table, so the remaining open question (Worker sweep
   grants, A's call) is stated accurately. The operation-key bound also checks
   out: `MaximumOperationKeyLength = 100` plus the `request:{32}:` prefix is at
   most 141 characters into `PublicUploadOccurrences.OperationKey`
   `nvarchar(450)` and the sender key alone into `RequestUploadReceipts.OperationKey`
   `nvarchar(256)`.
8. **Dead code / one owner / doc comments.** PASS. No legacy content-store path
   or helper survives; every private member has a caller; both ports the
   constructor dropped keep other production callers. Doc comments are one per
   member and explain the reason rather than restating the signature. See R-9
   and R-10 for the two blurred-ownership notes, and R-7 for the missing
   simplification record.

## Test lanes seen (wave 18, at `6c8b945bd`)

| Lane | Result | Detail |
| --- | --- | --- |
| 1-build | **PASS** | `Build succeeded. 0 Warning(s), 0 Error(s).` |
| 2-core | **PASS** | Failed 0, Passed 11 |
| 3-integration | **FAIL (1)** | Failed 1, Passed 58, Skipped 1. The single failure is the A-owned `DocumentCustodyDurabilityTests.FailedRequestUploadSaveRemovesUnreferencedContentBeforeSafeRetry`, pulled in by the lane's `FullyQualifiedName~RequestUpload` term; it is lane 4's subject and fails for lane 4's reason. Every C-owned test in the lane passed, `PublicUploadRetentionWebTests` included. The skip is `AcceptedCaseRetainsEmbeddedPhotographsBesideTheSource`, a pre-existing `[QdosMappingCustodyFact]` corpus gate. |
| 4-a-owned | **FAIL (4), A-owned** | Failed 4, Passed 1. All four fail identically: `SqlException: Cannot insert duplicate key row in object 'dbo.Principals' with unique index 'IX_Principals_Code'. The duplicate key value is (QDOS)`, thrown from `DocumentCustodyDurabilityTests.SeedCaseAsync` at `:495`. Three of the four (`StaffConfirmationOfThirdPartyVehicleEvidenceIsDurableAndExactlyReplayable`, `FailedDatabaseSaveRollsBackCaseAndRemovesUnreferencedContent`, `RemovingAFileWritesOneNoteTheOperatorCanActuallySee`) never touch `EfDocumentRequestStore` at all, which settles the cause: the fixture's own QDOS principal now collides with the 15 principals A's `V1PlatformFoundation` migration seeds. Confirmed as exactly the stated A-owned seed collision, and nothing else. The second stated A-owned reason — the legacy content-write premise — is **not observable at this head**, because the test aborts in seeding before it reaches an assertion. |
| 5-architecture | **PASS** | Failed 0, Passed 100 |

Lanes 1, 2 and 5 pass. Lane 3's recorded result is FAIL, so the dispatch's
"lanes 1,2,3,5 PASS" condition is not met as written, though its only failure is
lane 4's A-owned test. Lane 4 fails for one of the two stated A-owned reasons
and for nothing else. Even on a generous reading of the lane gate, the four
majors above are independently disqualifying.

## Residual risk if this is merged as-is

The public upload path is fail-closed and correct for a Confirmed disposition,
which is the only disposition any adapter produces today (there is no
`ICaseArtifactCustody` implementation in `src/`), so none of the four majors is
live at this head. They become live the moment A04 lands and can answer
`Pending`, `Failed`, `Unknown` or throw — which is also the moment the public
upload path stops returning 404. Fixing R-1 through R-4 before that
registration is much cheaper than diagnosing a double-counted link or a
permanently "Storing" document afterwards.

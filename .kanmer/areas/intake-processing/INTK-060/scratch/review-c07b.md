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

---

# SUPERSEDING ATTESTATION — correction round 1

This section supersedes the attestation above in full. The frontmatter block
below is the machine-facing record for head `6490623c3`; the one above is
retained only as the history of round 0.

```yaml
kind: review-attestation
pr: "none (controller override: no PR; worktree head review)"
head_sha: "6490623c3"
verdict: needs-changes
reviewer: "pegasus-reviewer (INTK-060 C07b, attempt 2 — correction round 1)"
independent: true
plan_hash: "pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md @ 6490623c3"
ticket_updated: "INTK-060 scratch/review-c07b version 3ec48aa8afcc1205"
board_sha: "n/a (controller override: Kanmer writes limited to scratch/review-c07b)"
expected_reviewers: []
threads_snapshot: []
findings:
  - id: C07B-R-1
    severity: major
    disposition: fixed
    summary: "Accepted totals are now derived from the session's confirmed/pending occurrences under an UPDLOCK,HOLDLOCK link read rather than incremented, and a new pre-custody state 'arrived' keeps an unoffered arrival out of the count. Verified exactly-once and replay-safe; Exhausted is set only from the derived totals. Proof: AReplayedArrivalThatEarnedNoReceiptIsStillCountedExactlyOnce."
  - id: C07B-R-2
    severity: major
    disposition: fixed
    summary: "The receipt is written only for a Confirmed disposition, and RetainIncomingArtifact reconciles Pending through ICaseArtifactCustodyStatus exactly as it reconciles Unknown. A Pending is no longer a dead end and is never re-offered bytes. Proof: APendingArrivalIsReconciledToConfirmed/ToFailedByTheNextArrivalWithTheSameKey."
  - id: C07B-R-3
    severity: major
    disposition: fixed
    summary: "RetainIncomingArtifact catches an uncertain hand-over, records the occurrence 'unknown' through the retention store and returns that retention; a refusal still surfaces. Proof: AThrownHandOverIsRecordedUnknownAndTheNextArrivalNeverRepeatsIt plus two Core proofs. Residual C07B-R-3a below."
  - id: C07B-R-4
    severity: major
    disposition: fixed
    summary: "New RequestUploadDecision.AcceptedPending; the page renders 'Your document was received and is being stored. You do not need to send it again.' and never 'retained securely' before custody has confirmed. Asserted positively and negatively through a real redirect follow."
  - id: C07B-R-5
    severity: minor
    disposition: fixed
    summary: "ApplyAcceptedTotalsAsync completes the Case workflow only when ArchivedCaseGuard.RequireMutable would still allow an edit (IsMutable), while still recording the arrival custody already holds. The choice is stated in the method's remarks."
  - id: C07B-R-6
    severity: minor
    disposition: fixed
    summary: "Request.cshtml.cs now also catches HttpRequestException, TimeoutException and SocketException, with a comment saying why. TaskCanceledException is correctly not caught: an aborted request is not a page render."
  - id: C07B-R-7
    severity: minor
    disposition: fixed
    summary: "The report carries a '## Simplification pass' with four honest lenses over the whole slice diff, including a stated cost (R-9's unindexed lookup now runs on the retry paths). Verified against the code: the reuse claims and the dead-code claim check out."
  - id: C07B-R-8
    severity: minor
    disposition: fixed
    summary: "Refuses<TException> asserts StaffAuthorizationException (and InvalidOperationException for holding) by exact type, and a fifth link is seeded non-Active with no revocation."
  - id: C07B-R-12
    severity: major
    disposition: open
    summary: "RetainIncomingArtifact.IsUncertainHandOver names HttpRequestException, which pulls System.Net.Http into Pegasus.Core — an explicitly forbidden Core dependency. DependencyDirectionTests.CoreHasNoInfrastructureOrHostDependencies now fails; lane 5 regressed from 100/100 at 6c8b945bd to 99/100 at 6490623c3."
  - id: C07B-R-13
    severity: minor
    disposition: open
    summary: "A hand-over cancelled rather than faulted (a bare TaskCanceledException / OperationCanceledException from the request-abort token) is not an uncertain hand-over, so the occurrence stays 'arrived', FindAsync reports no retention, and the same operation key re-offers the bytes — the re-offer C07B-R-3 was raised about, on a narrower path. Not recorded anywhere."
  - id: C07B-R-14
    severity: minor
    disposition: open
    summary: "RecordingCaseArtifactCustody.GetAsync (the new status port on the double) performs no RequireAuthority check, unlike RetainAsync. The reconciliation path's authority — the one property the hand-over double is careful to enforce — is unproven by any test."
  - id: C07B-R-3a
    severity: minor
    disposition: accepted-risk
    summary: "An 'unknown' recorded from a thrown hand-over carries no DocumentId/DocumentVersionId, so ReconcileAsync has no key and that retention stays Unknown for ever. Reason: accepted. No false success, no count, no re-offer under the same key, and the page mints a fresh operation key per render so no sender is locked out; the only true fix is an operation-key-addressed status read in A's frozen CustodyContracts.cs, which C07 does not own. Recorded by the implementer as C07B-R-3a."
  - id: C07B-R-15
    severity: note
    disposition: accepted-risk
    summary: "ApplyAcceptedTotalsAsync runs only on an accepted disposition, so a Pending reconciled to Failed leaves the link's totals one file high until the next accepted arrival recomputes them, and a link already Exhausted on the strength of that file never recovers (Exhausted is only ever set, never cleared). Reason: conservative direction; the first half is ASSUMPTION 6 and asserted in a test, the Exhausted half is unstated."
  - id: C07B-R-16
    severity: note
    disposition: accepted-risk
    summary: "LockLinkAsync hard-codes the table name [RequestUploadLinks] in raw SQL, so a rename would compile and fail only at runtime on SQL Server. Reason: the identical idiom is already used by six other stores; the non-SQL-Server fallback takes no lock, which is untested but not reachable in this suite."
  - id: C07B-R-17
    severity: note
    disposition: rejected-with-reason
    summary: "Pegasus.Core also references System.Data.Common (IntakeExceptionPolicy's DbException, from an earlier commit a55b94912). Reason: System.Data.Common is not on ForbiddenCoreDependencyPrefixes, it predates this slice, and no change is requested here."
  - id: C07B-R-9
    severity: note
    disposition: accepted-risk
    summary: "Carried forward. EfPublicUploadRetentionStore.FindAsync is a global SingleOrDefaultAsync on OperationKey with no matching index. Reason: the index is A-owned and the table is empty-to-small in v1. The correction widens its exposure to the Pending and Unknown retry paths, which the report states."
  - id: C07B-R-10
    severity: note
    disposition: accepted-risk
    summary: "Carried forward. DocumentVersionEntity.BoxFileId/BoxVersionId has two writers. Reason: unchanged this round, idempotent, Confirmed-only; no third writer added."
  - id: C07B-R-11
    severity: note
    disposition: rejected-with-reason
    summary: "Carried forward. Reason: DocumentCustodyDurabilityTests.cs is untouched by this round (confirmed by git diff --name-status 6c8b945bd..6490623c3). No change requested."
```

**Verdict: needs-changes.** Head `6490623c3`. Open: **1 major, 3 minors**
(C07B-R-3a is dispositioned `accepted-risk`, not open). All four round-0 majors
and all four round-0 minors are **fixed**. Ownership: **PASS**. The one blocker
is a layering regression the correction introduced, not a defect in the design
it corrected.

## What the correction did, and whether it holds

Three commits, seven files, all C-owned: `RequestUploadPolicy.cs`,
`RetainIncomingArtifact.cs`, `EfDocumentRequestStore.cs`, `Request.cshtml.cs`,
`RetainIncomingArtifactTests.cs`, `IncomingArtifactCustodyTests.cs`,
`PublicUploadRetentionWebTests.cs`. `git diff --name-status
6c8b945bd..6490623c3` confirms **no** `DocumentCustodyDurabilityTests.cs`, no DI
registration, no migration, no `src/Pegasus.Core/Custody/*`, no
`OperatorLabels.cs`. Ownership passes without qualification. (The worktree HEAD
`3c0e1931c` carries `6490623c3` plus an unrelated shared-branch merge —
`ApprovedMailboxAdministration.cs` and a corpus snapshot — which is not this
slice and was not reviewed.)

The keystone is right. Separating "we have not asked custody yet" (`"arrived"`)
from "custody answered Pending" (`"pending"`) is the distinction that was
missing, and it is what makes every other correction possible. It costs no
column, no index and no migration.

### Derived counting is exactly-once and replay-safe — verified

`ApplyAcceptedTotalsAsync` (`EfDocumentRequestStore.cs:566-596`) **sets** the
link's totals to a grouped `Count`/`Sum` over the session's occurrences in
`confirmed` or `pending`, rather than incrementing. Idempotence is then
structural: recomputing from the same committed set yields the same answer
however many times it runs.

- **Same key twice.** `AuthorizeAndRecordArrivalAsync` (`:394-420`) is
  get-or-create on `(SessionId, ScopedOperationKey)`, so a repeat reuses the one
  occurrence row (and refuses `OperationConflict` on different bytes). One row,
  one count. Verified in code and by
  `AReplayedArrivalThatEarnedNoReceiptIsStillCountedExactlyOnce`, which drives
  exactly the receipt-less branch that broke in round 0 and asserts one file,
  31 bytes and `Status == Active`.
- **Two concurrent arrivals with different keys.** `LockLinkAsync` (`:612-628`)
  is the transaction's **first** statement and takes `WITH (UPDLOCK, HOLDLOCK)`
  on the link row, so the second transaction blocks until the first commits and
  then recomputes over a set that already includes the first. Without the lock
  this would be a classic lost update (A reads 1, B reads 2, A commits last,
  the total sticks at 1); with it, every interleaving lands on the true count.
  Each arrival's own occurrence state is committed by
  `EfPublicUploadRetentionStore.RecordAsync` on its own context *before*
  `RecordAcceptedAsync` begins, so the read under the lock can never miss its
  own row. The lock is also the first lock taken, so two submissions queue on
  the link rather than deadlocking on the workflow row — the remarks say this
  and the code does it.
- **`Exhausted`.** Set only from `fileCount`/`byteCount` as derived
  (`:582-587`). No increment survives anywhere: `AcceptedFileCount` and
  `AcceptedByteCount` have exactly one writer in `src/`, these two lines.

The one thing not proved is the lock itself: no test drives two concurrent
POSTs. The raw SQL *is* executed by every accepted upload in the suite
(`IntakeWebApplicationFactory` runs on `LocalDbTestDatabase`, so
`IsSqlServer()` is true), so the statement is syntactically and semantically
exercised; only the contention is not. That is a test gap rather than a defect,
and I am not raising it as a finding.

### Pending and Unknown are reconciled, never re-offered — verified

`RetainIncomingArtifact.ExecuteAsync:145-153` sends **both** `Pending` and
`Unknown` to `ReconcileAsync`; only `Failed` — "custody said no, and said so" —
falls through to a second `RetainAsync`. `ReconcileAsync` now also refuses to
carry a remote identity for anything but a Confirmed answer, matching `Project`.
`RecordAsync` never downgrades a Confirmed, because `ExecuteAsync` returns a
confirmed retention before reaching it.

The receipt-only-on-Confirmed rule leaves the rest consistent. The only reader
of `RequestUploadReceipts` outside this store is `EfOperationsStore.cs:252`
(`lastReceiptAtUtc`), which now reflects confirmed files only — more accurate,
not less. `IGetRequestUpload`, revocation and the Case documents tab read no
receipt (re-verified by grep at this head), and `ToCreatedUploadLink`'s snapshot
check is on creation, untouched.

### `"arrived"` can never be read back as a custody answer — verified

Three readers only. `FindAsync` returns `null` for it before `ParseCustodyState`
is reached (`:952-959`); `ParseCustodyState` throws on it and is called from
nowhere else; the totals query compares against `ConfirmedCode`/`PendingCode`
explicitly, so `arrived` is neither counted nor mapped.
`AnArrivalNotYetOfferedToCustodyReportsNoRetention` proves the null and then
proves the first custody answer replaces it.

### The changed assertion is stronger — agreed

`Assert.Equal("arrived", call.CustodyStateAtHandOver)` proves the arrival
carried **no custody answer at all** at the moment of the hand-over.
`"pending"` could not distinguish that from a Pending custody had given. The
report's claim is accurate and no existing assertion was weakened.

### C07B-R-3a — acceptable recorded residual, not a major

An `unknown` written from the catch block is constructed with `DocumentId` and
`DocumentVersionId` defaulted to null (`RetainIncomingArtifact.cs:180-186`), so
`ReconcileAsync`'s guard returns `existing` unchanged and that retention stays
Unknown for ever. I judge this **acceptable**, dispositioned `accepted-risk`, on
four grounds:

1. Nothing false is ever rendered, nothing is counted, no receipt is written,
   and no document exists — all asserted by
   `AThrownHandOverIsRecordedUnknownAndTheNextArrivalNeverRepeatsIt`, which also
   asserts `custody.Calls == 1` and `StatusCalls == 0` on the retry. The
   residual is *proved*, not merely stated.
2. The bytes are never offered twice under that key, which is the property the
   Unknown state exists for.
3. No sender is locked out. `Request.cshtml.cs:59,68` mints a fresh
   `NewOperationKey()` on every GET, so a real retry through the page is a new
   occurrence that succeeds; only a scripted client reusing the key sees the
   dead end.
4. C07 cannot fix it. Custody assigns the document and version identities and
   returns them on the response that never arrived, and
   `ICaseArtifactCustodyStatus.GetAsync` is addressed by
   `(caseId, documentId, versionId)` in A's frozen `CustodyContracts.cs`. The
   real fix is an operation-key-addressed status read, which is A's to make.

The residue is one orphaned artifact in custody per transport fault, invisible
to the sender and recoverable by an A-owned sweep. The implementer recorded it
on `scratch/c07-notes` rather than inventing a second hand-over, which is the
right call.

## The open major

### C07B-R-12 (major) — the uncertain-hand-over classifier puts `System.Net.Http` into Core

`src/Pegasus.Core/Intake/RetainIncomingArtifact.cs:204-207` (commit
`05d9a0e49`):

```csharp
private static bool IsUncertainHandOver(Exception exception) =>
    exception is HttpRequestException
    || IntakeExceptionPolicy.IsTransientFailure(exception)
    || (exception.InnerException is { } inner && IsUncertainHandOver(inner));
```

`HttpRequestException` lives in `System.Net.Http`, and
`tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs:25-41` lists
`"System.Net.Http"` in `ForbiddenCoreDependencyPrefixes` — with an explicit
`[InlineData("System.Net.Http", true)]` row confirming it is deliberate, not
incidental. `CoreHasNoInfrastructureOrHostDependencies` now fails:

```
Assert.DoesNotContain() Failure: Filter matched in collection   (pos 12)
Collection: [..., System.Net.Http, Version=10.0.0.0, ...]
```

Lane 5 was **100/100 at `6c8b945bd`** and is **99/100 at `6490623c3`**. This is
a regression introduced by this correction round, and it is a real layering
statement, not a lint: Core is not allowed to know what transport the adapter
speaks. The simplification pass names the addition ("`IsUncertainHandOver` adds
only `HttpRequestException`, which the shared policy does not name and this seam
needs") and does not notice that the shared policy's silence on it is the rule,
not an omission.

**Fix — three shapes, in order of preference.**

1. Let the adapter translate. `IntakeExceptionPolicy.IsTransientFailure` already
   names `IntakeDependencyUnavailableException` as "the dependency-unavailable
   fault adapters translate to". A04's HTTP adapter wrapping its transport
   faults in that is the layering the codebase already has, and Core keeps one
   classifier.
2. Drop the clause and rely on `IsTransientFailure` alone. An `HttpClient`
   timeout surfaces as `TaskCanceledException` wrapping `TimeoutException`,
   which the recursive inner check already catches; a socket-level failure
   surfaces as `IOException`, likewise. The loss is narrower than it looks.
3. Invert the predicate: treat every exception as uncertain *except* the named
   refusals (`StaffAuthorizationException`, `ArgumentException`,
   `OperationCanceledException`). This also closes C07B-R-13, and it matches the
   command's own remark that a hand-over which neither confirmed nor refused is
   uncertain *whatever* threw.

Whichever is chosen, `Request.cshtml.cs` may keep `HttpRequestException` and
`SocketException` — `Pegasus.Web` is not under the rule.

## The open minors

### C07B-R-13 (minor) — a cancelled hand-over is not an uncertain one

`OperationCanceledException` is excluded from `IntakeExceptionPolicy` by design
and is not named by `IsUncertainHandOver`, so a bare `TaskCanceledException`
from the request-abort token propagates. The occurrence stays `"arrived"`,
`FindAsync` reports no retention, and the same operation key **re-offers the
bytes** — precisely the failure C07B-R-3 was raised about, on the narrower
sender-disconnect path. An `HttpClient` *timeout* is safe (its inner
`TimeoutException` is caught); a genuine client abort is not. Mitigating: the
page mints a fresh key per render, and recording through a cancelled token would
itself have to fail, so a real fix needs a fresh token or shape 3 above.
**Fix.** Take shape 3 of C07B-R-12, or record this in `scratch/c07-notes` beside
C07B-R-3a. It is currently unrecorded, which is the part I object to.

### C07B-R-14 (minor) — the status port on the double enforces no authority

`RecordingCaseArtifactCustody.RetainAsync` calls `RequireAuthority` and is the
whole basis of the "the double genuinely enforces A's rule" finding from round
0. The new `GetAsync` (`PublicUploadRetentionWebTests.cs:935-978`) calls
nothing: it takes `ActionActor actor` and never reads it. So the reconciliation
path — a second, newly-reachable way into custody — has no authorization proof
at all, and `CustodyRefusesEveryAuthorityThatIsNotThisExactActiveLink` does not
cover it. **Fix.** Call `RequireAuthority` from `GetAsync` too, and extend the
refusal test with one status-path case.

## Test lanes seen (wave 21, at `6490623c3`)

| Lane | Result | Detail |
| --- | --- | --- |
| 1-build | **PASS** | exit 0, `Build succeeded. 0 Warning(s) 0 Error(s)` |
| 2-core | **PASS** | Failed 0, Passed 16 (up from 11: the three new `RetainIncomingArtifactTests` proofs, one of them a 3-case `[Theory]`) |
| 3-integration | **PASS** | Failed 0, Passed 58, Skipped 1. Round 0's single failure is gone: the lane no longer drags in the A-owned durability test. The skip is the pre-existing `[QdosMappingCustodyFact]` corpus gate. |
| 4-a-owned | **FAIL (4), A-owned — as expected** | Failed 4, Passed 1. All four fail identically in `DocumentCustodyDurabilityTests.SeedCaseAsync` with `SqlException: Cannot insert duplicate key row in object 'dbo.Principals' with unique index 'IX_Principals_Code'. The duplicate key value is (QDOS)`. Identical to wave 18 in count, tests and message. Confirmed as exactly the stated A-owned seed collision and nothing else. |
| 5-architecture | **FAIL (1)** | Failed 1, Passed 99. `DependencyDirectionTests.CoreHasNoInfrastructureOrHostDependencies` — C07B-R-12. **Regressed by this round** (100/100 at `6c8b945bd`). |

The dispatch's pass condition is lanes 1, 2, 3, 5 PASS with lane 4 failing only
for the stated A-owned reason. Lane 4 is exactly as stated and lane 3 is now
clean, but **lane 5 fails**, so the condition is not met — and it fails for a
defect this round introduced, which is independently disqualifying.

## Ownership, one owner per rule, dead code — PASS

- **Seven files, all C-owned.** Verified by `git diff --name-status`.
- **One owner per rule.** The link's accepted totals now have exactly one writer
  in `src/` (`ApplyAcceptedTotalsAsync`). The receipt has one writer. The five
  occurrence state codes are `internal const` on `EfPublicUploadRetentionStore`,
  so the accept path's query and the store's own mapping cannot drift. What
  "retained" means stays in the Core command; what is stored and when it counts
  stays in the store; which sentence the sender reads stays on the page. The one
  exception is honest and recorded: the Pending completion string is a page
  constant until C08's labels batch moves it to `OperatorLabels`, stated in the
  constant's own doc comment and in `scratch/c07-notes`.
- **No dead code.** `AcceptedPending` has one producer and one consumer;
  `NotRetained` keeps its page case and both store callers; all five state
  constants are reached through `ToCode`/`ParseCustodyState`/the totals query;
  `ApplyAcceptedTotalsAsync`, `LockLinkAsync` and `IsMutable` each isolate a
  rule with its own remarks in a method that already carried three. The
  `RequestUploadDecision` switch in `Request.cshtml.cs` handles all ten members,
  so the new one cannot fall through.
- The `UPDLOCK, HOLDLOCK` reuse claim checks out: the identical idiom appears in
  `EfTriageStore`, `EfCaseWorkflowStore`, `EfIntakeReceiptStore`,
  `EfApprovedOutlookCategoryStore`, `EvaHandoffStore` and `EvaSubmissionStore`.

## Residual risk

With C07B-R-12 fixed, this slice would pass. The four corrections are the right
corrections, they are proved by tests that drive the real failure paths through
real SQL and real POSTs, and the report's dispositions are honest — including
the one place it states a cost it chose to accept rather than hide. The
remaining open items are one layering regression, one narrow unrecorded
re-offer path, and one unproved authorization on the new status port. None of
them touches the accounting or the sender-facing claim, which are the two
things round 0 blocked on.

# SUPERSEDING ATTESTATION — correction round 2

This section supersedes both attestations above in full. The frontmatter block
below is the machine-facing record for head `f55a5adac`; the two above are
retained only as the history of rounds 0 and 1.

```yaml
kind: review-attestation
pr: "none (controller override: no PR; worktree head review)"
head_sha: "f55a5adac9e4b84fdb1869213d422ed9cd1d6036"
verdict: needs-changes
reviewer: "pegasus-reviewer (INTK-060 C07b, attempt 3 — correction round 2)"
independent: true
plan_hash: "pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md @ f55a5adac (INTK-060 doc plan version 62649b22a7e43d77)"
ticket_updated: "INTK-060 scratch/c07-notes version 55f148c196c0e40c; scratch/review-c07b version fe52d464aa49961a"
board_sha: "28e7ba102879dca36f734addbaff18802014c045"
expected_reviewers: []
threads_snapshot:
  - source: manual
    id: "PR 673 comment 5560704411 (Stream A), relayed by the controller"
    author: "stream-a"
    resolved: false
    finding: C07B-R-3a
  - source: manual
    id: "PR 673 comment 5560704411 (Stream A), second point, relayed by the controller"
    author: "stream-a"
    resolved: false
    finding: C07B-R-22
findings:
  - id: C07B-R-12
    severity: major
    disposition: fixed
    summary: "The layering regression is gone. IsUncertainHandOver no longer names HttpRequestException; no code in src/Pegasus.Core references any System.Net.Http type, so the compiler emits no reference. The only two mentions in Core source are explanatory comments (RetainIncomingArtifact.cs:221 and the pre-existing EvaSubmissionWorkItem.cs:192-193). Lane 5 is 100/100. The predicate that replaced it is sound in shape but rests on a refusal contract A has now contradicted — tracked as C07B-R-22, not as this finding."
  - id: C07B-R-13
    severity: minor
    disposition: fixed
    summary: "A cancelled hand-over is uncertain like any other and the Unknown record is written on CancellationToken.None. The proof is structural, not a comment: RecordingStore.RecordAsync calls ThrowIfCancellationRequested and ThrowingCustody cancels the source after reading the bytes, so AHandOverCancelledAfterTheBytesWereReadIsUncertainAndIsStillRecorded fails if the fresh token is ever removed."
  - id: C07B-R-14
    severity: minor
    disposition: fixed
    summary: "RecordingCaseArtifactCustody.GetAsync now calls StaffAuthorization.Require(actor, PerformCasework) — counted before the rule is applied so a test can prove the read was attempted and refused — CustodyRefusesEveryAuthorityThatIsNotThisExactActiveLink gained the direct status-path case, and ReconcileAsync catches the refusal and returns the retention unchanged. The Core-side double throws UnreachableException if the rule ever stops refusing, so it cannot decay into a no-op."
  - id: C07B-R-14a
    severity: minor
    disposition: accepted-risk
    summary: "New residual this round records rather than hides: a public Pending that custody later confirms earns no RequestUploadReceipt and never opens the fixed session window, because both belong to the accept path and a reconciliation does not re-enter it. Reason: accepted. It does not breach plan item 7 — pending/failed/unknown are still persisted and rendered, still never render success, and the logical document/version identities are still kept for a reading authority (Project carries DocumentId/VersionId for every disposition), which is what lets AStaffReconciliationConfirmsAPendingArrivalWithoutASecondHandOver converge. Item 7 nowhere requires the public sender to be the reconciling authority; item 6's window is defined on a file whose custody is accepted, which this never becomes on that path. Closing it is one of A's two handoff shapes on scratch/c07-notes."
  - id: C07B-R-3a
    severity: major
    disposition: open
    summary: "RE-DISPOSITIONED from accepted-risk on Stream A's evidence (PR 673 comment 5560704411). An Unknown recorded from a thrown hand-over names no DocumentId/DocumentVersionId, so ReconcileAsync has nothing to ask with; rounds 0-2 all treated 'Request.cshtml.cs mints a fresh operation key per GET, so no sender is locked out' as the mitigation. It is the duplicate vector instead. AuthorizeAndRecordArrivalAsync dedupes only on (SessionId, scopedOperationKey) (EfDocumentRequestStore.cs:396-428) and nothing dedupes on Sha256, so a sender who does exactly what the page tells them — reload, send the same file again — arrives under a NEW key, matches no occurrence, and offers custody bytes it may already hold from the ambiguous first hand-over. That is a duplicate retained copy, on the ordinary page flow, not a scripted-client edge. Fix (both halves): C's retry must reconcile the existing arrived/unknown/pending occurrence for that link and content instead of minting a new key and re-offering bytes — the occurrence row already carries Sha256 per session, so the lookup exists — and the status read that resolves an Unknown carrying no document identities is A's contract change (an operation-key- or content-addressed read on ICaseArtifactCustodyStatus)."
  - id: C07B-R-22
    severity: major
    disposition: open
    summary: "The premise under the inverted classifier is false against A's real adapter. IsUncertainHandOver treats StaffAuthorizationException and ArgumentException as 'the two refusals custody raises before it has read anything' (RetainIncomingArtifact.cs:200-234, and the same sentence in the report and in the new Core test's name). Stream A (PR 673 comment 5560704411) states its adapter rechecks authority AFTER reading the bytes and before committing, so a StaffAuthorizationException can be raised with the bytes already staged. Such a throw is classified as a refusal: nothing is recorded, the occurrence stays 'arrived', FindAsync reports no retention, and the same or a fresh key re-offers bytes custody may be holding in staging — the exact re-offer C07B-R-3 was raised about, reintroduced through the refusal branch. The same argument applies to ArgumentException, which library internals raise mid-stream as readily as a validation gate does. The classifier cannot be written from Core's guesses about when custody throws: it needs A's explicit refusal contract (which exception types mean 'nothing was staged', guaranteed, and raised only before the stream is touched), recorded on scratch/c07-notes and reflected in both the predicate and the double. Until then, either the named-refusal set shrinks to types A guarantees are pre-read, or a refusal must also record a durable arrival state that the next attempt reconciles."
  - id: C07B-R-18
    severity: minor
    disposition: open
    summary: "StaffAuthorizationException derives straight from Exception (StaffAuthorization.cs:76) and is NOT on the public page's recoverable filter (Request.cshtml.cs:178-186, which lists ArgumentException, InvalidOperationException, IOException, UnauthorizedAccessException, HttpRequestException, TimeoutException, SocketException, DbUpdateException). This round makes it one of exactly two types that surface from a hand-over, and the handoff instructs A to use it for a refusal that must surface. A link revoked between C's arrival transaction and the hand-over — which deliberately runs outside that transaction — therefore reaches the page unhandled: the generic /Error page instead of the plain retry message, and LogPublicRequestUploadFailure never runs. Fix: one entry on the same filter C07B-R-6 already extended, or map the refusal to NotRetained in EfDocumentRequestStore. May be retired rather than fixed by C07B-R-22."
  - id: C07B-R-19
    severity: note
    disposition: superseded
    summary: "Superseded by C07B-R-22, which is the same defect with A's evidence behind it. Raised here as: the handoff states one half of the exception contract ('do not raise InvalidOperationException for a refusal you want to surface') and not the other — that a refusal must be raised before the content stream is touched, and that nothing raised after it may be an ArgumentException."
  - id: C07B-R-20
    severity: note
    disposition: accepted-risk
    summary: "ReconcileAsync's catch does not distinguish which actor was refused and Core records nothing about the refusal, so a mis-wired staff or system-worker authority on the status port is indistinguishable from the expected public refusal: the retention silently keeps its state and a sweep looks like it ran. Reason: accepted. It matches the two silent 'cannot ask' returns the method already had, Core commands here carry no logger, and no state is falsified."
  - id: C07B-R-21
    severity: note
    disposition: open
    summary: "Evidence hygiene. The report's round-2 commit table names 955bd7558 for the C07B-R-14 commit. That object exists but is on no branch (git branch --contains is empty); the branch carries f55a5adac, which is 955bd7558 plus one blank line in RetainIncomingArtifact.cs (git diff 955bd7558 f55a5adac = 1 insertion). No behavioural difference, but the report's SHA does not name a commit on the branch. Fix: correct the table to 4b7e9930d / f55a5adac."
  - id: C07B-R-9
    severity: note
    disposition: accepted-risk
    summary: "Carried forward unchanged. EfPublicUploadRetentionStore.FindAsync is a global SingleOrDefaultAsync on OperationKey with no matching index; the index is A-owned and the table is empty-to-small in v1."
  - id: C07B-R-10
    severity: note
    disposition: accepted-risk
    summary: "Carried forward unchanged. DocumentVersionEntity.BoxFileId/BoxVersionId has two writers; idempotent, Confirmed-only, no third writer added this round."
  - id: C07B-R-11
    severity: note
    disposition: rejected-with-reason
    summary: "Carried forward. DocumentCustodyDurabilityTests.cs is untouched by this round — git diff --name-status 3c0e1931c..f55a5adac lists three files and none is A-owned."
  - id: C07B-R-15
    severity: note
    disposition: accepted-risk
    summary: "Carried forward unchanged. A Pending reconciled to Failed leaves the link's totals one file high until the next accepted arrival recomputes them, and an Exhausted link never recovers."
  - id: C07B-R-16
    severity: note
    disposition: accepted-risk
    summary: "Carried forward unchanged. LockLinkAsync hard-codes [RequestUploadLinks] in raw SQL, matching six other stores."
  - id: C07B-R-17
    severity: note
    disposition: rejected-with-reason
    summary: "Carried forward. System.Data.Common is not on ForbiddenCoreDependencyPrefixes and predates this slice."
```

**Verdict: needs-changes.** Head `f55a5adac`. Open: **2 majors** (C07B-R-3a,
C07B-R-22), **1 minor** (C07B-R-18), 2 open notes (C07B-R-19 superseded,
C07B-R-21). Round 1's own three findings — the major C07B-R-12 and the minors
C07B-R-13 and C07B-R-14 — are all **fixed**, and fixed well. Ownership:
**PASS** — three files, all C-owned.

Both open majors are the same discovery arriving from two directions: **Core
cannot decide what a custody exception means by reasoning about when custody
probably throws.** Neither is a defect in the corrections this round made; both
are those corrections meeting Stream A's actual adapter for the first time.

## Scope of this round

`git diff --name-status 3c0e1931c..f55a5adac`: `RetainIncomingArtifact.cs`,
`RetainIncomingArtifactTests.cs`, `PublicUploadRetentionWebTests.cs`. Nothing
A-owned: no `CustodyContracts.cs`, no `DocumentCustodyDurabilityTests.cs`, no
DI registration, no migration, no `DependencyDirectionTests.cs`, no
`OperatorLabels.cs`. Two commits: `4b7e9930d` (R-12, R-13) and `f55a5adac`
(R-14).

## C07B-R-12 — the layering fix holds; the rule that replaced it does not

The mechanical part is settled. `grep -rn
"System.Net.Http\|HttpRequestException" src/Pegasus.Core --include=*.cs`
returns exactly two comment sites: `RetainIncomingArtifact.cs:221` and the
pre-existing `EvaSubmissionWorkItem.cs:192-193`. No code names the type.
`obj/`'s `GlobalUsings.g.cs` still carries `global using System.Net.Http;` from
`ImplicitUsings`, which is why the architecture test passed for years before
this slice existed: a using directive emits no assembly reference, only a used
type does. `IntakeExceptionPolicy` keeps live callers elsewhere and both `<see
cref>`s in the new remarks resolve (`IsRecoverable` at `IntakeContracts.cs:592`,
`IntakeDependencyUnavailableException` at `:930`), so nothing dead was left
behind. Lane 5 is back to 100/100.

The `try` wraps only `custody.RetainAsync` (`:158-173`), so `Validate`'s own
two throws cannot reach the filter — inside it, those types can only have come
from custody. That much of the design is right.

**But the premise is wrong, and A says so.** The rule reads "everything is
uncertain except the two refusals custody raises *before it has read
anything*". A's adapter rechecks authority after reading the bytes and before
committing. So `StaffAuthorizationException` is not a pre-read refusal, and a
throw from that recheck lands on the refusal branch: nothing is recorded, the
occurrence stays `arrived`, and bytes that are already staged inside custody
are eligible to be offered again. That is C07B-R-22, and it is the one
misclassification direction that is *not* safe.

I had reached the weaker form of this independently before A's comment arrived,
about `ArgumentException` rather than `StaffAuthorizationException` — the type
is raised from library internals mid-stream as readily as from a validation
gate, and `ArgumentNullException`/`ArgumentOutOfRangeException` inherit it. That
was C07B-R-19, a note asking for one sentence of handoff. A's evidence makes it
a major and makes the fix structural rather than documentary: **Core must be
told which types mean "nothing was staged", guaranteed; it cannot infer them.**

Two shapes, either acceptable:

1. **Shrink the named set to what A guarantees is pre-read.** If A can promise
   a distinct refusal raised before the stream is touched — and only there —
   name that, and treat every post-read throw, authority failures included, as
   uncertain. This keeps the inversion and its whole benefit.
2. **Make a refusal durable too.** If a refusal can arrive after staging, then
   a refusal must also record an arrival state the next attempt reconciles,
   rather than leaving the occurrence `arrived`. More code, but it stops
   depending on a promise about throw ordering.

What must not survive is the current shape plus a comment asserting an ordering
A does not implement. The Core test's own name —
`TheTwoRefusalsCustodyRaisesBeforeReadingAnythingSurfaceUnrecorded` — asserts
that ordering as fact, so it has to change with the rule.

## C07B-R-3a — the "fresh key per GET" mitigation is the duplicate vector

Rounds 0, 1 and 2 all leaned on the same sentence: an Unknown that named no
document is a dead end for that key, but `Request.cshtml.cs:59,68` mints a new
`NewOperationKey()` on every GET, so a real sender retrying through the page is
a new occurrence that succeeds and nobody is locked out. I repeated it in my own
first pass at this head. It is wrong in the direction that matters, and A is
right to force it open.

The dedupe is only `(SessionId, scopedOperationKey)`
(`EfDocumentRequestStore.cs:396-428`); the occurrence row stores `Sha256` but
nothing ever matches on it, and the only content check is *within* one key
(same key + different bytes → `OperationConflict`). So the sequence is:

1. hand-over throws or is cancelled; C records Unknown with no document
   identities and returns `NotRetained`;
2. the page tells the sender "The document could not be retained. Try again
   using the same upload operation" — but reloading the page, which is what a
   sender does, mints a **different** key;
3. the retry matches no occurrence, passes every guard, and offers the same
   bytes to custody a second time;
4. if the first hand-over did reach custody — which is precisely what "Unknown"
   means — the link now holds two retained copies of one file, and both count.

So the honest statement is not "no sender is locked out": it is "the sender's
natural retry can duplicate the file". The same path is now reached by every
exception type the inverted classifier does not name, not only transport
faults, which widens it rather than narrowing it.

**Fix, both halves.** C's half is reachable in C-owned code today: a retry must
find the existing non-terminal occurrence for this link and this content —
`arrived`, `unknown` or `pending`, keyed on `(SessionId, Sha256)`, which the row
already carries — and reconcile *that* occurrence under its original operation
key rather than creating a second one. A's half is the read that can actually
resolve an Unknown carrying no document identities: a status lookup addressed
by operation key or content hash on `ICaseArtifactCustodyStatus`, which is a
change to A's frozen `CustodyContracts.cs` and not C's to make. Neither half
alone closes it: without A's read the reconciled occurrence stays Unknown
(honestly, and without duplicating), and without C's lookup A's read is never
reached.

## C07B-R-13 — closed

`store.RecordAsync(uncertain, CancellationToken.None)` at `:191` with the
reason beside it, and the proof is the store rather than the comment:
`RecordingStore.RecordAsync` calls `ThrowIfCancellationRequested` and
`ThrowingCustody` cancels the source *after* `CopyTo`, so the test fails if the
fresh token is ever removed. That is the shape a proof of a cancellation
ordering rule has to have.

One consequence worth stating: a cancelled hand-over no longer propagates
`OperationCanceledException` out of `ExecuteAsync` — it returns an Unknown
retention. On the public path the caller returns `NotRetained` and the response
is written to a socket the sender already dropped, so nothing observable
changes and the durable record is the point. That Unknown is one of the ones
C07B-R-3a is about.

## C07B-R-14 — closed, and the gap it exposed does not breach plan item 7

The double enforces the rule now, counted before it is applied so a test can
prove the read was attempted rather than skipped; `ReconcileAsync` catches the
refusal and returns `existing`; and three tests show refusal and staff
reconciliation side by side rather than one standing in for the other.

Plan item 7 requires: persist and render `Pending`/`Confirmed`/`Failed`/
`Unknown`; pending/failed/unknown never renders upload success, never consumes
finalization, never ages out from staging; a confirmed replay returns the same
logical document/version; re-evaluation reads that exact logical version
through A04. All four hold at this head. The last two are what `Project`
preserves by carrying `DocumentId`/`VersionId` for every disposition
(`:308-320`), which is exactly what lets a *staff* reconciliation converge.
Item 7 nowhere makes the public sender the reconciling authority, so the
residual is acceptable pending A's contract choice and is recorded as
C07B-R-14a rather than waved through.

What the round changed in the sender's world is narrower than it looks: before,
a public retry of a Pending reconciled and earned the receipt and the window —
but only because the double was permissive about an authority A04 refuses. That
green was a fiction, and replacing it with the refusal is the honest move even
though it costs a receipt.

## The consequential change — the double's holding refusal

`InvalidOperationException` → `StaffAuthorizationException` for "a request-link
actor cannot retain into holding". Correct for the double under the classifier
as written, and **the production caller depends on neither type**: the only
production caller is `EfDocumentRequestStore.ExecuteAsync`, which passes
`arrival.CaseId` — the link's own recorded Case, read inside the authorizing
transaction — so the null-destination branch is unreachable from the public
path, and the store catches no custody exception type at all (its one `catch
(ArgumentException)` is around the token-digest lookup at `:236`). Nothing in
`src/` reads a custody refusal by type. The cost the implementer states — the
holding case losing its distinct type — is real and is stated in the test's own
comment.

It is the *page* that has the gap, and that is C07B-R-18: the type this round
promotes to "the refusal that surfaces" is the one type the page's recoverable
filter does not name.

## The three review questions

**Did the plan miss anything the ticket implies?** Yes, and it is what both
majors are about. Plan item 7 fixes the four states, the operation key and the
logical-version recovery, but says nothing about the exception or authority
contract of `ICaseArtifactCustody`/`ICaseArtifactCustodyStatus` — and "never
blindly resubmitted" depends entirely on that contract. It also assumes the
operation key is the only identity a retry needs, which C07B-R-3a shows is
false once a hand-over can fail without naming a document. C did not invent the
missing contract; it recorded what it needed as an A handoff with two named
shapes. That was the right instinct, one round too small.

**Did the implementation miss anything in the plan?** Not in the plan's own
terms — every item-7 property holds at this head. It missed something the
ticket implies: item 7's "never blindly resubmitted" is defeated by the
fresh-key path in C07B-R-3a, which is C-owned code and was treated as a
mitigation rather than a hole in all three rounds, mine included.

**Did the simplification pass run with honest dispositions?** Yes, and
specifically enough to check. The reuse claim (dropping `IsTransientFailure`
because it answers "is this worth a bounded retry", a different question) is
right, and the policy keeps its other callers. The dead-code claim checks out:
`RecordingCustodyStatus` keeps its two tests, `RefusingCustodyStatus` has one,
`ThrowingCustody`'s new optional parameter has exactly one caller. The altitude
claim — that A's status rule deliberately did *not* move into Core — is the
right call. It also states a cost it could have hidden. What it does not notice
is that the *other* rule it moved **into** Core is A's throw-ordering rule,
which Core has no way to enforce and A does not implement; that is C07B-R-22.

## Ownership and dead code — PASS

Three files, all C-owned, confirmed by `git diff --name-status`. No new
concept, no new query, no new column, no new registration. `Request.cshtml.cs`
was correctly left alone for the layering fix (`Pegasus.Web` is not under the
Core dependency rule).

## Test lanes seen (wave 25, at `f55a5adac`)

| Lane | Result | Detail |
| --- | --- | --- |
| 1-build | **PASS** | exit 0, `Build succeeded. 0 Warning(s) 0 Error(s)`. |
| 2-core | **PASS** | Failed 0, Passed 19 (16 at `6490623c3`: three net new proofs, one renamed). |
| 3-integration | **PASS** | Failed 0, Passed 59, Skipped 1. One more than round 1's 58 — the new staff-reconciliation test. The skip is the pre-existing `[QdosMappingCustodyFact]` corpus gate. |
| 4-a-owned | **FAIL (4), A-owned — exactly as stated** | Failed 4, Passed 1. All four fail in `DocumentCustodyDurabilityTests` seeding with `SqlException: Cannot insert duplicate key row in object 'dbo.Principals' with unique index 'IX_Principals_Code'. The duplicate key value is (QDOS).` Identical in count, tests and message to waves 18 and 21. Nothing else. |
| 5-architecture | **PASS** | Failed 0, Passed **100** — back from 99/100 at `6490623c3`. `CoreHasNoInfrastructureOrHostDependencies` is green, which is the mechanical confirmation that C07B-R-12's layering half is fixed. |

The dispatch's pass condition on lanes is met in full: 1, 2, 3 and 5 PASS and
lane 4 fails only for the stated A-owned seed collision. The lanes do not
change the verdict: the two open majors decide it on their own, and neither is
a test failure — both are behaviour the suite currently asserts as correct.

---

# SUPERSEDING ATTESTATION — correction round 3

```yaml
kind: review-attestation
pr: "none (controller override: no PR; worktree head review)"
head_sha: "0a0e8897505dd853a3460522584274188e6d7b51"
verdict: needs-changes
reviewer: "pegasus-reviewer (INTK-060 C07b, round 3)"
independent: true
plan_hash: "62649b22a7e43d77"
ticket_updated: "2026-09-06T17:37:17.700Z"
board_sha: "09dd7b10635c74b482806dcb755e99916404d8d3"
expected_reviewers: []
threads_snapshot:
  - source: manual
    id: "PR673-comment-5560737585"
    author: "stream-a"
    resolved: true
    finding: C07B-R-22
  - source: manual
    id: "PR673-comment-5560753915"
    author: "stream-a"
    resolved: false
    finding: C07B-R-24
  - source: manual
    id: "PR673-comment-5560704411"
    author: "stream-a"
    resolved: true
    finding: C07B-R-3a
  - source: manual
    id: "PR673-comment-5560716222"
    author: "stream-c"
    resolved: true
    finding: C07B-R-3a
  - source: manual
    id: "PR673-comment-5560748861"
    author: "stream-c"
    resolved: false
    finding: C07B-R-25
  - source: manual
    id: "PR673-comment-5560761330"
    author: "stream-c"
    resolved: false
    finding: C07B-R-24
findings:
  - id: C07B-R-3a
    severity: major
    disposition: fixed
    summary: "Identityless Unknown now reconciles by the ORIGINAL operation key through G15 FindByOperationKeyAsync, copying recovered DocumentId/VersionId; a null lookup returns the uncertain outcome and never authorizes a fresh key. RequestUploadPublicView.UnresolvedOperationKey plus UnscopeOperationKey re-present the sender's own key while an arrived/unknown/pending occurrence stands for the link, so no fresh key is minted over an unresolved occurrence. Proof: AnIdentitylessUncertainHandOverIsRecoveredByItsOriginalOperationKey (Core), ARecordThatFailsAfterCustodyAcceptedIsRecoveredByTheOriginalKey and TwoSimultaneousSubmissionsOfOneOperationKeyOfferTheBytesOnce (real SQL)."
  - id: C07B-R-22
    severity: major
    disposition: fixed
    summary: "Refusal mapping is now exactly A 5560737585. StaffAuthorizationException out of RetainAsync is a definite refusal of that attempted acceptance: the claimed occurrence records failed with FailureCode custody_refused and the refusal still surfaces. Adapter ArgumentException is removed from IsUncertainHandOver and is uncertain; only this command's own pre-call Validate refuses before anything is claimed. No bytes-read flag was added. Proof: ARefusedHandOverSurfacesAndClosesTheArrivalItWasClaimedFrom, AnAdapterArgumentExceptionIsUncertainAndNotARefusal (Core), ARefusedHandOverIsRecordedFailedAndTheNextLoadIssuesANewKey, AnAdapterArgumentExceptionLeavesTheArrivalUncertainAndTheKeyUnchanged (real SQL)."
  - id: C07B-R-18
    severity: minor
    disposition: fixed
    summary: "Request.cshtml.cs now catches StaffAuthorizationException and maps it to RefusedMessage ('This document was not accepted. Reload the link and try again.'), logging through LogPublicRequestUploadFailure. No Case, link or reason is disclosed and the wording does not invite a same-operation retry. OperatorLabels.cs is untouched, with the move to it deferred to C08's labels batch as before. Proof: ARefusedHandOverIsRecordedFailedAndTheNextLoadIssuesANewKey asserts HttpStatusCode.OK with RefusedMessage and DoesNotContain RetryMessage, i.e. not the 500 this finding named."
  - id: C07B-R-24
    severity: major
    disposition: open
    summary: "The monotonic transition is a non-atomic read-modify-write. RecordAsync loads PublicUploadOccurrenceEntity, tests MovesForward in memory, then saves; the entity carries no concurrency token (V1FoundationEntities.cs has no Version/ConcurrencyToken on it and V1FoundationModelConfiguration.cs configures only table/key/index/Sha256), so EF emits UPDATE ... WHERE Id = @id with no optimistic check. Two concurrent recorders on one occurrence are reachable: a claim loser reconciles while the winner is still inside RetainAsync, and A's own G15 note says the DocumentOccurrence row is written inside the accepting transaction, so the loser can read Pending before the winner returns Confirmed. Both read 'unknown', both pass MovesForward, and the later write wins - a Confirmed row downgraded to pending, which is exactly the regression A 5560761330 rule 2 forbids ('a late Pending/Unknown recorder against a Confirmed row is a no-op'). Blast radius is bounded (identities survive via ??=, the receipt and window are written from the returned artifact, and the next retry re-reconciles to Confirmed) but the invariant is not enforced where it is stated. The claim already shows the right shape: one conditional ExecuteUpdateAsync whose WHERE names the allowed source states. Test (a) does not catch it because the custody double commits nothing until the hold is released, so the loser's lookup returns null rather than Pending."
  - id: C07B-R-25
    severity: major
    disposition: open
    summary: "The web custody double applies A's published G15 fence to only one of the two status reads. RecordingCaseArtifactCustody.FindByOperationKeyAsync calls RequireStatusAuthority - staff casework, or the exact persisted RequestUploadLink with a matching CaseId, Status Active, RevokedAtUtc null and ExpiresAtUtc in the future, which is A's fence exactly - while GetAsync still calls StaffAuthorization.Require(actor, PerformCasework) alone and refuses a request-link actor. A 5560737585 states the adapter 'uses the same exact active/unrevoked/unexpired link + Case + accepted version creator/provenance fence for both status queries', and C's own disposition 5560748861 promised 'C's test doubles mirror the same link + Case + accepted-version fence for both status reads'. Neither is delivered. APendingArrivalIsNeverReOfferedAndThePublicSenderCannotReconcileIt then asserts the divergence as required behaviour (StatusCalls == 1 with the answer still Pending and StoringMessage on the second POST); against A's real adapter that submission would reconcile to Confirmed and redirect with RetainedMessage, so the test encodes a constraint the contract does not have and will fail when A publishes. No production defect follows - the divergence is in the safe direction - but the suite currently certifies the wrong contract."
  - id: C07B-R-26
    severity: major
    disposition: open
    summary: "Per-link key re-presentation disables plan item 6's additions without a recorded reconciliation. Plan item 6 states 'Additions and explicit replacements addressed by server-issued occurrence ID are allowed until explicit replay-safe finalization or expiry'. With UnresolvedCodes = [arrived, unknown, pending] the GET re-presents the outstanding occurrence's key for the whole link, and a POST of different bytes under that key hits the Sha256 mismatch branch of AuthorizeAndRecordArrivalAsync and is refused OperationConflict - so no second, different file can be added through the link until the first resolves. The implementer flagged this honestly in scratch/c07-notes ('a second file cannot be started through the link until the first resolves'), and it does follow A 5560761330's literal 'for that link' wording, so the behaviour is not itself a defect. What is missing is the reconciliation: no open question, assumption or governing-doc note records that a stated plan acceptance behaviour is now unavailable, and A's safety rule only requires that the unresolved occurrence's key not be replaced - it does not require refusing genuinely different bytes, so a per-occurrence reading satisfies both. Resolve by recording the conflict against plan item 6 and requesting A's ruling, or by presenting the outstanding key for a same-bytes retry while still allowing a new key for new content."
  - id: C07B-R-27
    severity: minor
    disposition: open
    summary: "The class contract on RetainIncomingArtifact overclaims. Its remarks say 'Every path to custody runs through a claim this caller won and committed first', but the claim is inside if (existing is not null): when FindAsync returns null the command calls custody.RetainAsync with no claim, and the StaffAuthorizationException handler's own if (existing is not null) guard then records nothing. Unreachable through the sole production caller today - EfDocumentRequestStore always commits the arrival before handing over, and the unique indexes on PublicUploadSessions.RequestUploadLinkId and PublicUploadOccurrences (SessionId, OperationKey) make the found row the caller's own occurrence - but the IntakeReceiptId destination the occurrence record already carries is a caller that would not pre-stage, and it would silently bypass the whole claim lifecycle. Either narrow the sentence to the staging contract it actually describes, or refuse an unstaged occurrence outright."
  - id: C07B-R-28
    severity: minor
    disposition: open
    summary: "Stale contract comment on the Core refusing double. RefusingCustodyStatus is documented as 'Custody's status port under its real rule: staff only. A request-link actor may hand bytes over and may not read what became of them.' A's published G15 fence authorizes the exact active/unrevoked/unexpired link for FindByOperationKeyAsync, so that is no longer custody's real rule. The double itself is still a legitimate refusing double (a revoked or mismatched link is refused), and AReconciliationTheActorMayNotReadLeavesTheRetentionWhereItWas still proves what it claims; only the comment misstates the contract."
  - id: C07B-R-29
    severity: minor
    disposition: fixed
    summary: "Ownership: 4a92a06e4 committed wave1/c07b-report.md inside the repository tree, a path outside C07's file ownership and outside the seven source/test files the report itself lists. The controller removed it at 0a0e88975 with no code change, and the report now lives in the controller scratchpad. Recorded as a process finding, fixed at the reviewed head."
  - id: C07B-R-30
    severity: note
    disposition: accepted-risk
    summary: "A link is permanently unusable by its sender after an uncertain hand-over custody never committed. The claim writes unknown before the call, so a crash or a fault between claim and commit leaves a row that TryClaimHandOverAsync will never move again (it only leaves arrived), G15 that returns null for ever, and a GET that re-presents the same key for ever. Reason for accepting: this is A 5560737585 and 5560761330 verbatim - 'null ... does NOT authorize a fresh operation key', 'never a fresh key, never a second RetainAsync' - and the alternative is the duplicate class the whole round exists to close. AnAdapterArgumentExceptionLeavesTheArrivalUncertainAndTheKeyUnchanged asserts this state deliberately. The operator escape hatch is the staff link reissue the session code already relies on ('the Case owner reissues on explicit staff action'): a new link gets a new session and new scoped keys. A timeout-based or staff-driven resolution of a stranded claim belongs to the custody stream, not C07."
  - id: C07B-R-31
    severity: note
    disposition: accepted-risk
    summary: "The first-insert race is guarded only by the unique index. Two concurrent first POSTs under one key both find no occurrence and both insert; the unique index on (SessionId, OperationKey) fails one commit, which surfaces as DbUpdateException on the page's recoverable filter and becomes the retry message. Safe - the loser never reaches custody, and its retry then finds the row and claims or reconciles - but unasserted. Reason for accepting: the index enforces it at the only level that matters and no test is required to establish a database constraint."
  - id: C07B-R-32
    severity: note
    disposition: accepted-risk
    summary: "UnscopeOperationKey returns null when the stored key does not carry the link prefix, and the caller maps that to UnresolvedOperationKey = null, which mints a fresh key - the one outcome the round exists to prevent. Unreachable as written, because the same query filters the session by the link the prefix is built from. Reason for accepting: defensive null on an unreachable branch; worth a comment rather than a change."
```

## What this round changed

Five commits over `4e3d3c803`, reviewed at `0a0e88975` (the controller's removal
of the in-tree report on top of the merge `37a923067`; code identical to
`4a92a06e4`).

| Commit | Change |
| --- | --- |
| `be71f0eee` | Both custody-status doubles implement G15 `FindByOperationKeyAsync` explicitly, no default fallback. |
| `f4c79e1ff` | `TryClaimHandOverAsync`, honest `FindAsync`, forward-only `RecordAsync`, identityless recovery by the original key, refusal mapping. |
| `c35cd2df9` | `RequestUploadPublicView.UnresolvedOperationKey` / `UnscopeOperationKey`; the page's refusal sentence. |
| `668d934d2` | Regression proofs (a)-(f). |
| `4a92a06e4` | The correction-round report (removed at `0a0e88975`). |

`git diff --stat 4e3d3c803..4a92a06e4` touches exactly the seven source and test
files the report lists, plus the report itself. `DocumentCustodyDurabilityTests.cs`,
DI composition, migrations, `src/Pegasus.Core/Custody/*` and `OperatorLabels.cs`
are all untouched.

## The three regression classes A named

**1. Atomic claim — satisfied.** `TryClaimHandOverAsync` is one
`ExecuteUpdateAsync` over `WHERE Id = @id AND CustodyState = 'arrived'` setting
`unknown`, and `claimed == 1` is the whole decision. It commits on its own
context before `RetainAsync`. `FindAsync` no longer maps `arrived` to null;
`ParseCustodyState` reads it as `Unknown`, so a loser sees the arrival it must
reconcile instead of a null it would hand over against.

There is no path left where two same-key callers both hand over. The production
caller reuses the occurrence row addressed by `(SessionId, scopedOperationKey)`
inside its authorizing transaction, and both that index and
`PublicUploadSessions.RequestUploadLinkId` are unique, so the row `FindAsync`
returns is always the caller's own occurrence and the CAS is on the right row.
A Confirmed return followed by a `RecordAsync` failure cannot reopen the
hand-over either: the row is already `unknown`, and `arrived` is the only state
the claim will leave, so the retry reconciles. `ARecordThatFailsAfterCustodyAcceptedIsRecoveredByTheOriginalKey`
proves that over real SQL, ending Confirmed with one `RetainAsync`, one lookup,
one document and one receipt. The residual is C07B-R-27: the unstaged
`existing is null` branch reaches custody with no claim.

**2. Monotonic recording — the rule is right, the write is not serialized.**
`MovesForward` ranks `unknown` 0, `pending` 1, `confirmed` and `failed` both 2,
so a late Pending or Unknown cannot pull a Confirmed back, `Failed` cannot
overwrite Confirmed (nor Confirmed overwrite Failed), and the `arrived`
special case lets any first answer land. Identities are `??=` only, never
erased, and the Box identity write is now gated on the row's post-update state
so a blocked transition cannot write remote identities onto a non-confirmed row.
`ALateRecorderNeverPullsAConfirmedRetentionBack` proves all three late states
over real SQL and asserts the identities survive. What is missing is atomicity:
see C07B-R-24.

**3. R-3a — satisfied.** `ReconcileAsync` no longer requires
`DocumentId`/`DocumentVersionId`; it asks `GetAsync` when it has them and
`FindByOperationKeyAsync(actor, caseId, existing.OperationKey)` when it does
not, copies `status.DocumentId`/`VersionId` onto the record, and returns
`existing` unchanged on a null lookup with the claim intact. The GET side
re-presents the sender's own unscoped key while any of `arrived`, `unknown` or
`pending` stands for the link, so no fresh key is minted over an unresolved
occurrence. No link+hash substitution appears anywhere.

## The remaining dispatch questions

**R-22 abuse path (4).** The failed-then-new-key path cannot re-offer bytes
custody holds *given A's contract*. `StaffAuthorizationException` records the
claimed occurrence `failed`, `ExecuteAsync` short-circuits Failed on the same
key for ever, and only the GET's fresh key admits a new submission - which
custody will refuse again unless the authority genuinely changed. The safety of
this rests entirely on A's assertion that the adapter settles authority before
it commits an accepted intent ("staging alone is not a committed accepted
intent"), which C07 documents rather than assumes silently. Adapter
`ArgumentException` is correctly uncertain.

**G15 doubles (6).** `RequireStatusAuthority` is A's fence exactly - staff
casework, or the exact persisted link with matching Case, `Active`, unrevoked
and unexpired - and `FindByOperationKeyAsync` answers from
`DocumentOccurrenceEntity`, the row A says the accepting transaction commits, so
absence is exactly "nothing committed observed". Both Core doubles implement the
method explicitly. The defect is that the web double's `GetAsync` was left on
the old staff-only rule: C07B-R-25.

**The two flagged consequences (7).** A `failed` retention not being re-offered
under the same key is right and follows A rule 5. Blocking a second file per
link is A's literal instruction but contradicts plan item 6, and the conflict is
unrecorded: C07B-R-26.

**Tests (8).** (a)-(f) are all real and none is skipped - there is no `Skip`
attribute in any of the three test files. The concurrency test's "gate" is a
`TaskCompletionSource` rendezvous inside the custody double
(`HandOverEntered` / `HoldHandOver`), which parks the winner inside `RetainAsync`
so the loser races a genuinely held claim; `HandOverAttempts` is incremented
before the park, so `Assert.Equal(1, custody.HandOverAttempts)` while the winner
is held is a real proof that the loser never reached custody. It is a
determinism device, not a skip, and lane 3 shows it ran on LocalDB. (c) lives in
`IncomingArtifactCustodyTests` rather than the web tests, which is the right
home for a store invariant and is disclosed in the notes.

**Ownership (9).** C07 files only, one owner per rule
(`IncomingArtifactCustodyProgress.MovesForward` is the single monotonic rule and
the Core double consumes it rather than copying it), no dead code beyond the two
defensive branches at C07B-R-27 and C07B-R-32. The one violation was the in-tree
report, C07B-R-29, fixed at the reviewed head.

## Lanes — wave 29 at `0a0e88975`

| Lane | Result | Evidence |
| --- | --- | --- |
| 1-build | **PASS** | exit 0, `Build succeeded. 0 Warning(s) 0 Error(s)`. |
| 2-core | **PASS** | Failed 0, Passed 23, Skipped 0. |
| 3-integration | **PASS** | Failed 0, Passed 66, Skipped 1 - the pre-existing corpus gate `CustodyOutboxIntegrationTests.AcceptedCaseRetainsEmbeddedPhotographsBesideTheSource`. Nothing in `PublicUploadRetentionWebTests` or `IncomingArtifactCustodyTests` was skipped. |
| 4-a-owned | **FAIL (4), A-owned — exactly as stated** | Failed 4, Passed 1, all four in `DocumentCustodyDurabilityTests` with `SqlException: Cannot insert duplicate key row in object 'dbo.Principals' with unique index 'IX_Principals_Code'. The duplicate key value is (QDOS).` That file is untouched by C07. No other failure class. |
| 5-architecture | **PASS** | Failed 0, Passed 100. |

The dispatch's lane condition is met in full: 1, 2, 3 and 5 PASS, and lane 4
fails only for the stated A-owned seed collision. The lanes do not decide this
round - all three open majors are behaviour the suite currently asserts as
correct, or an invariant no test exercises.

## Verdict

**needs-changes.** Head `0a0e88975`. Open: **3 majors** (C07B-R-24, C07B-R-25,
C07B-R-26), **3 minors** (C07B-R-27, C07B-R-28, plus C07B-R-29 fixed at head),
3 accepted-risk notes. The three findings A raised - the atomic claim, R-3a and
the refusal mapping - are all fixed, and the durable claim lifecycle is correct.
What remains is one real hole in the second half of A's instruction
(C07B-R-24), one contract the doubles certify wrongly (C07B-R-25), and one
unrecorded conflict with the plan (C07B-R-26).

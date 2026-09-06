## C07 slice, attempt 2 — progress (branch `c07-precase`)

**Item 1 (global Triage reference) — done.** Commits `c66b8580b` (production)
and `9a4aab92c` (tests).

- `TriageReferenceFormat.Format/TryParse` in `src/Pegasus.Core/Triage/TriageContracts.cs`
  (`T-` + sequence padded to five digits, expands past `T-99999`).
- `EfTriageStore.CreateAsync` allocates from the single seeded `TriageSequences`
  row inside its existing serializable transaction. The counter row is read with
  `WITH (UPDLOCK, HOLDLOCK)` (guarded by `Database.IsSqlServer()`, the house
  idiom from `EfIntakeReceiptStore.cs:388`) and is the **last** lock the
  transaction takes, so concurrent creations queue on it instead of deadlocking
  against each other's Triage range locks. This fixes the F-level defect where
  the store inserted `Sequence = 0` and violated `CK_Triage_Sequence`.
- Creation also records `TriageEntity.PrincipalId` from the originating
  instruction draft's `SuggestedPrincipalCode`, accepted only when it resolves
  to exactly one **active** principal; anything else stays null and renders
  `Not known`.
- `TriageSummary.Reference` is now the T reference; the provider claim number
  moved to a new `ClaimNumber` member. `TriageDetail` gained `PrincipalCode`,
  projected in the same round trip as the record.
- `/Triage/{id}` is titled by the reference and shows Triage reference,
  Registration and Principal. The `/Cases?tab=triage` row picks the T reference
  up through `TriageSummary.Reference` with no edit to the B-owned
  `Pages/Cases/Index.cshtml.cs`.

### ASSUMPTION 1 (C07 implementer, attempt 2) — `TriageSummary.Reference` stays nullable

Every persisted Triage now carries a reference, so the member should be
required. It is left `string?` because tightening it breaks two out-of-stream
in-memory fixtures — `tests/Pegasus.Core.Tests/Intake/ReconcileUnidentifiedDestinationsTests.cs:149`
and `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs:312` — which
pass `Reference: null` and are not in this slice's file list. Because: M5 keeps
the slice inside its files and the stop condition needs a green build.
Alternatives: edit both fixtures (outside scope), or keep the claim number in
`Reference` (contradicts the brief). Follow-up for those files' owner.

### ASSUMPTION 2 (C07 implementer, attempt 2) — one `Principal`/`Not known` label pair

PR 671's C17 named `OperatorLabels.ImageIntakePrincipal` /
`ImageIntakePrincipalNotKnown`. With Triage now showing a principal too, two
scoped constants would be the same two strings twice. They are consolidated to
`OperatorLabels.Principal` / `OperatorLabels.PrincipalNotKnown`, plus a new
`OperatorLabels.TriageReference`. Because: AGENTS.md "One list per concept"
binds over the PR's constant names, and the pair stays concept-scoped rather
than becoming a generic empty label. Alternatives: duplicate the pair per
surface, or alias one to the other.

## C07 slice, attempt 2 — READY_FOR_TESTS at `447e1c271`

Items 1–7 all landed in some form; build green throughout. Commits:
`c66b8580b`, `9a4aab92c`, `003e74800`, `b451d6a7d`, `897115133`, `c0fc39ce4`,
`89fbc6a7e`, `447e1c271`, plus the controller-directed merge `e7138ba27`
(G8/G9: `LondonCalendar.cs`, `CursorPaging.cs`).

### Public port signatures (item 2, for the report and for A)

```csharp
// src/Pegasus.Core/Triage/TriageContracts.cs
sealed record TriageListPosition(DateTimeOffset CreatedAtUtc, Guid Id);
sealed record TriageListSlice(IReadOnlyList<TriageSummary> Items, TriageListPosition? NextPosition);
Task<TriageListSlice> ITriageQueries.ListPageAsync(
    TriageState? state, TriageListPosition? after, int limit, CancellationToken ct);

// src/Pegasus.Core/Triage/TriageQueryUseCases.cs
sealed record ListTriagePageQuery(ActionActor Actor, TriageState? State, string? Cursor = null, int? Limit = null);
interface IListTriagePage { Task<CursorPage<TriageSummary>> ExecuteAsync(ListTriagePageQuery query, CancellationToken ct = default); }
sealed class ListTriagePage(ITriageQueries queries, ICursorProtector protector) : IListTriagePage;
```

Scope is `CursorPaging.CreateScope("triage", actor, state?.ToString(), "created_desc,sequence_desc")`.

### Production DI registrations A must add

- `IListTriagePage` → `ListTriagePage(ITriageQueries, ICursorProtector)`
- `IAddTriageNote` → `AddTriageNote(ITriageStore)`
- `ICaseEngineerChoices` → an adapter (none exists; G declared the port only)
- `IIncomingArtifactRetentionStore` → `EfPublicUploadRetentionStore`
- `RetainIncomingArtifact` and, behind it, an `ICaseArtifactCustody` adapter (A04)

### Handoffs

- **A04**: `ICaseArtifactCustody` has no production adapter. `RetainIncomingArtifact`
  is proved against a recording fake with scripted dispositions.
- **A**: `ICaseEngineerChoices` has no implementation. The Triage assignment
  picker is composition-gated on it; "Assign to me" is removed either way.
- **B**: PR 671 hunk B1 (`Pages/Cases/Index.cshtml.cs` `ImageRow`) is still B's.
  `ImageIntakeSummary.PrincipalCode` is populated and proved; the Awaiting row
  and quick view do not render it until B1 lands. `TriageSummary.ClaimNumber`
  is available if the Triage row should keep showing the provider claim number
  beside the T reference.
- **A (snapshots)**: `/Triage/{id}` and `/VehicleImages/{id}` both changed, so
  `docs/design/test-ui/pages/triage-details--*.html` and
  `vehicle-images-details--default.html` need regenerating.

### Further assumptions (3 and 4)

**ASSUMPTION 3 — typed-actor Triage mutations are NOT converted.** Changing
`string Actor` to `ActionActor` on the Triage mutation requests breaks six
files outside this slice's list (`src/Pegasus.Core/Intake/DurableIntake.cs`,
`src/Pegasus.Web/Mcp/TriageMcpTools.cs`, `src/Pegasus.Web/Pages/Intake/Details.cshtml.cs`,
`tests/…/CustodyOutboxIntegrationTests.cs`, `QdosAllocationRecoveryTests.cs`,
`SentEvidencePollPersistenceTests.cs`) and needs an actor-kind column on
`TriageHistoryEntity`, which is an A-owned migration. Because: M5 keeps the
slice inside its files and the schema is A's. Alternatives: edit six files
outside scope, or carry both an actor string and a typed actor (dual
representation). **This is the one part of the brief not delivered.**

**ASSUMPTION 4 — the read-count proof compares row counts instead of pinning a
number.** The brief asked for a measured baseline with three mixed rows. The
runner, not this agent, executes tests (M6), so no number could be measured
honestly. The test asserts the same Awaiting request costs equal reads over
three and over six image rows, which holds if and only if nothing is read per
row, and prints the observed count in its failure message. Because: an
absolute count is a fact about one base — exactly the defect the PR 671
disposition found in the branch's hard-coded `14`. Alternative: pin a guessed
number (rejected: fabricated evidence).

## C07 correction round 3 — review dispositions (head `2ba5e4e21`)

Independent review at `b46a07452` returned needs-changes: two majors, both in
`EfPublicUploadRetentionStore`, both invisible to the green build because the
port is unregistered.

### OPEN QUESTION 1 (C07-R-1) — missing UPDATE grant on `PublicUploadOccurrences`

- [ ] A must add `GRANT UPDATE ON OBJECT::[dbo].[PublicUploadOccurrences] TO
  [pegasus_web_runtime_role];`

`EfPublicUploadRetentionStore.RecordAsync` issues an UPDATE on that table, but
A's `20260906054658_V1PlatformFoundation.cs:1320` grants the web role only
`SELECT,INSERT` on it (against `SELECT,INSERT,UPDATE` on
`PublicUploadSessions` at `:1319`), and the worker role receives nothing on
either table. The first hand-over after A registers
`IIncomingArtifactRetentionStore` would throw a SQL permission error on every
disposition record. Nothing fails today only because the port is uncalled.

Worker role: needed only if the `Unknown` reconciliation sweep runs from the
Worker rather than a Web request — the likely shape once A04 lands, since
reconciliation is a durable retry. If so, `SELECT,INSERT,UPDATE` on both
`PublicUploadOccurrences` and `PublicUploadSessions` for
`[pegasus_worker_runtime_role]`. A's call, because it follows from where A
registers the caller.

Not needed: `DocumentVersions` already carries `SELECT, INSERT, UPDATE` for
both roles (`20260729199000_RuntimeRoleReconciliation.cs:126` WebGrants, `:230`
WorkerGrants), so the identity write itself is granted.

The migration is A-owned and outside C's file scope, so this is a statement and
no migration was changed.

### DECISION (C07-R-3) — one lifecycle token per Image Intake

`EfImageIntakeStore.SetPrincipalAsync` guards its write with
`LifecycleVersion` rather than a token of its own. Deliberate: one Image
Intake, one optimistic token. A principal save therefore does invalidate a
concurrently-open Merge or Close form — those forms reload — and that is
preferred over a second token, which would let two staff members write the same
record while each believed they held the current version. A same-value
re-submission leaves the version alone, so only a genuine change can invalidate
a form. Now recorded in the method's remarks.

### Fixed (C07-R-2, major) — a real defect of mine

`RecordAsync` wrote the document version's Box identities on every disposition
and nulled them for anything but `Confirmed`. Because a version can back more
than one occurrence, a later Pending or Failed record erased an earlier
occurrence's confirmed identities, and `FindAsync` then read that retention
back as Confirmed with no remote identity. My comment said "confirmed
disposition only"; the code did not. Now guarded on `Confirmed` and never
assigns null, with real-SQL proof in
`tests/Pegasus.IntegrationTests/IncomingArtifactCustodyTests.cs`.

### RESIDUAL (C07-R-5) — plan item 3 belongs to the formal-instruction slice

- [ ] Prove the T reference survives `ILinkTriageCase`, with plan item 3
  (formal instruction creates the normal Case, links the Triage/Image Intake
  and retains both pre-case references)

Not one of this brief's seven items. Safe by construction meanwhile: `Reference`
and `Sequence` are assigned only in `EfTriageStore.CreateAsync`'s initializer
and nowhere else in the store, and both columns carry unique indexes.

### Accepted residuals, no change

- C07-R-4: the pure session suite stays in `Pegasus.IntegrationTests` — the
  plan names the path and the runner filter reads it there, and the Core
  Documents folder is outside the slice's file scope. Moves with its filter
  when the accept path is wired.
- C07-R-6: one PK read per continuation page. Packing the sequence into the
  opaque sort key is the right fix but changes the cursor payload; doing that
  on a seam with no production caller and no ability to run a test is how a
  silent paging bug ships.
- C07-R-7: deviation 4 stands.
- C07-R-8: fixed (one subquery, and `ScopeOperationKey` now has a caller).

A `## Simplification pass` section was added to the report over this slice's own
diff — the review correctly found none.

## C07b — RetainIncomingArtifact gets its production caller (branch `c07-retention-caller`, head `87eebffe1`)

The public upload path (`/Uploads/{token}` → `IUploadToRequest` →
`EfDocumentRequestStore`) now hands its bytes to custody instead of claiming
custody itself. It stopped creating `CaseDocumentEntity`/`DocumentVersionEntity`
(with `CustodyStatus = Confirmed` before any custody)/`DocumentOccurrenceEntity`
and stopped writing through `IDocumentContentStore`. Order per request:

1. token lookup → prior-receipt replay/conflict → `uploadPolicy.Authorize` →
   archived/terminal guard (all unchanged);
2. get-or-create the link's one `PublicUploadSessionEntity`, refuse when the
   session no longer `AcceptsBytes` (`Unavailable`, no Case disclosure), refuse
   `LimitsVersionMismatch` when the session's recorded limits version is not the
   accepted one;
3. get-or-create the `PublicUploadOccurrenceEntity` as `pending` with the
   server-issued id; **commit**;
4. `RetainIncomingArtifact.ExecuteAsync(ActionActor.RequestLink(link.Id), …)`
   with the link row's own `CaseId`, read inside the transaction that
   authorized the upload, and `IntakeReceiptId: null`;
5. Confirmed/Pending → `Accepted`; Failed/Unknown → `NotRetained`. Only an
   accepted disposition writes the receipt, increments
   `AcceptedFileCount`/`AcceptedByteCount`/`Exhausted` and bumps the workflow
   version; only a Confirmed one runs `PublicUploadSessionPolicy.Start`.

### DECISION — the optional-bridge pattern (as C01 and C08)

`RetainIncomingArtifact? retention = null` is an optional constructor
dependency of `EfDocumentRequestStore`. When it is null `ExecuteAsync` returns
`Unavailable` **before any row is written or read** and logs nothing. No stub,
no fake success, no partial arrival. `UnavailableDocumentRequestStore` is
untouched, and `IDocumentContentStore` was removed from the constructor because
nothing on the path uses it any more (MS.DI resolves the optional parameter to
null while the port is unregistered).

### DECISION — `RequestUploadDecision.NotRetained` is a new member

The enum had nothing for "custody did not take it". `Unavailable` means the
link is gone and hides the Case, `RateLimited` names a limit that was not
reached, and `InvalidFile`/`LimitExceeded` would blame a file policy had
already accepted. `Failed` **and** `Unknown` both map to it: the same operation
key is the safe retry either way, because `RetainIncomingArtifact` reconciles
an uncertain hand-over instead of re-offering the bytes. `Request.cshtml.cs`
renders it with the same plain, Case-free sentence its exception path already
used.

### DECISION — the occurrence key is scoped by the link, not the session

`EfPublicUploadRetentionStore.ScopeOperationKey(Guid requestUploadLinkId, …)`
now mints `request:{linkId:N}:{key}` (was `public-upload:{sessionId:N}:{key}`).
It is the shape the legacy path already recorded on `DocumentOccurrences`, it
survives any rebuild of the session row beneath the link, and uniqueness is
unchanged because `PublicUploadSessions` carries a unique index on
`RequestUploadLinkId` (exactly one session per link, forever — an expired
session is never restarted; B reissues a new link on explicit staff action).
`IncomingArtifactCustodyTests` was updated to the new signature.

### ASSUMPTION 5 (C07b implementer, attempt 1) — the receipt is written after the hand-over, from the document occurrence custody created

`RequestUploadReceiptEntity.OccurrenceId` and `.VersionId` are non-nullable
`Guid`, and `CustodyModelConfiguration.cs:103` binds `OccurrenceId` as a
**foreign key into `DocumentOccurrences`** — so it can never hold a
`PublicUploadOccurrenceEntity.Id`, and `CaseArtifactCustodyResult` returns no
occurrence identity. The receipt is therefore recorded after the hand-over,
with `VersionId` from custody and `OccurrenceId` looked up as the document
occurrence custody created for that version. When an adapter creates none, no
receipt is written and `UploadToRequestResult.ReceiptId` is null; the accepted
result still stands. Because: the FK is A-owned entity configuration and out of
scope, and replay safety does not rest on the receipt — the
`PublicUploadOccurrenceEntity` is committed *before* custody and is what makes
a retry one retention (`RetainIncomingArtifact.FindAsync` returns the same
document/version). Alternatives: keep creating a `DocumentOccurrenceEntity` in
the caller (the brief forbids it, and it is the defect being removed), or stop
writing receipts entirely (silently freezes `EfOperationsStore`'s
`lastReceiptAtUtc` staff projection).

### ASSUMPTION 6 (C07b implementer, attempt 1) — a Pending disposition returns `Accepted`

The brief says Confirmed **or** Pending → `Accepted` because A04's Pending is
durable. The plan's stop condition reads "an accepted upload lacks confirmed
custody is a stop". They are reconciled as: `Accepted` means the bytes are held
durably, `Confirmed` means custody holds them, and nothing claims the latter on
a Pending — `CustodyStatus` stays `Pending`, no Box identity is written
(`RecordAsync` guards on Confirmed), the fixed window does **not** open, and
`RetainedIncomingArtifact.IsConfirmed` remains the one success gate. What a
Pending *does* consume is the link's accepted file and byte totals, per the
brief's "only for accepted dispositions". Because: the brief is the operative
instruction and the stop condition is about a false custody claim, which this
is not. Alternative: refuse a Pending, which would make a durable arrival look
like a failure to the sender and invite a duplicate.

### DEVIATION — two C-owned test files edited beyond the brief's enumeration

Both are in the plan's `### C07 files` "Existing C files" list, and both were
compile- or premise-broken by the change:

- `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs` — the only
  direct `new EfDocumentRequestStore(...)` caller, and its request-upload case
  proved that *this path* wrote content and removed the orphan when its own
  save failed. The path writes no content now, so the test is retargeted to the
  property that replaced it: a failed arrival save leaves no session, no
  occurrence, no receipt, no document and no hand-over, and the same operation
  key then succeeds exactly once. Its `ManagedOnlyDocumentContentStore` double
  had no other caller and was removed; the save interceptor now trips on the
  added `PublicUploadOccurrenceEntity`.
- `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` — its
  terminal-state test performs a real accepted upload, so its host now
  registers the accept path through `PublicUploadRetentionWebTests.WithRetention`
  (two lines). Without it the fail-closed `Unavailable` would have made a green
  test red for the right reason but the wrong subject.

`PublicUploadSessionTests` changed only a doc remark (it is pure policy; its
premises are untouched).

### C07-R-1 is CLOSED for the Web accept path

`20260906054658_V1PlatformFoundation.cs:1319` now grants
`SELECT,INSERT,UPDATE` on **both** `[dbo].[PublicUploadSessions]` and
`[dbo].[PublicUploadOccurrences]` to `[pegasus_web_runtime_role]`, with
`DENY DELETE` on both. That is exactly what this caller needs: INSERT the
session and the occurrence, UPDATE both. No grant statement is outstanding for
the Web request path.

- [ ] Still open, and A's call: `[pegasus_worker_runtime_role]` holds **nothing**
  on either table (`:1320` grants it other tables only). It needs
  `SELECT,INSERT,UPDATE` on both if and only if the `Unknown` reconciliation
  sweep runs from the Worker rather than a Web request — the likely shape once
  A04 lands, since reconciliation is a durable retry.

### RESIDUAL (C07-R-9) — the Worker-side channels are still uncalled

- [ ] Plan item 7 asks for `RetainIncomingArtifact` on *every* received,
  Unidentified, Triage and Image Intake occurrence. Only the public-upload
  channel has its caller. The mail/provider/Unidentified/Triage-image channels
  are untouched by this slice (their surfaces are Worker-side and outside this
  brief), and each will need its own `IIncomingArtifactRetentionStore`
  implementation for its own retained-record shape plus a `SystemWorker` actor
  for the hand-over. Nothing in them claims confirmed custody today, so the
  residual is a gap, not a defect.

### Host registration handoff for A

```csharp
services.AddScoped<RetainIncomingArtifact>();
services.AddScoped<IIncomingArtifactRetentionStore, EfPublicUploadRetentionStore>();
```

plus `ICaseArtifactCustody` **and** `ICaseArtifactCustodyStatus` resolving to
A04's `EfCaseArtifactCustody` (the status port is optional on the command, but
without it an `Unknown` hand-over can never be reconciled and stays Unknown
forever). `EfDocumentRequestStore` picks the command up through its optional
constructor parameter, so no change is needed where `AddScoped<EfDocumentRequestStore>()`
already stands. Until those three registrations land, every public submission
returns `Unavailable` / 404 and writes nothing — proved by
`PublicUploadRetentionWebTests.WithoutTheRetentionCommandTheSubmissionRefusesAndWritesNothing`.

## C07b correction round 1 — review dispositions (branch `c07-retention-caller`)

Independent review at `6c8b945bd` returned needs-changes: four majors, four
minors, three notes. All four majors are fixed, all four minors are fixed, and
the three notes are dispositioned below. Commits `05d9a0e49`, `dbdfab107`,
`6490623c3`.

### The shape of the fix

The review found one defect wearing four faces: **the receipt was load-bearing
for three things it cannot carry**. It was the replay guard for the link's
accounting (R-1), the thing that decided whether a Pending could ever be asked
about again (R-2), and — with a thrown hand-over recorded as nothing at all
(R-3) — the only durable trace of an arrival. The corrections move each job to
the row that can actually hold it:

1. **The arrival has its own pre-custody state.** A `PublicUploadOccurrenceEntity`
   committed before the hand-over now carries `EfPublicUploadRetentionStore.ArrivedCode`
   (`"arrived"`), not `"pending"`. That single change is what makes the rest
   possible: "we have not asked yet" and "custody answered Pending" were the
   same stored value, and no rule could tell them apart.
   `EfPublicUploadRetentionStore.FindAsync` reports **no retention** for an
   `arrived` row, so the bytes are handed over exactly as before.
2. **The link's accepted totals are derived, not incremented** (R-1).
   `ApplyAcceptedTotalsAsync` sets `AcceptedFileCount`/`AcceptedByteCount` from
   the session's occurrences in `confirmed` or `pending`, inside the accept
   transaction, with the link row read `WITH (UPDLOCK, HOLDLOCK)` first (the
   `EfTriageStore.AllocateSequenceAsync` idiom). An occurrence therefore counts
   exactly once however many times its key arrives, whether or not a receipt
   exists — which is precisely the branch the review named. A replay that
   changes nothing bumps nothing: no `link.Version`, no workflow completion.
3. **The receipt is a confirmed file's record only** (R-2). Writing one for a
   Pending refused every later submission of that key as `Replay`, so nothing
   could ever reach the command that owns Pending recovery.
4. **Pending is reconciled, not repeated** (R-2). `RetainIncomingArtifact` now
   sends `Pending` through `ReconcileAsync` exactly as it already sent
   `Unknown`: `ICaseArtifactCustodyStatus` under the same operation key,
   Confirmed → identities recorded, Failed → failure recorded, still Pending →
   stays Pending. The bytes are never offered a second time for a Pending or
   Unknown occurrence. Only `Failed` — custody said no, and said so — is
   offered again. A reconciliation that comes back anything but Confirmed now
   also carries no remote identity, the same rule `Project` already applied to
   a first hand-over.
5. **A thrown hand-over is recorded `unknown`** (R-3), from inside the command,
   before returning — so the retry asks instead of re-offering bytes custody
   may hold. "Uncertain" is defined narrowly (`IsUncertainHandOver`:
   `HttpRequestException` or `IntakeExceptionPolicy.IsTransientFailure`,
   recursing through inner exceptions), so an authorization refusal or a
   malformed request still surfaces rather than being buried.
6. **Pending has its own sentence** (R-4). A Pending disposition returns the new
   `RequestUploadDecision.AcceptedPending`, and `Request.cshtml.cs` renders
   "Your document was received and is being stored. You do not need to send it
   again." Nothing says "retained securely" before custody has said it.

### HANDOFF for C08 — the Pending completion sentence moves to `OperatorLabels`

`RequestModel.StoringCompletionMessage` and `RequestModel.RetainedCompletionMessage`
are `private const` in `src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs`.
`src/Pegasus.Web/Presentation/OperatorLabels.cs` is C08-owned and was **not**
edited. Both belong beside the other sender-facing strings and move there with
C08's labels batch; the doc comment on `StoringCompletionMessage` says so.

### ASSUMPTION 5 corrected (C07b implementer, correction round 1)

The review is right that ASSUMPTION 5 was honest about the foreign key and not
honest about what else the receipt carried. The corrected statement: the receipt
is written **only for a confirmed hand-over**, from the document occurrence
custody created for that version; when an adapter creates none, no receipt is
written and `UploadToRequestResult.ReceiptId` is null. Replay safety and the
link's accounting rest on the `PublicUploadOccurrenceEntity` alone — which is
now true of the accounting as well as of custody, because the totals are derived
from those rows. `EfOperationsStore`'s `lastReceiptAtUtc` staff projection is
still the only outside reader, and it now shows the last *confirmed* upload
rather than the last accepted one, which is the more honest reading of the
column.

### ASSUMPTION 6 stands, with its cost stated plainly

A Pending still consumes one file and its bytes from the link's totals. A later
Failed reconciliation does not hand the allowance back at the moment of the
refusal: the totals are recomputed only when an arrival is accepted, and a
refusal is not one, so they converge at the next accepted arrival. This is
asserted, not glossed, in
`APendingArrivalIsReconciledToFailedByTheNextArrivalWithTheSameKey`. The
alternative — recomputing on the refusal path too — is a second transaction and
a second lock on a link the sender has just been told to retry, and buys
nothing the next arrival does not.

### Minor dispositions

**C07B-R-5 (fixed, differently from the suggestion).** The second transaction
does not re-assert `ArchivedCaseGuard.RequireMutable` as a *refusal*, because
custody already holds the bytes and refusing to record them would lose a durable
custody fact. It asks instead: `ApplyAcceptedTotalsAsync` calls
`CaseMutationGuard.Complete(workflow)` only when `IsMutable(workflow)` is true.
So a Case archived or moved terminal during the hand-over still gets its arrival
recorded, and no longer has its workflow version bumped and its edit lease
cleared by an upload it would now refuse. The reason is in the method's remarks.

**C07B-R-6 (fixed).** `Request.cshtml.cs`'s recoverable filter gained
`HttpRequestException`, `TimeoutException` and `System.Net.Sockets.SocketException`,
with a comment saying why the list grew. Most transport faults no longer reach
it at all — R-3 turns them into a typed `NotRetained` — but the ones that are not
uncertain (a fault after custody answered, say) still land on the plain retry
message rather than a 500 on a page a member of the public is looking at.

**C07B-R-7 (fixed).** `wave1/c07b-report.md` now carries a `## Simplification
pass` section, covering both the original slice and this correction round, with
the four lenses and honest dispositions.

**C07B-R-8 (fixed).** The `Refuses` helper is now generic and asserts by exact
type: `StaffAuthorizationException` for every wrong authority,
`InvalidOperationException` for holding — so the holding case is proved to be
refused *for being holding* rather than for the validation the fake receipt was
injected to get past. A fifth link (`PUBUP5`) is seeded `Exhausted` with no
revocation, so the double's `Status != Active` branch is reached on its own.

### Note dispositions

**C07B-R-9 (accepted, unchanged).** `EfPublicUploadRetentionStore.FindAsync` is
still a global `SingleOrDefaultAsync` on `OperationKey` against an index of
`(SessionId, OperationKey)`, so every hand-over scans `PublicUploadOccurrences`.
The correction round makes it *more* used, not less — a Pending or Unknown
retry now reaches it too — but the shape of the fix is an index on
`OperationKey`, which is an A-owned migration and outside C's file scope. The
table is empty-to-small in v1 (one row per file per public link) and the port is
still unregistered, so this is a cost that does not exist yet. Recorded for A
alongside the worker-role grant question rather than worked around here.

**C07B-R-10 (accepted, unchanged).** `DocumentVersionEntity.BoxFileId`/
`BoxVersionId` still has two writers: A04's adapter and
`EfPublicUploadRetentionStore.RecordAsync`. The write is idempotent
(`?? version.BoxFileId`, and only on `Confirmed`), and the correction round adds
no third writer — a reconciliation routes through the same `RecordAsync`. The
clean shape is for the adapter to own the column outright and for the retention
store to record only the occurrence, which is an A04 decision about what its
`GetAsync` guarantees; the C07b test double already behaves that way (its status
port moves the version out of Pending itself), so the seam is proved to work
when A04 owns it. No behaviour defect at this head, and none introduced.

**C07B-R-11 (no action, correctly rejected).** The controller's compile-only
accommodation `6c8b945bd` to `DocumentCustodyDurabilityTests.cs` stands
untouched. That file is A-owned by explicit user ruling and this correction
round did not open it: `git log --oneline 6c8b945bd..HEAD -- tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs`
is empty.

### RESIDUAL (C07B-R-3a) — an Unknown that named no document cannot be reconciled

- [ ] A thrown hand-over records `unknown` with no `DocumentId`/`DocumentVersionId`,
  because custody threw before naming them. `ICaseArtifactCustodyStatus.GetAsync`
  is addressed by `(caseId, documentId, versionId)`, so there is nothing to ask
  with, and the retention honestly stays Unknown for ever: the sender is told to
  retry and the retry stays refused. This is the safe direction — the bytes are
  never offered twice — but it is a dead end for that one file.

The fix is a status read addressed by the operation key, which is A's frozen
`CustodyContracts.cs` and not this slice's to change. Recorded rather than
worked around: inventing a second hand-over here is exactly the duplicate the
command exists to prevent. Proved as it stands in
`AThrownHandOverIsRecordedUnknownAndTheNextArrivalNeverRepeatsIt` and in
`RetainIncomingArtifactTests.AThrownHandOverIsRecordedUncertainAndTheBytesAreNeverOfferedAgain`.

### Still open for A (unchanged from the first round)

- [ ] `[pegasus_worker_runtime_role]` holds nothing on `PublicUploadSessions` or
  `PublicUploadOccurrences`. It needs `SELECT,INSERT,UPDATE` on both if and only
  if the reconciliation sweep runs from the Worker. The Web accept path's grants
  are complete (C07-R-1 closed).
- [ ] Host registration: `RetainIncomingArtifact`,
  `IIncomingArtifactRetentionStore → EfPublicUploadRetentionStore`, and
  **both** `ICaseArtifactCustody` and `ICaseArtifactCustodyStatus` resolving to
  A04's adapter. The status port is now load-bearing for Pending as well as
  Unknown: without it a Pending arrival stays Pending for ever, which is the
  defect R-2 named in a different costume.

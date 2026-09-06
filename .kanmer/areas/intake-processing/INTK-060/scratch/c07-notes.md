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

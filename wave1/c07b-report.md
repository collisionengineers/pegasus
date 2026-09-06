# C07B — public upload retention caller

Slice `c07-retention-caller` on `task/pegasus-v1-intake`. Rounds 1 and 2 are
recorded on PR 673 and in the branch history; this file starts at round 3,
which is the first round written in this worktree.

## Correction round 3

Base `4e3d3c803` (C07 helper `f55a5adac` merged with the C branch `5405c88f7`,
which carries shared G15). Build gate: `dotnet build ./Pegasus.slnx
--configuration Release --no-restore`, exit 0, 0 warnings.

### Finding to commit

| Finding / A item | What changed | Commit |
| --- | --- | --- |
| Build red: `RefusingCustodyStatus` and `RecordingCaseArtifactCustody` did not implement G15's `FindByOperationKeyAsync` | Both doubles implement it explicitly, no default fallback. The Core double keeps its staff-only fence on both reads; the web double carries A's published lookup fence — staff casework, or the exact persisted link, that link's own Case, active/unrevoked/unexpired — and answers from the document occurrence the accepting transaction commits | `be71f0eee` |
| **A blocker 5560753915** — `arrived` was not a durable claim: `FindAsync` read it as null and `AuthorizeAndRecordArrivalAsync` handed the same occurrence to simultaneous same-key callers, so both could call custody; a Confirmed return followed by a failed `RecordAsync` left `arrived` and re-offered | One conditional update `arrived → unknown` (`WHERE Id = @id AND CustodyState = 'arrived'`) committed before the possibly-accepting call; rows affected = 1 is the sole winner. Losers never call custody. `FindAsync` returns every committed row, `arrived` as Unknown | `f4c79e1ff` |
| **Monotonic confirmation** (same comment) | `RecordAsync` moves forward only: `unknown → pending/confirmed/failed`, `pending → confirmed/failed`; Confirmed and Failed are both terminal, so a late Pending/Unknown is a no-op and Failed never displaces Confirmed. Identities are filled where missing and never erased. Box identities still only on Confirmed | `f4c79e1ff` |
| **R-3a / A 5560737585** — identityless Unknown recovery by the ORIGINAL key | `RetainIncomingArtifact` reconciles by `FindByOperationKeyAsync(actor, caseId, operationKey)` when the record names no document, copying recovered `DocumentId`/`VersionId` and state; null leaves it unknown and never authorizes a fresh key | `f4c79e1ff` |
| same finding, page half — a fresh GET key turned a retry into a second submission | `RequestUploadPublicView` carries `UnresolvedOperationKey`; the store fills it from the link's `arrived`/`unknown`/`pending` occurrence (unscoped), and the page presents that key instead of minting one. No link+sha256 substitution anywhere | `c35cd2df9` |
| **R-22 / A 5560737585** — refusal mapping | `StaffAuthorizationException` out of `RetainAsync` is a definite refusal of that attempted acceptance: the claimed occurrence records `failed` and the refusal still surfaces. No bytes-read flag. Adapter `ArgumentException` is now uncertain; only this command's own pre-call validation refuses before claiming | `f4c79e1ff` |
| **R-18** — `StaffAuthorizationException` from the hand-over path was an unhandled 500 | `Request.cshtml.cs` maps it to a plain refusal sentence that discloses nothing and does not say "retry the same operation", because the next load carries a new key | `c35cd2df9` |
| Regression proofs (a)–(f) | Two simultaneous same-key submissions → one `RetainAsync`, the other reconciles; Confirmed then failed `RecordAsync` → recovered by the original key, no second hand-over; late recorder cannot downgrade Confirmed; G15 null while a winner is in flight leaves the claim and the key intact; refusal → `failed` then a new key; adapter `ArgumentException` → unknown + lookup. Existing arrived/pending/unknown assertions moved to the new lifecycle (the confirmed hand-over now asserts custody sees `unknown`, i.e. the claim was committed first) | `668d934d2` |

### The lifecycle

```
                       (AuthorizeAndRecordArrivalAsync commits)
                                      |
                                      v
                                  [arrived]
                                      |
              TryClaimHandOverAsync: one conditional update,
              rows affected = 1 wins. Losers go straight to
              reconcile and never call custody.
                                      |
                                      v
   +--------------------------->  [unknown]  <---------------------+
   |                              /   |   \                        |
   |   RetainAsync -> Pending    /    |    \  RetainAsync throws    |
   |                            /     |     \ (uncertain)          |
   |                           v      |      \____________________/
   |                     [pending]    |
   |                        /  \      | RetainAsync -> Confirmed
   |   reconcile by        /    \     | (or reconcile recovers it)
   |   identities (GetAsync)     \    v
   |                     /        \  [confirmed]  (terminal)
   |                    v          \
   |              [failed] <--------+ StaffAuthorizationException
   |             (terminal)           = definite refusal
   |
   +-- reconcile by ORIGINAL operation key (FindByOperationKeyAsync)
       when the record names no document. Null = nothing committed
       observed: the claim stands, the key is re-presented, no fresh
       key and no second RetainAsync.

Forward only: rank(unknown) < rank(pending) < rank(confirmed) = rank(failed).
`arrived` is behind all of them. Confirmed and Failed never overwrite each
other; identities are learned once and never unlearned.
```

The page mints a new operation key only when the link has no `arrived`,
`unknown` or `pending` occurrence. That is the whole of what makes a further
submission a new deliberate one rather than a duplicate of bytes custody may
already hold.

### Host handoff (unchanged)

The production host must register, exactly as before:

- `RetainIncomingArtifact` — the one Core command that hands an incoming
  artifact to custody.
- `IIncomingArtifactRetentionStore` → `EfPublicUploadRetentionStore`.
- `ICaseArtifactCustody` **and** `ICaseArtifactCustodyStatus` → Stream A's A04
  adapter. Both ports, not one: without the status port a hand-over custody has
  not finished can never be asked about, and without
  `FindByOperationKeyAsync` a lost response can never be recovered.

No new table, worker, state word, migration or DI shape was added by this
round.

### Files touched

- `src/Pegasus.Core/Intake/RetainIncomingArtifact.cs`
- `src/Pegasus.Core/Documents/RequestUploadPolicy.cs`
- `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs`
- `src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs`
- `tests/Pegasus.Core.Tests/Intake/RetainIncomingArtifactTests.cs`
- `tests/Pegasus.IntegrationTests/IncomingArtifactCustodyTests.cs`
- `tests/Pegasus.IntegrationTests/PublicUploadRetentionWebTests.cs`

Tests: controller wave loop.

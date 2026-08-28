# Plan — TICK-077 (EXT-04) Direct EVA API integration

## Shape

One new act: `POST /Instruction/Inspection`, once per case, carrying the
mapped fields and every eligible image, gated on Review. Two per-principal
toggles decide whether an operator may fire it by hand (**Manual**) and whether
it fires itself when the case reaches Review (**Automatic**). No API update
path — EVA's update endpoints are unsuitable, so once a case is submitted,
later changes are not reflected in EVA and the export remains the only route by
which a changed case reaches EVA again.

## Steps

### 1. Core contracts and policy

`EvaApiContracts.cs`: `EvaSubmissionOutcome { Succeeded, Rejected, Partial,
Unknown }` (FRD-07 requires the four stay distinct), `EvaSubmissionResult`,
`EvaInstructionPayload`, `IEvaApiTransport`, `ISubmitCaseToEva`.

`CaseEvaApiMapping` — **reuses** `CaseEvaMapping`'s whole thirteen-field
mapping and normalisation, and only renames into EVA's field names.

`EvaSubmissionPolicy` — one owner of the toggle permissions, the required
access right per trigger, the once-per-case rule and the outcome table.

### 2. Extract the shared readers

Lift `LoadEligibleImagesAsync` and `BuildEvidence` out of `EvaHandoffStore`
into `EvaCaseImageReader` and `EvaCaseEvidenceReader`, used by both routes.
**Reuses** `EvaHandoffPolicy.SelectEligibleImages` and
`IDocumentContentStore.ReadVersionsAsync`. One query each, so the two routes
cannot state a case differently.

### 3. Transport

`EvaApiOptions` + `EvaApiTransport`. **Reuses** the
`DvlaDvsaProductionAdapter` shape: validating options factory with host
allow-list, `SemaphoreSlim` token cache, failure taxonomy. EVA specifics:
`expires_in` is minutes; form-urlencoded `Client_Id`/`Client_Secret`;
snake_case token members; case-insensitive envelope; a 400 envelope inside a
200 is a rejection; a `text/plain` 500 is not JSON. Options resolved lazily
(PLAT-013).

### 4. Persistence

`EvaSubmissions` + the two `Principals` columns, with a filtered unique index
making once-per-case a database constraint. `EvaSubmissionStore` **reuses**
`EvaHandoffStore`'s Review gate, version check, operation-key replay and action
history. Migration and its runtime grants ride the same diff.

Does **not** reuse `EvaFirstHandoffProxies` — check-constrained
`ClaimsExternalDelivery = 0`, so it cannot record a real delivery.

### 5. Per-principal toggles

Two boolean columns following ADR-0018, plus ADR-0034. **Reuses** the
`InspectionMode` pattern end to end. Adds the post-creation edit ADR-0018
deferred.

### 6. Web

The action bar's Export button becomes one **Send to EVA** control opening
`/Cases/{caseId}/Eva/Send`, which offers the API submission and the unchanged
export. **Reuses** the POST-form + `operationKey` + disabled-with-reason
convention and `Export.cshtml.cs`'s error handling.

### 7. Automatic submission

`ExternalWorkKinds.SubmitCaseToEva`, an arm in `ProcessQueuedExternalWork`,
`EvaSubmissionRetryPolicy`, and a reconciliation sweep on the existing timer —
the `ReconcileAutomaticVehicleLookups` pattern. **Reuses** the durable outbox,
lease, backoff, poison handling and Operations retry surface.

### 8. Secrets and infrastructure

`EVA_*_SECRET_URI` through the existing three-hop Box/DVSA chain to `Eva__*` on
Web and Worker, plus the fail-fast required-config list.

### 9. Governing documents

FRD-07, `capabilities.md`, `boundaries.md`, `open-decisions.md`,
`current-architecture.md`, `operations.md`, `runbook.md`, and ADR-0034.

### 10. Follow-up tickets

[[ENG-019]] live-key swapover (blocked, operator-gated) and [[ENG-020]] real
EVA fields for the inspection date and mileage (vendor-dependent).

## Operator decisions taken during implementation (2026-08-27)

- `InsName` carries the **claimant name**, not the insurer.
- `Agent` carries the **principal code**; `RequestFrom` is always
  `COLLENGAPI`.
- The **instruction date is not sent** — EVA sets it on receipt.
- **Inspection date and mileage** have no EVA field and travel as note lines,
  deferred to [[ENG-020]].
- The case bar carries **one Send to EVA control**, opening a page offering the
  API submission or the export.

## Acceptance

- A principal with Manual on can submit a Review case and the outcome is
  recorded with EVA's id and File Reference.
- A principal with Automatic on has a Review case submitted without operator
  action, with backoff and a visible terminal failure.
- A second submission of a succeeded case is refused by the database, not only
  by code.
- All four outcomes are reachable and distinct.
- The export still works, unchanged, for every principal.

## Verification

`dotnet restore --locked-mode`, `dotnet build --configuration Release`,
`dotnet test --filter "Category!=Corpus"`. Transport fixtures taken verbatim
from the connector's recorded traffic. One live test-environment submission,
with the operator's approval, before the ticket claims delivery.

## Simplification pass — 2026-08-27

Run over the branch diff with the `code-simplifier` agent across the four
lenses (reuse, simplification, efficiency, altitude).

**Applied:**

| Finding | Disposition |
| --- | --- |
| The six EVA config key names were written out three times (options factory, Web, Worker) | Fixed — `EvaApiOptions.Create(Func<string, string?>)` owns the key names; Web and Worker pass their own lookup. 12 duplicated literals gone. |
| `using System.Globalization` orphaned in `EvaHandoffStore` by the evidence extraction | Removed. |
| `401 or 403 => Rejected` fully covered by the `>= 400 and <= 499` arm beneath it | Removed. |
| `EfAutomaticEvaSubmissionStore` counter could only equal `due.Length`; its catch filter was unconditionally true | Simplified to an early return and `return due.Length`. |
| Three nested ternaries (house rule: none) | Rewritten as guard-then-ternary. |
| `outcome == Succeeded \|\| outcome == Partial` | `is Succeeded or Partial`, matching `IsDelivered`. |
| Two stray double blank lines | Removed. |

**Bugs found by the pass and fixed separately** (behaviour changes, so not part
of the pass itself):

1. `MapLocation` read index 0 twice and never read index 4, so a five-line
   inspection address silently lost its fifth line. Five body lines now fold
   into four fields.
2. `RecordSubmissionAsync`'s comment described a Review re-check under a row
   lock that the code does not perform. The code is correct — by then the
   request has reached EVA, and refusing to record it would lose the delivery
   and permit a second claim — so the comment was corrected.
3. `FindReplayAsync` matched action history on the operation key but returned
   the case's most recent submission row, so replaying an automatic attempt
   could report a later manual send's outcome. Attempts now persist their
   operation key and the lookup keys on it.

**Left, with reasons:**

- `EvaCaseImageReader.SelectedDocument` carries two never-read members. It is a
  verbatim extraction; keeping it byte-identical is what makes the "one query,
  not two that agree today" claim reviewable. Worth a follow-up, not this diff.
- `ReconcileAutomaticEvaSubmissions` is a pass-through to its store. It mirrors
  `ReconcileAutomaticVehicleLookups` exactly; removing it would make the two
  sweeps asymmetric for the Worker to call. The existing convention wins.
- `AllowsAutomaticSubmission` has one caller. Kept for symmetry with
  `AllowsManualSubmission`, which the Send page calls directly.
- Two outcome-to-text tables (`SendModel.Describe`, `OutcomeLabel`). They serve
  different surfaces — an error banner and a status list — and consolidating
  into `OperatorLabels` would change rendered strings that snapshot tests
  assert. Raised for the reviewer rather than changed silently.
- `catch (DbUpdateException)` in the sweep is unfiltered, so a non-duplicate
  database failure reports "enqueued none" rather than surfacing. This was
  already true before the pass; named here for a reviewer's eye against rule 12.

**Efficiency:** no N+1. Box images are read exactly once per submission, after
the replay guard and the once-per-case check, so a replayed or already-delivered
case does no Box work at all. The sweep joins Principals through Cases in one
query.

**Altitude:** clean. Policy is in Core and called from Infrastructure rather
than re-implemented; no second implementation of a business rule.

One violation the pass did not catch but the architecture tests did: Core had
acquired a `System.Net.Http` reference by naming `HttpRequestException` in a
catch filter. The custody read now translates to `IOException` at the
Infrastructure boundary and Core is clean again.

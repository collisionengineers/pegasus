# Plan — INTK-035

Give staff the second half of the operator's Stage 0 rule: supply the
registration a stranded Triage request never carried, and the Triage opens.

Stacks on **PR #525 / `task/intk-033-triage-from-intake`**. The PR targets that
branch, not `dev`.

## Scope

One button on the intake receipt page `/Received/{id}`, beside "Register Image
intake": **"Open the Triage"** (the operator's own phrase). It takes a
registration, opens the Triage from the receipt's already-recorded
accepted-match evidence, and closes the receipt's open Unidentified item to it.

Out of scope, decided: no MCP tool; no change to the automatic path; no change
to how the Unidentified resolution attributes its actor (`Automation`, as the
existing component records — the staff actor is on the Triage's own history).

## Steps

### 1 — Widen `ProcessIntake.IsTriageRequest` to `public`

`src/Pegasus.Core/Intake/ProcessIntake.cs:334`. `internal` → `public`, nothing
else. **Reuses:** the existing single owner of "did the accepted route classify
this as a Triage request". The alternative is a second copy of the rule in the
page, which "one list per concept" forbids. `IsUnidentifiedEligible` and
`IsDeferredForAutomation` stay `internal` — no surface needs them.

### 2 — Page state: `Triage` and `CanOpenTriage`

`Details.cshtml.cs`. Inject `ITriageQueries`. In the existing
`LoadImageIntakeAsync` (renamed to `LoadReceiptDestinationsAsync` — it already
loads one destination and now loads two), fetch
`triageQueries.GetByOriginReceiptAsync(Receipt.Id, ct)` and set:

```
CanOpenTriage = ProcessIntake.IsTriageRequest(Receipt)
    && Receipt.Decision == IntakeDecision.NeedsSorting
    && Triage is null;
```

**Reuses:** `ITriageQueries.GetByOriginReceiptAsync` (INTK-033);
`ProcessIntake.IsTriageRequest`; the shape of `CanRegisterImageIntake` two
lines above it, which is the same three-clause gate.

### 3 — `OnPostOpenTriageAsync`

Modelled line-for-line on `OnPostRegisterImageIntakeAsync`:

```
public async Task<IActionResult> OnPostOpenTriageAsync(
    Guid id, string? vehicleRegistration, string operationKey,
    CancellationToken cancellationToken = default) =>
    await ExecuteCommandAsync(
        id,
        async actor =>
        {
            StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework);
            var receipt = <the loaded receipt>;
            var acceptedMatch = <the receipt's single AcceptedTriageMatch entry>;
            var origin = await imageIntakeOriginResolver.ResolveOriginAsync(id, ct) ?? throw …;
            await createTriage.ExecuteAsync(
                new(
                    new TriageOrigin(origin.ReceiptId, origin.SourceIdentity,
                                     origin.SourceHash, origin.EvaluationRevisionId),
                    ImageIntakeLifecycleRules.NormalizeRegistrationInput(vehicleRegistration),
                    acceptedMatch,
                    actor.SubjectId,
                    $"triage-from-staff:{operationKey}"),
                ct);
            <reconcile>
        },
        "The Triage was opened.",
        cancellationToken);
```

Points that are load-bearing:

- **The evidence is the receipt's own entry**, taken from `Receipt.Evidence`
  where `Finding == AcceptedTriageMatch`, passed back unmodified.
  `EfTriageStore.CreateAsync:71-79` re-checks it by full record equality
  against what is retained on the receipt; a reconstructed one fails closed.
  If there is not exactly one, throw `InvalidOperationException` —
  `ExecuteCommandAsync` already turns that into the recorded page error.
- **Origin comes from `IImageIntakeOriginResolver`**, whose `ImageIntakeOrigin`
  is the same four fields in the same order as `TriageOrigin`. Field-for-field
  construction, no adapter type, no new port (no second caller ⇒ no
  abstraction).
- **Authorization is explicit** because `CreateTriageFromIntakeRequest.Actor` is
  a bare `string` and the use case validates no rights.
  `StaffAuthorization.Require(actor, PerformCasework)` is the same owner
  Core's image-intake path calls; `ExecuteCommandAsync` maps the exception to
  `Forbid()`.
- **No reason field.** `CreateTriageFromIntakeRequest` has none. Not invented.

### 4 — Close the Unidentified item

After the create, call
`reconcileUnidentifiedDestinations.ResolveForReceiptAsync(Receipt, ct)`,
swallowing recoverable faults with a comment naming the periodic sweep as the
backstop — the convention `ConfirmMatchingSuggestionsAsync` already sets on
this page for bookkeeping after a committed write.

**Reuses:** the one owner of INTK-007's supersession rule, including the Triage
branch INTK-033 added. No second supersession is written. Verified that
`ResolveForReceiptAsync`'s opening `IsUnidentifiedEligible` guard does *not*
fire for this receipt (research, premise 7 + the eligibility note): a
`NeedsSorting` triage request is "deferred for automation", hence not eligible,
hence the method proceeds to its Triage branch.

### 5 — Markup

`Details.cshtml`, gated on `Model.CanOpenTriage`, placed immediately after the
`CanRegisterImageIntake` panel:

```
<section class="panel form-panel section-gap" aria-labelledby="open-triage-title">
    <h2 id="open-triage-title" class="section-label">Open the Triage</h2>
    <form method="post" asp-page-handler="OpenTriage" asp-route-id="@Model.Receipt.Id">
        <input type="hidden" name="operationKey" value="@Guid.NewGuid().ToString("N")" />
        <label for="open-triage-vrm">Vehicle registration</label>
        <input id="open-triage-vrm" name="vehicleRegistration" maxlength="20" required autocomplete="off" />
        <button type="submit" class="primary-action">Open the Triage</button>
    </form>
</section>
```

Labels and values only. **No `<p>` explanatory sentence** — the necessary-copy
list in `docs/design/README.md` is closed, and the neighbouring Image-intake
panel's sentence is an existing approved one, not a licence to write another.
No new operator question arose: heading, field label and button are all names,
not guidance.

### 6 — Test

`tests/Pegasus.IntegrationTests` — one test walking the whole operator rule:
a triage request whose extraction found no registration is processed → it
lands in Unidentified with no Triage → staff POST the registration to the
handler → a Triage exists for that origin receipt, `Open`, with the normalized
registration → the Unidentified item is `Resolved` with target kind `Triage`
and the Triage's id.

**Reuses:** the existing triage/unidentified integration fixtures
(`UnidentifiedReconciliationTests`, `TriageQueuesWebTests`) for receipt
construction and service resolution.

## Rejected: `CorrectDraft`

`OnPostCorrectDraftAsync` → `IResolveIntake` with
`IntakeResolutionKind.CorrectDraft` is the obvious way to supply a
registration and is **wrong**. `EfIntakeMutationStore.cs:194-220`
unconditionally rewrites the decision to `CaseCreated`/`BlockedIntake`, which
sends a triage request back into case allocation — the exact fault INTK-033
fixed — and breaks `ProcessIntake.IsDeferredForAutomation`, which keys off
`NeedsSorting`. Considered and rejected.

## Acceptance

- A stranded Unidentified triage request can be given a registration by staff.
- Doing so opens exactly one Triage, `Open`, against the origin receipt.
- The Unidentified item closes to that Triage with the resolution recorded.
- The button is absent when the receipt is not a triage request, is not
  `NeedsSorting`, or already has a Triage.
- A non-casework actor is refused.
- `dotnet build -c Release` clean; `Pegasus.Core.Tests` green;
  `Pegasus.IntegrationTests --filter "Category!=Corpus"` green.

## Simplification pass

_(dated heading added below before the PR)_

## Simplification pass — 2026-08-24

Run over this branch's own diff against its base `task/intk-033-triage-from-intake`:
my own four lenses first, then an independent `code-simplifier` pass told the
project rules and the load-bearing constraints.

### Applied

| # | Lens | Finding | Disposition |
| --- | --- | --- | --- |
| 1 | Efficiency | The Triage lookup was issued on **every** receipt page load, including the overwhelming majority that could never have a Triage. | **Applied.** Gated on `ProcessIntake.IsTriageRequest(Receipt)` first — a Triage only ever opens from a receipt the accepted route classified as one, so `Triage is not null ⇒ IsTriageRequest`, and the gate is behaviour-preserving. Mirrors the "cheapest discriminator first" reasoning already written into `ReconcileUnidentifiedDestinations`. |
| 2 | Altitude | The three gate clauses were inline in `LoadReceiptAsync`, unlike the sibling image-intake load. | **Applied.** Extracted `LoadTriageAsync`, matching `LoadImageIntakeAsync` beside it. |
| 3 | Simplification | `receipt.Evidence.Where(…).Take(2).SingleOrDefault()` — `Take(2)` buys nothing, since `SingleOrDefault(predicate)` already throws on the second match. | **Applied.** Collapsed to one `SingleOrDefault(predicate) ?? throw`. Four lines to two; same exception type, same handling by `ExecuteCommandAsync`, same operator message. The evidence stays the receipt's own instance, so `EfTriageStore`'s full-record-equality re-check is unaffected. |
| 4 | Convention | `LoadTriageAsync` took `receipt` as a parameter while `LoadImageIntakeAsync` on the adjacent line read the `Receipt` property — two shapes for one job. | **Applied.** Reads `Receipt`, like its sibling. |
| 5 | Tidy | Stray double blank line left beside the new properties. | **Applied.** |

### Found and deliberately not applied

| Finding | Honest reason |
| --- | --- |
| The new form panel writes `@Guid.NewGuid().ToString("N")` inline for its operation key, though the inherited `StaffPageModel.NewOperationKey()` exists and `_CaseHistory.cshtml` uses it. | **Not applied.** *Every* form panel in `Details.cshtml` does it inline. Changing only the new one makes the file less internally consistent; changing all of them is scope outside this branch. The new code follows the surrounding file correctly. The inconsistency is pre-existing and file-wide — not raised as a ticket, because it is cosmetic and file-local. |
| The new test hoists `FormUrlEncodedContent` where the nearest analogue (`ImageIntakeWebTests.cs:34-44`, posting to the same page) hoists the token instead. | **Not applied.** Purely cosmetic, no readability gain, and the test file it lives in sets no competing local convention. |
| The handler's "exactly one accepted Triage-match record" message restates a check `EfTriageStore.CreateAsync` also makes. | **Not applied.** The page must *select* the evidence in order to pass it, and `SingleOrDefault` enforces exactly-one as a by-product of that selection — not as a second copy of the rule. The store stays the authority; the page's message only reaches the log. |
| `CloseUnidentifiedForTriageAsync`'s comment says it behaves "exactly as the suggestion bookkeeping below", which logs where the other stays silent. | **Not applied.** The claim is accurate about the *disposition* — advisory, non-blocking, sweep is the backstop — which is the point the comment makes. Logging is the better of the two behaviours. Reworded, the intent would be the thing lost. |
| `ProcessIntake.IsTriageRequest` could move to the already-public `IntakeDecisionPolicy`, which this page already consults. | **Not applied.** The one-word widening is far the smaller change; moving the member would edit code outside this branch's diff and reassign ownership. That is a plan decision, not a simplification-pass one. Recorded here rather than silently dropped. |
| `Pages/Triage/*.cshtml.cs` gate on a class-level `[Authorize(Roles = …)]` while this page and the Administration/Connect/Mcp surfaces call `StaffAuthorization.Require` — two authorization idioms for Triage mutations. | **Not applied.** Entirely pre-existing and outside this diff. Noted for the reviewer, not reconciled here. |

### Bug hunt during the pass — three suspicions raised and disproved

Recorded so the reviewer need not repeat them:

1. **A double POST minting two Triages.** `operationKey` is fresh per render, so two renders defeat the replay guard. **Disproved:** past the replay check, `EfTriageStore.CreateAsync` runs a `Serializable` transaction and looks up `OriginReceiptId == … || (SourceChannel, ExternalReceiptToken) match`, returning the existing record. A second Triage per receipt is unreachable.
2. **The staff path accepting evidence the automatic path pre-qualifies.** `EfTriageStore` does `acceptedMatch.MatcherKey!.Trim()` — a null-forgiving deref that would surface as a 500 rather than a handled fault. **Disproved:** `CreateTriageFromIntake.ExecuteAsync` calls `ValidateCreate` → `ValidateAcceptedMatchEvidence` first, which enforces Strong strength, a non-empty matcher key and a positive matcher version as `ArgumentException`/`ArgumentOutOfRangeException` — both caught by `ExecuteCommandAsync`. Fails closed with an operator error. The handler is right not to restate the qualification list; Core owns it.
3. **Missing authorization.** `Pages/Intake/Details.cshtml.cs` carries no class-level `[Authorize]`, unlike `Pages/Triage/Details.cshtml.cs`. **Confirmed the explicit `StaffAuthorization.Require(actor, PerformCasework)` is necessary, not redundant** — and it is the convention the other non-attributed staff surfaces follow.

No defect was found in this branch's diff.

## Deviations from the plan as written

- Step 2 planned to rename `LoadImageIntakeAsync` to `LoadReceiptDestinationsAsync` and load both destinations in it. **Not done:** a sibling `LoadTriageAsync` is the smaller and more honest change, and keeps the image-specific work (suggestions, prefill, candidates) out of a method whose name would no longer say so.
- The plan did not mention showing the opened Triage. **Added:** a destination panel mirroring the existing Image-intake one, so the receipt page still leads somewhere after the action is taken and the button is gone. Labels and values only, no new copy.
- No operator question arose. Every string added is a name — a section heading, a field label, a button, and one status line reusing `Pegasus.Web.Pages.Triage.IndexModel.StateLabel`, the existing single owner of the Triage state vocabulary.

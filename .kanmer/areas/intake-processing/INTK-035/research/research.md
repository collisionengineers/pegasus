# Research — INTK-035

## The rule

`docs/operator-notes.md` § Stage 0, step 2 (protected, verbatim):

> keep it as **Unidentified** (formerly `Needs sorting`; …) until a vehicle
> registration is known, then open the Triage

[[INTK-033]] shipped the automatic half of that transition. The staff half —
"here is the registration, open the Triage" — has no caller.

## Premises, and how each was checked

All reads are against `task/intk-035-open-triage`, branched from
`task/intk-033-triage-from-intake` @ `e6144344` (PR #525, unmerged). Reading
`dev` gives a false picture: none of the Triage-from-intake code is there.

| # | Premise | Verified how | Result |
| --- | --- | --- | --- |
| 1 | `ICreateTriageFromIntake` has one implementation and one caller, and carries no authorization | `src/Pegasus.Core/Triage/TriageLifecycle.cs:5-16`; `grep CreateTriageIfQualifying` → `DurableIntake.cs:497,621,927` only | **Confirmed.** `CreateTriageFromIntakeRequest.Actor` is a bare `string`; `TriageLifecycleRules.ValidateCreate` never consults an `ActionActor`. Authorization is the caller's job. |
| 2 | The accepted-Triage-match evidence already exists on a registration-less receipt | `ProcessIntake.AcceptedTriageMatchEvidence` (`ProcessIntake.cs:598-624`) reads only `classification.IsTriageRequest` and `classification.Category?.IsReplyContext`; appended unconditionally at `ProcessIntake.cs:558-562` | **Confirmed.** Registration is never consulted. This is the premise the whole ticket rests on and it holds. |
| 3 | The gate that fails is the blank registration | `DurableIntake.cs:931-944` — `string.IsNullOrWhiteSpace(registration)` is the *first* disjunct of the `NotQualifying` return; the other three (single match, Strong, matcher key/version) are all satisfied by the evidence premise 2 builds | **Confirmed.** A stranded receipt satisfies everything except the registration. |
| 4 | `EfTriageStore.CreateAsync` re-checks the evidence by full record equality | `EfTriageStore.cs:71-79`: `retainedAcceptedMatches.Length != 1 \|\| retainedAcceptedMatches[0] != acceptedMatch` → throw | **Confirmed.** Pass the receipt's own entry back; a reconstructed one would differ and fail closed. |
| 5 | `IntakeReceipt` does not carry its evaluation revision id, and `IImageIntakeOriginResolver` already solves that | `IntakeReceipt` (`IntakeContracts.cs:366-400`) has no revision field. `IImageIntakeOriginResolver.ResolveOriginAsync(receiptId)` (`ImageIntakeContracts.cs:279-284`) returns `ImageIntakeOrigin(ReceiptId, SourceIdentity, SourceHash, EvaluationRevisionId)`; `TriageOrigin` (`TriageContracts.cs:28-32`) is the **same four fields in the same order with the same types** | **Confirmed.** Field-for-field construction, no adapter, no new port. |
| 6 | `EfTriageStore.CreateAsync` is idempotent per origin receipt | `EfTriageStore.cs:44-59` — replay by operation key, then `SingleOrDefault(item.OriginReceiptId == …)` returns the existing record | **Confirmed.** A staff action racing the automatic path cannot mint two. |
| 7 | `ReconcileUnidentifiedDestinations.ResolveForReceiptAsync` already closes the Unidentified item once a Triage exists | `ReconcileUnidentifiedDestinations.cs:87-155`; the Triage branch is at 122-131 | **Confirmed, and it works unchanged for the staff path — see the trap below.** |
| 8 | `NormalizeRegistrationInput` and `ValidateNormalizedRegistration` agree character-for-character | `ImageIntakeLifecycle.cs:174-188` vs `TriageLifecycle.cs` `TriageLifecycleRules.ValidateNormalizedRegistration` | **Confirmed identical**: both require non-empty, ≤ 20 chars, every char `IsAsciiLetterUpper \|\| IsAsciiDigit`. `NormalizeRegistrationInput` uppercases and filters to exactly that set. Same message text too. (The two validators are a duplicated rule across Core files — pre-existing, noted in the plan's simplification pass, not this ticket's diff.) |
| 9 | `ITriageQueries.GetByOriginReceiptAsync` exists | `TriageContracts.cs:296-303` (added by INTK-033) | **Confirmed.** |

### The eligibility check that could have killed it — checked, it passes

`ResolveForReceiptAsync` opens with `if (ProcessIntake.IsUnidentifiedEligible(receipt)) return false;`. If a stranded triage-request receipt were still "eligible", the staff action would open a Triage and leave the Unidentified item open forever.

`IsUnidentifiedEligible` (`ProcessIntake.cs:310-315`) = `Decision is NeedsSorting|Unsupported|OcrRequired|TechnicalFailure && !IsDeferredForAutomation(receipt)`.
`IsDeferredForAutomation` (`:325-327`) = `Decision == NeedsSorting && (IsImageOnlyMaterial || IsTriageRequest)`.

For our receipt: `NeedsSorting` + `IsTriageRequest` ⇒ deferred ⇒ **not** eligible ⇒ the guard does not fire and the method proceeds to its Triage branch. Verified by reading, not assumed.

### Nothing will close the item on its own

`DurableIntake.SynchronizeUnidentifiedAsync` (`:710-731`) registers the Unidentified item for a deferred non-qualifying triage request and **returns** — it never reaches `ResolveForReceiptAsync` in that pass. Only the periodic sweep (`ReconcileUnidentifiedDestinations.ExecuteAsync`, whose own doc comment names "a staff action" as exactly the out-of-pass case it exists for) would eventually close it. So the staff handler should call `ResolveForReceiptAsync` itself for an immediate queue, with the sweep as the backstop.

## The trap: `CorrectDraft` is not the way to supply a registration

`OnPostCorrectDraftAsync` (`Pages/Intake/Details.cshtml.cs:191-235`) → `IResolveIntake` with `IntakeResolutionKind.CorrectDraft` is the obvious-looking route. It is wrong:

- `EfIntakeMutationStore.cs:194-220` unconditionally rewrites the decision to `CaseCreated`/`BlockedIntake`. A triage request would go straight back into case allocation — re-creating the exact fault INTK-033 fixed (`ProcessIntake.cs:551-556` demotes a triage request *out* of `CaseCreated` for that reason).
- It would also break `ProcessIntake.IsDeferredForAutomation`, which keys off `Decision == NeedsSorting`.

Rejected. Recorded in the plan.

## Visibility gap

`ProcessIntake.IsTriageRequest` is `internal`, and `Pegasus.Core`'s only `InternalsVisibleTo` is `Pegasus.Core.Tests` (`Pegasus.Core.csproj:11`). The Web page needs to ask the same question to gate the button. Its own doc comment says it is "Named once because … no surface re-derives it from the taxonomy" — a UI surface is now one of those. Widening it to `public` is the reuse-preserving move; re-deriving `receipt.MailClassificationDecision is { IsTriageRequest: true }` in the page would be a second copy of the rule. `IsUnidentifiedEligible` and `IsDeferredForAutomation` stay `internal`.

## Template to copy

`OnPostRegisterImageIntakeAsync` (`Pages/Intake/Details.cshtml.cs:524-544`): normalize → resolve origin → call the use case → all inside `ExecuteCommandAsync` (`:385-417`), which owns `TryGetActor`/`Forbid`/`TempData`/redirect and already catches `StaffAuthorizationException`, `ArgumentException`, `InvalidOperationException`, `KeyNotFoundException` and recoverable faults. Its gating property `CanRegisterImageIntake` (`:506-509`) is the model for `CanOpenTriage`.

Authorization: the Triage use case carries none (premise 1), and the Image-intake equivalent gets it from Core's `RequireRegistrationActor` → `StaffAuthorization.Require(actor, StaffAccessRight.PerformCasework)`. The staff handler calls that same owner directly before creating; `ExecuteCommandAsync` turns the exception into `Forbid()`.

Advisory-failure convention on this page: `ConfirmMatchingSuggestionsAsync` (`:565-600`) swallows recoverable faults after the main write has committed, with a comment saying why. The reconcile call follows it.

## Not in scope

- **No MCP tool.** None exists for Triage creation; adding one edits a pinned 35-name inventory and a current-architecture count. Out of scope (decided).
- **Actor on the Unidentified resolution stays `Automation`.** `ReconcileUnidentifiedDestinations` records `ActionActor.Automation("intake-processing")` because `UnidentifiedValidation.ValidateResolve` requires Staff or Automation. The staff actor is recorded on the Triage's own creation history. Accepted as-is; no change to that component.

## Governing docs

- `docs/operator-notes.md` § Stage 0 (protected — read, not edited)
- `docs/frd/frd-03-triage.md` — Triage begins from a provider request
- `docs/frd/frd-02-intake-and-source-identity.md` — receipt/origin identity
- `docs/design/README.md` — necessary-copy list is closed; labels and values only

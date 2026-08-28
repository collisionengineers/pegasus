# Plan

## Shape of the change

Keep `EditLeaseDuration` at five minutes. Add a heartbeat that renews it every
60 seconds from the open page, so the lease never lapses under an editor. Give
Assessment an explicit edit mode over that same one lease. Change no save path.

Keeping the duration is the deliberate simplification: no expiry arithmetic in
any existing test moves, no FRD expiry sentence is reworded, and the two holders
that cannot heartbeat — MCP automation and the no-JS operator — keep exactly
what they have today.

## Steps

Each step names what it reuses.

1. **Core contract.** `HeartbeatCaseEditLeaseRequest(CaseId, Actor, LeaseToken)`
   and `ILeaseCaseForEdit.HeartbeatAsync`; `IHeartbeatCaseEditLease` beside the
   other three in `CaseCommandContracts.cs`. *Reuses* the existing request/port
   shape of `RenewCaseEditLeaseRequest` minus its operation key.
2. **Core use case.** `HeartbeatCaseEditLease` + `ValidateHeartbeat` in
   `CaseCommandSeams.cs`. *Reuses* `CaseCommandSeamRules` token-length
   validation (L203-241) and `CaseEditAuthority.LeaseTokenLength`.
3. **Store.** `EfCaseWorkflowStore.HeartbeatAsync`. *Reuses* `RenewAsync`'s
   Serializable transaction, `AcquireWorkflowMutationLockAsync`,
   `StaffAuthorization.Require`, `ArchivedCaseGuard.RequireNotArchived` and
   `RequireLease`. Drops the operation key, the request hash, all three replay
   helpers, `AddLeaseOperation`, and `RequireVersion`.

   Two constraints that are the whole reason this is not just `RenewAsync`:
   - it writes **no** `CaseEditLeaseOperations` row — that table is never
     pruned, and one row per minute per editor is unbounded growth. FRD-01:89
     already classifies heartbeat as telemetry.
   - it must **not** write `EditLeaseOperationKey` or `EditLeaseRequestHash`.
     `RenewAsync:258` overwrites the operation key, and
     `Details.cshtml.cs:411-418` parses that same column back as the *claim*
     key — a heartbeat touching it would silently break "Recover editing" on
     every page load.
4. **Persistence tests 1-4, 9.** Red then green, before any Web work.
5. **Web helper.** `CaseMutationPageModel`: a `protected` method returning
   `NoContent()` / `Conflict()`. **It must touch no TempData on any path,
   including not calling `ClearLeaseState()` on failure** — TempData here is
   cookie-backed (L20-29, L269), so a heartbeat re-issuing the cookie can race a
   concurrent form POST and drop the operator's token mid-edit. The refusal copy
   already lives on the next navigation.
6. **Details page.** Inject the port, add `OnPostHeartbeatLeaseAsync`, render a
   hidden `data-edit-heartbeat` form beside the existing lease forms.
7. **site.js.** New IIFE after L536: hide the Renew form, beat on the rendered
   interval, beat once immediately on `visibilitychange` → visible, stop
   permanently on any non-204 and when the form is absent — which is the state
   after a save, so a cleared lease can never be resurrected by an in-flight
   beat. *Reuses* the `fetch` + `new FormData(form)` convention at L298-300,
   whose FormData carries `__RequestVerificationToken` automatically; no new
   antiforgery mechanism, no custom header.
8. **Web tests 5-6.**
9. **Copy deletions.** `EditModeDisplay.cs:28,31-32,46-49` lose their expiry
   clauses; `availableAtUtc`, `WallClock` and `ResolveLondonTimeZone` then have
   no reader and go with them; call sites at `Details.cshtml:97` and
   `Triage/Details.cshtml.cs:501` follow. `Details.cshtml.cs:184,230` and
   `Operations/Index.cshtml.cs:148` likewise. Every one deletes a clause and
   writes no new sentence, so the closed approved-copy list and the
   `design/README.md:191,435` vocabulary bans stay satisfied.
10. **Assessment.** Rebase `IndexModel` onto `CaseMutationPageModel` (adds one
    `ILogger` ctor parameter); add claim/release/heartbeat handlers and the
    controls in the existing `record__bar`. `:216` and `:535` drop their inline
    claim and read `PeekLeaseToken()` guarded by `PeekGuid(LeaseCaseIdKey) == id`.
    `:409`'s claim goes; `:442`'s re-claim **stays** (the document add really
    does clear the lease) and its token is carried forward with
    `StoreLeaseAuthority` so the operator stays in edit mode across the
    redirect. *Reuses* `CaseMutationPageModel`, `EditModeDisplay`,
    `_StatusChip.cshtml:66-69`, and the extracted `_EditFinishConfirm` partial.

    Do **not** call `ExecuteCaseCommandAsync`/`ExecuteTransportCommandAsync`
    from this page — both end in `RedirectToDetails` (L173-177), which would
    throw the operator off the assessment mid-edit.
11. **Web tests 7-8** and the changed suites.
12. **Docs.**

## Decisions taken, with reasons

**One window, not two.** An earlier draft had a shorter heartbeat window (150s)
so an abandoned case freed sooner. Rejected: it regresses MCP automation, which
cannot heartbeat and can exceed 150s in one model turn, and the no-JS operator;
it churns ten passing tests; and hidden-tab throttling makes a sub-60s interval
worthless anyway. Abandonment recovery stays at today's five minutes — no
improvement, but no regression, and the actual complaint (being timed out
mid-edit) is fully fixed.

**Automatic mail association stops yielding to the lease.** Deleting
`EfIntakeMutationStore.cs:107-112` is a correction, not a bypass: that path
writes receipt-side rows only, never touches `caseWorkflow.Version`, and records
`ExpectedCaseVersion = null` — FRD-01:89 already places such records outside
editable case state. Without this, mail arriving during any editing session
would silently require manual linking, because the yield is one-shot and never
retried (`DurableIntake.cs:950-955`). Safety is provable: the case version is
untouched, so an editor's pending save still validates and nothing is
overwritten. `ArchivedCaseGuard` stays; the image-intake path at `:510` keeps
its check because it really does mutate the case.

**The manual "Renew editing" button survives**, hidden by script. This codebase
supports no-JS deliberately (`Details.cshtml.cs:51-56`, `site.js:538-539`,
the `form.submit()` fallback at `:316`), and it keeps
`docs/design/README.md:746`'s six named recovery interactions true unchanged.

**`RenewAsync` survives unchanged**, for the MCP `pegasus_case_edit_renew` tool
and that button. The heartbeat is deliberately **not** exposed as an MCP tool —
it would break the ingress replay/audit contract and change the tool census.

## Needs operator sign-off before merge

- The copy deletions in step 9.
- Widening the recorded UI-15 exception (`docs/design/README.md:896-910`, whose
  "staff save paths … remain forbidden") by putting an edit-mode control on the
  assessment surface. The four staff save paths already exist there; this makes
  them operator-visible.

## Not in this ticket

`KANMER-005` — lease exclusivity between staff and Automation Actors. A real
open defect that longer-held leases make more visible, but it is its own record
and this change neither fixes nor worsens the underlying claim path.

## Simplification pass

To be run over the branch diff before the PR, per the repository workflow, and
recorded here under a dated heading.

## Simplification pass — 2026-08-28

Run over this branch's own diff (`git diff origin/dev...HEAD`) with the
`code-simplifier` agent across the four lenses, before the PR.

**Reuse — nothing found.** The pass confirmed the reuses the plan named:
`RequireOperationKey` lifted to the base and its three private copies deleted,
the finish-confirm dialog extracted to a shared partial, `EditModeDisplay`'s
`WallClock` (a duplicate of `OperatorLabels.OfficeTime`) removed with the copy
that used it, `_EditHeartbeat` following the existing `view-data` partial
convention, and the interval living once in `CaseEditAuthority`.

**Altitude — nothing found.** Core owns the seam and its validation,
`ILeaseCaseForEdit` is the port, the store is the only adapter, Web holds only
a token and TempData keys, and the intake deletion moves no policy.

Findings and dispositions:

| # | Finding | Disposition |
| --- | --- | --- |
| 1 | `OnPostClaimLeaseAsync`/`OnPostReleaseLeaseAsync` were copied onto the assessment, **and the two copies disagreed**: Details replays the same claim key on a non-lease-loss refusal, the new copy invented a fresh one | **Fixed.** Both handlers moved to `CaseMutationPageModel`, parameterised by a `Func<IActionResult>` redirect and virtual `StatusTempDataKey`/`ErrorTempDataKey`. Details' rule wins — a claim is idempotent by its key, so a retry must replay rather than claim twice. `ResetClaimLeaseOperationKey` deleted with the divergence. This was a real defect, not a style point. |
| 2 | `HeldLeaseToken(caseId, presented)` fell back to TempData when the form carried no token — a second source for a value the form already renders, and a hole: a tab rendered before edit mode could save under a lease another tab later entered | **Fixed.** The helper is gone; the three handlers guard on `string.IsNullOrWhiteSpace(editLeaseToken)` and take the posted token, exactly as every other mutating page does. |
| 3 | `CaseVersion` duplicated `AssessmentWorkspaceHeader.Version`, already bound in the same request | **Fixed.** Property deleted; the view uses `Model.Case!.Header.Version`. `CaseIsArchived` stays — the header carries no archive. |
| 4 | `OnPostSaveDamage` and `OnPostAcceptSpecification` ran `getCase` *before* the edit-mode guard, paying a case query to refuse | **Fixed.** Guard moved above the query in both. `OnPostImportEstimate`'s guard stays after a `getCase` that earlier validation needs. |
| 5 | `site.js` kept `timer` and `beating` for one piece of state | **Fixed.** `timer === null` is the single "stopped" signal. |
| 6 | `Details.RestoreLeaseState(Guid, ActionActor)` was a private overload calling its own protected base overload — reads as recursion | **Fixed.** Inlined at its one call site. |
| 7 | `ViewerHoldsEditAuthority`'s self branch is unreachable on the assessment (the holder always matches an earlier `if`) | **Accepted, no change.** It mirrors the existing shape at `Details.cshtml:97-101`; changing one and not the other would be the inconsistency. Noted so the dead branch is not read as live behaviour. |
| 8 | `stop()` leaves the manual renew control hidden, so a stopped beat has no manual path | **Rejected with reason.** `stop()` runs only on a non-204 — a 409 means the lease is genuinely gone, and renewal would fail. Offering the control there would be dishonest. A network failure does not stop the beat (the `catch` deliberately continues). |

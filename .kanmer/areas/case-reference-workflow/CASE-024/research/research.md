# Research — the case edit lease as built

Every premise below was verified by reading the code at `dev` (783b4b88).
Premises marked *assumed* were not verified in this repository.

## The lease has one policy owner and one duration constant

`src/Pegasus.Core/Workflow/CaseEditAuthority.cs` owns the whole expiry
question. `IsHeld(expiresAtUtc, nowUtc) => expiresAtUtc > nowUtc` is asked by
every guard and every projection; its own doc comment states the design — *"An
abandoned lease expires without a sweeper, so every projection and guard asks
this one question."*

The duration is **not** in Core. `EfCaseWorkflowStore.cs:20` holds
`EditLeaseDuration = TimeSpan.FromMinutes(5)`, applied at `:173` (claim) and
`:254` (renew).

**No documentary authority for five minutes.** Searched `docs/` (PRD, all 12
FRDs, all ADRs, `capabilities.md`, `operator-notes.md`, `boundaries.md`,
`runbook.md`, `engineering.md`, `design/README.md`), `docs/reference/` and
`reference/`. No file states any case-edit-lease duration. `operator-notes.md`
contains no statement about editing conflicts, locking, or simultaneous editors
at all — the nearest is `:431` CAP-016 ("Give staff full case-management
capability, including editing case details as necessary"), which says nothing
about exclusivity or timing. The five minutes leaks into ticket prose
(`PR-049:39`, `PR-052:41`) as though it were policy. It is not.

What *is* operator-settled is exclusivity itself: `docs/open-decisions.md:11`
lists "exclusive one-case edit actions" among settled matters, and CASE-27
(`docs/capabilities.md:151`) is accepted.

## Governing text

`docs/frd/frd-01-case-identity-and-lifecycle.md`, "Case edit authority and
recovery" (L83), is the canonical owner:

- L85 — "Entering edit mode acquires the case's one server-owned **expiring**
  lease. Other authorised staff remain read-only and can see the holder and
  recovery state."
- L87 — "The holder may leave editing; an abandoned lease **expires by server
  time** and may then be reacquired… There is **no Administrator bypass, forced
  takeover**, collaborative merge…"
- L89 — "Background append-only receipt, dispatch, and document-processing
  records **remain separate from editable Case state** and cannot bypass Case
  versions to alter it… routine renewal, expiry, **heartbeat**, polling, and
  adapter mechanics **remain telemetry**."

L89 matters twice over: it pre-authorises a heartbeat as telemetry (so it writes
no history row), and it is the boundary that decides the mail-association
question below.

`docs/design/README.md:746` names the six recovery interactions — "Enter edit
mode, renewal, Leave editing, authoritative expiry, reload/compare, and
reacquire" — and forbids takeover. `:191`/`:435` ban the words "lease" and
"version" from operator copy.

## The save already ends the lease

`CaseMutationGuard.Complete()` = `Version++` then `ClearLease()`. Every case
mutation runs it inside its own transaction:
`EfCaseWorkflowStore.MutateAsync:800-802`, `EfCaseDataStore:125,236`,
`EfCaseAssessmentStore:262`, plus `EfDocumentCustodyStore`,
`EfDocumentRequestStore`, `EfExternalWorkStore`, `EfTriageStore`,
`EfQueuedCustodyProcessor`, `EfVehicleLookupWorkStore`, `EfCaseTaskStore`,
`EfRepairSpecificationStore`, `EfRecordEngineerFinding`,
`EfLinkedCaseReplacementStore`, `EfIntakeMutationStore:544,727`.

So the brief's "lease should end within 60 seconds of a save" is **already met
at 0 seconds**. This is the single most important finding: it means requirement
2 needs a regression test, not an implementation.

## There is no heartbeat and no sweeper

`grep Lease src/Pegasus.Worker` returns nothing — no hosted service, no timer,
no cleanup job. Expiry is entirely lazy-on-read (`EfCaseQueryStore.cs:196-201`).
Stale columns are physically cleared only by the next claim
(`EfCaseWorkflowStore.cs:170`). `CaseEditLeaseOperations` rows are never pruned
— which is why a heartbeat must not write one.

Client side, `wwwroot/js/site.js` has exactly one lease-aware block
(`:497-536`, the CASE-007 unsaved-changes dialog). Renewal is a manual button
post only.

## Assessment is a second editing surface with a different idiom

`Pages/Cases/Assessment/Index.cshtml.cs` extends `StaffPageModel`, not
`CaseMutationPageModel`, renders no lease UI (grep for lease in `Index.cshtml`
returns nothing), and self-claims a throwaway lease inline before each of four
mutations — `:216` (save damage), `:409` + `:442` (estimate import, twice,
because the document add clears the lease), `:535` (accept specification).

Consequences, all verified: an engineer working an assessment appears unlocked
to other staff; a claim fails closed if anyone holds the workspace; and a
mid-sequence failure in the import (document lease succeeds, draft lease fails)
strands a five-minute lease.

Governance: `docs/design/README.md:896-910` records the assessment workbench as
a route-restored exception whose "staff save paths … remain forbidden until the
full UI-15 re-entry approval" — while the code already has four of them, and
`Index.cshtml.cs:16-26` says so in its own class comment. Adding an edit-mode
control widens that recorded exception, so it needs operator sign-off.

## The two intake association paths are not alike

This is the finding that decides how the change handles the mail worker.

`EfIntakeMutationStore.AssociateAutomaticAsync` (`:99-165`) — automatic
mail→case association:

- writes `IntakeManualAssociationEntity` and `IntakeMutationHistoryEntity`, both
  receipt-side
- bumps `receipt.Version` (`:137`), never `caseWorkflow.Version`
- never calls `CaseMutationGuard.Complete`, writes no `CaseWorkflowEvents` row
- records `ExpectedCaseVersion = null, BeforeCaseVersion = null,
  AfterCaseVersion = null` (`:156-158`) — it declares in its own history that it
  did not touch the case
- reads `caseWorkflow` only for the case id and the archived guard
- and yet **yields** to a live edit lease at `:107-112`, one-shot: the caller
  swallows it (`DurableIntake.cs:950-955`) and never retries

The image-intake path at `:510` is the opposite: it checks
`ExpectedCaseVersion` (`:502`), calls `CaseMutationGuard.Complete(caseWorkflow)`
(`:544`), and writes a `CaseWorkflowEvents` row. That one genuinely conflicts
with an editor.

So the lease check at `:107` sits on the wrong side of the boundary FRD-01:89
draws. Removing it is a correction, not a bypass, and it is safe to prove: the
case version is untouched, so an editor's pending save still validates against
the version they loaded and nothing is overwritten.

## Everything else that reads the lease

Just-in-time claimers that will refuse more often once leases are held for whole
sessions — all fail closed with honest refusals, none needs code:
`Pages/Operations/Index.cshtml.cs:136`, `Pages/Mail/Message.cshtml.cs:237,295`,
`Pages/Intake/Details.cshtml.cs:271`, `Pages/Triage/Details.cshtml.cs:380`,
`Presentation/UploadCaseDecision.cs:150,243`. `EfOperationsStore.cs:702` gates
`CanRetry` on `!activelyLeased`.

MCP automation holds the same lease with no browser:
`Mcp/CaseMcpTools.cs:249-380` (`pegasus_case_edit_begin`/`_renew`/`_end`). It
cannot heartbeat, which is the reason the five-minute grant must not shrink.

Copy that names an absolute expiry, and therefore becomes wrong once a heartbeat
extends it: `Pages/EditModeDisplay.cs:28,31-32,46-49`,
`Pages/Cases/Details.cshtml.cs:184,230`, `Pages/Operations/Index.cshtml.cs:148`
("Try again in a few minutes").

## Open defect, not ours

`KANMER-005` (backlog, `automation-integrations`): a staff user was observed
taking an Automation Actor's unexpired lease, contradicting FRD-01:87. Read
`ClaimAsync:165-168` refuses a held lease, so the steal is happening by some
other route — plausibly the assessment self-claim or the replay path. Longer
leases make it more visible. Linked, not absorbed.

## Assumed, not verified

- Chrome throttles timers in hidden tabs to roughly one fire per minute. This is
  documented browser behaviour, not something checked here; the 60 s interval
  inside a 5-minute window is sized so that being wrong about it still leaves
  five missed beats of slack.
- Whether `AssessmentWorkspace` exposes `ActiveEditLease` for rendering holder
  state. If it does not, the page already injects `IGetCase` and can read
  `CaseDetails.ActiveEditLease` as Details does — prefer that over widening the
  workspace projection.

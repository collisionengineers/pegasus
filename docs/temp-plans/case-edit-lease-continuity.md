# CASE-27 edit-lease continuity and conflict recovery

Task line: CASE-27 edit-lease continuity and conflict recovery for both
callers (MCP-02/MCP-04), branch `task/case-edit-lease-continuity`.

The lease mechanism exists and holds: one server-owned expiring lease per
case, a token hash plus holder plus version guard on every staff mutation,
an idempotent replay ledger, and no takeover path. This task closes the
clauses of
[Case edit authority and recovery](../requirements.md#case-edit-authority-and-recovery)
that shipped behaviour does not yet satisfy, and gives the Automation Actor
the same continuity the staff app has. It adds no new concept: no takeover,
no force-save, no Administrator bypass, no merge, no new lease kind.

## Why now, and the environment it lands in

Read-only production inspection on 2026-08-05 (`rg-pegasus-prod`,
subscription `e6076573-23a5-46a8-acef-7e22d264e5db`):

- `AppExceptions` over the last 168 hours contains zero
  `CaseEditLease*`/`CaseVersionConflict`/`CaseOperationConflict` records, and
  the ADR-0020 verification on 2026-08-04 recorded zero accepted cases. There
  is no live lease state and no data to migrate; the risk of this change is
  entirely local-verification risk, not production-behaviour risk.
- `pegasus-prod-web-252ow37gij` is a Container App with
  `minReplicas: 0`/`maxReplicas: 1` (`infra/modules/platform.bicep:453-455`).
  Two consequences bind the design: the Web is single-replica, so nothing
  here may assume more than one instance *or* rely on it; and the app scales
  to zero, so no editor state may live in process memory that dies with the
  replica. `Pegasus.Web` registers no session — `TempData` is the default
  cookie provider protected by the Data Protection keys persisted to blob
  storage — so `TempData` survives a recycle and is the correct carrier for
  retained proposed values, subject to the cookie size ceiling handled below.

## Changes

### 1. One mutation guard (Core-owned)

`docs/engineering.md` "One Core owner" forbids a third implementation of a
business rule. The lease/version guard has three:
`Persistence/CaseMutationGuard.cs:15-82`,
`Persistence/EfCaseDataStore.cs:487-509`, and
`Persistence/EfCaseTaskStore.cs:360-385`. They have already drifted —
`CaseMutationGuard` tolerates a malformed stored hash, `EfCaseTaskStore`
calls `Convert.FromHexString` unguarded and throws `FormatException` instead
of a lease conflict.

Collapse to the single `CaseMutationGuard` implementation and delete the two
copies, keeping every call site's existing exception contract
(`CaseEditLeaseConflictException`, `CaseEditLeaseExpiredException`,
`CaseVersionConflictException`). The decision the guard encodes —
missing/expired/wrong-holder/stale-version refuses without overwriting — is
Core policy, so the shared guard's decision function moves to
`Pegasus.Core` and Infrastructure supplies the persisted material to it;
Infrastructure keeps only the row read, the fixed-time hash comparison, and
the lease clear.

### 2. One lease-token length contract

Seam validators cap `EditLeaseToken` at 128 characters
(`Lifecycle/CaseCommandSeams.cs:221,238`, `Lifecycle/CaseLifecycle.cs:405`),
`Intake/DurableIntake.cs:991` caps at 64, and the column is `nchar(64)`
(`Persistence/CaseWorkflowModelConfiguration.cs:32`). Tokens are issued as
64 hex characters (`EfCaseWorkflowStore.cs:167`), so the 128 allowance can
never round-trip and is a validation hole, not a feature. Settle on 64
everywhere through one named Core constant, and cover the over-length case
with a test that asserts rejection rather than a database truncation error.

### 3. Expired means free, everywhere it is projected

**Corrected against `origin/dev` during implementation.** This section as
first written claimed `EfCaseQueryStore` projected the lease columns with no
expiry comparison and that Triage would therefore show "held by \<holder\>
until \<a time in the past\>". That was wrong: `EfCaseQueryStore` already
had `&& expiresAtUtc > timeProvider.GetUtcNow()` and
`EfOperationsStore.MapLeaseState` already had `&& expiresAtUtc > nowUtc`, so
an expired lease already projected as free for every consumer and **no
behavioural gap existed**. Nothing was broken here and CASE-27 closes
nothing at this clause.

What remains, and what was done: the same expiry rule was written out three
times — twice in the stores and once again client-side in
`Pages/Cases/Details.cshtml.cs`, which re-tested the expiry the projection
had already applied. Both stores now ask one Core owner
(`CaseEditAuthority.IsHeld`), and the redundant page-level compensation is
removed so one rule ships instead of three. This is a consolidation, not a
fix: no sweeper, no background service, no new deployment unit, and no
change in what any surface shows. Expiry stays lazy in the store, which was
already correct — the claim path refuses only a *live* lease.

### 4. The holder and the recovery state are visible to non-holders

The requirement is that other authorised staff "can see the holder and
recovery state". `Pages/Cases/Shared/_CaseWorkflow.cshtml:53-56` shows only
"Another staff member currently holds edit authority for this case", while
`Pages/Triage/Details.cshtml.cs:311` already renders holder and expiry — the
disclosure is inconsistent and the primary surface is the one that fails the
clause.

Show, on the case workspace and in Triage, who is editing and when editing
becomes available, in the operator vocabulary the
`docs/ui-work/ui-standards-and-review.md:109` ban requires: "lease",
"opaque", "token" and "expiry" do not appear in the copy. The expiry renders
as a Europe/London wall-clock time consistent with the rest of the
application. Keep the change confined to the existing edit-mode panel
partial: the whole-page redesign of the case container is a separate queued
task (`NOW.md` "Cases (pages 4/5, 12)"), and this task must not pre-empt it.

**Corrected twice during implementation.**

The holder is *not* a display identity. `CaseEditLeaseSnapshot.Holder` is the
staff subject identifier, and the standard bans GUIDs from operator copy in
the same list as "lease" and "opaque". The holder is therefore resolved to
the staff account name by a Core use case over the existing
`IStaffAccountQueries` read, and an unresolvable holder is disclosed as
"Another member of staff" — no identifier reaches a page in any state.

**The Operations half of this section was dropped on merge.** `origin/dev`
has since landed the UI programme's rework of `Pages/Operations/Requests`,
which deliberately removes the edit-mode ceremony from that page: one post
performs the whole withdrawal, and whether someone else is editing the case
is the result of trying rather than a state the operator is asked to manage.
That is a merged operator decision about that page and it takes precedence,
so the "Edit mode" column, the holder disclosure, and the enter/recover/
renew/leave controls are not reinstated there, and the code behind them was
removed. The requirement's clause stays satisfied by the case workspace and
Triage, which are where a case is actually edited. The lease and version
guards on that page's withdrawal POST handlers are untouched — the
protection stayed, only the ceremony went.

### 5. A rejected editor keeps their proposed values

Today every lease/version failure in `Pages/Cases/Details.cshtml.cs` is a
bare `RedirectToDetails(id)` (lines 210, 257, 300, 826, 966, 1005, 1029,
1068, 1159, 1235, 1297, 1331) and the posted form is discarded. The
requirement is explicit: "The rejected editor keeps proposed values for
comparison and must reload and reacquire rather than merge or force the
save."

On a refused mutation, carry the submitted values of that one form through
the PRG redirect in `TempData` and render them beside the reloaded current
values, labelled as the values the editor proposed and the values the case
now holds. There is no accept/apply/merge control: the only way forward is
to re-enter edit mode and retype, which is what the requirement's "reload
and reacquire rather than merge or force" demands. Constraints:

- Cookie `TempData` has a ~4 KB ceiling. Retain the failed form's own fields
  only, cap the retained payload, and when it does not fit, degrade to the
  existing behaviour plus an explicit statement that the proposed values
  could not be kept. Silently dropping them is the current defect and is not
  an acceptable fallback.
- No lease token, no version number, and no case identifier beyond the route
  value is written into the retained payload.

### 6. The Automation Actor gets the same continuity

`Mcp/CaseMcpTools.cs` exposes `pegasus_case_edit_begin` and
`pegasus_case_edit_end` only; `IRenewCaseEditLease` is not injected, so an
automation run whose work exceeds the five-minute lease cannot renew and
must re-claim under a fresh operation key. Staff have a renew control
(`_CaseWorkflow.cshtml:28-34`); the Automation Actor does not. Add
`pegasus_case_edit_renew` under the existing `automation.cases` scope,
wrapping `IRenewCaseEditLease` — the same Core use case the staff renew
handler calls, no new policy — and returning the same
`CaseEditLeaseToolResult` shape as `begin`.

The refusal contract must also be legible to the Automation Actor: a
refused tool call reports which guard refused (no active edit authority,
held by another actor, or the case changed since it was read) and the
current case version, so the actor can reload and reacquire rather than
retry blindly. It reports no token and no other holder's material beyond the
holder identity staff already see. `pegasus_document_add` and
`pegasus_document_export` (MCP-04) inherit this through the shared guard;
`pegasus_document_download` stays read-only and lease-free.

Renewal, expiry and reacquisition stay telemetry, not action history, per
the requirement's last paragraph — a deliberate recovery and a material
denial remain attributable history.

## Explicitly out of scope

- No heartbeat or auto-renew timer. The requirement names heartbeat only as
  a telemetry classification, not as a promised behaviour, and a JavaScript
  keep-alive collides with the undecided production-CSP inline-script
  question already queued in `NOW.md`.
- No lease-duration configuration option. Five minutes is asserted by
  existing tests and changing it is an operator decision, not an
  implementation one.
- No change to the plaintext `EditLeaseToken` column retained for replay
  recovery. It is a real observation — a secret at rest beside its hash —
  but removing it changes the accepted replay contract and needs its own
  decision. It is recorded as a follow-up line, not fixed here.
- No case-container redesign, no new page, no schema migration.

## Verification

Local, from the task worktree:

- `dotnet restore`, `dotnet build --configuration Release`.
- `dotnet test` on `Pegasus.Core.Tests` and `Pegasus.ArchitectureTests`
  whole, then the focused integration classes below, then the full
  non-corpus integration suite (long — roughly half an hour; the full log is
  kept).

New and changed tests, each named for the rule it proves:

- Core: expired-lease refusal, wrong-holder refusal, stale-version refusal
  and over-length-token rejection against the single guard, so the
  guard's decisions are proved outside the LocalDB-dependent suite for the
  first time.
- Core/Infrastructure: a lease that has passed its expiry projects as no
  active editor; a live one projects holder and expiry.
- `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs`:
  reacquisition by a *different* holder after expiry, which no test covers
  today; the existing five lease tests must still pass unchanged.
- `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`: a refused save
  returns the proposed values alongside the current values and offers no
  apply/force control; a second staff member sees the holder and the time
  editing frees, in copy containing none of the banned vocabulary.
- MCP: `pegasus_case_edit_renew` extends the lease through the same Core use
  case and is refused for a non-holder; a refused document tool reports the
  refusing guard and the current version.

Evidence tier: this task can reach a green local build and test run plus a
green `repository-check` — tier "green build", not "deployed" and not
"accepted". Nothing here is exercised against production, and the MCP
surface stays behind its existing `Features:AutomationMcp` composition gate.

## Documentation touched

`docs/capabilities.md` CASE-27 and the MCP-02/MCP-04 rows gain the renew
tool and the settled disclosure/retention behaviour if their activation text
becomes inaccurate; `docs/architecture.md:390` names nine MCP tools and
becomes ten. No new Markdown file beyond this plan, and no ADR: nothing here
constrains future architecture beyond the "one Core owner" rule that already
binds.

## Known coordination risk

`task/send-to-ai-round-trip` is in flight and adds five Automation Actor
tools; both tasks touch `src/Pegasus.Web/Mcp/` and the tool inventory
recorded in `docs/architecture.md`. The collision is additive (a new tool
each) and resolves by merging `origin/dev` into this branch before the PR.

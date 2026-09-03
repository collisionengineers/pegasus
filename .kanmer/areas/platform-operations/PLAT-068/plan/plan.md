# Plan — PLAT-068 (2026-09-02, gpt-5.6-terra high; revised 2026-09-03 after cross-model review)

Planned read-only at `897db953` (`origin/dev`) in the detached
`.worktrees/research` checkout; `git status --porcelain` empty before and
after. Board state (ticket, D29–D46, EPIC-011/EPIC-012 context and waves,
research, files, open questions) was supplied verbatim by the Claude wrapper;
Codex has no Kanmer access. Prompt and raw output: scratchpad
`prep/PLAT-068/plan-prompt.md`, `plan-out.md`; review prompt and output:
`prep2/PLAT-068/review-prompt.md`, `review-out.md`.

## Premise checks

- Read `AGENTS.md`, `CLAUDE.md`, the research and files documents, FRD-04,
  FRD-12, FRD-01 §Sign-off Engineer, FRD-11, and `docs/design/README.md`.
- Ran `rg`/`Get-Content` against `StaffAccountAdministration.cs`,
  `EfStaffAccountAdministration.cs`, `EfStaffAccountQueries.cs`,
  `PegasusDbContext.cs`, `DependencyInjection.cs`, the Accounts page and its
  tests, `OperatorLabels.cs`, the migration/grant convention, the Test UI
  catalogue, and the mockup.
- Migration tail is `20260829212237_GrantProviderSubmissionAcceptRecovery`;
  the new migration is `<timestamp>_StaffAccountSignOff`, immediately after.
- FRD-04 §Staff accounts (D31): the initial flagged accounts are
  A Patterson, N O'Reilly and E Mawdsley, held as application data, never
  hard-coded — which supports the no-seed decision below.
- **Corrected 2026-09-03 (review finding 7).** The Web role's `AspNetUsers`
  grant is `SELECT, INSERT, UPDATE`
  (`20260729199000_RuntimeRoleReconciliation.cs:106`, inside `WebGrants`).
  The `SELECT, INSERT, UPDATE, DELETE` tuple at line 218 is inside
  `PreviousWebGrants` (207–263), **not** `WorkerGrants` (166–206), which
  contains no `AspNetUsers` entry at all. The research document's claim that
  the Worker holds `AspNetUsers` rights is wrong. Every caller added by this
  ticket runs in `Pegasus.Web`; the Worker renders no report and reads no
  staff account, so the existing Web table-level grant covers the new columns
  and this migration adds no `GRANT`. If a later lane needs the signatory
  from the Worker, that lane adds the Worker grant in its own migration.
- **Corrected 2026-09-03 (review finding 3).** `wwwroot/js/site.js:4-7` and
  `:875` record that the deployed CSP is `default-src 'self'` with no nonce
  or hash allowance, so an **inline script is silently discarded in
  Production**. No inline script is added by this ticket.

## Objective

Give an Administrator a reasoned, permanently recorded Sign-off Engineer
account setting: the flag, the printed signatory name, qualifications, a
stored PNG signature, and the single "Default sign-off Engineer"
designation. Show the setting's exact state on the Accounts table and expose
one Core-owned sign-off seam — list eligible profiles, and fetch one by
account ID — for [[CASE-040]] and [[DOCS-017]].

Not in this ticket: selecting or defaulting a Case sign-off engineer
([[CASE-040]]), report rendering or the projection-source wiring
([[DOCS-017]]/[[CASE-040]]), password reset ([[PLAT-064]]), an image-serving
route, seeding production accounts, or any other EPIC-012 lane's files.

## Resolved model (2026-09-03 operator and controller answers)

These are settled; the steps below are written to them, not to earlier
defaults.

- **No seed.** An Administrator sets the flag and uploads each signature
  through the new control. The migration seeds nothing and maps no name to
  an account.
- **Eligibility** = account enabled **and** Engineer role **and** flag set
  **and** a signature on file. Qualifications are optional (Neil signs
  without a qualification line until his are recorded).
- **Printed signatory name** is a field of this setting: one nullable
  column, **required when the flag is Yes**. There is no general account
  Name field.
- **Default sign-off Engineer** is a designation an Administrator sets on
  exactly one flagged account. [[CASE-040]]'s default rule reads it.

## Governing docs and design rules

FRD-04 requires an Administrator-only D31 setting, permanent action history,
and data-driven sign-off accounts. FRD-12 requires every drawn control to
have a handler and forbids inert controls. `docs/design/README.md` binds:

- no explanatory copy — labels, values, and only the existing permitted
  destructive consequence sentence;
- every new operator word lives in
  `src/Pegasus.Web/Presentation/OperatorLabels.cs` (shared lock, capacity
  one; minimal additive change);
- exact state labels;
- an unavailable capability is absent, never drawn disabled — so the
  Settings control and the sign-off fields are rendered **only** on Engineer
  rows;
- the existing dialog convention is reused unchanged: the `[data-dialog]` /
  `data-dialog-open` behaviour already in `wwwroot/js/site.js` (lines
  868–985). `Pages/Shared/_ReasonDialog.cshtml` is **not** reused — it owns a
  complete single-purpose form and exposes only hidden fields plus a reason,
  so it cannot carry a multipart file control or the sign-off fields; the
  Accounts-local form therefore reuses only the shared `data-dialog`
  behaviour. Neither `_ReasonDialog.cshtml` nor `site.js` is edited.

## Data and Core contract

Extend `PegasusIdentityUser : IdentityUser<Guid>`
(`Persistence/PegasusDbContext.cs:1022`; entity configuration at line 185).
ASP.NET Identity maps it to `AspNetUsers`. New columns on that table:

- `IsSignOffEngineer`, non-null, default `false`;
- `SignOffPrintedName`, nullable bounded text (required when the flag is set,
  enforced in Core);
- `SignOffQualifications`, nullable bounded text;
- `SignOffSignature`, nullable bytes;
- `SignOffSignatureDigest`, nullable bounded text (lowercase hex SHA-256 of
  the stored bytes; used for replay comparison and history, never the bytes);
- `IsDefaultSignOffEngineer`, non-null, default `false`.

No `SignOffSignatureContentType` column: `SignOffSignaturePolicy` permits
`image/png` and nothing else, so a stored media type would be a second copy
of that one list. The profile exposes the media type as the policy constant.

One account attribute set needs no new table, document store, custody path,
blob store, migration stream, or runtime unit. The migration creates no
table, so it adds no `GRANT` (see the corrected grant premise above);
`Test-MigrationGrants.ps1` enforces grants only for created tables.

Core additions in `src/Pegasus.Core/Identity/StaffAccountAdministration.cs`:

- `SignOffSignaturePolicy`: the sole source of upload validation and the sole
  owner of the permitted media type — `image/png` only, PNG signature bytes,
  at most 1 MiB (the largest brand asset is 80,989 bytes; case-document
  limits are not borrowed).
- A reasoned, idempotent sign-off update request/result/store/use case
  validated through a new `StaffAccountAdministrationPolicy.Normalize`
  overload. It rejects a set flag without a printed name, and rejects a
  default designation on an account that is not eligible.
- `SignOffEngineerProfile`: `StaffId`, `PrintedName`, `Qualifications`,
  `Signature`, `SignatureContentType` (the policy constant), `IsDefault`.
  This is the shape [[DOCS-017]]'s "Contract for PLAT-068 and CASE-040"
  asks for.
- `IStaffAccountQueries.ListSignOffEngineersAsync(CancellationToken)` and
  `IStaffAccountQueries.GetSignOffEngineerAsync(Guid staffId,
  CancellationToken)` returning `SignOffEngineerProfile?` for an eligible
  account and `null` otherwise. [[CASE-040]] lists for its selector and
  applies its own default rule from `IsDefault`; the report projection source
  resolves the selected account by ID. Both are added to the interface
  explicitly — no default interface implementation.
- `SignOffEngineerEligibility.IsEligible` — the one Core rule (enabled +
  Engineer role + flag + signature on file) used by the EF query, the
  default-designation check and the enforcement path. Qualifications never
  affect eligibility.

`StaffAccountSummary` gains one non-positional `SignOff` state property
(flag, printed name, qualifications, `HasSignature`, `IsDefault` — **never**
the signature bytes), so the four Core test files that construct it
positionally keep compiling.

**Interface breadth (review finding 4).** Adding two members to
`IStaffAccountQueries` breaks every existing implementation. Eight fakes in
seven test files implement it and must each gain the members:
`tests/Pegasus.Core.Tests/Identity/ActorDisplayNamesTests.cs:82`,
`tests/Pegasus.Core.Tests/Identity/IdentityUseCaseTests.cs:105`,
`tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs:889` and `:901`,
`tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs:510`,
`tests/Pegasus.Core.Tests/Reports/EngineerActivityReportTests.cs:126`,
`tests/Pegasus.Core.Tests/Triage/GetTriageDisplayNameTests.cs:162`,
`tests/Pegasus.Core.Tests/Workflow/CaseEditAuthorityTests.cs:271`.

## Ordered steps

### Step 1 — Core sign-off contract, eligibility rule, signature policy

Files:

- `src/Pegasus.Core/Identity/StaffAccountAdministration.cs`
- `tests/Pegasus.Core.Tests/Identity/IdentityUseCaseTests.cs`
- the seven fake-bearing test files listed above (interface members only)

Reuses `StaffAccountAdministrationPolicy.Normalize` (Administrator, reason,
operation key), its `RequireAdministrator` / `NormalizeRequiredText` helpers,
and the `RecordingStaffStore` / `RecordingQueries` fakes in
`IdentityUseCaseTests`. `CaseEngineerEligibility` is the shape the new
eligibility rule follows; it is not extended, because sign-off eligibility
adds the flag and the signature and belongs beside the staff-account
contract.

Add the request, result, store, use case, error outcome, summary state,
`SignOffSignaturePolicy`, `SignOffEngineerProfile` and
`SignOffEngineerEligibility`. The normalizer validates the flag, the printed
name (required when flagged), bounded qualifications, an optional PNG, the
default designation, and the required reason and operation key. A sign-off
update for a non-Engineer account, and a default designation on an account
that is not eligible, are both rejected through the one Core rule.

Acceptance: only an Administrator can invoke the update; reason and
operation key are normalized and forwarded; a set flag with no printed name
is rejected; non-PNG, invalid-PNG, empty and oversized signatures are
rejected; qualifications may be absent; an enabled flagged Engineer **with a
signature** is eligible and disabled, non-Engineer, unflagged and
signature-less accounts are not; a default designation on an ineligible
account is rejected.

### Step 2 — Persist, audit, query, register; the single migration

Files:

- `src/Pegasus.Infrastructure/Persistence/EfStaffAccountAdministration.cs`
- `src/Pegasus.Infrastructure/Persistence/EfStaffAccountQueries.cs`
- `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`
- `src/Pegasus.Infrastructure/DependencyInjection.cs`
- `src/Pegasus.Infrastructure/Persistence/Migrations/*_StaffAccountSignOff.cs`
  and `*_StaffAccountSignOff.Designer.cs`
- `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
  (shared lock, capacity one)
- `tests/Pegasus.IntegrationTests/StaffAccountsAndRolesWebTests.cs`

Reuses `EfStaffAccountAdministration`'s serializable transaction, replay
check, `AddHistory` and snapshot pattern; `EfStaffAccountQueries.Summary`
and `ParseRole`; the staff-account registrations at
`DependencyInjection.cs:167-185`.

Persist the columns with text limits. Add exactly one EF migration after
`20260829212237_GrantProviderSubmissionAcceptRecovery`, no `GRANT`. It also
creates a **filtered unique index** on `IsDefaultSignOffEngineer` (unique
where the column is `1`) so "exactly one default" is a database invariant,
not only an application check.

**Default designation lifecycle.** Setting the designation on account B
clears it from the current holder in the same serializable transaction, so
the transfer is atomic and the filtered index can never be violated; the
before/after snapshot records the previous and new default account IDs. The
initial state is no default at all — Core reports no default and
[[CASE-040]] falls back to its own rule; nothing fails closed on the
Accounts page. A designated account that is later disabled, loses the
Engineer role, is unflagged, or has its signature removed **retains** the
designation but stops being eligible through the one Core rule, and the
Accounts column shows that state. Disable and role changes are not blocked
by the designation — that would be a new cross-cutting rule D31 does not
state.

Event kind `staff_account_sign_off_updated`; before/after snapshots record
the flag, printed name, qualifications, signature presence, the signature
**digest**, and the previous/new default IDs — never the bytes. The replay
check compares the digest as well as the other fields, so the same operation
key with a different image is a conflict rather than a silent no-op.
Implement `ListSignOffEngineersAsync` and `GetSignOffEngineerAsync` in the
existing EF query adapter, applying the Core eligibility function once.

If another migration lands first: `git merge --no-edit origin/dev`,
regenerate this one migration after the new tail; never a second migration.

Acceptance: a successful update is persisted and replay-safe; an identical
replay is a no-op and the same key with a different image is rejected; the
action log carries actor, time, reason, event kind and before/after state
with no bytes; a default transfer clears the previous holder atomically and
the filtered unique index rejects a second default; role removal makes a
flagged account ineligible with no second eligibility implementation; both
seam members are reachable through `IStaffAccountQueries`;
`./scripts/Test-MigrationGrants.ps1` passes.

### Step 3 — Accounts settings surface, table state, labels

Files:

- `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml`
- `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml.cs`
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` (shared lock, capacity
  one; additive constants in `StaffAccounts` only)
- `tests/Pegasus.IntegrationTests/StaffAccountsAndRolesWebTests.cs`

Reuses `IndexModel.RunAsync`, `Validate`, `NewOperationKey`, the existing
`[Authorize(Policy = StaffRoleNames.Administrator)]` on `IndexModel`
(`Index.cshtml.cs:27`), and the shared `data-dialog` behaviour unchanged. The
`Edit` route is not extended (it is exercised only by Test UI focused
rendering).

Labels added to `OperatorLabels.StaffAccounts`: Sign-off Engineer, Yes, No,
Printed name, Qualifications, Signature image, On file, Not on file, Upload
signature, Replace signature, Default sign-off Engineer, Settings, Save,
Cancel, `Signature missing`, `Yes · qualifications missing`, `Yes · default`,
`Yes · not eligible`, and the printed-name-required validation message. No
operator-facing string is written in the page.

Add a Settings control **on Engineer rows only** — a non-Engineer account
has no sign-off capability, so the control is absent, not disabled and not
an empty dialog. It opens an Accounts-local `data-dialog` form posting
multipart to `OnPostSignOffAsync` with: the sign-off Yes/No selector,
printed name, qualifications, an `image/png` file control, the signature
state On file / Not on file, the Default sign-off Engineer control, a
required reason, Cancel and Save. **All sign-off fields are rendered
server-side for every Engineer row; nothing is shown or hidden by script,
and there is no save-disabled-until-changed behaviour** — the deployed CSP
discards inline scripts, so such a control would be inert in Production
(FRD-12 forbids that), and adding the behaviour to `site.js` is outside this
ticket's paths. Core rejects a flagged update with no printed name and the
page renders that error through the existing `_ErrorSummary`; native
`required` attributes cover the reason. No password-reset control
([[PLAT-064]]). The image is never served, previewed or linked. No change to
`Pages/Shared/*`, `Pages/Administration/Shared/*`, `site.css`, `site.js`.

Sign-off Engineer table column, exact states: non-Engineer `—`; Engineer not
flagged `No`; flagged, no signature `Signature missing`; flagged with
signature, no qualifications `Yes · qualifications missing`; flagged,
eligible and holding the designation `Yes · default`; flagged but not
eligible while holding the designation `Yes · not eligible`; otherwise
`Yes`.

Acceptance: route and handler stay Administrator-only (non-Administrator
denied); Save posts to `OnPostSignOffAsync`, requires a reason, and a
flagged save without a printed name is refused; every visible word comes
from `OperatorLabels`; the column shows every state with no explanatory
copy; non-Engineers get no Settings control; the page contains no inline
script; the integration test proves the reasoned multipart update, persisted
state, the default transfer, and the Action History entry.

### Step 4 — Test UI snapshot and bounded verification

Files: `docs/design/test-ui/pages/administration-accounts--default.html`
(shared lock, capacity one).

Reuses the existing Accounts catalogue entry (`catalogue.json:105-118`).
Regenerate the populated Accounts snapshot after the routed page change.
`catalogue.json` stays unchanged: the dialog-open state is client-side and
has no distinct server route. Fixture values may follow D43.

Acceptance: every command below exits 0; the regenerated snapshot shows the
column and the Settings control on Engineer rows only; results and exit
codes are recorded in the post-implementation report.

## Cross-lane contract

[[DOCS-017]] owns `ReportSignatory` on `AssessmentReportProjectionInput` and
the renderer; [[CASE-040]] owns the Case `SignOffEngineerId`, its selector,
the default rule and the projection-source wiring that fills
`AssessmentReportProjectionInput.Signatory` from `GetSignOffEngineerAsync`.
PLAT-068 supplies exactly the profile shape DOCS-017's contract names and
touches none of their files. PLAT-068's own production caller is the
Accounts page; the query seam's production caller lands in [[CASE-040]]. The
ticket's "Renderer reads the sign-off tuple" verification is therefore
satisfied by the merged CASE-040 + DOCS-017 integration, and PLAT-068 claims
only that the seam exists and returns the tuple — it must not be reported as
renderer delivery.

## Do not modify

Every path in `files.md` §"Must not touch" — the DOCS-017 renderer and
projection files, and the CASE-040 case/EVA files and their snapshots — plus
anything under `src/Pegasus.Core/AiWork/`, `src/Pegasus.Core/Assessment/`,
`src/Pegasus.Core/Reports/` or `src/Pegasus.Infrastructure/Reports/`
(ENG-035, DOCS-017, AUTO-018). No `Pages/Shared/*`, `site.js`, `site.css`,
or a second migration.

## Verification commands

```powershell
./scripts/Test-MigrationGrants.ps1
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

## Stop condition

Stop when the scoped implementation is committed, its PR to `dev` is open
with `Kanmer: PLAT-068`, and the ticket is in Review. Do not merge, start
[[CASE-040]] or [[DOCS-017]], or implement [[PLAT-064]].

## Simplification pass (2026-09-02)

To be recorded by the implementer before the PR opens.

## Wrapper checks (Claude, research checkout at the same SHA)

Confirmed by independent read: `PegasusIdentityUser` at
`PegasusDbContext.cs:1022` and its entity configuration at line 185;
`site.js` `[data-dialog]` binding at 868–985 and the CSP notes at 4–7 and
875; `catalogue.json` Accounts entry at 105–118; `RecordingStaffStore` and
`RecordingQueries` fakes in `IdentityUseCaseTests.cs`;
`StaffAccountsAndRolesWebTests.cs` with its non-Administrator denial test;
`[Authorize(Policy = StaffRoleNames.Administrator)]` at
`Accounts/Index.cshtml.cs:27`; `RunAsync`/`Validate`/`NewOperationKey`;
staff-account registrations at `DependencyInjection.cs:167-185`; migration
tail `20260829212237`; the eight `IStaffAccountQueries` fakes; the
`WebGrants` / `WorkerGrants` / `PreviousWebGrants` ranges; FRD-04 §Staff
accounts, FRD-01 §Sign-off Engineer and FRD-11's signatory paragraphs;
DOCS-017's "Contract for PLAT-068 and CASE-040". No Codex claim was dropped.

## Resolutions (2026-09-03)

- Operator: signatures are uploaded by an Administrator through the new
  control; no migration seed.
- Controller: eligibility = enabled + Engineer role + flag + signature on
  file; qualifications optional.
- Controller: the setting carries the printed signatory name (nullable
  column in the same migration, required when the flag is Yes).
- Operator (scope addition): the setting also carries one "Default sign-off
  Engineer" designation an Administrator sets on exactly one flagged
  account; CASE-040 reads it. Added to the migration, the Core profile and
  the Accounts page in this ticket.

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

Verdict received: REQUEST CHANGES. The reviewer confirmed the file set is
inside PLAT-068's paths and disjoint from ENG-035, DOCS-017 and AUTO-018,
that no package is added, and that nothing in the plan assumes a staff
review flag (D44), a damage type (D45) or crop behaviour (D46).

| # | Severity | Step | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | Steps 1–3, checklist | The ordered steps were never reconciled with the 2026-09-03 resolutions: four columns, `DisplayName` from `UserName`, eligibility without the signature, no printed name, no default designation. | **Fixed.** Added the "Resolved model" section and rewrote the data contract, all four steps, acceptance checks, labels, history snapshot and checklist around it. |
| 2 | blocker | Steps 1–2 | "Exactly one default" had no invariant or lifecycle. | **Fixed.** Core rejects a designation on an ineligible account; the migration adds a filtered unique index; a transfer clears the previous holder in the same serializable transaction; initial no-default and later-ineligible behaviour are stated. **Partly rejected:** blocking disable or role removal until a replacement is chosen is a new cross-cutting rule D31 does not state — the designation is retained, the account becomes ineligible, and CASE-040's default rule covers the gap. |
| 3 | blocker | Step 3, verification | The Accounts-local inline script is silently discarded by the deployed CSP (`site.js:4-7`, `:875`), so save-disabled-until-changed and script-toggled fields would be inert in Production (FRD-12). The test filter also excluded `Category!=Browser`, which is not the canonical rail. | **Fixed; independently verified.** All sign-off fields are now server-rendered for Engineer rows, the changed-state scripting is dropped as unrequired scope, no inline script is added, `site.js` is untouched, and the canonical `--filter "Category!=Corpus"` is restored. |
| 4 | blocker | Steps 1–2, file map | Adding members to `IStaffAccountQueries` breaks eight fakes in seven test files; the plan named only `IdentityUseCaseTests`. | **Fixed; independently verified** (grep confirms eight implementations). All seven files are named in the contract section and the checklist; no default interface implementation is used. |
| 5 | blocker | Objective, Step 2 | DOCS-017's contract requires the profile **by selected account ID**; the plan exposed only a list, and no lane was named for the projection-source wiring. | **Fixed.** `GetSignOffEngineerAsync(Guid, CancellationToken)` added to the seam, and a "Cross-lane contract" section assigns the projection-source wiring to CASE-040 and states that PLAT-068 must not claim renderer delivery. |
| 6 | should-fix | Step 2 replay | Presence-plus-content-type snapshots cannot distinguish two different PNGs under one operation key. | **Fixed.** A lowercase-hex SHA-256 `SignOffSignatureDigest` column is stored, compared in the replay check and recorded in history (never the bytes), with tests for identical replay and same-key/different-image conflict; previous/new default IDs are in the snapshot. |
| 7 | should-fix | Step 2 migration/grants | The grant premise was partly false: `WorkerGrants` has no `AspNetUsers` entry; the cited `…, DELETE` tuple is in `PreviousWebGrants`. | **Fixed; independently verified.** The premise now states the Web-only caller, the correct `WebGrants` tuple, and that a future Worker consumer adds its own grant. The conclusion is unchanged: no `GRANT` in this migration. |
| 8 | should-fix | Data contract, Step 3 | `SignOffSignatureContentType` duplicates the PNG-only policy; `_ReasonDialog` cannot carry this form; "Settings per account" conflicted with "non-Engineers get no control"; the label list was incomplete. | **Fixed.** The content-type column is dropped and the media type is the policy constant (one list per concept); the plan states that only the shared `data-dialog` behaviour is reused and why `_ReasonDialog` is not; Settings renders on Engineer rows only; printed name, default designation and validation labels are added to `OperatorLabels`. |
| 9 | should-fix | All steps, checklist | Execution-packet advisories: abbreviated paths, `<timestamp>` placeholders, no "Do not modify" boundary, checklist not mapped to steps. | **Partly fixed.** Exact repository-relative paths, the `*_StaffAccountSignOff.cs` glob and a "Do not modify" section are added, and every checklist line names its step. **Rejected:** recasting the plan into a labelled-field template — `files.md` already carries the boundary table, and the packet's field names are template ritual rather than content the implementer lacks. |

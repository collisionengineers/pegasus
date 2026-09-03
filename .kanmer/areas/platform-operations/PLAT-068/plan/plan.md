# Plan — PLAT-068 (2026-09-02, gpt-5.6-terra high)

Planned read-only at `897db953` (`origin/dev`) in the detached
`.worktrees/research` checkout; `git status --porcelain` empty before and
after. Board state (ticket, D29–D43, EPIC-011 context and waves, research,
files, open questions) was supplied verbatim by the Claude wrapper; Codex has
no Kanmer access. Prompt and raw output: scratchpad
`prep/PLAT-068/plan-prompt.md`, `plan-out.md`.

## Premise checks

- Read `AGENTS.md`, `CLAUDE.md`, the research and files documents, FRD-04,
  FRD-12, FRD-01 §Sign-off Engineer, and `docs/design/README.md`.
- Ran `rg`/`Get-Content` against `StaffAccountAdministration.cs`,
  `EfStaffAccountAdministration.cs`, `EfStaffAccountQueries.cs`,
  `PegasusDbContext.cs`, `DependencyInjection.cs`, the Accounts page and its
  tests, `OperatorLabels.cs`, the migration/grant convention, the Test UI
  catalogue, and the mockup.
- Migration tail is `20260829212237_GrantProviderSubmissionAcceptRecovery`;
  the new migration is `<timestamp>_StaffAccountSignOff`, immediately after.
- FRD-04 §Staff accounts (D31): the initial flagged accounts are
  A Patterson, N O'Reilly and E Mawdsley, held as application data, never
  hard-coded — which supports the no-seed default below.

## Objective

Give an Administrator a reasoned, permanently recorded Sign-off Engineer
account setting: flag, qualifications, and a stored PNG signature. Show the
setting's exact state on the Accounts table and expose one Core-owned
eligible sign-off profile seam for [[CASE-040]] and [[DOCS-017]].

Not in this ticket: selecting or defaulting a Case sign-off engineer
([[CASE-040]]), report rendering ([[DOCS-017]]), password reset
([[PLAT-064]]), an image-serving route, seeding production accounts, or any
other EPIC-012 lane's files.

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
- an unavailable capability is absent, never drawn disabled;
- the existing dialog convention is reused unchanged:
  `Pages/Shared/_ReasonDialog.cshtml` and the `[data-dialog]` /
  `data-dialog-open` binding in `wwwroot/js/site.js` (lines 868–985);
  neither file is edited.

## Data and Core contract

Extend `PegasusIdentityUser : IdentityUser<Guid>`
(`Persistence/PegasusDbContext.cs:1022`; entity configuration at line 185).
ASP.NET Identity maps it to `AspNetUsers`. Nullable columns on that table:

- `IsSignOffEngineer`, non-null, default `false`;
- `SignOffQualifications`, nullable bounded text;
- `SignOffSignature`, nullable bytes;
- `SignOffSignatureContentType`, nullable bounded text.

One account attribute needs no new table, document store, custody path,
blob store, migration stream, or runtime unit. The migration creates no
table, so it adds no `GRANT`: `20260729199000_RuntimeRoleReconciliation`
already gives the Web role `SELECT, INSERT, UPDATE` and the Worker role
`SELECT, INSERT, UPDATE, DELETE` on `AspNetUsers`, which covers new columns
("one migration with grants" is satisfied by those existing grants;
`Test-MigrationGrants.ps1` enforces grants only for created tables).

Core additions in `src/Pegasus.Core/Identity/StaffAccountAdministration.cs`:

- `SignOffSignaturePolicy`: the sole source of upload validation —
  `image/png` only, PNG signature bytes, at most 1 MiB (the largest brand
  asset is 80,989 bytes; case-document limits are not borrowed).
- A reasoned, idempotent sign-off update request/result/store/use case
  validated through `StaffAccountAdministrationPolicy.Normalize`.
- `SignOffEngineerProfile`: `StaffId`, `DisplayName`, `Qualifications`,
  `Signature`, `SignatureContentType`. Plan default: `DisplayName` is the
  account `UserName`, the same resolution `Core/Actors/ActorDisplayNames`
  already uses; no real-name mapping exists in the repository. Whether the
  printed signatory name ("A Patterson") is a field of this setting or a
  general account name is open question 3 — if the operator wants it on the
  setting, it is one more nullable column in the same single migration and
  one more dialog field, required when the flag is Yes.
- `IStaffAccountQueries.ListSignOffEngineersAsync(CancellationToken)`
  returning eligible `SignOffEngineerProfile` values. [[CASE-040]] consumes
  the list; [[DOCS-017]] resolves its selected account from the same list.
- `SignOffEngineerEligibility.IsEligible` — the one Core rule used by the EF
  query and the enforcement path. Plan default: enabled + Engineer role +
  flag; qualifications never affect eligibility; whether a stored signature
  is also required is open question 2 and changes only this function.

`StaffAccountSummary` gains a non-positional sign-off state property, so the
four Core test files that construct it positionally are untouched.

## Ordered steps

### Step 1 — Core sign-off contract, eligibility rule, signature policy

Files: `src/Pegasus.Core/Identity/StaffAccountAdministration.cs`,
`tests/Pegasus.Core.Tests/Identity/IdentityUseCaseTests.cs`.

Reuses `StaffAccountAdministrationPolicy.Normalize` (Administrator, reason,
operation key) and the `RecordingStaffStore` / `RecordingQueries` fakes in
`IdentityUseCaseTests`.

Add the request, result, store, use case, error outcome, summary state,
`SignOffSignaturePolicy`, and `SignOffEngineerEligibility`. The normalizer
validates the flag, bounded qualifications, required reason and operation
key, and an optional PNG. A sign-off update for a non-Engineer account is
rejected through the same Core eligibility policy the store uses.

Acceptance: only an Administrator can invoke the update; reason and
operation key are normalized and forwarded; non-PNG, invalid PNG, empty and
oversized signatures are rejected; qualifications may be absent; an enabled
flagged Engineer is eligible under the default and disabled, non-Engineer
and unflagged accounts are not.

### Step 2 — Persist, audit, query, register; the single migration

Files: `src/Pegasus.Infrastructure/Persistence/EfStaffAccountAdministration.cs`,
`.../EfStaffAccountQueries.cs`, `.../PegasusDbContext.cs`,
`src/Pegasus.Infrastructure/DependencyInjection.cs`,
`.../Migrations/<timestamp>_StaffAccountSignOff.cs` + `.Designer.cs`,
`.../Migrations/PegasusDbContextModelSnapshot.cs` (shared lock, capacity
one), `tests/Pegasus.IntegrationTests/StaffAccountsAndRolesWebTests.cs`.

Reuses `EfStaffAccountAdministration`'s serializable transaction, replay
check, `AddHistory` and snapshot pattern; `EfStaffAccountQueries.Summary`
and `ParseRole`; the staff-account registrations at
`DependencyInjection.cs:167-185`.

Persist the columns with text limits. Add exactly one EF migration after
`20260829212237_GrantProviderSubmissionAcceptRecovery`, no `GRANT`. Event
kind `staff_account_sign_off_updated`; before/after snapshots record the
flag, qualifications, signature presence and content type — never the
bytes. Sign-off data is retained when the Engineer role is later removed;
the account becomes ineligible through the one Core rule. Implement
`ListSignOffEngineersAsync` in the existing EF query adapter, applying the
Core eligibility function once.

If another migration lands first: `git merge --no-edit origin/dev`,
regenerate this one migration after the new tail; never a second migration.

Acceptance: a successful update is persisted and replay-safe; the action
log carries actor, time, reason, event kind and before/after state; role
removal makes a flagged account ineligible with no second eligibility
implementation; the profile seam is reachable through `IStaffAccountQueries`;
`./scripts/Test-MigrationGrants.ps1` passes.

### Step 3 — Accounts settings surface, table state, labels

Files: `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml`,
`Index.cshtml.cs`, `src/Pegasus.Web/Presentation/OperatorLabels.cs` (shared
lock, capacity one; additive constants in `StaffAccounts` only),
`tests/Pegasus.IntegrationTests/StaffAccountsAndRolesWebTests.cs`.

Reuses `IndexModel.RunAsync`, `Validate`, `NewOperationKey`, the existing
`[Authorize(Policy = StaffRoleNames.Administrator)]` on `IndexModel`, and
the shared dialog binding unchanged. The `Edit` route is not extended (it is
exercised only by Test UI focused rendering).

Labels added to `OperatorLabels.StaffAccounts`: Sign-off Engineer, Yes, No,
Qualifications, Signature image, On file, Not on file, Upload signature,
Replace signature, Settings, Save, `Signature missing`,
`Yes · qualifications missing`.

Add a Settings control per account opening an Accounts-local dialog/form
that posts multipart to `OnPostSignOffAsync`: the sign-off selector only
for Engineer accounts; qualifications and an `image/png` file control when
flagged; signature state On file / Not on file; required reason; cancel;
save. No password-reset control ([[PLAT-064]]). The image is never served,
previewed or linked. An Accounts-local inline script in `Index.cshtml`
records initial values and keeps Save disabled until a setting changes;
native required-field validation enforces the reason. No change to
`Pages/Shared/*`, `Pages/Administration/Shared/*`, `site.css`, `site.js`.

Sign-off Engineer table column, exact states: non-Engineer `—`; Engineer
not flagged `No`; flagged, no signature `Signature missing`; flagged with
signature, no qualifications `Yes · qualifications missing`; flagged with
signature and qualifications `Yes`.

Acceptance: route and handler stay Administrator-only (non-Administrator
denied); Save posts to `OnPostSignOffAsync`, requires a reason, is disabled
until changed; every visible word comes from `OperatorLabels`; the column
shows every state with no explanatory copy; non-Engineers get no sign-off
control; the integration test proves the reasoned multipart update,
persisted state, and the Action History entry.

### Step 4 — Test UI snapshot and bounded verification

Files: `docs/design/test-ui/pages/administration-accounts--default.html`
(shared lock, capacity one).

Reuses the existing Accounts catalogue entry (`catalogue.json:105-118`).
Regenerate the populated Accounts snapshot after the routed page change.
`catalogue.json` stays unchanged: the dialog-open state is client-side and
has no distinct server route. Fixture values may follow D43.

Acceptance: every command below exits 0; the regenerated snapshot shows the
column and Settings control; results and exit codes are recorded in the
post-implementation report.

## Verification commands

```powershell
./scripts/Test-MigrationGrants.ps1
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
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
`_ReasonDialog.cshtml` header and `site.js` `[data-dialog]` binding at
868–985; `catalogue.json` Accounts entry at 105–118; `RecordingStaffStore`
and `RecordingQueries` fakes in `IdentityUseCaseTests.cs`;
`StaffAccountsAndRolesWebTests.cs` with its non-Administrator denial test;
`[Authorize(Policy = StaffRoleNames.Administrator)]` at
`Accounts/Index.cshtml.cs:27`; `RunAsync`/`Validate`/`NewOperationKey`;
staff-account registrations at `DependencyInjection.cs:167-185`; migration
tail `20260829212237`; `ActorDisplayNames.ResolveStaffNamesAsync` returns
`UserName`. Added by the wrapper: the FRD-04 application-data premise, the
printed-name gap (open question 3), the Settings label, and the Edit-route
note. No Codex claim was dropped.

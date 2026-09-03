# Checklist — PLAT-068 (2026-09-02; revised 2026-09-03 after plan review)

- [ ] Step 1a — Core: add `SignOffSignaturePolicy` (PNG only, magic bytes,
  1 MiB, and the sole `image/png` media-type constant) and
  `SignOffEngineerEligibility.IsEligible` (enabled + Engineer role + flag +
  signature on file; qualifications never affect eligibility) to
  `src/Pegasus.Core/Identity/StaffAccountAdministration.cs`.
- [ ] Step 1b — Core: add the reasoned, idempotent sign-off update
  request/result/store/use case through a new
  `StaffAccountAdministrationPolicy.Normalize` overload that rejects a set
  flag with no printed name and a default designation on an ineligible
  account; `SignOffEngineerProfile` (`StaffId`, `PrintedName`,
  `Qualifications`, `Signature`, `SignatureContentType` from the policy,
  `IsDefault`); `IStaffAccountQueries.ListSignOffEngineersAsync` **and**
  `GetSignOffEngineerAsync(Guid, CancellationToken)`; a non-positional
  `SignOff` state on `StaffAccountSummary` carrying no signature bytes.
- [ ] Step 1c — add the two new interface members to all eight fakes in the
  seven test files: `Identity/ActorDisplayNamesTests.cs:82`,
  `Identity/IdentityUseCaseTests.cs:105`, `Intake/RetainedMailTests.cs:889`
  and `:901`, `Operations/DashboardBoundaryTests.cs:510`,
  `Reports/EngineerActivityReportTests.cs:126`,
  `Triage/GetTriageDisplayNameTests.cs:162`,
  `Workflow/CaseEditAuthorityTests.cs:271`. No default interface
  implementation.
- [ ] Step 1d — `tests/Pegasus.Core.Tests/Identity/IdentityUseCaseTests.cs`:
  Administrator-only; reason/operation-key forwarding; flagged-without-
  printed-name rejected; non-PNG, invalid-PNG, empty and oversized
  signatures rejected; absent qualifications allowed; eligibility matrix
  (enabled / role / flag / signature); default designation on an ineligible
  account rejected.
- [ ] Step 2a — `PegasusDbContext.cs`: six `PegasusIdentityUser` properties
  (`IsSignOffEngineer`, `SignOffPrintedName`, `SignOffQualifications`,
  `SignOffSignature`, `SignOffSignatureDigest`,
  `IsDefaultSignOffEngineer`) with text limits. No content-type column.
- [ ] Step 2b — exactly one migration
  `src/Pegasus.Infrastructure/Persistence/Migrations/*_StaffAccountSignOff.cs`
  (+ `.Designer.cs` + model snapshot) serialized after
  `20260829212237_GrantProviderSubmissionAcceptRecovery`; adds the filtered
  unique index on `IsDefaultSignOffEngineer`; no `GRANT` (the Web role's
  existing `AspNetUsers` table grant covers the new columns and the Worker
  has no `AspNetUsers` grant and no caller here).
- [ ] Step 2c — `EfStaffAccountAdministration.cs`: serializable, replay-safe
  update with `AddHistory` event `staff_account_sign_off_updated`;
  before/after snapshot carries flag, printed name, qualifications,
  signature presence, signature digest and previous/new default IDs, never
  the bytes; the replay check compares the digest; a default transfer clears
  the previous holder in the same transaction; role removal retains the
  data and the designation.
- [ ] Step 2d — `EfStaffAccountQueries.cs`: project the sign-off state into
  `Summary`; implement `ListSignOffEngineersAsync` and
  `GetSignOffEngineerAsync` applying the one Core eligibility rule; register
  the store/use case beside `DependencyInjection.cs:167-185`.
- [ ] Step 2e — `tests/Pegasus.IntegrationTests/StaffAccountsAndRolesWebTests.cs`:
  identical replay is a no-op; the same operation key with a different image
  is rejected; a second default is impossible; a default transfer clears the
  previous holder.
- [ ] Step 3a — `Presentation/OperatorLabels.cs` (shared lock): additive
  `StaffAccounts` constants only — Sign-off Engineer, Yes, No, Printed name,
  Qualifications, Signature image, On file, Not on file, Upload signature,
  Replace signature, Default sign-off Engineer, Settings, Save, Cancel,
  `Signature missing`, `Yes · qualifications missing`, `Yes · default`,
  `Yes · not eligible`, printed-name-required message.
- [ ] Step 3b — `Accounts/Index.cshtml.cs`: `OnPostSignOffAsync` multipart
  handler via `RunAsync`/`Validate`/`NewOperationKey`; page stays
  `[Authorize(Policy = Administrator)]`.
- [ ] Step 3c — `Accounts/Index.cshtml`: Sign-off Engineer column with the
  seven exact states; a Settings control **on Engineer rows only** opening
  an Accounts-local `data-dialog` form (Yes/No selector, printed name,
  qualifications, `image/png` file control, On file / Not on file, Default
  sign-off Engineer, required reason, Cancel, Save); every sign-off field
  server-rendered; **no inline script and no save-disabled-until-changed**
  (the deployed CSP discards inline scripts); no explanatory copy; no image
  served or linked; no shared partial, CSS or `site.js` edit.
- [ ] Step 3d — `tests/Pegasus.IntegrationTests/StaffAccountsAndRolesWebTests.cs`:
  reasoned multipart update persists; Action History entry present;
  non-Administrator denied; non-Engineer receives no Settings control; a
  flagged save with no printed name is refused; the page renders no inline
  script.
- [ ] Step 4 — regenerate
  `docs/design/test-ui/pages/administration-accounts--default.html` (shared
  lock); `catalogue.json` unchanged.
- [ ] `./scripts/Test-MigrationGrants.ps1`
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode`
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"`
- [ ] `./scripts/Update-TestUiSnapshots.ps1`
- [ ] `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`
- [ ] `./scripts/Test-UiCatalogue.ps1`
- [ ] Simplification pass recorded in `plan/plan.md` under the dated heading
  with dispositions.
- [ ] post-implementation report written; it claims the sign-off seam only,
  never renderer delivery (that lands with CASE-040 + DOCS-017).
- [ ] PR opened with Kanmer: PLAT-068

# Checklist — PLAT-068 (2026-09-02)

- [ ] Open questions 1–3 ticked or parked with a reason; the chosen answers
  (seed vs upload, signature required for eligibility, printed name) are
  reflected in the plan defaults before any code is written.
- [ ] Step 1a — Core: add `SignOffSignaturePolicy` (PNG only, magic bytes,
  1 MiB) and `SignOffEngineerEligibility.IsEligible` to
  `src/Pegasus.Core/Identity/StaffAccountAdministration.cs`.
- [ ] Step 1b — Core: add the reasoned, idempotent sign-off update
  request/result/store/use case through
  `StaffAccountAdministrationPolicy.Normalize`; `SignOffEngineerProfile`;
  `IStaffAccountQueries.ListSignOffEngineersAsync`; non-positional sign-off
  state on `StaffAccountSummary`.
- [ ] Step 1c — `tests/Pegasus.Core.Tests/Identity/IdentityUseCaseTests.cs`:
  Administrator-only, reason/operation-key forwarding, PNG rejection cases,
  absent qualifications allowed, eligibility matrix (enabled/role/flag).
- [ ] Step 2a — `PegasusDbContext.cs`: four `PegasusIdentityUser` properties
  with text limits.
- [ ] Step 2b — exactly one migration `<timestamp>_StaffAccountSignOff`
  (+ Designer + model snapshot) serialized after
  `20260829212237_GrantProviderSubmissionAcceptRecovery`; no `GRANT`
  (existing `AspNetUsers` table grants cover the columns).
- [ ] Step 2c — `EfStaffAccountAdministration.cs`: serializable, replay-safe
  update with `AddHistory` event `staff_account_sign_off_updated`
  (before/after without signature bytes); role removal retains data.
- [ ] Step 2d — `EfStaffAccountQueries.cs`: project sign-off state into
  `Summary`; implement `ListSignOffEngineersAsync` applying the one Core
  eligibility rule; register store/use case beside
  `DependencyInjection.cs:167-185`.
- [ ] Step 3a — `Presentation/OperatorLabels.cs` (shared lock): additive
  `StaffAccounts` constants only — Sign-off Engineer, Yes, No,
  Qualifications, Signature image, On file, Not on file, Upload signature,
  Replace signature, Settings, Save, `Signature missing`,
  `Yes · qualifications missing`.
- [ ] Step 3b — `Accounts/Index.cshtml.cs`: `OnPostSignOffAsync` multipart
  handler via `RunAsync`/`Validate`/`NewOperationKey`; page stays
  `[Authorize(Policy = Administrator)]`.
- [ ] Step 3c — `Accounts/Index.cshtml`: Sign-off Engineer column with the
  five exact states; per-account Settings control opening an Accounts-local
  `data-dialog` form (selector for Engineer accounts only; qualifications +
  `image/png` file when flagged; On file / Not on file; required reason;
  Cancel; Save); inline script keeps Save disabled until changed; no
  explanatory copy; no image served or linked; no shared partial/CSS/JS edit.
- [ ] Step 3d — `tests/Pegasus.IntegrationTests/StaffAccountsAndRolesWebTests.cs`:
  reasoned multipart update persists, Action History entry present,
  non-Administrator denied, non-Engineer receives no sign-off control.
- [ ] Step 4 — regenerate
  `docs/design/test-ui/pages/administration-accounts--default.html` (shared
  lock); `catalogue.json` unchanged.
- [ ] `./scripts/Test-MigrationGrants.ps1`
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode`
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`
- [ ] `./scripts/Update-TestUiSnapshots.ps1`
- [ ] `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`
- [ ] `./scripts/Test-UiCatalogue.ps1`
- [ ] Simplification pass recorded in `plan/plan.md` under the dated
  heading with dispositions.
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: PLAT-068

## Scope decisions

- **Reason and State stay on the add form.** The operator's verbatim complaint ("address +
  route scope, nothing else") targets the internal identifiers, not these two fields:
  `Reason` is required Core audit metadata (`UpdateApprovedMailbox` throws without it) and
  removing it would break "Keep OnPostUpdateAsync semantics intact"; `State` lets an
  administrator stage a mailbox Disabled before go-live, an existing capability the ticket
  never asked to remove.
- **The "Version" column is also removed from the table**, beyond the ticket's literal
  identifier list: `docs/design/README.md:161` explicitly bans "lease/version" operator
  copy, and the ticket's own verification requires the page to pass the banned-terms rule.
  The underlying `ExpectedVersion` optimistic-concurrency value stays as a hidden form
  field — that is data plumbing, not operator copy.
- **A legacy row with no bound identity cannot be moved to Approved through the edit form
  any more** (there is no field left to add an identity to it). This is an accepted
  consequence of removing the identity inputs entirely per the ticket; production's only
  current row (`instructions@collisionengineers.co.uk`) already has both identities bound,
  so this has no live impact today.

## Implementation

1. **Core seam** (`ApprovedMailboxAdministration.cs`): add `ApprovedMailboxIdentityResolution`
   + `IResolveApprovedMailboxIdentity`, reusing the existing record/interface conventions in
   the same file. No change to `UpdateApprovedMailbox`/`EfApprovedMailboxStore` policy.
2. **Production adapter** (`GraphApprovedSources.cs`): `GraphApprovedMailboxResolver`, reusing
   `TokenCredential`/`HttpClient`/`JsonDocument` patterns already in this file
   (`GraphMailClient`). Extracted `GraphApprovedMailboxOptions.ParseBaseUri` so the existing
   base-URI validation is not duplicated for a caller that has no fixed mailbox to configure.
3. **DevelopmentOffline fake** (`LocalApprovedMailboxIdentityResolver.cs`): deterministic,
   always succeeds — DevelopmentOffline has no tenant to fail against; a test that needs the
   honest-failure path substitutes its own resolver via `IntakeWebApplicationFactory`'s
   existing override convention (`artifactStore`/`extractionPolicy`/…), extended with
   `approvedMailboxIdentityResolver`.
4. **Composition** (`DependencyInjection.cs`, `Program.cs`): a new
   `AddProductionApprovedMailboxResolver` beside `AddProductionExternalAdapters` (reused
   pattern, not reused registration — Web must not compose the Worker-only pollers). Web's
   `Program.cs` registers its own `TokenCredential` (previously only a local variable for
   Data Protection/Blob) and calls the new extension in the Production branch; the
   DevelopmentOffline branch registers the local fake beside the existing
   `VehicleLookupAvailability.DevelopmentOfflineReplay` registration.
5. **Page** (`Mailboxes.cshtml(.cs)`): remove the three identity `[BindProperty]`s and their
   inputs/labels from both forms; `OnPostUpdateAsync` now loads `Mailboxes` up front (needed
   so an existing row's own identity can be resent to `UpdateApprovedMailbox` without ever
   exposing it — see the `BindIdentity`/precheck note in `files.md`); a new mailbox
   (`ExpectedVersion == 0`) resolves identity from the normalized address before calling
   `UpdateApprovedMailbox`, failing closed with one honest sentence and creating no row on
   failure; copy reworded (narration, "Read-only route scope" → "Route scope", the version
   number dropped from the save-confirmation message).
6. **Infra**: `Graph:BaseUri` added to Web's Production required-keys check and to
   `platform.bicep`'s Web container settings (mirrors the Worker's existing value exactly).
   `docs/runbook.md` gets a short addendum: Web's own managed identity needs its own
   `User.Read.All`/`Mail.Read` grant — a real Azure AD write, explicitly out of scope for a
   lane (no cloud writes) and left as a documented manual step, same as the Worker's existing
   Mail.Read grant already is.
7. **Tests**: rewrote `ApprovedMailboxAdministrationWebTests.cs`'s four scenarios for the new
   flow (resolve-and-round-trip without leaking identity, resolution-failure fails closed,
   address-immutability-on-rebind, no-identifiers-or-duplicated-narration on a fresh load);
   added fake-backed `GraphApprovedMailboxResolverTests.cs` (HTTP `DelegateHandler`, matching
   `ProductionGraphSourceTests.cs`) and `LocalApprovedMailboxIdentityResolverTests.cs`
   (determinism). Existing Core-level `AdministrationPolicyTests.cs` (11 tests) and the EF
   store's own persistence tests were read and left untouched — they test `UpdateApprovedMailbox`
   / `EfApprovedMailboxStore` directly, whose behaviour did not change.

## Verification

- `dotnet build ./Pegasus.slnx -c Release --no-restore` — 0 warnings, 0 errors.
- `dotnet test --filter "FullyQualifiedName~ApprovedMailboxAdministrationWebTests"` — 4/4.
- `dotnet test --filter "FullyQualifiedName~GraphApprovedMailboxResolverTests|FullyQualifiedName~LocalApprovedMailboxIdentityResolverTests"` — 6/6.
- `dotnet test --filter "FullyQualifiedName~AdministrationPolicyTests|FullyQualifiedName~Identity"` (Core) — 61/61, unchanged.
- `dotnet test --filter "FullyQualifiedName~ApprovedMailboxEstateIntegrationTests|FullyQualifiedName~ApprovedMailboxIdentityMigrationTests|FullyQualifiedName~AdministrationPolicyPersistenceTests|FullyQualifiedName~AdministrationSearchAccountWebTests"` — 15/15, unchanged.
- `dotnet test Pegasus.ArchitectureTests` — 97/97, unchanged.
- `az bicep build --file infra/modules/platform.bicep --stdout` — compiles.
- `./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — passes.
- Browser/AccessibilityTests (`/Administration/Mailboxes`, Playwright-driven) — not run in
  this sandbox (no Playwright browser install available); the page keeps one `<h1>` via the
  shared `_PageHeader` partial (untouched) and every removed control was a `<label>`/`<input>`
  pair, not a landmark, so no structural a11y regression is expected, but this is not proven
  locally and should be spot-checked before merge if a browser runner is available in CI.

## Simplification pass (2026-08-20)

- Reused: `IntakeWebApplicationFactory`'s existing override convention (no new test
  infrastructure invented); `GraphMailClient`'s credential/HttpClient/JsonDocument pattern
  for the new resolver rather than a parallel HTTP stack; `ApprovedMailboxOptions`'s base-URI
  validation extracted once instead of copied.
- Considered and rejected: registering the resolver inside `AddProductionExternalAdapters`
  itself (would force Web to compose `PollApprovedInbox`/`PollSentEvidence`, a real Worker-only
  concern — kept the new extension separate instead, one list of "what Web composes", one of
  "what Worker composes").
- Considered and rejected: giving `MailboxesModel` a second constructor/parameter set for
  "resolve mode" — the existing `ExpectedVersion == 0` new-vs-edit branch already carries this
  distinction throughout the file, reused rather than duplicated.

- Browser/AccessibilityTests: playwright was in fact available locally. Ran
  `dotnet test --filter "FullyQualifiedName~AccessibilityTests"` — 24/24 passed, including
  `/Administration/Mailboxes` (no axe violations, exactly one `<h1>`, no inline styles).

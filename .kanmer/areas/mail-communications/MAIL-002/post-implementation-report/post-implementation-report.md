## What changed

`/Administration/Mailboxes` no longer displays or asks for `MailboxIdentity`,
`InboxFolderIdentity`, or `SentFolderIdentity` anywhere — not on the current-policies
table, not on the per-row update form, not on the add form — for any role. Adding an
address now resolves its Graph mailbox and well-known Inbox/Sent-items folder identities
server-side via a new `IResolveApprovedMailboxIdentity` port; resolution failure shows one
honest sentence ("The address could not be found in the mail system.") and creates no row
(fails closed). Editing an existing row (route scope/state/reason) resends that row's own
already-bound identity from the freshly loaded record, never from the client, so
`UpdateApprovedMailbox`'s existing missing-identity precheck keeps working invisibly.
The "Version" column/number was also dropped from operator-facing copy (banned
"lease/version" terminology per `docs/design/README.md:161`), and remaining narration was
reworded ("Read-only route scope" → "Route scope"; the save-confirmation message no longer
states a raw version integer).

## Root-cause note found along the way

`UpdateApprovedMailbox.ExecuteAsync`'s own missing-identity precheck treats a `null`
identity in the request as "missing" unconditionally when `State == Approved` — it has no
visibility into what the row already has in the database. The original page relied on this
by always resubmitting the saved identity value (readonly, but still part of the POST) on
every edit. Removing the identity inputs broke that implicitly, caught by the new
`RebindingAnEstablishedMailboxsAddressIsRefusedWithTheImmutabilityReason` test going red
with the wrong error ("This mailbox cannot be approved for that route scope yet." instead
of the immutability message) until `OnPostUpdateAsync` was changed to look the row's
current identity up itself and resend it.

## Files changed

See the ticket's `files` document for the full list; summary: new Core port
(`ApprovedMailboxAdministration.cs`), new production Graph adapter and DevelopmentOffline
fake (`GraphApprovedSources.cs`, `LocalApprovedMailboxIdentityResolver.cs`), new
independent DI composition (`DependencyInjection.cs`, `Program.cs`, `platform.bicep`), the
page itself (`Mailboxes.cshtml(.cs)`), a `docs/runbook.md` addendum, and rewritten/new
tests.

## Test evidence

- `dotnet build ./Pegasus.slnx -c Release --no-restore` — Build succeeded, 0 Warning(s), 0 Error(s).
- `dotnet test --filter "FullyQualifiedName~ApprovedMailboxAdministrationWebTests"` — 4/4 passed.
- `dotnet test --filter "FullyQualifiedName~GraphApprovedMailboxResolverTests|FullyQualifiedName~LocalApprovedMailboxIdentityResolverTests"` — 6/6 passed (new).
- `dotnet test --filter "FullyQualifiedName~AdministrationPolicyTests|FullyQualifiedName~Identity"` — 61/61 passed, unchanged.
- `dotnet test --filter "FullyQualifiedName~ApprovedMailboxEstateIntegrationTests|FullyQualifiedName~ApprovedMailboxIdentityMigrationTests|FullyQualifiedName~AdministrationPolicyPersistenceTests|FullyQualifiedName~AdministrationSearchAccountWebTests"` — 15/15 passed, unchanged.
- `dotnet test Pegasus.ArchitectureTests` — 97/97 passed, unchanged.
- `dotnet test --filter "FullyQualifiedName~AccessibilityTests"` (Playwright, includes `/Administration/Mailboxes`) — 24/24 passed.
- `az bicep build --file infra/modules/platform.bicep --stdout` — compiles.
- `./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — passed.

## Verification checklist (from the ticket)

- [x] No mailbox/folder ID appears anywhere on the page (view or form), for any role —
  confirmed by `ThePageNeverShowsMailboxOrFolderIdentifiersOrDuplicatedRunbookNarration`.
- [x] Adding an address by email alone produces a working approved mailbox (identity
  resolved backend-side); failures are stated honestly — confirmed by
  `AddingAnAddressResolvesItsIdentityWithoutExposingItOnThePage` and
  `AnAddressThatCannotBeResolvedIsRefusedWithoutCreatingARow`.
- [x] Copy passes the narration and banned-terms rules; browser + accessibility suites
  green — 24/24 AccessibilityTests passed.

## Deployment follow-up (explicitly not done here — Azure AD write, out of scope for a lane)

Production resolution requires Web's own managed identity (separate from the Worker's) to
hold `User.Read.All` + `Mail.Read` Graph application permissions with tenant admin
consent. Documented in `docs/runbook.md`'s "Runbook: admitting a new mailbox to the
tenant" as a new step 0. Until granted, every address resolution in Production fails
closed with the honest on-page message — no crash, no row created, no identifier ever
shown.

## Scope note

`docs/frd/frd-08-email-mailbox-and-background-processing.md` and
`docs/frd/frd-12-operator-experience.md` were checked; neither describes an
identifier-entry UI flow, so neither needed an update.

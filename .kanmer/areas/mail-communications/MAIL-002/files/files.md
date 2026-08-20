## Files touched

- `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs` — new `ApprovedMailboxIdentityResolution` record and `IResolveApprovedMailboxIdentity` port (Core owns the port; no policy logic changed).
- `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` — extracted `GraphApprovedMailboxOptions.ParseBaseUri` (shared with the resolver, was inline in `Create`); new `GraphApprovedMailboxResolver` (production Graph-backed implementation) alongside the existing `GraphApprovedInboxSource`/`GraphApprovedSentSource`.
- `src/Pegasus.Infrastructure/Email/LocalApprovedMailboxIdentityResolver.cs` (new) — DevelopmentOffline fake, deterministic per address.
- `src/Pegasus.Infrastructure/DependencyInjection.cs` — new `AddProductionApprovedMailboxResolver` extension, following the existing `AddProductionExternalAdapters` composition pattern (`DependencyInjection.cs:536-546`) but independent of it, since Web never composes the Worker-only pollers.
- `src/Pegasus.Web/Program.cs` — registers `TokenCredential`, calls `AddProductionApprovedMailboxResolver` in the Production branch; registers `LocalApprovedMailboxIdentityResolver` in the DevelopmentOffline branch; added `Graph:BaseUri` to the Production required-keys check.
- `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml(.cs)` — removed every mailbox/folder identifier input and display; new-mailbox add resolves identity server-side from the address; existing-row edits resend their own already-bound identity from the freshly loaded record (never from the client) so `UpdateApprovedMailbox`'s own missing-identity check still passes; reworded copy (narration, banned "version" terminology, mislabeled "Read-only route scope" legend).
- `infra/modules/platform.bicep` — added `Graph__BaseUri` to the Web container's app settings (Web never had any Graph composition before this ticket).
- `docs/runbook.md` — added a note to "Runbook: admitting a new mailbox to the tenant" that Web's own managed identity (separate from Worker's) additionally needs `User.Read.All` + `Mail.Read` for address resolution to work in Production; until granted, resolution fails closed with the honest on-page message.
- `tests/Pegasus.IntegrationTests/ApprovedMailboxAdministrationWebTests.cs` — rewritten for the new address-only add flow, resolution failure, and address-immutability-on-rebind scenarios; identifier-absence assertions.
- `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` — `IntakeWebApplicationFactory` gained an optional `approvedMailboxIdentityResolver` override, following the existing `artifactStore`/`extractionPolicy`/`mailClassificationPolicy` convention.
- `tests/Pegasus.IntegrationTests/GraphApprovedMailboxResolverTests.cs` (new) — fake-backed HTTP tests for the production resolver, following `ProductionGraphSourceTests.cs`'s `DelegateHandler`/`FixedCredential` convention.
- `tests/Pegasus.IntegrationTests/LocalApprovedMailboxIdentityResolverTests.cs` (new) — determinism tests for the DevelopmentOffline fake.

## Files read (context, not touched)

- `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs` (`UpdateApprovedMailbox`, `BindIdentity`, `ApprovedMailboxUpdateError`) — confirmed the missing-identity precheck runs before the store call and treats a null identity in the request as "missing" regardless of what the row already has, which is why an existing row's own identity must be resent server-side, never why an operator would type it.
- `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxStore.cs` — unchanged; `IsApprovedAsync`/`UpdateAsync` semantics preserved exactly.
- `docs/frd/frd-08-email-mailbox-and-background-processing.md` — checked; it documents allowlist/fresh-start/disable *policy*, not an identifier-entry UI flow, so no update needed here.
- `docs/design/README.md:151-168` — narration, banned-terms ("lease/version" included), and no-raw-identifier rules the copy/markup changes follow.
- Production ground truth (read-only, no writes): confirmed via `az functionapp config appsettings list` that the Worker's `Graph__BaseUri` is `https://graph.microsoft.com/v1.0/`, reused verbatim for Web's new setting.

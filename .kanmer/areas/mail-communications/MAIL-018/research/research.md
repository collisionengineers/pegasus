# MAIL-018 research

## Premises (all verified by read-only checks on origin/dev a9184315, 2026-08-27)

- `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs:17-28` — `ApprovedMailbox` already carries `DateTimeOffset? ActivatedAtUtc`; `ListApprovedMailboxes` returns it to the page. No new query is needed for activation time.
- `src/Pegasus.Core/Identity/ApprovedMailboxSubscriptions.cs` — `ApprovedMailboxSubscription(ApprovedMailboxId, SubscriptionId, Resource, ExpiresAtUtc, LifecycleState, LastMaintainedAtUtc, LastMaintenanceFailureCode)` and `IApprovedMailboxSubscriptionStore` (GetActive / ListMaintenanceCandidates / Save / RecordMaintenanceFailure). There is no list-all read. Enum `ApprovedMailboxSubscriptionLifecycleState { Active, Missed, Removed, ReauthorizationRequired }`.
- `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxSubscriptionStore.cs` — one row per `ApprovedMailboxId` (SaveAsync upserts by that key); private `Map(entity)` already exists. Registered scoped in `DependencyInjection.cs:256` for both hosts (Web already resolves it for the Graph webhook).
- Implementations of the port: EF store + one test fake (`tests/Pegasus.IntegrationTests/GraphMailWebhookTests.cs:225`). Extending the port costs one fake member.
- `scripts/Invoke-AzureDatabaseBootstrap.ps1:334` — `pegasus_web_runtime_role` already holds SELECT on `ApprovedMailboxSubscriptions` (DML denied). No grant/migration change.
- `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml(.cs)` — table columns Address / Route scope / State / Polling; `PollStatusFor` renders a sentence-style value from `IApprovedMailboxPollStatusQueries` ("Not yet polled." / "Not polled." when no row). `OperatorLabels.OfficeTime(DateTimeOffset?, absent)` is the existing absent-value convention ("Not scheduled", "Not recorded"); `OperatorLabels.Humanise` is the existing enum/code → label convention (`ReauthorizationRequired` → "Reauthorization required").
- `docs/design/README.md#no-explanatory-copy-and-page-economy` — labels and values only; no hint copy. Design README line 40-43: after changing a routed Razor page run `scripts/Update-TestUiSnapshots.ps1` then `-Verify`. `docs/design/test-ui/catalogue.json:156-168` lists `administration-mailboxes--default` ("One approved identity-bound mailbox before its first poll").
- `tests/Pegasus.IntegrationTests/ApprovedMailboxAdministrationWebTests.cs` — existing page tests; `ThePageNeverShowsMailboxOrFolderIdentifiers...` asserts "Not yet polled." is present. `TestMailboxId.From("instructions")` is the seeded mailbox id; `ApprovedMailboxEstateIntegrationTests` sets its identities/ActivatedAtUtc by SQL.
- FRD-08 "Mailbox wake-up and recovery": subscription row stores id, resource, expiry, lifecycle state, last maintenance result; "Failure is visible per mailbox". This ticket is the Web surface of that sentence.

## Decision inputs

- Reuse: extend `IApprovedMailboxSubscriptionStore` with `ListAsync` rather than adding a parallel `...SubscriptionQueries` port (search-before-build: the store is already the only owner of that table and is already injected into Web).
- Rendering follows the existing Polling column: one value string per mailbox from a PageModel method, absent case a plain value.

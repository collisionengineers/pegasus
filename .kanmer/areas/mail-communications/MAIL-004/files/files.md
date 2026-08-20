# Files — MAIL-004

## Where the smallest change lands

| Path | Why |
|---|---|
| New `src/Pegasus.Core/Intake/ApprovedOutlookCategories.cs` | Own the one global Active/Disabled catalogue, normalized display-name validation, list/update requests, Active-only resolver used by MAIL-13, replay/conflict result and Core use cases. Keep Outlook labels distinct from Pegasus `MailCategory` classification. |
| `src/Pegasus.Core/Identity/StaffAuthorization.cs` | Add one named Administrator-only `ManageApprovedOutlookCategories` right rather than mislabel category policy as mailbox administration. Reuse the existing management-right pattern; Automation and ordinary casework remain denied. |
| New `src/Pegasus.Infrastructure/Persistence/EfApprovedOutlookCategoryStore.cs` | One serializable versioned/idempotent store with case-insensitive duplicate prevention, Active-only resolution and permanent `ActionHistory`. Disable rather than DELETE. Do not mix categories into `EfApprovedMailboxStore`. |
| `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyEntities.cs` and `AdministrationPolicyModelConfiguration.cs` | Add one category entity/table definition and unique normalized-name constraint inside the existing administration-policy persistence boundary. No per-mailbox join or Graph id/color column. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Register the one catalogue set. |
| One migration plus `Migrations/PegasusDbContextModelSnapshot.cs` | Add the table/index and least Web `SELECT, INSERT, UPDATE` grants, with DELETE denied and no Worker grant unless a concrete Worker caller later appears. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Register one store for the admin use cases and MAIL-13 Active-only resolver, following the existing approved-mailbox registrations. |
| New `src/Pegasus.Web/Pages/Administration/MailCategories.cshtml(.cs)` | Dedicated narrow list/add/disable page using authenticated actor derivation, anti-forgery, expected version, operation key, reason, visible conflict/replay/failure and accessible forms. Show names/state only—no Graph id, color or mailbox mechanics. |
| `src/Pegasus.Web/Pages/Administration/Index.cshtml` | Add one “Outlook categories” administration card. Do not turn `/Administration/Mailboxes` into a generic email-rules page. |
| `tests/Pegasus.Core.Tests/Intake/ApprovedOutlookCategoryTests.cs` | Prove Administrator-only management, Active-only resolution, validation, normalized duplicate refusal, version/replay/conflict and no classification-taxonomy coupling. |
| `tests/Pegasus.IntegrationTests/ApprovedOutlookCategoryPersistenceTests.cs` | Prove serializable persistence, one row per normalized name, immutable history, disable/no DELETE, and concurrent/replay behavior. |
| `tests/Pegasus.IntegrationTests/ApprovedOutlookCategoryAdministrationWebTests.cs` | Prove real admin caller, non-Administrator denial, no raw Graph identifiers/colors, add/disable/conflict/replay behavior and accessible form state. Reuse `IntakeWebTestSupport` conventions. |
| `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` and accessibility/browser coverage | Prove exact runtime grants/DELETE denial and the new administration page's standard accessibility contract. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md`, `docs/frd/frd-12-operator-experience.md`, `docs/design/README.md`, and `docs/capabilities.md` | Canonicalize the narrow Administrator-owned allowlist and MAIL-13 consumption; explicitly say it is not a generic mailbox-rule editor or Outlook synchronization surface. Update capability evidence only after delivery. |

## Existing files to reuse, not duplicate

| Path | Reuse |
|---|---|
| `src/Pegasus.Core/Identity/ApprovedMailboxAdministration.cs` | Versioned/idempotent reasoned administration request/use-case shape. Do not add category fields or reuse mailbox route/state vocabulary. |
| `src/Pegasus.Infrastructure/Persistence/EfApprovedMailboxStore.cs` | Serializable operation-key replay, expected-version, ActionHistory and disable-not-delete convention. Reuse the pattern, not a generic administration repository. |
| `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml(.cs)` | MAIL-002's server-derived actor, honest errors, hidden concurrency plumbing and accessible add/update pattern. Leave this page focused on addresses/routes. |
| `tests/Pegasus.IntegrationTests/ApprovedMailboxAdministrationWebTests.cs` | Administration authorization, anti-forgery, identifier-hiding and test-factory conventions. |
| TICK-054 research/open questions | Concrete Active-only catalogue consumer and exact message-state action boundary. |
| EPIC-006 `context.md` | One Core owner; no local-alpha Outlook mutation. |

## Caller and overlap map

- **TICK-054 / MAIL-13 — hard consumer and blocked item:** consumes the Active-only resolver by internal catalogue id. Exact overlap in Core contracts, DI, MAIL-13 action tests, FRD-08/design/capabilities and eventual Web composition. MAIL-004 lands first; MAIL-13 then refreshes/rebases. Message mutation remains TICK-054 scope.
- **MAIL-002 — convention and near overlap, not a business dependency:** current `origin/dev` already contains its address-only administration work. Reuse patterns in `StaffAuthorization.cs`, DI, administration tests, migrations/grants and the admin index; do not edit its `Mailboxes.cshtml(.cs)` or Graph identity resolver.
- **TICK-053 / MAIL-11:** no implementation overlap. Its accepted search inputs are body/attachment filename/content plus mailbox/folder scope; no category predicate or retained category projection exists. Do not touch `RetainedMail.cs`, `EfRetainedMailboxMessageStore.cs`, mail search pages or indexes.
- **TICK-051/052 / MAIL-09/10:** no implementation overlap. Automatic association keys are unique VRM/exact thread; manual linking uses canonical Case search and reasoned association. Do not touch Case query/match/association stores or use Outlook categories as link evidence.
- **TICK-056 / UI-10:** downstream display of MAIL-13's action controls only; it does not own or edit the catalogue.
- **AUTO-003:** downstream caller of MAIL-13's Core action, never the catalogue-management surface. No `MailMcpTools.cs` change belongs here.

## External boundary

MAIL-004 itself performs no Graph/Azure/mailbox read or write and needs no Graph adapter. MAIL-13 may later validate the selected name against the exact mailbox master list using separately approved `MailboxSettings.Read`; creating/updating/deleting master categories would require `MailboxSettings.ReadWrite` and is explicitly excluded.

## Close/archive checkpoint

At take/plan time, re-check that TICK-054 still contains the configured-category action. If it does not, archive MAIL-004 without code: search, linking, Automation and administration alone do not establish an independent consumer.

## Out of scope

No Outlook master-category create/update/delete/synchronization; no stored Graph category id/color; no per-mailbox duplicate allowlist; no generic settings/rules framework; no category search/filter/index; no Case match/link evidence; no message PATCH; no MCP management tool; no deployment, permission or live mailbox write.

## Refresh — 2026-08-20

Current merged TICK-064 confirms reuse of `ApprovedMailboxAdministration.cs`, `EfApprovedMailboxStore.cs`, `AdministrationPolicyEntities.cs`, `AdministrationPolicyModelConfiguration.cs`, `DependencyInjection.cs`, the Administration index/page conventions, migration/runtime-grant matrix, and focused Core/relational/Web test shapes. MAIL-004 remains separate from mailbox folder bindings and edits no Graph or retained-mail files.

# Files — PLAT-028

## Where the change lands

| Path | Why |
| --- | --- |
| src/Pegasus.Core/Cases/CaseContracts.cs | Name-based atomic customer creation; known caller update only. |
| src/Pegasus.Core/Cases/OrganizationAdministration.cs | Principal paging/detail and creation normalization using current owner. |
| src/Pegasus.Infrastructure/Persistence/EfOrganizationAdministration.cs | Existing transaction creates customer identity and Principal; direct bounded principal queries. |
| src/Pegasus.Infrastructure/DependencyInjection.cs | Register the principal query use cases. |
| src/Pegasus.Web/Pages/Administration/Principals/** | Flat list, create customer, Settings including provider commands, same-customer code replacement. |
| src/Pegasus.Web/Pages/Administration/Organizations/** | Remove obsolete separate administration routes. |
| src/Pegasus.Web/Pages/Administration/Index.cshtml | Remove obsolete Organisations entry. |
| src/Pegasus.Web/Presentation/OperatorLabels.cs | One owner for labels. |
| tests/Pegasus.Core.Tests/Cases/OrganizationAdministrationTests.cs | Updated creation contract/normalization. |
| tests/Pegasus.IntegrationTests/OrganizationDirectoryWebTests.cs | Update existing create/settings routed caller; preserve independent location/EVA assertions. |
| tests/Pegasus.IntegrationTests/OrganizationAdministrationWebTests.cs | Actual customer create/settings/credential, authorization and replay journeys. |
| tests/Pegasus.IntegrationTests/OrganizationAdministrationPersistenceTests.cs | Atomic name/code uniqueness and replay. |
| tests/Pegasus.IntegrationTests/PrincipalCredentialPersistenceTests.cs | Updated existing creation caller. |
| tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs | Updated existing creation caller. |
| tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs | Remove retired route states if catalogue requires it. |
| docs/frd/frd-04-parties-accounts-and-access.md | Current single customer behavior, no hierarchy. |
| docs/design/README.md | Principal-only administration wording. |
| docs/design/test-ui/** | Root verifier owns scoped capture and catalogue update. |

## Context files

| Path | Constraint |
| --- | --- |
| src/Pegasus.Core/Cases/PrincipalCredentials.cs | Existing authorization, lifecycle, show-once and replay contracts. |
| src/Pegasus.Core/Cases/OrganizationDirectory.cs | Real repairer/storage/location directory remains independent. |
| src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml | Existing semantic dialog pattern and common script. |
| docs/frd/frd-09-provider-and-intermediary-routes.md | Existing provider API and route authority; unchanged. |
| docs/engineering.md | Existing testing and reuse rules. |

## Ripple effects

Existing callers follow the changed create request. Old URLs are removed, not retained as compatibility paths. Snapshot route records follow removals. No new package, table, migration or runtime.

## Out of scope

Live pegasustest creation, live credential issuance, sending mail, intake allocation, mail routing policies, report/Glass changes, unrelated administration and directory changes.

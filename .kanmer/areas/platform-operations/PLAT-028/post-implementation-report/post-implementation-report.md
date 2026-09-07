# Post-implementation report — PLAT-028

## Summary

Implemented one customer-facing Principals workflow in .worktrees/plat-028 on
PLAT-028-principal-customer, based on origin/dev
3da60bd0c270111d5168dc17246dc831882108ea. Source is frozen and uncommitted
pending the root verifier. This is an implementation claim, not post-merge
proof: no build, test, capture, live write or deployment was run by this worker.

## Changes

| File | Change and reason |
| --- | --- |
| docs/design/README.md | Flat customer list and existing Settings-page convention; no masked-secret retrieval claim. |
| docs/design/test-ui/catalogue.json | Principal Settings route metadata; removed retired page records. |
| docs/design/test-ui/pages/administration-organization-edit--default.html | Removed snapshot for retired routed page; recoverable in Git. |
| docs/design/test-ui/pages/administration-organizations--default.html | Removed snapshot for retired routed page; recoverable in Git. |
| docs/design/test-ui/pages/administration-principal-eva-submission--default.html | Removed snapshot for retired routed page; recoverable in Git. |
| docs/frd/frd-04-parties-accounts-and-access.md | Operator-authorised one-customer behavior and show-once credential contract. |
| src/Pegasus.Core/Cases/CaseContracts.cs | Name-based creation; removed obsolete organization APIs and successor-owner selection. |
| src/Pegasus.Core/Cases/OrganizationAdministration.cs | Bounded Principal queries and same-customer replacement; removed superseded APIs. |
| src/Pegasus.Infrastructure/DependencyInjection.cs | Wire new Principal query callers; remove dead organization registrations. |
| src/Pegasus.Infrastructure/Persistence/EfOrganizationAdministration.cs | Atomic customer creation and direct projections; removed unused organization operations/projections. |
| src/Pegasus.Web/Pages/Administration/Index.cshtml | Remove the retired Organizations entry. |
| src/Pegasus.Web/Pages/Administration/Organizations/Edit.cshtml | Removed obsolete separate organization administration route/model. |
| src/Pegasus.Web/Pages/Administration/Organizations/Edit.cshtml.cs | Removed obsolete separate organization administration route/model. |
| src/Pegasus.Web/Pages/Administration/Organizations/Index.cshtml | Removed obsolete separate organization administration route/model. |
| src/Pegasus.Web/Pages/Administration/Organizations/Index.cshtml.cs | Removed obsolete separate organization administration route/model. |
| src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml | Single-customer create/list/settings/replacement Razor workflow using existing Core commands. |
| src/Pegasus.Web/Pages/Administration/Principals/Create.cshtml.cs | Single-customer create/list/settings/replacement Razor workflow using existing Core commands. |
| src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml | Retired old two-identifier route; consolidated into Settings. |
| src/Pegasus.Web/Pages/Administration/Principals/EvaSubmission.cshtml.cs | Retired old two-identifier route; consolidated into Settings. |
| src/Pegasus.Web/Pages/Administration/Principals/Index.cshtml | Single-customer create/list/settings/replacement Razor workflow using existing Core commands. |
| src/Pegasus.Web/Pages/Administration/Principals/Index.cshtml.cs | Single-customer create/list/settings/replacement Razor workflow using existing Core commands. |
| src/Pegasus.Web/Pages/Administration/Principals/Replace.cshtml | Single-customer create/list/settings/replacement Razor workflow using existing Core commands. |
| src/Pegasus.Web/Pages/Administration/Principals/Replace.cshtml.cs | Single-customer create/list/settings/replacement Razor workflow using existing Core commands. |
| src/Pegasus.Web/Presentation/OperatorLabels.cs | Shared Principal UI labels in existing owner. |
| tests/Pegasus.Core.Tests/Cases/OrganizationAdministrationTests.cs | Focused caller/contract assertions and snapshot matcher updated for one Principal workflow. |
| tests/Pegasus.IntegrationTests/OrganizationAdministrationPersistenceTests.cs | Focused caller/contract assertions and snapshot matcher updated for one Principal workflow. |
| tests/Pegasus.IntegrationTests/OrganizationAdministrationWebTests.cs | Focused caller/contract assertions and snapshot matcher updated for one Principal workflow. |
| tests/Pegasus.IntegrationTests/OrganizationDirectoryWebTests.cs | Focused caller/contract assertions and snapshot matcher updated for one Principal workflow. |
| tests/Pegasus.IntegrationTests/PrincipalCredentialPersistenceTests.cs | Focused caller/contract assertions and snapshot matcher updated for one Principal workflow. |
| tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs | Focused caller/contract assertions and snapshot matcher updated for one Principal workflow. |
| tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs | Focused caller/contract assertions and snapshot matcher updated for one Principal workflow. |
| src/Pegasus.Web/Pages/Administration/Principals/Settings.cshtml | Single-customer create/list/settings/replacement Razor workflow using existing Core commands. |
| src/Pegasus.Web/Pages/Administration/Principals/Settings.cshtml.cs | Single-customer create/list/settings/replacement Razor workflow using existing Core commands. |

## Governing docs

FRD-04 is modified under the operator's explicit correction: a Principal is
one customer, not an organisation-owned hierarchy. The backing Organization
row and role persistence stay internal to that identity; the real repairer,
storage and location directory is unchanged. Create is name/code atomic and
list/detail are Principal-bounded. Same-customer code replacement is enforced
in Core; the obsolete successor-organisation argument is removed.

FRD-09's existing Provider API remains the credential authority.
Settings invokes IIssuePrincipalCredential, IPausePrincipalCredential,
IResumePrincipalCredential, IRevokePrincipalCredential and
IGetPrincipalCredential. Secrets exist only in the immediate no-store
issue/reset response, never TempData or a subsequent query/replay. The
existing endpoint constant is displayed. Settings reads canonical
QdosMailRoutePolicy.AcceptedDirectDomains, not a copied activation list.

PLAT-050 overlap is folded: default inspection location and manual-only EVA
use existing commands and independent forms. The historical automatic EVA
toggle is superseded by ADR-0038; it is not restored. Root confirmed the
existing Settings page convention, not a new modal-loader mechanism.

## Simplicity disposition

Root explicitly required removal of the old organization-admin API after the
source-caller audit found only DI and test callers. All four obsolete
interfaces, requests, projections, use cases, registrations and EF operations
are removed. Meaningful name/code normalization, duplicate failure with no
orphan, exact/concurrent replay, authorization, actual settings forms,
credential lifecycle and immutable reference assertions remain on the
Principal contract. No new dependency, schema, migration, runtime or directory
is introduced. Root retains independent simplification/review ownership.

## Risks / follow-ups

- Root verification and scoped generated HTML/catalogue index are pending;
  current missing Settings snapshot is intentional until that capture.
- The existing replacement-default loss was fixed under root's explicit
  disposition: all six DefaultInspection* fields are copied into the same
  customer's successor. One existing persistence test configures the location
  through the current Core command and asserts the complete location/source
  tuple after replacement, alongside immutable case/reference assertions.
- General activated-domain metadata remains root's route follow-on. The
  top-15 extraction profile catalog is not treated as active route authority.
- No live pegasustest or credential was created. Root can use
  /Administration/Principals/Create, Name pegasustest, Code PEGASUSTEST.
  Address binding belongs to the accepted contact/route owner; no email
  contact field was invented here. Only digital@collisionengineers.co.uk is
  authorised for live outbound testing.
- Creation/replacement history and existing case references remain stored;
  retired UI/code/snapshots are recoverable in Git.

## Verification hand-off

Static git diff --check: exit 0. Static retired route and obsolete API search:
no source/test/catalogue references remain. These are not runtime proof.

Root only, in the recorded worktree after the one coordinated build:

- Core filter: FullyQualifiedName~Cases.OrganizationAdministrationTests
- Integration filter:
  FullyQualifiedName~OrganizationAdministrationWebTests|FullyQualifiedName~OrganizationAdministrationPersistenceTests|FullyQualifiedName~OrganizationDirectoryWebTests|FullyQualifiedName~PrincipalCredentialPersistenceTests|FullyQualifiedName~ProviderApiSubmissionTests
- CaptureFilter: FullyQualifiedName~OrganizationAdministrationWebTests
- Scope:
  administration,administration-principals,administration-principal-create,administration-principal-settings,administration-principal-replace
- Update-TestUiSnapshots then Verify -SkipCapture with the same Scope and
  Test-UiCatalogue. Credential-bearing POST bodies never enter the capture
  helper. Reuse that focused run where possible; do not start competing rails.
- kanmer-verify later verifies the exact integrated dev SHA, using the board's
  configured integration branch, not a hard-coded main assumption.

## Stop

Keep the taken record/worktree. No PR or Review move until root supplies
verification and the explicit handoff. No worker build/test/capture is allowed.

Source refrozen after this bounded fix; git diff --check exit 0. Root tests/capture remain pending.

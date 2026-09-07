# Research — PLAT-028: Organizations, Principals, and provider controls

## Question

How should the existing Administration Organizations and Principals pages be redesigned as one coherent experience while adding safe provider-credential controls owned by the Principal?

## Findings

- Organizations and Principals are already one Core administration capability with shared `ManageOrganizationsAndPrincipals` authorization, list/detail queries, create/update/replace commands, expected versions, replay-safe operation keys, reasons, and permanent history (`src/Pegasus.Core/Cases/OrganizationAdministration.cs`).
- The Web currently splits Organizations and Principals across separate indexes and create/replace pages. The Organization edit page already lists its Principals, so it is the natural consolidated detail surface (`src/Pegasus.Web/Pages/Administration/Organizations/`, `src/Pegasus.Web/Pages/Administration/Principals/`).
- The current page contains explanatory empty-state and overflow copy that conflicts with the repository's no-explanatory-copy/page-economy rule; redesign should use labels, values, and concise action consequences only (`docs/design/README.md`).
- EPIC-008 explicitly requires Organizations and Principals to be consolidated. PLAT-024 is now archived as a duplicate.
- FRD-04 currently allows Administrator principal administration but forbids credential-secret administration through the staff UI. The operator has explicitly superseded that limitation for principal-scoped provider credentials, requiring a durable FRD change.
- FRD-09 currently says sources do not define an administration UI; the operator has now supplied the missing intent: generate, reset, revoke, pause, and resume from the Principal administration surface.
- API-04 owns credential policy, persistence, one-time secret generation, authentication, and audit history. PLAT-028 must consume those Core commands and projections rather than implement credential rules in Razor handlers.
- The one-time clear secret cannot be redisplayed after redirect/refresh. The safe UI pattern is a dedicated POST result page/view rendered once from the command result, with no TempData/session/database copy of the secret.
- Pause and revoke are distinct: pause denies new submissions but preserves authenticated result reads; revoke invalidates authentication. Destructive confirmations may contain one consequence sentence.

## Implications

Make the Organization list the single entry point and the Organization detail the consolidated owner of roles, Principal rows, create/replace navigation, and per-Principal provider access. Remove the separate Principal index destination. Add credential actions only after API-04 supplies Core contracts. Keep the clear secret in the immediate response model only, never navigation state or persistence. Update FRD-04, FRD-09, and design authority in this ticket.

## Open questions

The operator has resolved ownership, pause semantics, and consolidation. Visual details follow existing design tokens and will be proved in a real browser.

## Azure architecture refresh — 2026-08-21

The administration UI already runs in the production Web Container App and persists through Azure SQL. Provider controls therefore need no Azure Portal surface, App Configuration, Key Vault secret browser, new app, or new deployment unit. The page consumes TICK-061's Core commands and status projection. Clear generated/reset text exists only in the immediate HTTPS response; Azure SQL stores only its verifier and lifecycle metadata, and logs/telemetry must never capture it. Live issuance remains an external write requiring exact-target approval.

## Current remediation research — 7 September 2026

The current operator and EPIC-014 supersede the earlier organisation-detail plan. Principal is one customer identity. At origin/dev 3da60bd0c270111d5168dc17246dc831882108ea, Principals/Index.cshtml still groups customers under organisations and Create.cshtml requires an existing Work Provider. FRD-04 already requires inline creation. CreatePrincipalRequest in CaseContracts.cs has only three production/Core owners and a few test callers; replace its selected organisation ID with customer Name, and create the existing backing identity plus Principal in one transaction. No schema change or second directory is needed.

EfOrganizationAdministration owns transaction, idempotency, permanent history and normalized uniqueness. Extend its query projection to page Principals directly and resolve one Principal by ID. Existing OrganizationDirectory has independent repairer/storage/location callers and is not removed.

PrincipalCredentials.cs already supplies issue/reset/pause/resume/revoke and show-once outcomes with registered production implementations; no administration page currently calls these. The current EvaSubmission page supplies manual-only EVA and default-location settings; rename/fold it into one Principal Settings dialog/surface with provider controls and existing route catalog addresses. Retire separate Organizations pages, keep customer code replacement on the same backing customer with no owner choice. FRD-09 policy is unchanged. No sources are declared (get_sources returned an empty list).

Existing validation owners: OrganizationAdministrationWebTests, OrganizationAdministrationPersistenceTests, Cases/OrganizationAdministrationTests, PrincipalCredentialPersistenceTests; ProviderApiSubmissionTests has one create caller to update. Root is the sole verifier. No live customer or credential is created by this lane.

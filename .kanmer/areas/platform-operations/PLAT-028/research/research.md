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

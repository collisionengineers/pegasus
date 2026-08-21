# Research — TICK-061: provider credential lifecycle

## Question

How should Pegasus issue, reset, revoke, pause, and resume a Principal's provider credential through the redesigned Administration surface while preserving principal isolation and one-time secret handling?

## Findings

- ADR-0004 and FRD-09 require separately issued principal-scoped client IDs and opaque secrets, hash-only storage, rotation, and revocation.
- Existing Organization/Principal administration already has Administrator-only Razor Pages, Core authorization, expected-version checks, reason, operation key, and durable history (`src/Pegasus.Web/Pages/Administration/Organizations/Edit.cshtml.cs`, `src/Pegasus.Core/Cases/OrganizationAdministration.cs`, `src/Pegasus.Infrastructure/Persistence/EfOrganizationAdministration.cs`).
- The Automation administration surface provides a close UI precedent for enable/disable and rotate-once secrets, but provider credentials belong to the Principal surface, not Automation (`src/Pegasus.Web/Pages/Administration/Automation/Index.cshtml.cs`).
- PLAT-028 now owns the consolidated Organizations/Principals redesign and provider controls; PLAT-024 is archived.
- The operator chose principal-level pause: deny new submissions immediately while authenticated reads of prior receipts/results remain available. Revocation instead invalidates authentication.
- One credential per Principal is the smallest v1 contract; a second concurrent key has no current second caller.
- No credential entity or authentication handler exists today, so API-04 must add an external security boundary, persistence, migration, and Web composition.

## Implications

Add one Principal-owned credential record with immutable client ID, password-hasher output, lifecycle timestamps, enabled-for-submission flag, version, and no recoverable secret. Generation and reset create a cryptographically random secret shown once; reset immediately replaces the hash. Revocation clears/invalidates the hash. Pause changes only submission permission. Every mutation requires Administrator authorization, expected version, reason, replay-safe operation key, and permanent action history. PLAT-028 renders the controls on the redesigned Principal detail.

## Open questions

The operator decisions needed for a v1 plan are resolved. Multiple simultaneous credentials and live rollout remain explicitly deferred.

# Post-implementation report — MAIL-07

## Outcome

Implemented the confirmed retained-mail folder move as a narrow, dedicated operation. Staff can confirm the already-computed recommendation with a reason; the server resolves the exact approved mailbox binding and current retained-message coordinates, records an idempotent operation, calls the exact Microsoft Graph move/probe boundary, and exposes durable outcome/current-location evidence. The live provider remains unavailable in the default composition, so this branch makes no deployment or production-activation claim.

## Implementation

- Added `MoveRetainedMailFolder` and its focused request/result and mover/store ports in Core. It reuses the existing `PerformCasework` authorization, retained classification/recommendation policy, approved mailbox version and binding. It rejects automation actors, stale versions, invalid operation keys and invalid reasons.
- Added one dedicated EF operation/current-location entity and store. The store resolves all Graph identities server-side, persists the claim before the provider call, enforces matching replay/different-input conflict, blocks concurrent unresolved operations, and resolves uncertain responses with an exact parent-folder probe.
- Preserved the immutable retained arrival row. Successful moves are overlaid as current location, excluded from the Inbox list with a SQL `NOT EXISTS`, and permanently attributed through action history.
- Extended the existing Graph client only with the exact folder-scoped move and immutable-id parent-folder probe. Tests use a fake HTTP handler; no live mailbox was contacted.
- Added the authenticated retained-message confirmation POST and reused the shared reason dialog. The browser submits only the retained internal id, freshness tokens, operation key and reason—never mailbox, source or destination Graph identity.
- Added migration `20260820144004_RetainedMailFolderMoves` with restrictive foreign keys, unique operation key, Web SELECT/INSERT/UPDATE grants, explicit Web/Worker DELETE denial, and no Worker write grant.
- Updated `docs/capabilities.md` and `docs/current-architecture.md` only to describe local, test-backed, default-off evidence.

## Governing-doc compliance

The implementation follows `docs/frd/frd-08-email-mailbox-and-background-processing.md`: a move requires explicit authenticated staff confirmation and reason; classification remains unchanged; provider identity is server-derived; failures remain visible and recoverable; retained evidence is not deleted. No new product scope or technical architecture boundary was introduced, so no new PRD/FRD/ADR was needed.

## Simplicity pass

The dated plan disposition covers reuse, simplification, efficiency and altitude. The final diff keeps one concrete caller and one external-provider boundary, reuses the existing retained-mail query, Graph client, approved binding/version, policy and shared dialog, and does not introduce a generic mail-command framework. Applied findings removed an unused move reason, retained actor roles, avoided duplicate uncertain-replay history, rendered the durable reason, and removed a duplicate migration object caused by snapshot overlap. No findings remain unapplied.

## Verification

All verification was local, using fake HTTP and local SQL only.

- `dotnet restore ./Pegasus.slnx --locked-mode` — passed.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — passed; 0 warnings, 0 errors.
- Focused Core folder-move tests — 6/6 passed.
- Focused persistence/fake-Graph/authenticated-Web tests — 4/4 passed.
- Full `Pegasus.Core.Tests` — 848/848 passed.
- Broader retained-mail/Web/Graph test set — 81/81 passed.
- Exact migration schema/runtime permission tests — 2/2 passed.
- `git diff origin/dev...HEAD --check` — passed.

## Commits

- `8b1e6d74` — feature, persistence, migration and tests.
- `f60248af` — qualified local-evidence documentation.

The PR reference and final head SHA will be recorded on the ticket after the branch is pushed and the PR is opened.

## Risks and verification handoff

- The default `IRetainedMailFolderMover` is intentionally unavailable. Production activation still requires separate exact permission/RBAC/deployment approval and live evidence; none was requested or performed here.
- A provider timeout that cannot be resolved by probing either exact source or destination remains durable as uncertain and blocks a second operation key, preventing a duplicate move.
- On merged code, verification should repeat locked restore, Release build, full Core tests, the retained-mail/Web/Graph integration set, and the migration permission tests. It should also confirm the production composition still has no live writer before any separately authorized activation work.

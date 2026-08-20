# Post-implementation report — MAIL-08

## Outcome

Implemented the accepted minimum MAIL-08 slice as a read-derived advisory. `GetRetainedMail` now returns either no suggestion or one concrete `RetainedMailSuggestedMove` after the landed MAIL-05 recommendation and MAIL-07 current-location/latest-operation state are known. The message page labels the advice and delegates its button to MAIL-07's existing reasoned confirmation dialog and POST. An unavailable writer/recommendation, current destination or Uncertain move yields no fresh advisory; a terminal source-folder failure can offer a newly confirmed retry.

Viewing advice performs no history, persistence, provider or external write. No broad action matrix, enum/registry/framework, EF/store/query/migration, transaction, operation key, adapter, MCP tool or Automation surface was added.

## Changed files

- `src/Pegasus.Core/Intake/RetainedMail.cs` — concrete nullable advisory and read-time derivation.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml` — labelled advice around the unchanged MAIL-07 confirmation control.
- `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` — current recommendation/location/Uncertain/abstention derivation.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — exact authenticated absence, one-action, delegation, retry and no-view-mutation evidence.
- `docs/capabilities.md` — local source/test evidence for MAIL-08.
- `docs/current-architecture.md` — as-built optional advisory on the existing detail read.

## Governing docs

`docs/frd/frd-08-email-mailbox-and-background-processing.md` remains unchanged: exact-message actions stay on detail, only the designated destination is offered, classification/recommendation remains separate from the reasoned folder move, and no automatic move occurs. Existing `docs/design/README.md` confirmation conventions are reused without change. No ADR is needed.

## Verification

- `dotnet restore ./Pegasus.slnx --locked-mode` — passed.
- Focused `RetainedMailTests` — 34/34 passed.
- Direct authenticated Web advisory cases — 3/3 passed.
- Full `MailWorkspaceWebTests` — 30/30 passed.
- Full `Pegasus.Core.Tests` — 848/848 passed.
- Full `Pegasus.ArchitectureTests` — 98/98 passed.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — passed, 0 warnings/errors.
- `git diff --check` — passed.

All Web/provider evidence used the existing local integration factory and fake mover. No live Graph, mailbox, cloud, permission, deployment or external write was performed.

## Simplicity and risks

The dated plan records all four lenses with no unapplied finding. The advisory intentionally carries no command/freshness/transport identity. MAIL-07 remains the only execution owner and revalidates all state on confirmation. Broader actions remain deferred pending an accepted matrix and their own Core contracts.

## Handoff

Commit/PR traceability will be appended after publication. Independent review should verify the optional Core projection, the absence paths and that the existing POST remains the only mutation route. Deployment and the separately accepted read-only production viewer check are not claimed.

## Commit and pull request

- `75c9f3a0576b73c722c03b6e1a71b39205711602` — derive the optional suggested Move and render it through MAIL-07.
- PR #480 targets `dev`: https://github.com/collisionengineers/pegasus/pull/480
- Replacement CI run 32401331139 started after the push and remains for independent review.

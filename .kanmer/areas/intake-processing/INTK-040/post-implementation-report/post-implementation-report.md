# Post-implementation report — INTK-040

## Summary

Future otherwise-Unidentified mailbox emails with direct image attachments now submit those attachments as one mailbox-provenance group through the existing grouped Image Intake lifecycle. The parent email no longer opens a competing Unidentified item; the existing group policy performs VRM recognition, case matching, Image-initiated Case creation and image custody. Inline images, EML bytes and non-image attachments are excluded. U35 itself is unchanged and no replay/backfill was added.

## Changes

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Core/Intake/MailboxImageIntakeSubmission.cs` | Added the narrow mailbox attachment selector and grouped-submission adapter | Reuses grouped intake and owns only mailbox-to-group routing, stable replay and terminal failure visibility |
| `src/Pegasus.Core/Intake/DurableIntake.cs`, `ProcessIntake.cs`, `ReconcileGroupedImageIntake.cs` | Wired the fresh-processing hook, deferred the parent outcome, suppressed replay duplication and converged partial-group failure | Ensures one durable group outcome without changing historical receipts |
| `src/Pegasus.Core/Intake/GroupedIntake.cs` | Made group channel explicit and added nullable parent receipt provenance | Preserves mailbox source identity while keeping manual upload on the identical business route |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeSubmissionGroupStore.cs`, `PegasusDbContext.cs`, migration `20260825145216_MailboxImageIntake` and snapshot | Persisted/validated parent provenance and added the Worker write grants | Makes replay deterministic and permits the existing Worker composition to create group rows |
| `src/Pegasus.Worker/WorkerDependencyInjection.cs` | Composed grouped intake and mailbox submission services | Activates the route in the actual mailbox processing host |
| `src/Pegasus.Web/Pages/Upload.cshtml.cs` | Passed the manual-upload channel explicitly | Retains the existing manual caller while preventing implicit provenance |
| `tests/Pegasus.Core.Tests/**`, `tests/Pegasus.IntegrationTests/**`, `tests/Pegasus.ArchitectureTests/**` | Added/updated routing, exclusions, replay, failure, persistence, custody, schema and composition coverage | Proves the U35 shape and guards the shared route |
| `docs/operator-notes.md`, `docs/frd/frd-02-intake-and-source-identity.md` | Recorded the operator ruling and normative behavior | Keeps business truth and intake behavior aligned with the requested future route |

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: modified with explicit operator authorization to define the mailbox-image group route, exclusions, one-outcome rule and future-only boundary.
- `docs/frd/frd-05-documents-extraction-and-custody.md`: met by retaining each selected direct image as an ordinary child source; EML, inline and non-image assets are not image custody.
- `docs/frd/frd-12-operator-experience.md`: met by preventing a redundant parent email U-item.
- `docs/adr/0029-image-initiated-case-projection.md`: met by invoking the existing grouped Image Intake owner; no second recognition, matching or case implementation was added.
- `docs/operator-notes.md`: modified with explicit operator authorization to record replacement of the parent U outcome and that U35 is not retroactively changed.

## Risks / follow-ups

- No deployment or mailbox mutation was performed. The migration must be deployed with the application before the route is live.
- U35 remains unchanged by operator decision; there is deliberately no replay or backfill.
- A partial terminal child-submission failure registers one group-scoped technical U-item. Reconciliation uses the same origin and operation key, so it cannot split into per-child outcomes.
- If every member is already durable and only a transient final read fails, the complete group is left to settle normally rather than opening a competing technical U.
- No unrelated issues or follow-up tickets were identified.

## Verification hand-off

Run on merged `dev` (and again on the exact release candidate before production):

- `dotnet restore Pegasus.slnx --locked-mode`
- `dotnet build Pegasus.slnx --configuration Release --no-restore` — expect 0 warnings and 0 errors.
- `dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build` — implementation result after review fix: 990 passed.
- `dotnet test tests/Pegasus.ArchitectureTests/Pegasus.ArchitectureTests.csproj --configuration Release --no-build` — implementation result: 99 passed.
- Run `OtherwiseUnidentifiedMailSubmitsOnlyDirectPhotosAsOneImageGroup` — expect one mailbox group containing exactly three direct JPEGs, no parent U-item, idempotent replay, and one unassociated AB12CDE Image-initiated Case with those three images.
- Run the full non-corpus/non-browser Integration profile — implementation result: 910 passed, 2 expected skips; the affected SQL-backed scenario passed again after the final simplification.
- Inspect migration `20260825145216_MailboxImageIntake` and its schema census assertion for the parent FK/index and Worker INSERT grants.

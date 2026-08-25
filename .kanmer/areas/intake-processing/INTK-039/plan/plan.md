# Plan — INTK-039

## Approach

Correct the two proven faults at their existing owners: grant the Worker the append-only lifecycle-event access already required by `EfImageIntakeStore`, and make the upload presentation distinguish a completed durable evaluation from a settled grouped-image destination. Leave lifecycle, queue, and Box policies unchanged so the existing merge transaction remains the one source of truth.

## Governing docs

- **FRD-02 — meets:** one manual multi-image submission remains one routing group; every image-only member reports the group’s settled destination.
- **FRD-05 — meets:** a committed merge continues to enqueue the existing custody fold into the formal Case’s `Evidence/Images`.
- **FRD-12 — meets:** the upload page reports processing until truth is settled, and Not Ready totals/rows converge through the real lifecycle rather than a display exception.
- **ADR-0029 — meets:** the existing AwaitingInstruction → Merged transition and append-only lifecycle history remain authoritative.

## Steps

1. Add the next EF migration after `20260825105037_AssessmentAccessExportVersion`: grant `pegasus_worker_runtime_role` SELECT and INSERT on `ImageIntakeLifecycleEvents`, deny UPDATE and DELETE, and reverse only those permissions in Down.
2. Mirror that exact permission contract and corrected ownership comment in `Invoke-AzureDatabaseBootstrap.ps1`.
3. In the existing upload outcome builder, return a Working outcome for grouped image-only material that has no current Case, Image Intake registration/merge, or open Unidentified destination. Keep terminal Case/Image Intake/Unidentified results unchanged.
4. Keep the grouped status page polling when any rendered outcome is Working, including when its queue row is already Complete. The existing open-decision derivation then withholds create/attach controls automatically.
5. Add non-sensitive failure type/status tags to the existing image-association activity catches; do not log exception messages, file content, or extracted values.
6. Add focused permission, migration-census, upload race/group outcome, lifecycle/custody, and queue consistency tests. Run locked restore, Release build, focused suites, full tests, and the required simplification review.
7. Open a PR to `dev`, obtain an independent Kanmer review and green CI, merge, then include the result in the exact-SHA production release. After explicit target approval, selectively wipe only Pegasus SQL transactional data and transient-intake blobs, preserving identity/configuration/mailbox cursors/reference sequences and leaving Outlook/Box untouched.

## Acceptance

- The screenshot-1 intermediate state renders Processing, auto-refreshes, and has no group decision form.
- The screenshot-2 journey settles both images against the one eligible formal Case with no awaiting Image Intake.
- Image-first then instruction-first creates a merge event/history/custody work item; after custody processing the formal Case owns the images and the temporary image folder is removed.
- Not Ready counts equal visible filtered rows.
- Production read-back proves the Worker’s effective SELECT/INSERT and denied UPDATE/DELETE lifecycle-event permissions.

## Risks

- A privileged LocalDB test can hide a missing runtime grant; the migration/bootstrap contract and production permission read-back are mandatory.
- No production backfill is added. The user selected disposal through the established pre-release wipe.

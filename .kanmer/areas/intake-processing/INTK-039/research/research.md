# Research — INTK-039

## Question

Why did the 2026-08-25 grouped-image tests show a premature Pending decision, linked-but-active Image Intake rows, missing Box folding, and mismatched Not Ready counts?

## Verified findings

- The supplied test log and screenshots show a two-image group first rendered as two open “Pending” decisions; after staff intervention one row reported the formal Case while the other reported a new Image Intake. The queue badge showed three while two rows rendered.
- FRD-02 makes the image group, not each image, the routing unit. FRD-05 requires merged image custody under the formal Case. FRD-12 requires honest upload outcomes and queue totals that match their rows. ADR-0029 defines AwaitingInstruction → Merged as the lifecycle transition.
- Production SQL on `pegasus-prod-sql-252ow37gij/pegasus` showed GD65TVY-01 and LO10VCC-01 still `awaiting_instruction`, each already linked to its formal Case, with no lifecycle event and no `merge_image_case_custody` work item.
- `EfImageIntakeStore.TransitionAsync` reads and inserts `ImageIntakeLifecycleEvents` in the same transaction that updates the lifecycle, writes Case history, and enqueues custody work.
- The effective production permission matrix grants the Worker neither SELECT nor INSERT on that table. The migration and bootstrap matrix grant only Web access; the bootstrap comment incorrectly says the Worker never touches it.
- Queue counts include every awaiting/unmerged Image Intake, while list rows omit associated records. That is why a partial link appears in the badge but not the table. Fixing the lifecycle transaction removes the inconsistency; a count-specific exception would hide the real state.
- Group work is marked Complete before the bounded `ReconcileGroupedImageIntake` pass settles a straggler. `UploadGroupStatusModel.RefreshAutomatically` only polls Received/Processing work states, and `UploadOutcomeQueries` maps an unsettled grouped image to an open ReadyToCreate decision. The operator can therefore act before the group reaches its authoritative outcome.
- Focused existing tests pass under privileged LocalDB and therefore do not exercise the missing Worker role grant. Azure AppLens/log/health checks showed no broad host or dependency outage, so a .NET trace or dump is not indicated.

## Implications

- Add the missing append-only Worker permission through the normal migration and bootstrap contracts.
- Keep grouped image members visibly Working until a Case, Image Intake, or Unidentified destination exists, and poll on that rendered state.
- Reuse the existing merge transaction, custody processor, group resolver, and queue queries. Do not add backfill, count exceptions, Box fallbacks, or a second lifecycle owner.
- Existing production rows are disposable pre-release test data and will be removed through the separately approved selective SQL/blob wipe; Outlook and Box are outside that wipe.

## Takeover — 2026-08-19

Taken over by claude-code under DELIV-012, by operator decision, continuing PR #417 (`intk-006-grouped-image-routing`, stacked on `intk-005-grouped-upload`). Working only in `.worktrees/intk-006`.

**Operator ruling (given verbatim, 2026-08-19), to be expressed by the routing behaviour/FRD here and by INTK-008 in the protected operator notes:**

> It could be either an image initiated case, OR it could be images being received for an existing case. ie if we get images, with a registration that doesnt match any existing case, then that creates an image initiated case. If they match an existing case (by VRM), then get get attached as evidence to that case.

So: readable VRM matching exactly one existing eligible Case → images attach as evidence to that Case. Readable VRM matching no existing Case → Image-initiated Case (this ticket registers into the existing ImageIntake owner; INTK-008 owns the Image-initiated Case lifecycle naming/searchability). "No unique match" (zero or many eligible cases) and "ambiguous/conflicting VRMs across the group" are two distinct fail-closed reasons and must never associate.

## Work done this session

1. Merged `origin/intk-005-grouped-upload` (head `d70118b1`) into this branch. Two real conflicts (`EfIntakeSubmissionGroupStore.cs`, `IntakePersistenceIntegrationTests.cs` migration list) resolved by hand; migration list verified against the actual `Migrations/` folder contents byte-for-byte (not trusted from the merge alone) — matched exactly after resolution (49 migrations, then 50 after my own new migration).
2. Removed the single-file upload bypass in `Upload.cshtml.cs` per the takeover brief — every upload now flows through `IGroupedIntakeSubmission`; a one-member group redirects to `/UploadStatus` with the bare (non-`:0`) receipt token, same as before.
3. Fixed the headline blocker: `ImageIntakeAutomation.TryRegisterAndAssociateAsync` now takes the group's `ImageIntakeGroupRoutingDecision` and only searches for/associates a Case when the decision is `AssociateExistingCase`. A per-member exact-match search can no longer overrule a group hand-off caused by an ambiguous or zero eligible-case count.
4. Found and fixed a second, independent bug while wiring group-level tests: `GroupedIntakeMemberToken` shape had drifted — `SubmitGroupedIntake` submits ordinal 0 with the bare token (INTK-005's fix), but `ImageIntakeAutomation`'s member lookup always queried `{token}:{ordinal}` including for ordinal 0. Any real multi-member group could therefore never find its own first member and would wait forever. Extracted one shared `GroupedIntakeMemberToken.Create` helper; both call sites now use it.
5. `ScanAsync` now reuses an already-recorded suggestion per asset (matched by asset id + storage key + content hash) instead of re-invoking the ONNX engine on every group-trigger event.
6. Non-image members in a mixed batch are excluded from recognition and from the group's routing decision (`receipts.Where(IsImageOnly)`).
7. `IntakeSubmissionGroup` now persists `ExpectedMemberCount` (set once at group creation from the originating submission's file count) so routing waits on the *declared* batch size rather than on however many member rows happen to exist yet when a worker picks up an early member. New migration `20260819140113_ImageIntakeGroupExpectedMemberCount`.
8. Added FRD-02 "Grouped image-intake routing" subsection (membership/completeness, non-image exclusion, distinct-VRM aggregation, associate-or-hand-off precedence, group-not-per-member fail-closed rule, scan-once) and an FRD-06 paragraph on the detector/recognizer diagnostic distinction and scan-once behaviour. Registered both under INT-28/INT-17 in `docs/capabilities.md`. `Test-TestMarkdownPlacement.ps1` and `Test-DocumentationLinks.ps1` both pass locally.
9. Compiled the PR's 13 review comments via `gh api repos/.../pulls/417/comments` and dispositioned every one — see plan.md "Round-2 simplification / review disposition" heading.
10. Pushed as `bfacefeb`. Confirmed CI actually started (`changes`/`documentation`/`reference-data` all showed `pending` immediately after push).

## Known gap left open (recorded, not fixed)

"Retry incomplete group registration and association" (P1 review comment): if a recoverable failure hits registration/association for a *later* member in a group after earlier members already succeeded, the loop in `TryApplyGroupAsync` doesn't propagate that failure, and if the failing member was the *last* to complete, nothing later re-triggers the group. Registration/association are each idempotent by receipt-scoped operation key, so any later re-trigger of the same group (another member arriving, a manual reprocess) safely completes what failed — the gap is specifically "last member fails, nothing re-triggers." A real fix needs a durable per-group routing-attempt/outcome record, which the ticket's own plan.md already delegates to INTK-008 ("durable Image-initiated lifecycle outcome persistence is delegated to INTK-008"). Not fixed here to avoid inventing a second outcome/retry mechanism ahead of that design.

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

## Coordinator update folded in — 2026-08-19 (later same session)

Re-fetched and merged the moved base `origin/intk-005-grouped-upload` (now `0f71ee60`, adds the grouped-intake grant census entries to `Invoke-AzureDatabaseBootstrap.ps1`). Clean merge, no conflicts (`038105a6`).

Ran `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`: it throws `Database bootstrap must account for grant-carrying migration 20260819104953_MailClassificationCorrectionHistory.cs` — exactly the pre-existing, unrelated-to-this-branch failure the coordinator said to expect (that migration belongs to the mail-classification work merged via `origin/dev`, not to INTK-005/006). Confirmed my own new migration (`20260819140113_ImageIntakeGroupExpectedMemberCount`) carries zero `GRANT` statements (`grep -c GRANT` = 0), so it is excluded from the script's grant-carrying-migration scan entirely and needs no census entry. `20260819101344_GroupedIntakeSubmission` (INTK-005's grant-carrying migration) is already accounted for and would have passed had the script reached it — it sorts before the failing one alphabetically/chronologically but the check loop still hits `20260819104953_...` before reaching my migration's timestamp, so this run never got that far; not a gap in my own work.

`scripts/Test-MigrationGrants.ps1` still does not exist in this branch after the merge — skipped per the original instruction ("if present").

Rebuilt (0 warnings/errors) and pushed as `038105a6`. Confirmed CI started again on this new head (`changes`/`documentation`/`reference-data` all `pending` immediately after push, run 32263442445).

## Final state — 2026-08-19

Pushed head: `caef9dff` (adds `OneEligibleCaseAssociatesEveryGroupMember`, the direct multi-member-association proof for the ordinal-0 token fix).

Checklist updated to 30/41 (from 26/41), with an honest Progress note on every unchecked/changed item — see checklist doc.

`gh pr checks 417` at report time (run `32263668308`):
- `changes` — pass
- `documentation` — pass
- `reference-data` — pass
- `infrastructure` — **fail**, but this is the exact pre-existing, unrelated `dev`-branch gap the coordinator flagged mid-session (`20260819104953_MailClassificationCorrectionHistory` missing from the database-bootstrap census; fixed on `dev` by PR #426, not yet merged there). Reproduced locally with `Test-AzureDeploymentPlan.ps1 -Mode Local` before pushing — same message. Confirmed my own new migration carries zero GRANT statements, so it needs no census entry and is not implicated.
- `browser`, `sql-integration (1/2/3)`, `unit` — still `pending` at report time; a background poll (`until ! gh pr checks 417 | grep -q pending`) was left running to catch the final result but had not completed when this report was written. Whoever picks this ticket back up should re-run `gh pr checks 417` to see the final state of those five jobs before treating CI as fully green.

Plan.md has the full 13-comment disposition table and the dated simplification pass under "Takeover remediation — 2026-08-19".

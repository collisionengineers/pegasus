# Checklist — PR-069

*One independently tickable box per ordered plan step, then the verification the
post-implementation report will summarise. Append progress notes rather than
rewriting.*

- [x] Step 1 — Add `ReopenUnidentifiedRequest`, `UnidentifiedReopenResult`, `ValidateReopen` and the three `IUnidentifiedStore` members, and implement them on all five test fakes; Release build exits 0.
- [x] Step 2 — Implement `ReopenAsync`, `ListResolutionsToRecheckAsync` and `MarkResolutionRecheckedAsync` in `EfUnidentifiedStore`, add `ReconciledAssociationVersion` to the entity and model, and commit the migration plus generated snapshot with the Worker-role `UPDATE` assertion; Release build exits 0.
- [x] Step 3 — Rename `ResolveForReceiptAsync` to `SynchronizeForReceiptAsync`, factor the destination chain once, reopen/retarget an automation-resolved item whose effective destination changed, add the bounded recheck loop with the `Corrected` count, and build both operation keys as `intake-unidentified-{transition}:{item.Id:N}:{item.Version}`.
- [x] Step 4 — Update `DurableIntake.cs:911` and `Details.cshtml.cs:659` to the renamed method, narrow the page handler's catch so `UnidentifiedOperationConflictException` is no longer swallowed, and log `Corrected` on the existing sweep message; the whole solution builds Release-clean.
- [x] Step 5 — Add the Core tests for reopen-on-withdrawn, reopen-and-retarget, unchanged-destination no-op, staff-resolved untouched, and the interpolated item-keyed operation keys; update the two existing expected keys without weakening them.
- [x] Step 6 — Add the real-SQL lifecycle test (correct → link → sweep → unlink → sweep → relink → sweep, single queue, all-zero steady state) and the real recheck-predicate test, and add the new migration id to the applied-migration census.
- [x] Step 7 — Run the simplification pass over this branch's own diff and record every finding and disposition under the dated `## Simplification pass` heading in the plan.
- [x] Verification — record the exact commands, cwd, exit codes and results: `dotnet restore --locked-mode`, Release build, and (test runner) `Pegasus.Core.Tests` and `Pegasus.ArchitectureTests`; note that `sql-integration`, `browser` and `test-ui` are evidenced by CI `repository-check` at the PR head, since this workstation has no SQL Server LocalDB.
- [x] Verification — confirm the acceptance checks: named production callers, complete schema change (column + Worker-role assertion + census), reversal, relink, all-zero steady state, idempotent keys, surfaced conflict, no weakened assertion, one owner.
- [x] Report — write the post-implementation report naming every changed file and its reason, the governing-doc position, the retained evidence, and any deviation; then stop at the plan's stop condition without merging, promoting, or starting another ticket.

## Progress notes

Append with `set_ticket_doc(doc: "checklist", append: true)`.

**2026-09-02 (pegasus-implementer, attempt 1).** The refresh merge of `origin/dev` (`9b8f78a3`) into the branch is conflict-free: merge commit `3bf28244`, and its diff against `ed19e77f` names only the 14 dev-side paths — none of the 13 adopted paths. Pushed; PR #640's head is `3bf28244`, 0 behind `origin/dev`, base `dev`, OPEN, `mergeStateStatus: CLEAN`. Release build 0 warnings / 0 errors (the first attempt failed with 7 x NETSDK1004 on the never-refreshed worktree; one locked-mode dependency refresh preceded the successful build — deviation 2 / ASSUMPTION 2). The read-only audit traced all three Verification boxes to named diff lines and confirmed the regression boundary. The simplification pass is appended to the plan: 8 lenses, one reported finding S1 (the two added `.row-button[aria-current="true"]` selectors match no Inbox element and restyle the Cases selected row instead) — reported, not fixed, per the plan's Step 3 rule. PR retitled to the stop condition's exact title and re-footered `Kanmer: MAIL-032` through the GitHub API after `gh pr edit` failed on token scope (deviation 3); the body's falsified "No markup, CSS, or endpoint change" bullet was corrected (deviation 4). CI run 33581617718 at `3bf28244`: every required check green, `infrastructure` skipping. Tests: controller wave loop. The Step 7 box is ticked by the move itself.

**Move recorded.** MAIL-032 moved `implementing` -> `review` at the merged head `3bf28244`; stopped there for the independent reviewer.

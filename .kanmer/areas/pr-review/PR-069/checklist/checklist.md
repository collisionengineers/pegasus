# Checklist — PR-069

*One independently tickable box per ordered plan step, then the verification the
post-implementation report will summarise. Append progress notes rather than
rewriting.*

- [ ] Step 1 — Add `ReopenUnidentifiedRequest`, `UnidentifiedReopenResult`, `ValidateReopen` and the three `IUnidentifiedStore` members, and implement them on all five test fakes; Release build exits 0.
- [ ] Step 2 — Implement `ReopenAsync`, `ListResolutionsToRecheckAsync` and `MarkResolutionRecheckedAsync` in `EfUnidentifiedStore`, add `ReconciledAssociationVersion` to the entity and model, and commit the migration plus generated snapshot with the Worker-role `UPDATE` assertion; Release build exits 0.
- [ ] Step 3 — Rename `ResolveForReceiptAsync` to `SynchronizeForReceiptAsync`, factor the destination chain once, reopen/retarget an automation-resolved item whose effective destination changed, add the bounded recheck loop with the `Corrected` count, and build both operation keys as `intake-unidentified-{transition}:{item.Id:N}:{item.Version}`.
- [ ] Step 4 — Update `DurableIntake.cs:911` and `Details.cshtml.cs:659` to the renamed method, narrow the page handler's catch so `UnidentifiedOperationConflictException` is no longer swallowed, and log `Corrected` on the existing sweep message; the whole solution builds Release-clean.
- [ ] Step 5 — Add the Core tests for reopen-on-withdrawn, reopen-and-retarget, unchanged-destination no-op, staff-resolved untouched, and the interpolated item-keyed operation keys; update the two existing expected keys without weakening them.
- [ ] Step 6 — Add the real-SQL lifecycle test (correct → link → sweep → unlink → sweep → relink → sweep, single queue, all-zero steady state) and the real recheck-predicate test, and add the new migration id to the applied-migration census.
- [ ] Step 7 — Run the simplification pass over this branch's own diff and record every finding and disposition under the dated `## Simplification pass` heading in the plan.
- [ ] Verification — record the exact commands, cwd, exit codes and results: `dotnet restore --locked-mode`, Release build, and (test runner) `Pegasus.Core.Tests` and `Pegasus.ArchitectureTests`; note that `sql-integration`, `browser` and `test-ui` are evidenced by CI `repository-check` at the PR head, since this workstation has no SQL Server LocalDB.
- [ ] Verification — confirm the acceptance checks: named production callers, complete schema change (column + Worker-role assertion + census), reversal, relink, all-zero steady state, idempotent keys, surfaced conflict, no weakened assertion, one owner.
- [ ] Report — write the post-implementation report naming every changed file and its reason, the governing-doc position, the retained evidence, and any deviation; then stop at the plan's stop condition without merging, promoting, or starting another ticket.

## Progress notes

Append with `set_ticket_doc(doc: "checklist", append: true)`.

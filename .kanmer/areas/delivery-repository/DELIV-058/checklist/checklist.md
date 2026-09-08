# Checklist — DELIV-058

- [x] Step 1 — From a ready packet, refresh `origin/dev`, create only `.worktrees/deliv-058` on `DELIV-058-architecture-assertions`, and record the isolated workspace/branch through Kanmer take.
- [x] Step 2 — In `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`, assert selector presence, absence of any ProcessIntake parameter assignable to `IInstructionExtractionPolicy`, and the selector's sole `IEnumerable<IInstructionExtractionPolicy>` constructor while retaining Core-only ownership checks.
- [x] Step 3 — In `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs`, retain exact ordered equality and add existing image/Triage pairing dependencies in their Worker positions.
- [x] Step 4 — Run only static name/diff/hash checks, freeze the two-file diff for root/static review, and defer .NET verification to the shared slot.
- [x] [pre-review] Confirm neither D56 source nor [[INTK-002]] scope is touched; D58 is independent except for the shared verifier slot.
- [x] [pre-review] Do not run restore/build/test, commit, push, open a PR, merge or start another ticket before the next explicit grant.

## Progress notes

2026-09-08: Root approved the bounded two-test target. Plan correction: restore uses only `--locked-mode`; later build/tests use `--configuration Release`. The Core negative claim covers every ProcessIntake parameter assignable to `IInstructionExtractionPolicy`, not just the direct interface. D56 diagnosis/remediation is separate; only the verifier slot is shared.

2026-09-08: Refreshed and created the clean isolated worktree at `origin/dev` SHA `a1f0bfe260ea05df531df6e0ca3109141e7697da`. Frozen diff is exactly the two declared architecture tests. `git diff --check` passed; resulting blob hashes: `DependencyDirectionTests.cs` `77ca689ea1d91d694429cdcdee74ffd4ab29daf4`, `StagedArtifactReconciliationFunctionTests.cs` `80b74ef998d4ba905cd4b6dc05b10cea1945cd87`. No restore, build, test, commit, push, PR, merge, D56, or INTK-002 action occurred.

---

## Closeout — DELIV-058

- [ ] PR merge verified (`gh pr view --json state,mergedAt`)
- [ ] proof.md finalised (PR URL + merge date confirmed)
- [ ] Moved to final stage
- [ ] Outcome recorded in ticket body (PR link, follow-ups)
- [ ] cd out of worktree; `git worktree remove .worktrees/deliv-058`
- [ ] `git branch -d DELIV-058-architecture-assertions` (`-D` if squash/rebase-merged)
- [ ] `git fetch --prune` + `git worktree prune`
- [ ] `take_ticket action: "release"`

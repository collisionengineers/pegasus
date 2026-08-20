# Checklist — PLAT-014

- [ ] Update only the Windows branch of `Get-PegasusDatabaseState` to recognize the explicit, requested-instance LocalDB missing diagnostic while preserving all existing state, `Unknown`, Linux/Docker, and caller behavior.
- [ ] Add `scripts/Test-PegasusPlatform.ps1` assertions for zero-exit requested-instance missing, wrong-instance missing, unrelated output, Running, Stopped, and non-zero missing outcomes without mutating LocalDB.
- [ ] Add an always-run `windows-latest` CI job that explicitly invokes `./scripts/Test-PegasusPlatform.ps1`, without adding conditional change-classification plumbing.
- [ ] Run the focused PowerShell test plus the runbook's canonical locked restore, Release build, and non-corpus test commands; record exact results.
- [ ] Run the required reuse, simplification, efficiency, and altitude pass over the branch diff and append dated findings/dispositions to plan.md.
- [ ] From a clean committed checkout, record the pre-existing LocalDB inventory and complete Offline Doctor → Initialize → Start → Status → Smoke → exact-run Reset.
- [ ] Confirm the exact run directory and `PegasusDevelopment_<run-id>` instance are absent after Reset and every pre-existing LocalDB instance remains present.
- [ ] Write the post-implementation report with test/build/lifecycle evidence and exact run identity, keep progress current, and open the PR to `dev`.
- [ ] After independent review and merge, produce merged-source proof and hand [[PLAT-005]] back to its supported visual-capture lifecycle.

## Progress notes

(append with `set_ticket_doc(doc: "checklist", append: true)`)

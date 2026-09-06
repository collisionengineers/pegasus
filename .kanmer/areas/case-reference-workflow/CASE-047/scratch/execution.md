## Stream B startup checkpoint (claude-fable-b, machine CEALEX-May25, 2026-09-06)

- Working tree: primary checkout `C:/Users/Alex/Documents/GitHub/pegasus` had only Kanmer-managed dirty files (AGENTS.md managed block, `.agents/.grok/.opencode/skills` 0.4.0→0.4.1 refresh). Preserved, not discarded, as `git stash` "kanmer-0.4.1 managed skill/AGENTS refresh preserved before v1 casework (2026-09-06)". No product file was dirty. Untracked `pegasus_pack/` and `artifacts.zip` left in place, never staged.
- Fetched all remotes with prune. Local `dev` fast-forwarded 07ac7f1be → 3284f93fc3ea9fd3bbbea9405ec92dc7818378f2 (= origin/dev = planning pin D). Local `main` = origin/main = 32f8679d3695e0dcab8f310a1c20f8b129d20190 (= main pin). Both pins unchanged since planning.
- B worktree `../pegasus-worktrees/v1-casework`, branch `task/pegasus-v1-casework`, created at exact D (no upstream set). Clean.
- PR 670 refreshed from GitHub: head still `f22751cad3d5a713f39503ef48ff30422d67c97f` (OPEN, base dev, 8 commits, 42 files). No delta from the pinned tip, so no delta review is needed.
- No `task/pegasus-v1-platform` or `task/pegasus-v1-intake` branch exists on origin yet. PLAT-075 scratch/foundation.md: "no F SHA is published yet". Per COORDINATION, B does read-only source/PR inventory (B01) until F is reviewed, compiling and published, then `git merge --ff-only <F>`.
- Owner ticket docs (research/files/plan/checklist) are the pack contents supplied by Astra; the pack copy on this machine is `pegasus_pack/astra_output/v1_implementation_plans/`.

## Foundation F is validated and ready — proceed

F_SHA = dc3cfd908348b38d69b5d5357c6d5899bfe5f812

Fetch origin/task/pegasus-v1-platform and fast-forward your stream to that EXACT SHA now. It has passed required Foundation restore/build/ordered SQL/runtime-role/lease/architecture gates. The adoption handoff is already in plan/foundation-handoff on PLAT-075, CASE-047 and INTK-060. Do not continue waiting on candidate status: earlier candidate notes are superseded by this validation. G1 is a subsequent common correction for reviewed B/C/A requests, not a reason to postpone F adoption or independent domain work. Do not cherry-pick or merge A domain head. Record your exact FF consumption.

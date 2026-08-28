Implementation delegated to an external codex agent (gpt-5.6-sol, xhigh
reasoning) running inside the ticket's own worktree/branch. Orchestrating
Claude session verified independently: clean git status, single commit ahead
of origin/dev, pushed, diff confined to the three named files, build green,
focused test filter 10/10 green, out-of-scope CaseWorkScheduling.cs finding
confirmed real and correctly left untouched.

One judgment call surfaced during verification: the moved boundary logic is
not byte-identical to the deleted `OfficeBoundaries` on the two DST
transition days themselves (old code reused the current instant's offset for
midnight; new code resolves midnight's own offset). This is a correctness
improvement, disclosed in the post-implementation report rather than silently
shipped as "identical". Reviewer should confirm this is acceptable scope for
a "fix" profile ticket whose brief said "behaviour-preserving for the
dashboard".

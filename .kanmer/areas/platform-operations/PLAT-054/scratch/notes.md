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

## Round 2 — verifier remediation, 2026-08-29

Verifier verdict `needs-work`; 3 majors + 1 minor, all confirmed by re-running
the commands, all closed. Nothing rejected — the verifier was right on every
point.

Commit `3e16e506`. PR #611 body rewritten (via `gh api -X PATCH`, since
`gh pr edit` fails on this token: missing `read:project` scope).

Note for whoever picks up **PLAT-051** (Reports page): `LondonCalendar.ToUtcRange`
was withdrawn from this PR — it had no caller and was test-only code. Build the
half-open UTC period from `LondonCalendar.StartOfDay` + `StartOfNextDay`, the
way `EfCaseQueryStore.cs:79-80` and `:92` already do, or add the named helper
together with its caller in PLAT-051's own diff.

Follow-up filed: **PLAT-060** — the four remaining `Europe/London` lookups
(`CaseWorkScheduling.cs:80`, `EfCaseAcceptanceStore.cs:23`,
`EfLinkedCaseReplacementStore.cs:22`, `OperatorLabels.cs:650`), none of which
is in this ticket's `Owns` list.

Ticket left in `review`; no `proof` written (that is post-merge work).

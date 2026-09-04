## 2026-09-04 — execution paused: blocked by TOOLING NO-TOUCH vs same-diff grant rule

Took the ticket (branch `task/case-039-engineer-notes`, worktree
`.worktrees/case-039`, off `origin/dev` `ddbbc5e8c` which already includes
CASE-038's merge, PR #656). Ran the implementer (Codex gpt-5.6-sol, medium)
against the full packet (ticket body, plan, checklist, files, EPIC-012
context, RULES). It stopped before making any repository change — worktree
confirmed clean at `ddbbc5e8c`.

**The contradiction:** the ticket's own governing documents require the
migration, its grants, and the bootstrap census to ride the same diff
(CLAUDE.md rule 16; plan Step 3 "the migration, its grants and the bootstrap
census ride the same diff"; checklist item "`scripts/Invoke-AzureDatabaseBootstrap.ps1`:
add the `pegasus_web_runtime_role|G|SELECT|EngineerNotes` and
`|G|INSERT|EngineerNotes` census rows"; files.md lists it as a `change`
target). Confirmed by inspection: `scripts/Invoke-AzureDatabaseBootstrap.ps1`
currently has no `EngineerNotes` census entries (only `CaseValuations` at
line 397-402 as the modelled precedent).

But `scripts/Invoke-AzureDatabaseBootstrap.ps1` is a `scripts/*.ps1` file,
and the TOOLING NO-TOUCH rule — present verbatim in EPIC-012 context.md
§Build policy ("No lane other than UIIMP-015 edits ... `.github/workflows/ci.yml`
or `scripts/*.ps1`; a lane that needs such a change stops, reports it as
waiting, and the controller files a ticket") and independently restated in
this execution's own binding instructions — forbids editing any
`scripts/*.ps1` file and prescribes exactly this remediation: stop and
report waiting.

Adding the census rows without touching that file is not an option: it
would ship a migration/grant without its required bootstrap census entry,
violating rule 16 and leaving `Invoke-AzureDatabaseBootstrap.ps1`'s
exhaustive matrix stale — a real correctness gap, not a formality.

**No repository change was made.** The ticket stays taken (implementing) on
its recorded branch/worktree for a controller-directed resume once this is
resolved (either an explicit one-file exception for CASE-039 on
`scripts/Invoke-AzureDatabaseBootstrap.ps1`, or the controller filing/
routing the census-row addition through UIIMP-015).

**Resume point:** branch `task/case-039-engineer-notes`, worktree
`.worktrees/case-039` (clean, at `ddbbc5e8c4a3c81c647b5966a1137bf216702f6d`
= `origin/dev` tip including CASE-038). No commits made. Re-supply the same
packet (see this session's
`C:/Users/PC/AppData/Local/Temp/claude/C--Users-PC-Documents-GitHub-pegasus/3825d380-2360-40b3-a7ce-137884afb3c6/scratchpad/build/CASE-039/packet.md`)
once the scripts/*.ps1 conflict is resolved, and re-run from Step 3.

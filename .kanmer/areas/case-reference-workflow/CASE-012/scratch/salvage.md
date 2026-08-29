## 2026-08-29 — the parallel branch's refactor is NOT salvageable by cherry-pick

`task/case-012-case-workspace-parallel` @ `866fe459`
("refactor(ui): one section list, one Due rule, one editing flag on the Case
workspace model") is real, wanted work that never merged. The orchestration
session attempted to land it during the EPIC-011 closeout and **stopped**.

Cherry-picking `866fe459` onto `dev` at `55e23b02` conflicts on all seven of its
files, two of them fatally:

```
UU src/Pegasus.Web/Pages/Cases/Details.cshtml
UU src/Pegasus.Web/Pages/Cases/Details.cshtml.cs
DU src/Pegasus.Web/Pages/Cases/Shared/_CaseFiles.cshtml      <- deleted on dev
UU src/Pegasus.Web/Pages/Cases/Shared/_CaseHistory.cshtml
UU src/Pegasus.Web/Pages/Cases/Shared/_CaseSummary.cshtml
DU src/Pegasus.Web/Pages/Cases/Shared/_CaseVehicle.cshtml     <- deleted on dev
UU src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml
```

`_CaseFiles.cshtml` and `_CaseVehicle.cshtml` no longer exist on `dev` — PR #599
and PR #615 rewrote the Case workspace after this commit was written. The commit
refactors a version of the page that is gone. The cherry-pick was aborted and its
scratch branch and worktree removed; **no repository change was made** and the
original branch is untouched.

## The underlying finding still stands, and is still worth fixing

The duplication the commit set out to remove is still present on `dev`:

```
src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml:10
    (string Key, string Label, string Icon)[] sections =
```

The six-section list is declared inline in the partial rather than on
`DetailsModel`, so the page model and the nav partial each carry their own idea
of what the Case workspace's sections are. That is a "one list per concept"
breach by the repository's own rule.

## Who should fix it, and who should not

`waves.md` gives `_CaseWorkspaceNav.cshtml` to **lane E1 — this ticket
(CASE-012)**. Lane E2 (`CASE-027`) owns `Vehicle.*`, `Custody.*`, `Tasks.*`,
`_CaseDocuments` and `Documents/**` — **not** this partial. CASE-027 must not
touch it (AGENTS.md rule 2: never absorb another ticket's scope).

So: re-apply the *intent* — hoist the section list onto `DetailsModel` and have
the partial read it — as part of this ticket's own remaining work when CASE-012
is unblocked and re-proved in the rule-14 wiring wave. Re-write it against
current `dev`; do not try to reuse `866fe459`.

Once that is done, `task/case-012-case-workspace-parallel` carries nothing of
value and can be removed with its worktree.

# Research — DELIV-003: first fast-forward release transition

## Question

How can Pegasus converge the presently divergent shared branches without
rewriting either history, then make the first exact-SHA `dev` → `main`
promotion safely and with evidence?

## Findings

- [[DELIV-003]] is blocked by [[DELIV-002]], which is currently in Preparing.
  DELIV-002's plan deliberately places its policy, CI-guard, and test changes
  in a PR to `dev`, then hands the remote transition to this ticket.
- The 2026-08-18 read-only remote check matches the local tracking refs:
  `origin/main` is `2b0df78cd599cef9f273a8ae04ce3b7889c97f78` and
  `origin/dev` is `b763157a9f0a44f96560da1a6526454a822c8e7e`.
  Neither branch is an ancestor of the other; `main...dev` is `1 6`, with
  merge base `a6d801b4300c89b42fc696ff39846c273e210416`.
- The sole main-only commit is PR #394, `2b0df78`, with parents
  `e2020af` and `a6d801b`. Its tree equals its second (then-`dev`)
  parent. The divergence is therefore release metadata rather than unique
  main content, but Git still cannot fast-forward `main` to current
  `dev`.
- A non-rewriting convergence is necessary: a merge that has the current
  `dev` and `main` heads as parents makes the existing `main` commit an
  ancestor of the resulting `dev` head. A later non-force update of
  `main` to that exact `dev` SHA then makes the heads equal.
- At the current source revision, `docs/engineering.md` still specifies a
  PR merge commit for `dev` → `main`; the PowerShell guard and its CI
  invocation likewise require two-parent mainline commits. DELIV-002 must be
  observed merged into `origin/dev` before this ticket uses its replacement
  policy and ancestry guard.
- The current allowed-operation list in `AGENTS.md` permits merging
  `origin/dev` into a task branch, but not the required one-time merge of
  `origin/main` into the DELIV-003 branch. DELIV-002 must explicitly add
  that narrowly scoped convergence allowance; until it is present, this ticket
  must stop rather than invent an alternative Git operation.
- The repository workflow requires proof only after merged `main`, and the
  chore profile requires proof to enter Done. If “DELIV-002 completed” means
  Kanmer Done, waiting for it creates a cycle: DELIV-002 needs this ticket's
  first promotion to reach `main`, while this ticket would wait for
  DELIV-002 to be Done.
- GitHub branch protection and rulesets remain intentionally out of scope on
  subscription grounds. The revised main-push guard is detective; explicit
  `MERGE AUTH GRANTED` for the then-current refs is the authorization
  boundary for the `main` update.
- `AGENTS.md` and `docs/current-architecture.md` require current-state
  documentation to be refreshed after a release in the same task. The actual
  post-promotion facts must be inspected before deciding the precise
  `docs/operations.md` and `docs/current-architecture.md` update; they
  must not be prewritten from an assumed SHA.
- The root worktree has an unrelated modified `.codex/config.toml`. It is
  not this ticket's work and must remain untouched.

## Implications

- The safe predecessor is **DELIV-002 merged into `dev` with its PR CI
  green**, plus a read-back that its documentation explicitly permits the
  one-time convergence. Waiting for DELIV-002's final Done stage is
  impossible under the existing proof rule.
- DELIV-003 should use its own worktree and task branch from the then-current
  `origin/dev`. After the new policy permits it, merge the then-current
  `origin/main` into that branch without rebase, reset, or force; review the
  resulting merge and deliver it through the normal PR-to-`dev` path.
- After that PR is merged, fetch both remote refs again. Record the exact
  reviewed `origin/dev` SHA, prove `origin/main` is its ancestor, and only
  after explicit `MERGE AUTH GRANTED` for those exact refs push that SHA to
  `refs/heads/main` without force. Fetch again and require both remote heads
  to equal the recorded SHA.
- A rejected push, changed preflight ref, conflict, unequal post-push refs, or
  failed revised main-push CI is a stop condition. No rebase, reset,
  force-push, or GitHub settings change is an allowed recovery.
- Before closeout, refresh the current-state documentation from observed
  release facts and write proof on merged `main`.

## Open questions

- See `open-questions.md`: the requested meaning of “DELIV-002 completed”
  must be resolved before this ticket can be planned.

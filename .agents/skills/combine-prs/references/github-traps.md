# GitLab and CI traps

Each of these cost an earlier combination run time or correctness.

## CI coverage

- **A conflicting MR still gets a pipeline**, on the branch head, so a pipeline
  is not evidence that the MR merges. Check
  `glab mr view N -F json` for `has_conflicts` and `detailed_merge_status`
  (`conflict`), not only the pipeline status.
- **Individually green is not jointly green.** Each MR's pipeline tested its own
  head. In one case MR A removed an interface member that MR B (merged first)
  had started using. Both MRs were green, and the target stopped compiling once both
  landed. Only a pipeline on the combined tip is evidence for the combination.
- **A green descendant covers its ancestor's code**, so a stacked parent does not
  need its own rerun when a child containing it is green
  (`git merge-base --is-ancestor parent child`). It does not prove that the parent
  merges cleanly on its own; `has_conflicts` answers that.
- **Some repos run no CI on pushes** to the integration or target branch, only on
  MRs. The combined MR's pipeline is then the only union evidence. Do not plan on
  "CI will run after the merge".
- **Path-selected lanes** run only the jobs the changed paths need. A skipped job
  is not evidence. Read which jobs actually ran, including the child pipeline's.
- **A pipeline can run for an hour.** CI shares one self-hosted runner, so a full
  code pipeline takes roughly 45 to 60 minutes and pipelines queue. A pipeline
  that is superseded or stuck is cancelled with `glab ci cancel pipeline <id>`.

## Watching checks

- `glab ci status --wait` waits until the pipeline finishes. Report the **first**
  failure as soon as it appears instead: poll
  `glab api projects/:id/pipelines/<id>/jobs` (and the child pipeline's jobs) and
  look for `status == "failed"`.
- Do not assume `jq` exists (Git Bash on Windows often has none). Use `glab --jq`
  or Python.
- A job log is readable while the job is live:
  `glab api projects/:id/jobs/<job-id>/trace`, or `glab ci trace <job-id>`.
- Arm any background poller only after running its command once by hand.
  A poller that dies on a missing tool looks the same as one that is still waiting.
- Editing an MR's description starts no pipeline. After a change that CI reads
  from the description (the `## Migration` section), start one with
  `glab api -X POST projects/:id/merge_requests/<iid>/pipelines`.

## Merging and MR state

- **Squash and rebase merges break inclusion.** They create new commits, so the
  constituent MR heads are not ancestors of the target, and nobody can prove from
  history that their commits landed. The project forbids squash. Land the combined
  MR with a merge commit, or fast-forward the target to the tested tip.
- **Check each constituent's state after landing** (Landing step 3). An MR based on
  another branch (a stack) stays open; source branches are not deleted on merge,
  so it keeps targeting its base branch.
- **Retargeting a stacked MR after the fact can be refused** because everything is
  already in the target. Close it with a comment:
  `glab mr note create N -m "Merged into <target> via !M (<merge sha>); head <sha> is an ancestor."`
  then `glab mr close N`.
- **Never close the originals before the combined MR lands.** They are the
  fallback if the combination is abandoned. Closed-unmerged MRs also read as
  rejected work.
- **Closing keywords** (`Closes #12`) only fire when the MR merges into the
  repository's **default branch** (`main`). For any other target, list the issues
  and close them after landing, with the evidence (Landing step 4).
- **`glab mr issues N` is empty for an MR into `dev`.** An MR body that merely
  mentions `#12` ("raised from #12", "see #12") links nothing either. Read the
  `Closes #N` lines from the description, grep for `#\d+`, and ask the user which
  of the unkeyworded ones the MR resolves.
- **Protected branches and "pipelines must succeed"** on the target block the
  combined MR until its pipeline is green, even when every constituent was
  green. Check `detailed_merge_status` and `head_pipeline.status` in
  `glab mr view M -F json` and tell the user what is missing. Unresolved
  discussions on a constituent are the review signal; there is no change-request
  state.

## Local git

- `git branch X origin/Y` and `git worktree add -b X <path> origin/Y` set `X` to
  track `origin/Y`. A later bare `git push` or `git pull` then targets the wrong
  branch. Use `--no-track`, or run `git branch --unset-upstream` straight away.
- Pin the target and every head as SHAs. Branch names move while you work
  (another session pushes a fix), and a moved head means your audit tested
  something other than what you merged.
- Build in a dedicated worktree. The main checkout may hold another session's
  uncommitted edits.
- Write any conflict-resolution scripts to a file, not into heredocs with escape
  sequences (`\b` becomes a backspace byte). Afterwards inspect the bytes you changed.

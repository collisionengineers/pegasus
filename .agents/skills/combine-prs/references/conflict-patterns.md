# Conflict patterns

Read this when a real merge or the simulation reports a conflict. The aim of
every resolution is the **union of intents**: each PR's change survives unless
another PR in the set deliberately replaces it. When that happens, the
replacement is named in the merge commit and in the PR body.

## First, read both intents

For a conflicted file during `git merge`:

```bash
git diff --diff-filter=U --name-only            # files still conflicted
base=$(git merge-base HEAD MERGE_HEAD)
git diff $base HEAD       -- <file>              # what the branch so far did
git diff $base MERGE_HEAD -- <file>              # what the incoming PR did
git log --oneline $base..MERGE_HEAD -- <file>    # why (commit subjects)
```

Look for the reason behind each side's hunk in the commit message, the PR body
and any review thread on that file before you choose. A conflict marker shows
where the texts collide, not what each author meant.

## Patterns

| Situation | Resolution |
|---|---|
| Both sides add entries at the same point of a list, enum, switch, DI registration, route table or label set | Keep both. Keep the list's own order (sorted, by id, by timestamp). Check separators (trailing commas, `;`). |
| Pinned or ordered lists that tests compare exactly (migration chains, snapshot lists, ordered fixtures) | Keep every entry, ordered by the list's key (usually a timestamp). If another pinned copy of the same list exists elsewhere, update it too, or CI fails later. |
| Both sides claim the same sequence number (ADR-0059 twice, migration 042 twice, error code) | Keep the earlier PR's number. Renumber the later one everywhere it is referenced (file name, title, links, index row). Report the renumbering to the user and in the PR body. |
| Both sides claim the same register row id (`docs/rulings.md` R550 twice) | Keep both rows, finish the merge commit, then run `scripts/renumber_register.py --head <sha> --target <sha> --label N` for the later PR and commit its edits. It shifts that PR's new ids to the next free numbers and rewrites the citations on that PR's own added lines in every file, nothing else. Paste its summary into the PR body. Run the audit with `--id-pattern` so those lines read as renumbered, not lost. |
| One side rewrote the file (large restructure) while the other made small edits | Start from the rewrite. Read the small side's diff hunk by hunk and re-apply each intent in the rewritten structure. Do not try to merge line by line. Afterwards confirm each small-side intent is present (the audit's missing-line list helps). |
| One side deleted or renamed a symbol, file or option that the other side uses or edits | Do not pick a side silently. If the deleting PR's purpose clearly covers the other's usage (for example, a feature was removed on purpose), port the user to the replacement or drop it, and record that. If the purposes conflict, **stop and ask the user**. |
| Modify/delete conflict on a whole file | Same as above. Find out whether the delete was a move (look for the content elsewhere with `git log --follow` or a grep for a distinctive line) and apply the modification at the new location. |
| Both sides changed the same lines for different reasons | Write a line that satisfies both intents if one exists (for example, both conditions in a guard). Otherwise this is a product decision, so ask. |
| Both sides made the same change | Take it once. |
| Lockfiles (`package-lock.json`, `yarn.lock`, `poetry.lock`, `Cargo.lock`, `packages.lock.json`) | Take one side, then regenerate with the package manager so the union of declared dependencies resolves. Never hand-merge a lockfile. |
| Generated files (compiled assets, snapshots, model snapshots, codegen output) | Regenerate from the merged sources with the project's generator. Hand-merge only when the repo has no generator, and say so. |
| Docs, changelogs and indexes | Keep both entries. Make the combined text read as one document. |

## Mechanics

- **Line endings.** Preserve the file's existing line endings. Before and after a scripted edit, compare the CR count (`grep -c $'\r' file`) with the version on either side. Editors and `sed -i` on Windows checkouts (`core.autocrlf=true`) silently convert. Prefer the Edit tool over ad-hoc `sed`/heredoc rewrites for conflict files.
- **No wholesale side-picking.** `git checkout --ours/--theirs <file>`, `-X ours`, `-X theirs` and `git merge -s ours` each discard one PR's work in that file. Use them only for a lockfile or generated file you then regenerate.
- **No marker leftovers.** Before committing, run `git diff --check` and `git grep -nE '^(<<<<<<<|=======|>>>>>>>)( |$)' -- <files>`.
- **Commit message.** Finish the merge with a message that lists each resolution:

  ```
  Merge PR #1016 (task/inbox-composer) into merge-1016-1020

  Conflicts resolved:
  - EfCaseQueryStore.cs: #1018 added an origin-receipt parameter, #1016 split
    received/sent reads; kept both, the origin receipt now feeds the received read.
  - _TriageCase.cshtml: #1018 added a Reconcile form; #1016 removed Reconcile
    everywhere, so the form is dropped (intent of #1016).
  ```

  The same notes go into the PR body's "Merge resolution" section. The audit's
  FAIL and REVIEW lines should each match one of them.

## After resolving

Re-run `audit_combined.py`. Every "missing line" and "line written only in a
merge resolution" must correspond to a resolution you recorded. Anything else is
work you lost by accident.

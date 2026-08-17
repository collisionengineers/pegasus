## Verification attempt — 2026-08-17

Verification cannot be completed on merged `main`.

Read-only Git evidence after `git fetch origin --prune`:

- BUG-001 merge commit: `03ce5a715fc633e80703f6711d8edfeb40a69b13`.
- `origin/dev`: `03ce5a715fc633e80703f6711d8edfeb40a69b13`; the ticket merge commit is an ancestor.
- `origin/main`: `e2020af1129e816afdd18fa4c498392e802c0d34`; the ticket merge commit is not an ancestor.
- The repository checkout is on local `dev` and was three commits behind `origin/dev`; it was not switched or mutated.
- Ticket state remains `verifying`; the enter-Done gate still requires `proof`.

The repository workflow requires proof to be written from merged `main`. A `dev` → `main` merge requires the operator's literal `MERGE AUTH GRANTED`, which has not been given. No merge, deployment, proof document, or Done transition was performed. Existing PR #386 CI and review evidence remains valid for the merged `dev` change, but is not represented as release/main proof.

## Main release attempt — 2026-08-17

The operator supplied the literal `MERGE AUTH GRANTED`.

- Opened release PR #394: https://github.com/collisionengineers/pegasus/pull/394
- GitHub initially reported `dev` conflicting with `main` at `.gitignore`.
- Reconciled by merging `origin/main` into `dev` without rewriting history; merge commit `4773ab9e0f78083aa08f4792ad07d2a58e13bb02`.
- Conflict resolution retained the accumulated dev ignore rules and normalized the main worktree rule to `/.worktrees/`.
- Pushed `dev`; PR #394 became mergeable and CI started.
- CI run `32036229676` documentation job `95407153250` failed. The Markdown-placement validator reports new Markdown outside the authorized PRD/FRD/ADR/registered-workspace paths, including files under `.design-sync/`, `.grok/`, `.stitch/`, `design/planning-and-old-designs/`, `docs/design/`, and `docs/design/system/`.
- This failure is accumulated release content unrelated to BUG-001. The validator was not weakened and other tickets' files were not deleted or moved.
- At last observation: changes, reference-data, infrastructure, and source-workspaces passed; documentation failed; unit/browser/three SQL shards were still pending.

PR #394 remains open and unmerged because repository policy requires green CI. Consequently BUG-001 is still absent from `main`; no `proof.md` or Done transition is valid yet. No deployment was performed.

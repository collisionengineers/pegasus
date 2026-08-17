## Verification attempt — 2026-08-17

Verification cannot be completed on merged `main`.

Read-only Git evidence after `git fetch origin --prune`:

- BUG-001 merge commit: `03ce5a715fc633e80703f6711d8edfeb40a69b13`.
- `origin/dev`: `03ce5a715fc633e80703f6711d8edfeb40a69b13`; the ticket merge commit is an ancestor.
- `origin/main`: `e2020af1129e816afdd18fa4c498392e802c0d34`; the ticket merge commit is not an ancestor.
- The repository checkout is on local `dev` and was three commits behind `origin/dev`; it was not switched or mutated.
- Ticket state remains `verifying`; the enter-Done gate still requires `proof`.

The repository workflow requires proof to be written from merged `main`. A `dev` → `main` merge requires the operator's literal `MERGE AUTH GRANTED`, which has not been given. No merge, deployment, proof document, or Done transition was performed. Existing PR #386 CI and review evidence remains valid for the merged `dev` change, but is not represented as release/main proof.

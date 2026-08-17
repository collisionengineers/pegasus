# Shared constraints

- The active UI revamp owns all Web presentation work.
- Do not edit `src/Pegasus.Web/**`, UI-focused browser/snapshot tests, `design/**`, or `.stitch/**`.
- Each ticket must re-check its file inventory against the current UI-revamp changes before implementation.
- Work is limited to CI workflows, repository validation scripts, non-UI tests, and their governing documentation.
- Keep one ticket, branch, worktree, and PR per repository workflow.
- PRs target `dev`; merging `dev` to `main` requires separate explicit `MERGE AUTH GRANTED`.

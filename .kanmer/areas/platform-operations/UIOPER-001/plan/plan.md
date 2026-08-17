# Plan: UIOPER-001 Remove self referential dashboard link

## Approach
1. In the worktree `.worktrees/UIOPER-001`, edit `src/Pegasus.Web/Pages/Index.cshtml` to remove lines 114-119 (`<nav class="drilldowns">`).
2. Remove `.drilldowns` styles from `src/Pegasus.Web/wwwroot/css/site.css`.
3. Validate build and run test suites to ensure all tests pass.
4. Fast-forward / commit changes directly to `dev` as instructed.

## Governing Docs
- Satisfies UI design authority (no broken / self-referential links).

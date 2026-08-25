# Plan

- Create `task/tick-222-reconcile-mcp-config` from the reviewed `origin/dev` head.
- Merge `origin/main` without force or history rewriting. The expected net addition is only the two `C:\Users\PC` Kanmer path updates from `c9005efb`.
- Confirm the MCP command, board `--root`, and repository `--repo-root` remain exactly correct; run the documentation/config CI checks relevant to the two files.
- Push one branch, open one PR to `dev`, obtain independent review and green CI, then merge. Rerun exact-SHA release ancestry preflight.

No product code, deployment configuration, schema, compatibility path, or new abstraction is in scope.

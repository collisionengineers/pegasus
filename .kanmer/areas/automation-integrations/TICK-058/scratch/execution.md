## Transitions

- 2026-09-02T17:57:36.313Z stage verifying → preparing by codex-mcp-client; reason: proof FAIL plan: the recorded Markdown-placement check uses mutable origin/dev as its base; at exact merged SHA 0d985c9e0b3284f211f824d387e2f36460c0c826 it now reverse-diffs later unrelated removals and fails. Bind the check to an immutable integration base before fresh verification.

- 2026-09-02T18:03:00Z plan-only verification remediation: released the unusable legacy missing-worktree claim after confirming its original PR was merged, then took fresh workspace `.worktrees/tick-058` on `TICK-058-verification-plan-remediation` from `origin/dev` at `cad00be9d42dbeaee9edf34c2d24de222d7ddb9d`. No repository file changed. The only change is the plan's immutable Markdown-placement base `23b0c564c81bf8a0665bc5a65f3f54d88010f835` for exact merge `0d985c9e0b3284f211f824d387e2f36460c0c826`; planning fact-check exit 0. Hand off to independent review of the corrected verification authority and unchanged merged PR #594.

## Independent review blocker — 2026-09-02

PR #594 thread `PRRT_kwDOThBrk86dJ3SK` remains open-major. At exact source head `8ef4775c5fa00accafabe7ed9df44f1d7e5593d3`, `ProcessIntake` returns the Provider API `DeclaredAssessment` before `EvaluateIntakeCaseMatch`, so a repeated declared instruction can allocate a second Case. FRD-09 requires Provider API instructions to use the same Core intake policy and case-creation path as equally definitive email instructions; FRD-02 prohibits a duplicate when there is a definitive existing-Case match. However, the accepted QDOS matcher is mail-route-shaped and no governing text explicitly authorises treating a Provider API declaration's Principal/claim/VRM as the route-specific match evidence. AUTO-013 explicitly required an operator answer and left this unchanged. The durable auto run is paused without changing code or resolving the thread pending that exact product-policy decision.

- 2026-09-02T18:34:50.602Z stage review → implementing by codex-mcp-client; reason: operator: API-01 is create-only. A definitive match to an existing Case must be rejected without updating that Case and without allocating a duplicate; existing-Case updates are deferred to AUTO-017.; review_round 1

- 2026-09-02T18:39:23.834Z lease-phase implementing → running-command (lease 52f7ffbd-c4b4-4d08-9b0a-ce5414b26f13 rev 3; expires 2026-09-02T20:09:23.830Z)

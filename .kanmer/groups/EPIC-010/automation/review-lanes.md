# Review / verify / closeout lanes — run 2026-08-27T11-23Z-claude-code

`2026-08-27T16:50Z` — operator instruction: "use subagents for reviews, that's independent". Review lanes run `kanmer:kanmer-review`; verify lanes run `kanmer:kanmer-verify` at the PR merge SHA; closeout lanes run `kanmer:kanmer-closeout`.

| Lane | Ticket | PR | State | Notes |
| --- | --- | --- | --- | --- |
| R1 | INTK-044 | #572 | passed; merged 935d58ff | finding → INTK-045 |
| R2 | MAIL-019 | #573 | passed; merged be507faf | 3 notes dispositioned |
| R3 | MAIL-017 | #571 | passed; merged 61d80539 | flaky shard re-run once → green |
| R4 | MAIL-021 | #575 | passed; merged 86113ea1 | finding → MAIL-022 |
| R5 | MAIL-020 | #576 | passed; merged 14c6fd41 (17:45Z) | first CI run hit the stale-merge-ref checkout hang + the flaky SQL shard; one close/reopen → all 11 jobs green; 3 minor/note findings dispositioned (docs wrap; `docs/current-architecture.md:163-170` still describes the workspace-quota diagnosis — must be refreshed by the provisioning release) |
| V1 | MAIL-019 | #573 | PASS → done | live liveness block passed |
| V2 | INTK-044 | #572 | PASS → done | code-level; live proof at next release |
| V3 | MAIL-021 | #575 | PASS → done | proof v e16036d66a611366 |
| V4 | MAIL-017 | #571 | PASS → done | migration head correct; prod `__EFMigrationsHistory` still 20260826151807 (undeployed) |
| V5 | MAIL-020 | #576 | PASS → done (proof v 42fe2dcc299157b2) | live caps still 0.1 GB; cap raise = operator billing approval at next release |
| C1 | MAIL-019 | — | closed out | — |
| C2 | INTK-044 | — | closed out | — |
| C3 | MAIL-021 | — | closed out | — |
| C4 | MAIL-017 | — | closed out | — |
| C5 | MAIL-020 | — | closeout running (dispatched 17:55Z) | — |
| — | MAIL-018 | — | controller serial build+suite running (pid 26192) → PR → R6 → V6 → C6 | snapshot commit 47ebad54 scoped to one page; snapshot staleness → MAIL-023 |

Rules: reviewers merge to `dev` only when the review passes and every required check is green; never touch `main`; verifiers use a disposable detached worktree at the exact merge SHA; Verifying → Done only on a truthful PASS.

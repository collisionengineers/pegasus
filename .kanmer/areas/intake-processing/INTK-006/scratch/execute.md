Execution not started: ticket is blocked by [[INTK-005]], which is now in Review at PR #416 and must merge before this worktree can consume the durable group relation. Plan also requires kanmer-docs reconciliation because current FRD/operator notes contradict the confirmed Image-Only Case fallback. No ticket claim or code changes made.

Execution base confirmed: worktree `.worktrees/intk-006`, branch `intk-006-grouped-image-routing`, based on INTK-005 PR branch `intk-005-grouped-upload` at SHA `ed04f498` (PR #416). INTK-005 review may change the base; after review, rebase this branch onto the resolved INTK-005 result and reconcile any conflicts before merge.

Opened PR #417: https://github.com/collisionengineers/pegasus/pull/417. Commit 70d7c89c. PR is based on INTK-005 PR branch SHA ed04f498; rebase onto the reviewed INTK-005 result before merge. Review boundary explicitly calls out the unresolved authorized Image-Only Case principal/reference contract.

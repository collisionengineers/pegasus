## 2026-08-25 implementation start

Prerequisites INTK-041, INTK-040, and INTK-003 are merged. Ticket taken on `task/intk-042-immediate-publication` at `../pegasus-worktrees/intk-042-immediate-publication`, based on merged dev `7dbb7c39`.

Post-merge code inspection settled the smallest exact shape:

- Add exact staged-receipt and external-work dispatch claims; immediate and recovery paths share one claim/enqueue/mark/release implementation. Exact selection prevents old backlog starving new committed work.
- Wrap `ReceiveIntake` with an Infrastructure `IIntakeSubmission` decorator that awaits the durable commit, best-effort publishes the exact staged receipt, records failure, and still returns the committed result. `SubmitGroupedIntake` already consumes the interface; change mailbox polling from concrete `ReceiveIntake` to the interface.
- Publish custody work after commit from the three stores that create it and already know the work id: `EfCaseAcceptanceStore`, `EfLinkedCaseReplacementStore`, and the register/merge paths in `EfImageIntakeStore`. Never publish inside the transaction or scan unrelated vehicle work.
- Move the two Azure queue sender adapters and neutral queue-client/provisioning types to Infrastructure; compose the same senders in Web and Worker. Worker retains queue processing.
- Web gets sender-only RBAC on the two queue resources plus one queue service endpoint; Worker keeps contributor. Recovery schedules become one minute. No always-ready change and no deployment.

No product files have been edited yet in this worktree; this checkpoint avoids leaving a partial cross-layer implementation.

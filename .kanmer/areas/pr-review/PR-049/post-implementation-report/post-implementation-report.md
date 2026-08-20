# Post-implementation report

Resolved in `6b7c62a4` on PR #490. Definitive post-acquire association refusals release through the existing Case lease port with `CancellationToken.None`; unknown outcomes retain the exact prepared confirmation. SQL/Web proof forces a stale receipt after acquisition, observes no association/history and no lease token, then immediately reacquires authority. Successful link/unlink paths also prove canonical lease consumption. No Core/EF/schema/recovery framework changed.

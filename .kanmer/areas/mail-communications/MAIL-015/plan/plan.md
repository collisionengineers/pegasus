# Plan

1. Replace the invalid seven-field Inbox recovery schedule with the valid six-field five-minute NCRONTAB expression in its existing owners and assertions.
2. Run focused deployment-plan, smoke-contract and diff validation.
3. Review, merge, promote and provision the corrected setting; do not rebuild or redeploy unchanged application packages.

## Simplification pass

The fix changes the existing values only; no abstraction or compatibility path.

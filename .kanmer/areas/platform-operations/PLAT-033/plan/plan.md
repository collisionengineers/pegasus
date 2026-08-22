# Plan

Committed in `3d7f87d6`.

"Approved inbox" describes how the system is configured. The operator sees a case that
arrived by e-mail, so Origin reads **E-mail**.

Both overloads changed together. No test asserted either string — checked before changing.

## Acceptance

- A case created from a mailbox message shows Origin **E-mail**. ✅ (both overloads agree)
- The sibling channel labels were reviewed and needed nothing. ✅
- Live: the case Origin row — Phase 6.

## Simplification pass

2026-08-22. One label, one meaning, two call sites kept in step. No findings deferred.

# Plan

1. Reuse `IReleaseCaseEditLease` after definitive final association refusal.
2. Use `CancellationToken.None` for compensation; retain the same confirmation for uncertain outcomes.
3. Prove stale post-acquire failure clears authority and immediate retry can acquire; prove successful commands consume leases.
4. Add no store, worker, lease framework or schema.

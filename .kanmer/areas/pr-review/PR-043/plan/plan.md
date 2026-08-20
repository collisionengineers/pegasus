# Plan — PR-043

## Chosen approach

Keep the existing dedicated operation state vocabulary and filtered index. A matching Pending replay is refused as still processing without probing or updating the row. The original provider-exception path first persists the existing row as Uncertain, then uses the existing probe recovery. Only a row already in Uncertain is replay-probe eligible.

## Governing docs

`docs/frd/frd-08-email-mailbox-and-background-processing.md` requires deliberate duplicate-safe retries and visible recoverable provider failures. Retaining the active slot while Pending prevents duplicate movement; persisting Uncertain after the provider result makes the existing explicit status check safely recoverable. No ADR or governing-doc change is needed.

## Steps

1. Split matching replay handling in `EfRetainedMailFolderMoveStore`: Pending throws the focused still-processing error; Uncertain alone calls `RecoverAsync`.
2. On provider exception, save Outcome=Uncertain and FailureReason before calling `RecoverAsync`; reuse the same row/index/probe.
3. Add one deterministic LocalDB test that overlaps the original blocked provider call with a same-key replay and a different-key attempt. Assert no replay probe, one provider move total, one active row, new-key refusal until original completion, and successful same-key replay afterward.
4. Run focused persistence plus existing uncertain Web recovery, Release build and proportional checks. Apply the four simplification lenses and update TICK-049/PR-043 reports, checklist and traceability.

## Risks

- A Pending row left by a process crash cannot be safely distinguished from a live call without a lease. This ticket deliberately refuses it rather than guessing; provider exceptions now transition to Uncertain before recovery, covering the known result path without adding a lease framework.
- Concurrent Uncertain probes may duplicate reads but never the external move; this is unchanged and outside the blocker.

## Simplification pass — 2026-08-20

- **Reuse:** Kept the existing Pending/Uncertain outcomes, filtered active index, operation row, exception surface, probe recovery and BlockingFolderMover fixture.
- **Simplification:** Split one combined replay condition and added one durable outcome save; no lease, timer, worker, wrapper, flag, new state, endpoint or generic framework.
- **Efficiency:** Pending replay now performs zero provider reads/writes. Only the original call moves; Uncertain recovery remains a parent probe only.
- **Altitude:** Infrastructure owns external-operation lifecycle; Core/Web contracts remain unchanged.
- **Applied findings:** Pending is refused as still processing; provider exception persists Uncertain before recovery; exact overlap evidence covers same-key, new-key and completed replay.
- **Unapplied findings:** none.

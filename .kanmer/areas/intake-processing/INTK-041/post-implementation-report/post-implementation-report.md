# Post-implementation report

## Result

INTK-041 now defines one authoritative target for near-real-time durable e-mail and manual-upload intake. Tight polling is no longer the normal trigger in the target state: work publishes immediately after its durable commit, Graph basic notifications wake mailbox delta processing, and slow timers remain recovery only. Web stays callback-only and Worker remains the sole mailbox/intake processor.

## Files changed

| File | Change and rationale |
| --- | --- |
| `docs/prd/pegasus-product.md` | Added the truthful near-real-time product outcome, ten-second ordinary p95 target, durable recovery requirement, and seven-day idle Function cost guardrail. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Added commit-before-publication, immediate identifier publication, one-minute loss recovery, correlated stage timing, and truthful transient/terminal state rules shared by email and manual upload. |
| `docs/frd/frd-08-email-mailbox-and-background-processing.md` | Added Graph callback validation, identifier-only wake messages, SQL subscription state, protected clientState, six-hour maintenance, 48-hour renewal margin, lifecycle/delta recovery, five-minute fallback, and neutral unresolved sender. |
| `docs/adr/0032-near-real-time-durable-intake-triggering.md` | Recorded the single architectural decision: immediate durable publication plus Graph wake-up with recovery timers and scale-to-zero default. |
| `docs/adr/0002-dotnet-modular-monolith-on-azure.md` | Marked only polling/timer-first triggering as partially superseded; the remaining modular-monolith decision stays accepted. |
| `docs/adr/README.md` | Indexed ADR-0032 and made ADR-0002's partial supersession visible. |
| `docs/capabilities.md` | Registered INT-33, updated planned/alpha counts to 203/132, and linked the canonical FRD sections. |

## Governing-doc traceability

The PRD owns the intended outcome and quality/cost boundaries. FRD-02 owns the shared intake state and dispatch contract. FRD-08 owns mailbox notification/subscription behaviour. ADR-0032 owns the technical trigger choice. The capability registry contains only allocation and canonical links.

## Verification

- ADR-0032 frontmatter id/status checked.
- Capability registry recounted: 203 planned and 29 not planned.
- All changed governing files exist.
- `git diff --check` passed (line-ending conversion warnings only).
- Focused docs-only simplification pass found no duplicate policy owner or unauthorized runtime implementation.

## Risks and follow-ups

Implementation and deployed proof intentionally remain in [[INTK-003]], [[INTK-042]], [[MAIL-013]], [[INTK-001]], [[INTK-043]], [[PLAT-036]], and [[DELIV-021]]. The claimed [[INTK-040]] worktree was not touched. No Azure or mailbox state changed.

## Verify after merge

On merged `dev`, confirm the PR merge SHA contains ADR-0032 and INT-33, rerun the capability count/frontmatter checks and `git diff --check` equivalent, then link ADR-0032 into the ticket refs. Production verification remains DELIV-021.

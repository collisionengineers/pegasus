# Plan — warm unified intake and custody route

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: retain one Core-owned, durable, fail-closed route for both e-mail and upload.
- `docs/frd/frd-05-documents-extraction-and-custody.md`: custody remains complete, immutable and idempotent.
- Create a thin ADR superseding the scale-to-zero critical-path portion of ADR-0032; update the intake PRD/FRD with the five-second best-effort total and provider attribution.

## Approach

Use one typed queue and one warm queue-trigger function for mailbox-discovered and manually-uploaded work. A normal process message performs reading, identification, classification, extraction, case allocation and Box custody in one invocation. Retry/recovery stays durable but is not a normal hop. Reuse the existing Core processors and Infrastructure ports; do not create a second implementation.

## Steps

1. Add a fixed correlated stage vocabulary and explicit timing around queue claim, reading, retention, allocation and each custody operation. Configure telemetry so required performance spans and exceptions are not sampled, without recording content.
2. Replace the normal intake/external queue hand-off with the typed unified dispatcher. Preserve commit-before-publication, leases, idempotency and recovery; run ordinary custody inline.
3. Preload the existing image model in the warm worker. Configure exactly one always-ready 2 GB instance for the unified function in Bicep; keep scale-out for bursts.
4. Benchmark every supported repository input cohort, before and after, with p50/p95/p99, queue wait, stage breakdown, retries and provider attribution. Apply source-reader/hash/copy or EF changes only where the new trace proves a material bottleneck.
5. Bound concurrent Blob retention and Box uploads after folder creation. Preserve ordinal names, all required assets, cancellation, retry behaviour and exactly-once custody outcome.
6. Update governing and as-built documentation. `MAIL-013` subsequently replaces regular mailbox polling with Graph wake-up plus a five-minute recovery poll; `INTK-001` corrects the observable sender/retry state.
7. Run focused and full verification, simplify the branch diff, then report, commit, push and open a `dev` PR.

## Acceptance evidence

- Manual uploads and mailbox-discovered work use the same Core route and have no normal custody queue delay.
- Pegasus-controlled p95 is at most five seconds per supported input cohort. Total Outlook-arrival-to-confirmed-Box-custody p95 is reported separately; a provider-only miss is explicit rather than hidden.
- All existing integrity, classification, extraction, case-allocation and custody tests remain green.
- Deployment verification checks the exact unified function’s always-ready configuration, telemetry receipt and a genuine manual-upload/mailbox cohort.

## Risks

- Graph and Box can exceed the total target; stage attribution prevents false Pegasus blame.
- Parallel asset work can affect custody ordering; preserve assigned ordinal/name before starting concurrent transfers.
- 2 GB has one CPU; move to 4 GB only if post-change traces meet the documented CPU threshold.

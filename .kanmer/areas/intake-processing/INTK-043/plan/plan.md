# Plan — measure and reduce ordinary intake processing latency

## Chosen approach

Instrument the existing shared queued-intake route first, establish a representative baseline after INTK-041/INTK-042 land, and optimize only the measured dominant stage. The observed 17-second interval is not evidence that the source reader alone is slow: it includes Blob download/hash/promotion, identity lookup, MIME/PDF reading, sequential asset retention, assessment persistence, and work completion.

Reuse the existing Activity/Application Insights path, immutable QDOS fixtures, single `ProcessQueuedIntake -> ProcessIntake -> IIntakeSourceReader` route, and current correctness suites. Add no queue, worker, cache, reader implementation, or fabricated domain fixture.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: implement correlated stage timing and ordinary ten-second p95 while preserving durable custody, fail-closed gates, idempotency, bounded processing, and truthful Processing for large/retrying inputs.
- ADR-0032 after merge: measure the path after immediate publication so recovery/queue delay is not misattributed to processing.
- `docs/current-architecture.md` and `docs/operations.md`: record only the as-built telemetry/optimization and clearly separate local evidence from deployed proof.

## Ordered steps

1. Wait for INTK-041/INTK-042 and overlapping INTK-040 work; create a fresh worktree from `origin/dev`.
2. Add stable, low-cardinality correlated stages around staged download/integrity, durable promotion, identity lookup, source read, asset retention, assessment/receipt persistence, association/allocation, and completion.
3. Add tests preventing source content/high-cardinality tags and proving correlation/outcome coverage.
4. Build a repeatable local evidence command over repository-provided ordinary QDOS e-mail and manual-upload fixtures; report sample count, p50, p95, worst, channel, and size class, separating retries/cold starts/large inputs.
5. Name the dominant stage from evidence before changing it. Revise this plan/checklist with the exact measured optimization; if no stage justifies a safe change, stop and report the evidence rather than speculate.
6. Apply the smallest optimization in the existing owner, preserving byte integrity, content-addressed immutability, MIME/PDF bounds, attachment order/evidence, and business outcomes.
7. Re-run the baseline and correctness suites; require ordinary local p95 improvement consistent with the ten-second end-to-end budget and no semantic regression.
8. Update as-built docs, run Release/full relevant validation and simplification lenses, then report/commit/push/open the PR.

## Proof

Before/after artifact records exact command, fixture set, sample count, p50/p95/worst and stage distribution for both channels. Correctness suites prove unchanged sender/classification/extraction/custody/allocation outcomes and truthful large-input Processing. Production p95/cost/cold-start proof remains DELIV-021.

## Risks and mitigations

- **Optimizing a guess:** measurement is a hard plan checkpoint.
- **Telemetry leakage/cardinality:** stage vocabulary is fixed; only stable ids/bounded outcomes, never content.
- **Semantics regression:** reuse genuine fixtures and existing QDOS/mailbox/upload suites.
- **Misleading local percentile:** label it local and require deployed proof separately.
- **Scope growth:** optimize one proven stage only; file unrelated findings as tickets.

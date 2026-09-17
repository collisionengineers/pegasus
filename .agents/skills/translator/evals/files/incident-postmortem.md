# Postmortem: INC-2291 — Elevated 5xx rates on checkout-api (2026-09-02)

**Severity:** SEV-2 · **Status:** Resolved · **Author:** checkout on-call · **Reviewers:** platform, SRE

## Summary

Between 14:07 and 14:45 UTC on 2026-09-02, checkout-api experienced elevated 5xx error rates (peak 31% of requests, sustained >10% for 38 minutes) following a rolling deploy of release 2026.09.02-r3, which introduced a change to the pricing-service client's connection pool configuration. Approximately 22% of checkout attempts during the window failed with HTTP 503 or timed out at the edge; automatic retries by the web client mitigated a portion of end-user impact but exacerbated backend load. An estimated 1,840 orders were affected, of which 1,210 completed on retry and ~630 were abandoned. No data loss or corruption occurred, and no orders were double-charged.

## Background

checkout-api depends synchronously on pricing-service for cart totals and on inventory-service for stock reservation. pricing-service responses are cached in Redis with a 300s TTL keyed by (sku, region, currency). The r3 release reduced the pricing client's maximum connection pool size from 64 to 16 per pod as part of a resource-efficiency initiative (see RFC-118), based on p50 utilization telemetry that did not account for burst behaviour during cache-expiry alignment.

## Timeline (UTC)

- 13:58 — Rolling deploy of r3 begins (canary 5% → 25% → 100%).
- 14:07 — Canary reaches 100%. A large batch of Redis cache entries populated during the 14:02 pricing catalogue refresh expires simultaneously, producing a thundering-herd pattern against pricing-service.
- 14:09 — Connection pool exhaustion on checkout-api pods; requests queue for pool acquisition and exceed the 2s upstream timeout. p99 latency rises from 410ms to 6.8s.
- 14:11 — PagerDuty alert fires (checkout-5xx-rate > 5% for 3m). On-call engineer acknowledges.
- 14:18 — Initial hypothesis: pricing-service degradation. pricing on-call confirms service healthy; saturation is client-side.
- 14:31 — Pool-size change identified via deploy diff. Rollback to r2 initiated.
- 14:39 — Rollback at 100%. Error rate declining.
- 14:45 — Error rate below 1%. Incident mitigated.
- 15:20 — Incident resolved after 30 minutes of stable metrics.

## Root cause

The reduced pool size was adequate for steady-state load but not for the burst caused by synchronised cache expiry, which the 5-minute TTL and the periodic catalogue refresh make a recurring pattern (every 300s after each refresh). The absence of a circuit breaker or bounded retry policy on the pricing client allowed queued requests to amplify load rather than shed it. The canary phase did not surface the issue because the canary window (9 minutes) did not overlap with a cache-expiry burst.

## Contributing factors

- Pool sizing derived from p50 rather than p99/burst telemetry.
- No load test covering the cache-expiry alignment scenario.
- Web client retries (3 attempts, no backoff jitter) increased backend request volume ~2.4x during the incident.
- Canary duration insufficient to cover a full TTL cycle.

## What went well

- Alerting fired within 4 minutes of impact onset.
- Rollback path was exercised recently and completed in 8 minutes.
- No customer data integrity issues; payment idempotency keys prevented duplicate charges on retry.

## Action items

| # | Action | Owner | Due |
|---|---|---|---|
| 1 | Restore pool size to 64 (done via rollback); revisit RFC-118 with burst-aware sizing | checkout team | 2026-09-16 |
| 2 | Add TTL jitter (±20%) to pricing cache entries so expiry is spread out | platform | 2026-09-19 |
| 3 | Implement circuit breaker and bounded retry with exponential backoff and jitter in the pricing client | checkout team | 2026-09-30 |
| 4 | Extend minimum canary soak to 15 minutes (> 1 TTL cycle) | release eng | 2026-09-12 |
| 5 | Add cache-expiry-burst scenario to the pre-release load test suite | QA | 2026-10-07 |

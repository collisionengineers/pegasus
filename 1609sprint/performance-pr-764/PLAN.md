# PR 764: first-use performance and thumbnail caching plan

Temporary operator-requested sprint handover, 16 September 2026.
See [current progress](PROGRESS.md) and the linked
[six-shard CI work](../ci-six-shards/PLAN.md).

## PR aim

[PR #764: Improve first-use Case paths and production thumbnail caching](https://github.com/collisionengineers/pegasus/pull/764)
aims to make initial Case/Work Centre navigation, visible sections and image
viewing responsive while preserving authorization, current account checks,
edit leases, document integrity and workflow behavior.

The governing detailed implementation and measurement plan is the
[frozen performance plan at e4fc0ea05](https://github.com/collisionengineers/pegasus/blob/e4fc0ea05762aab67dd6ec3c8aaf35a280d5f2aa/.codex/PLAN.md).
Its historical progress notes are superseded by this sprint's progress report;
its requirements and measurement contract are not silently relaxed.

## Work packages and selected implementation

| Package | Intended change |
| --- | --- |
| P0: baseline and attribution | Capture a real authenticated baseline and add bounded allowlisted timing phases to the existing sampled telemetry pipeline. Distinguish activation, handler, rendering, authentication, shell and thumbnail queue time. |
| P1: production Identity caching | Stop validation-only cookie renewal after successful current-account/security-stamp checks. Preserve genuine sliding renewal, two-hour idle and eight-hour absolute limits. Keep protected HTML/fragments no-store and authorized current thumbnail caching private. |
| P2: Case request work | Reuse the already-authorized, Case-bound frame for direct Vehicle/Valuation/Files/Notes reads. Standalone fragments obtain fresh state. Avoid resolving report generators on ordinary Case GET; guarded report actions resolve them when needed. |
| P3: thumbnail misses | Decode plain thumbnails at reduced resolution with original limits, orientation, alpha, crop and rotation preserved. Coalesce concurrent same-representation misses and admit source buffering only after the decode gate. Version URL, cache variant and ETag together. |
| P4: browser work | Avoid scrolling the closed command palette; preserve open keyboard behavior. Make additional font/compression changes only if measurements and safety review justify them. |
| P5: delivery and live acceptance | Complete source review and exact-candidate CI, integrate into dev, prepare an approval-bound release packet, then verify authenticated production behavior and matched performance after approved deployment. |

Use existing owners. No new infrastructure, cache service, telemetry SDK,
dependency, database schema, hosting tier or background warmer is selected.
Provider-read/decode limits remain four/two. Shell parallelization,
ReadyToRun, prewarming, font subsetting and dynamic HTML compression are not
implemented by default.

## Acceptance contract

Use real Identity cookies and release publish shape with matched data, role,
viewport, host, route order and process/browser/source/derived-cache states.
Windows offline tests cannot establish Linux first-use performance.

| Scenario | Required evidence/target |
| --- | --- |
| Warm Office/Mine/default Case | At least 30 observations per primary scenario; p95 LCP at most 1.5s and document TTFB at most 1.0s |
| Fresh-process first use | Five controlled observations per route/order; every TTFB at most 1.5s and meaningful above-fold readiness at most 2.5s; report every sample, median and maximum |
| Direct/lazy section journey | At least 30 observations; event-to-usable section p95 at most 1s, with correct content and permissions |
| Fresh browser-cache hit | Zero new thumbnail GETs and zero transferred image bytes on unchanged Report/Files revisits before expiry, excluding forced revalidation |
| Revalidation | Authorized current representation may return 304 only after current authorization/preparation checks; genuine renewal may make it non-cacheable |
| Nine-image derived-cache hits | At least 30 bursts; gallery-open to last visible image p95 at most 2s |
| Nine-image controlled misses | Five matched bursts per revision; every burst completes within 5s and median improves at least 50%, or retain the miss and obtain an explicit evidence-backed disposition |
| Reliability/resource cost | No introduced access gap, stale preparation, duplicate fetch, errors, OOM/recycle, provider throttling or unbounded memory growth; compare requests, bytes, operations and memory |

For matched warm p95 comparisons, increases above the larger of 100ms or
10% of baseline trigger another matched batch. A repeated breach is a
regression even if the absolute target passes; conflicting batches remain
inconclusive. Do not fill missing paint observations with zeroes.

Capture LCP/FCP/TTFB, meaningful content readiness, section mounting,
first/last visible images, long tasks, CLS and browser/network failures.
Keep cold-process, browser-cold, cache-bypass and different server-cache
states separate. Treat nine-image bursts as bursts, not independent images.

## Verification and release sequence

1. Finish the linked six-shard remediation without changing product behavior.
2. Complete independent review and exact-head required CI. Reuse qualifying
   prior evidence honestly; retain all failures and incomplete runs.
3. Complete outstanding real-cookie browser, Linux first-use, image-burst,
   resource-cost and accessibility evidence where an authorized fixture
   exists. Record remaining gaps explicitly where it does not.
4. Integrate reviewed green source into dev under the original task authority.
5. Prepare exact-source Web/Worker artifacts and manifest/package hashes,
   migration classification, target identities, activation/smoke sequence
   and recovery boundary using the existing release procedure.
6. Obtain fresh approval for main promotion and the exact Azure operations.
   Previous release approval does not authorize these new bytes.
7. After an approved deployment, verify source identity and readiness, capture
   first authenticated business-route use before warming where feasible,
   then run full smoke and authenticated cache/section/image checks.
8. Observe subsequent traffic and natural first-use windows. Close only when
   acceptance is evidenced or specific remaining limitations are accepted.

No controlled matching Linux fixture has been established. Do not provision
one, restart production, purge caches, mutate accounts/provider data or deploy
to manufacture missing evidence without the exact required authorization.
Downloaded browser-local image bytes cannot be remotely revoked; preserve
next-network-request authorization without claiming cache erasure on logout.

## Document ownership

These sprint files are local temporary handovers in the operator-requested
location, not release approval or a claim of completion. Leave them
uncommitted unless separately requested; the current Markdown placement gate
does not admit `1609sprint/`. Durable results belong in PR evidence, the
runbook/architecture owners and, only after release, Operations.

# Pegasus performance improvement plan

**Estimated diff:** 10–18 production/diagnostic files, 8–14 test or browser
verification files, and affected documentation; approximately six reviewable
implementation slices. This is a planning estimate, not a file-count target.
Profiling can remove conditional work. No new service, queue, database schema,
runtime dependency, hosting tier or instance is part of the default scope.

**Updated:** 16 September 2026. **Status:** implementation in progress;
confirmed source changes prepared, candidate verification and release pending.
This replaces the completed first performance plan. Its implementation and
evidence remain in [the deployed plan history](https://github.com/collisionengineers/pegasus/blob/e8efb19779baadc5ea46bd9c62e6c9c54740cac7/.codex/PLAN.md)
and [PR 761](https://github.com/collisionengineers/pegasus/pull/761).

## 1. Baseline, outcome and authority

Current `dev`: `5765a527a7729e606fe5683b3af3d47a17c708ff`.
The live read-only `/diagnostics/version` check on 16 September returned
`e8efb19779baadc5ea46bd9c62e6c9c54740cac7`.
At the initial branch check, `8b9d358f` differed from that deployed revision
only in `docs/operations.md`. During planning, `5765a527a` added documentation,
skills and design updates; its only application-source change is a Razor
comment in `_StatusChip.cshtml`. The earlier performance changes are already
deployed, and these subsequent commits introduce no different runtime behavior
in the examined paths. Preserve the updated branch contents and recheck source
and deployed identities at implementation and release.

Make initial Case/Work Centre navigation, visible sections and image viewing
consistently responsive, including first use. Preserve current access,
document integrity, edit leases and workflows. Complete the actual production
authentication/browser path, rather than relying on offline-authentication
measurements.

Governing owners: [access](../docs/frd/frd-04-parties-accounts-and-access.md),
[documents and custody](../docs/frd/frd-05-documents-extraction-and-custody.md),
[Case lifecycle](../docs/frd/frd-01-case-identity-and-lifecycle.md),
[operator experience](../docs/frd/frd-12-operator-experience.md),
[engineering](../docs/engineering.md) and
[release procedure](../.agents/skills/pegasus-release/SKILL.md).

The independent investigation and sanitized samples are retained locally at
`artifacts/performance/lcp-20260916/`, especially `report.md`,
`azure-evidence.json`, `azure-phase-evidence.json`,
`browser-summary.json` and `thumbnail-auth-cookie-evidence.json`.
These ignored files are private working evidence, not portable PR links.
Attach a sanitized result summary and reproducible queries to the eventual PR;
never upload raw cookies, case content, document URLs or immutable `corpus/`.

### Findings carried forward

| Finding | Evidence | Consequence for this plan |
| --- | --- | --- |
| Case server response can be slow | 7.333 s; 47 recorded SQL calls total 178 ms. Main handler span 4.175 s; time outside it remains unattributed. | Attribute activation, authentication, handler, shell and rendering. Do not call SQL capacity or QuestPDF the proven cause. |
| Work Centre has a different slow path | 3.373 s response, including one 1.801 s SQL dependency; later full request 524 ms. | Measure its queries/connection and initialization costs separately; overlapping dependency times are not additive. |
| Browser thumbnail caching is ineffective | Current thumbnail responses contain the intended ETag, a renewed sign-in cookie, and `no-cache,no-store`. | Repair the interaction between per-request Identity validation and cookie renewal. |
| First measured nine-image pass is slow | Nine downloads took 12.26–16.41 s; one confirmed cache miss spent 6.164 s at the provider gate and 4.385 s rendering, including 3.440 s at the decode gate. | Improve cold retrieval/rendering as well as repeat navigation; do not assume all nine prior cache states were known. |
| Direct sections reread the frame | Introduced focused readers reload workflow/summary already loaded by the full page. | Reuse the authorized same-request frame; standalone fragments still read current state. |
| Warm navigation is already relatively fast | Five visits per scenario: Cases 0.35–0.60 s LCP; Work Centre Office/Mine 0.24–1.11 s. | Preserve warm performance while fixing first-use behavior. |
| LCP alone can give a misleading completion signal | Case LCP was the shell search placeholder in the examined viewport; lazy images arrived later. | Measure meaningful content readiness and visible-image completion alongside LCP. Do not alter content merely to improve the score. |

The cache/authentication interaction and cold retrieval path predate the first
performance branch. The direct-frame reread is a small introduced regression.
The earlier W6 recorded investigation limits; it did not implement a
production startup optimization.

## 2. Acceptance targets and measurement contract

The following are proposed engineering acceptance targets for this work,
not an existing contractual SLA. Freeze them with the implementation baseline.
A missed target remains visible and needs an explicit disposition; do not
rename an unresolved target a pass.

Use production-style Identity cookies, the release publish configuration and
a representative dataset containing Case A without confirmed images and
Case B with nine confirmed images. Keep the same source data, role,
viewport, host/SKU, process/cache state and throttling between comparisons.
Record image formats, encoded sizes and decoded dimensions.

| Scenario | Acceptance |
| --- | --- |
| Warm Work Centre Office/Mine and default Case | At least 30 runs per primary scenario; p95 LCP ≤1.5 s and p95 document TTFB ≤1.0 s on the unthrottled desktop test. Apply the regression rule below. |
| Fresh Web-process first Case and Work Centre use | Five controlled observations per selected route/first-use order; report every sample and median/max, not a p95 from five. Each observation must have TTFB ≤1.5 s and meaningful above-fold readiness ≤2.5 s. |
| Case direct/lazy sections | At least 30 observations per selected primary section journey; section usable within 1 s at p95. Measure from the actual UI event, excluding automation-driver waiting. Correct content and edit permissions are mandatory. |
| Unchanged current thumbnail, fresh browser cache hit | Zero new thumbnail GETs and zero transferred image bytes when revisiting Report/Files before expiry, except an explicitly forced revalidation. Treat browser cache-disable mode as a separate scenario. |
| Thumbnail revalidation | Authorized current representation can return 304; authorization/preparation checks occur before 304. Genuine cookie renewal may make that response non-cacheable. |
| Nine-image burst, derived-cache hits and browser misses | At least 30 bursts; p95 actual gallery-open event → last visible thumbnail complete ≤2 s. Record first completion and concurrent workload too. |
| Nine-image burst, controlled derived/original misses | Five matched bursts per revision; report each burst, median/max. Target all nine visible thumbnails within 5 s in every burst and ≥50% reduction in median event → last-thumbnail duration. If provider latency prevents this, retain the miss and an evidence-backed design/cost decision. |
| Reliability and cost | No introduced errors, authorization gap, stale preparation, duplicate fetches, OOM/recycles, provider throttling or unbounded memory growth. Compare requests, bytes, provider/Blob/SQL operations and memory per journey. |

Measure LCP, FCP, TTFB, main content ready, section mount, first/last visible
thumbnail, long tasks and CLS. Retain console/network failures.
Use the previous 1920×855/DPR 1 desktop measurement for comparability, plus
supported narrow/reflow coverage for affected UI. CPU 4× / Fast 4G is a
separate sensitivity run, not pooled with the primary results.

For matched warm LCP, TTFB and section-duration comparisons, a candidate p95
increase exceeding `max(100 ms, 10% of baseline p95)` triggers one additional
matched baseline/candidate batch. A repeated breach is a regression even if the
absolute target passes; conflicting batches are inconclusive. Retain both.
Fix route order within each comparison and report it; use separate first-use
orders to expose initialization moved between routes.

Distinguish these conditions explicitly:

- fresh Web process and first route use, with browser assets already held
  (server first-use, not browser-cold);
- warm Web process with a fresh browser profile/cold assets;
- warm Web process with browser-cache bypass;
- warm Web process and normal browser caching;
- derived cache hit versus derived miss with original cached;
- derived and original cache misses; expiry; concurrent same-image misses.

Record Web process/instance identity, browser profile/cache state, asset
preloading, source/derived cache state, session age, route order and exact reset
action for every cell. A cache-bypass reload proves none of the other cold
conditions. Keep nine-image samples grouped as bursts, not 270 independent
images when calculating a burst percentile.

Before reset-dependent execution, name the disposable fixture host, database,
cache namespace, Case/image source manifest and owner in the operator context.
No matching isolated Linux fixture is established by this plan. Those cells
remain pending until an existing permitted fixture is identified or a concrete
new target is authorized. Existing synthetic fixtures may supply data; live
Cases are read-only evidence. Production restarts, cache purges, account changes
and provider mutations are not authorized by this plan-writing task.

## 3. Implementation packages

### P0 — Establish a reproducible production-style baseline and attribution

**Output:** a fixed baseline, narrow missing phase measurements, and a bounded
choice of work for P2/P3. Begin P1/P2's confirmed fixes independently of
long-running profile analysis.

Owners: existing
[phase allowlist](../src/Pegasus.Core/Documents/DocumentReadTelemetry.cs),
[telemetry bridge](../src/Pegasus.Web/DocumentReadTelemetryBridge.cs),
[Program](../src/Pegasus.Web/Program.cs),
[Case model](../src/Pegasus.Web/Pages/Cases/Details.cshtml.cs),
[rail filter](../src/Pegasus.Web/Presentation/RailCountsPageFilter.cs)
and the existing performance harness.

1. Record source/package identity, framework/publish mode, instance, hosting
   settings, session type, dataset identity and cache preparation for every run.
   Capture the existing authenticated baseline before optimization.
2. Extend the existing sampled/allowlisted telemetry only where needed:
   authentication validation, expensive renderer initialization, main handler
   subphases, post-handler shell and Razor/result rendering. Use a Case-scoped
   resource/result filter, or the existing equivalent, to include PageModel
   activation before handler execution and isolate result rendering. Name frame,
   access, workspace, direct/engineer section and extras phases, plus shell
   counts/Operations/notifications. Use a profiler for JIT, EF compilation,
   allocations, GC and activation gaps.
   Do not build a new tracing framework or browser telemetry SDK.
3. Correlate browser navigation to request and dependency/phase events.
   Durations are in `AppEvents.Measurements["durationMs"]`, with phase names in
   `Properties["phase"]`. Keep full page, automatic Refresh fragment and Case
   Section routes distinct. Do not double-count nested or parallel spans.
4. Profile first-use on Linux with the actual release publish shape and real
   cookie authentication. Windows offline profiles remain useful functional
   evidence, not proof of Linux production first-use latency.
5. Stop broad exploration once each dominant delay has a named operation and
   a bounded experiment. Produce a short before/after hypothesis table,
   retained failed observations, and the selected implementation choice.

Limit profiler capture to three baseline and three candidate traces per
selected slow condition, at most 60 seconds each. Permit one replacement only
for a technically invalid capture and retain its failure reason. Use separate
unprofiled timings for acceptance. Seek unattributed request time no greater
than `max(100 ms, 10% of request duration)`; after the capture budget, record
any remaining gap and its consequence rather than continuing indefinitely.

Keep existing sampling, Entra ingestion, CSP and daily cap. No SQL text,
identity values, tokens or document contents in new telemetry. Measure
telemetry overhead and ingested bytes; remove temporary probes after retaining
the diagnosis, keeping only useful bounded operational spans.
Stop temporary capture if projected ingestion would exhaust remaining daily
cap headroom; do not raise the cap or sampling rate implicitly.

**Linux execution boundary:** use an available authorized Linux test host.
Do not assume B1 deployment slots or provision an extra environment by default.
If no matching host exists, prepare capture for the approved release restart
before warm business-page smoke; keep cold acceptance pending until captured.
Any additional cloud target or restart needs its exact operation/cost scope
approved first.

### P1 — Make image caching work with production Identity cookies

**Output:** effective private caching for unchanged current images, with every
new authenticated request retaining current access checks.

Owners: [cookie configuration](../src/Pegasus.Web/Program.cs),
[download endpoint](../src/Pegasus.Web/Pages/Cases/Documents/Download.cshtml.cs),
[sign-in tests](../tests/Pegasus.IntegrationTests/StaffSignInSecurityTests.cs)
and [download tests](../tests/Pegasus.IntegrationTests/CaseDocumentDownloadWebTests.cs).

- Keep zero-interval security-stamp validation and current account/session
  checks. After successful validation has supplied the request principal,
  clear `context.ShouldRenew` at the end of the successful existing
  `OnValidatePrincipal` callback, after all rejection checks. This suppresses
  validation-driven cookie reissue across that callback; do not add a
  thumbnail-specific authentication exception. Preserve genuine sliding
  renewal, absolute session age, original-issue claims and rejection paths.
  The disabled/missing-user rejection must return before the successful tail.
  Current role/security changes update the security stamp and revoke the
  session; no principal-comparison framework is needed to preserve them.
- Verify the distinction between validation-driven `ShouldRenew` and the
  cookie handler's independent sliding-renewal state in controlled-clock tests.
  Keep the current two-hour sliding and eight-hour absolute lifetimes.
  Do not restore caching by weakening validation or by overwriting all response
  headers after authentication runs.
- Keep version/preparation identity, ETag semantics, current authorization
  before 304, canonical Report/Files URLs and private-only caching.
  Errors, stale/missing preparation addresses and full-image fallback must
  remain non-cacheable as required by the existing endpoint.
- Exercise the actual Identity cookie middleware in integration and browser
  verification. DevelopmentOffline/custom fake-auth tests cannot prove this
  change.
- Check response policies beyond images: protected HTML/fragments and
  sensitive/error/download responses must retain their required `no-store`
  behavior independently of redundant cookie renewal. Work Centre and Case
  already declare it explicitly; correct any affected path that relied only
  on the cookie handler's incidental headers. Keep static asset caching intact.

**Required proof:** unchanged current thumbnail 200 → browser reuse; forced
authorized revalidation → 304; genuine session renewal; idle/absolute expiry;
disabled/deleted account, role change, password reset, changed stamp and Force
logout; missing/wrong Case/version access; invalid/stale preparation; crop and
rotation invalidation; same URL in Report and Files; no cross-account response
serving by a server/proxy cache that bypasses current network authorization.

Already downloaded bytes cannot be remotely revoked. FRD-04 requires the next
request to see current authority; verify that on network requests and do not
claim browser-local cached copies are erased by logout. Preserve the existing
private-browser cache contract: it is not partitioned by Pegasus account, so
switching users in the same browser profile does not erase previously fetched
bytes. Do not assert otherwise in verification. Changing that contract would
be a separate explicit policy decision.

### P2 — Remove unnecessary work from the Case request path

**Output:** confirmed redundant reads removed and measured first-use cost
reduced at its actual owner.

Owners: [Case model](../src/Pegasus.Web/Pages/Cases/Details.cshtml.cs),
[focused contracts](../src/Pegasus.Core/Cases/CaseQueries.cs),
[EF readers](../src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs),
[rail filter](../src/Pegasus.Web/Presentation/RailCountsPageFilter.cs)
and affected report/query/web fixtures.

**A. Remove GET's dependency on POST-only report initialization.**

The Case constructor currently receives both report-generation services, which
resolve the QuestPDF renderer and register fonts. Move their resolution to
their owning report POST path, after the normal authorization/readiness/lease
guards, using a narrowly scoped existing composition pattern. Keep one
renderer and its existing font-registration/concurrency rules. Do not split
the entire Case page, add a generic factory framework or remove report
content from the GET. Verify draft generation, promotion and final report
generation, including failure/refusal paths. Measure the gain; this change
alone is not assumed to explain the 7.333 s response.

**B. Reuse the full page's authorized frame for direct sections.**

Pass the already-read frame through the focused direct-render request.
Vehicle, Valuation, Files and Notes use it with the existing workspace/data/
document projections. Add an optional `CaseSectionFrame` to
`GetCaseSectionQuery`; full-page callers supply `Case.Frame`, while independent
Section requests omit it and obtain a fresh frame. Include the already-loaded
custody root/state scalars so Files can skip its workflow/summary reads too.
Validate that the supplied frame's Case identity matches the requested Case.
Keep reuse request-local and Case-bound; update all known callers and doubles
together, without retaining an obsolete query contract.

Prove the two redundant workflow/summary queries disappear on each direct
path, while missing/forbidden Cases, stale workflow versions and wrong/expired
edit leases still fail correctly. Cover direct/lazy and Scroll/Tabs, read/edit
modes, and Report controls while Files is deferred.

**C. Reduce only the remaining measured critical path.**

Use P0 spans to decide whether shared shell serialization, repeated identity/
document reads, JIT, EF compilation or Razor initialization dominates.
Runtime Razor compilation is already disabled; disabling it is not new work.
Reuse existing request-local results. Run independent shell reads concurrently
only if the shell exceeds 100 ms or 10% of request duration, and only when they
use separate contexts and retain existing cancellation,
authorization and partial-failure behavior. Never parallelize EF work on the
same scoped DbContext.

If JIT remains dominant, compare one release-publish optimization against its
larger artifact/startup cost. If route warming is considered, measure the work
it moves to startup and exclude business/provider effects. Neither ReadyToRun
nor prewarming is the default fix. Stop once targets are met; otherwise record
the measured remaining blocker and the smallest next candidate.

### P3 — Reduce thumbnail miss latency and control its resource cost

**Output:** measured improvement in the first visible image burst, not merely a
fast repeat page.

Owners: [content and thumbnail caches](../src/Pegasus.Infrastructure/Custody/CachedDocumentContentStore.cs),
[provider read gate](../src/Pegasus.Infrastructure/Custody/BoxDocumentContentStore.cs),
[thumbnail policy](../src/Pegasus.Core/Documents/CaseDocumentPreview.cs),
[cache tests](../tests/Pegasus.IntegrationTests/DocumentContentCacheTests.cs)
and the existing gallery callers.

1. Run the same nine-image corpus through each cache state after P1.
   Keep provider wait/read, original-cache lookup, verification, derived-cache
   lookup, decode wait, render and cache write separately attributable.
   Record peak managed/native memory, CPU, errors and first/last image ready.
2. Remove proven duplicate work for the same requested representation and
   unnecessary serialization at the existing cache/reader owner. If concurrent
   same-key misses duplicate retrieval/rendering, reuse the existing locking
   mechanism or add the smallest bounded coordination there; do not add a
   distributed cache/service without a measured multi-instance requirement.
3. Implement reduced-resolution decoding for the plain thumbnail path:
   `ImageThumbnailRendering.Render` currently fully decodes before resizing;
   the prepared path already uses `SKCodec.GetScaledDimensions`. Preserve
   original pixel/byte limits before allocation, source-hash verification,
   orientation, white alpha composition, rotation, crop geometry and colour.
   Retain the 480-pixel longest-edge contract. Do not use a thumbnail as
   full-resolution viewing/cropping input or alter evidential originals.
   Since decoded output may change, version the generated canonical thumbnail
   URL, derived-cache variant and ETag renderer identity together. Missing/old
   representation identities follow existing validated, non-cacheable address
   behavior; keep one current renderer, not old/new implementations.
4. Inspect memory retained while requests await decode, including source
   buffering before gate admission. Optimize demonstrated excessive buffering
   before considering larger concurrency.
5. Retain the current four provider-read and two decode slots for the first
   comparison. If targets still miss, test at most two bounded alternatives:
   four provider/three decode slots, then six provider/three decode slots.
   Reject either for more than 10% peak-RSS increase, any introduced image
   failure, recycle/OOM or 429/5xx, increased provider calls/bytes for the same
   corpus, or more than 10% concurrent non-image p95 regression. Retain the
   smaller bounds unless a candidate meets both latency and resource gates.

If cold provider latency still prevents the target, compare early generation
within an existing confirmed-custody/preparation flow with leaving work on
first view. Record exact trigger, identities, invalidation, retry/cancellation,
provider requests and storage cost before selecting it. Do not create an
unbounded warmer, extend the FRD's 24-hour idle retention, or add a worker by
default. A decision to move work earlier must remain in this package's explicit
scope and receive its own review.

**Required proof:** original/derived miss and hit; idle expiry; concurrent same
and different images; cancellation while queued/reading/decoding; malformed and
oversized sources; temporary provider/Blob failures; integrity mismatch; cache
write failure; preparation changing during retrieval; placeholders for pending/
failed custody; no durable-custody or logical-association changes.

### P4 — Reduce measured browser payload and layout costs

**Output:** smaller first-visit cost without a redesign or a cosmetic LCP fix.

Owners: [site script](../src/Pegasus.Web/wwwroot/js/site.js),
[Case script](../src/Pegasus.Web/wwwroot/js/case-workspace.js),
[shared layout](../src/Pegasus.Web/Pages/Shared/_Layout.cshtml),
[styles](../src/Pegasus.Web/wwwroot/css/site.css) and the existing font assets.

- Avoid palette initialization forcing `scrollIntoView` while its UI is closed.
  Preserve keyboard selection/visibility when opened. Batch read/write layout
  work only where the captured forced layouts justify it.
- Evaluate subsetting the 352 KB regular and 388 KB italic Inter fonts against
  actual supported glyphs, symbols, names and fallback rendering. Keep license
  notices, variable weights and visual consistency. Do not add preload to every
  font or replace `font-display:swap` merely to change the metric.
- Measure selective dynamic HTML compression for the observed 48–123 KB
  documents. Review HTTPS response secrets, antiforgery tokens and reflected
  input before enabling it. Compress only eligible responses with correct
  `Vary`; keep images/already-compressed assets and sensitive responses out
  where required. If safe savings are insignificant, record a justified skip.
- Recheck delayed section mounting, keyboard navigation, focus, scroll,
  supported widths, 200% reflow, forced colours, reduced motion and CLS.
  Use the existing Razor implementation/review skills for any UI diff.

This package follows the server/cache changes and is measurement-gated.
Do not hide/remove the search placeholder, defer required Case content, or
claim its LCP represents all images being ready.

### P5 — Integrate, review, release and verify the live result

**Output:** reviewed code, exact-candidate evidence, an approved deployment and
authenticated live acceptance with remaining limitations stated.

1. Create the implementation worktree from refreshed `dev`; record the actual
   base SHA. Leave unrelated skill and v27 design work untouched. Commit
   cohesive slices: attribution; auth/cache; Case reads/activation; thumbnail
   work; any justified browser changes; final evidence/documentation.
2. Run appropriate focused verification after each relevant slice. Shared
   authentication/composition changes require full required solution/CI
   evidence on the final candidate. Use the repository's current test platform,
   run-tests skill and [runbook](../docs/runbook.md); do not invent command flags
   or weaken fixtures to make them pass.
3. Run a pegasus-reviewer before pushing, then open a draft PR. Perform the
   independent direct review on its complete diff; apply the Razor review
   skill to affected routed UI. Post findings with exact-SHA source, sanitized
   browser/Azure evidence and CI links. Remediate blockers and rerun only the
   affected checks unless the changed scope requires more.
4. Update architecture only for actual structural changes; update affected FRDs
   only for an accepted behavior change. Put dated deployed results in
   [Operations](../docs/operations.md). Keep the PR's performance table tied to
   exact artifact, environment, dataset and authentication identities.
5. With no blocking findings, integrate the reviewed candidate into `dev`.
   Prepare promotion and deployment through the existing release procedure,
   honoring the current operator's authorization and its exact target/artifact
   bounds. New deployment approval is requested only after the release packet
   is concrete and reviewable; approval of the previous release is not an
   approval of these future bytes.
6. The packet identifies source SHA, manifest and package hashes, changed
   services, schema classification, target resources, expected restart effects,
   capture order, smoke and recovery. Default schema classification is unchanged,
   but verify it against actual candidate/deployed migrations.
7. After approved deployment, verify exact source/package identity and readiness.
   Capture first authenticated business-route use before warm checks when
   feasible, then run full prescribed smoke and the authenticated Work Centre,
   Case section, Report/Files and cache checks. No carry-forward waiver of the
   previous release's skipped authenticated check.
8. Observe subsequent ordinary traffic and the next available natural first-use
   window. Query slow-route tails, request/dependency failures, provider/decode
   waits, memory/recycles and ingestion usage. Close only with the acceptance
   table completed; record missed/inconclusive targets honestly.

If matching Linux cold measurements are first obtainable during the approved
release, deployment health and warm smoke are provisional performance evidence;
the first-use acceptance item remains open until measured. Do not deploy
additional infrastructure or cause extra restarts to manufacture a pass.

## 4. Agent ownership and verification coordination

The primary owns integration, overlaps, operator communication, GitHub writes
and release operations. Use the repository's configured roles:

| Role | Bounded assignment |
| --- | --- |
| pegasus-scout | Inventory the exact touched callers, fixtures and evidence paths if the base changed. |
| pegasus-investigator | P0 causal attribution and the bounded P3 choice, returning evidence and a stop condition. |
| pegasus-implementer A | P1 authentication/download scope and its tests. |
| pegasus-implementer B | P2 Case contracts/readers and affected fixtures. |
| pegasus-implementer C | P3 cache/provider/render scope; P4 only as a separate later assignment. |
| pegasus-reviewer | Independent final source/requirements/evidence review; no self-review of authored implementation. |
| pegasus-verifier | Sole assigned host build/test/browser/profile owner against frozen inputs. |

Normally keep 2–4 useful children active. Program/telemetry/Case overlaps are
integrated by the primary in sequence, not edited by overlapping workers.
Every assignment names direct-work or ticket/worktree, source root, exact
revision, allowed files, output and stop condition. Children do not recursively
delegate or start host workloads.

Create the current implementation operator context and point participating
sessions to the one canonical shared host-slot record. Reuse its coordination
history, never its stale grant; do not create a competing slot file. Reread
explicit idle and record a new owner, current host, frozen revision/input hashes
and permitted workload before execution. Check other execution contexts and
processes, and record explicit idle before each handoff.
Local verification and release packaging never overlap; remote CI evidence
must identify its host/run/candidate separately.

## 5. Regression and cost ledger

| Risk | Required guard/evidence |
| --- | --- |
| Caching improves speed by weakening access | Real Identity-cookie tests; network revalidation observes revocation and rejects unauthorized 304. Private caching never becomes shared/public. |
| Cookie renewal fix changes idle/absolute expiry | Controlled-clock tests across renewal boundaries and original-issue preservation, including concurrent requests. |
| Same-request frame reuse leaks stale/different Case state | Case-bound request-local data, fresh standalone fragment reads, workflow/lease and forbidden/missing cases. |
| Removing eager report services breaks report actions | All affected report handlers, rejection paths and exact production composition; preserve font registration and render limits. |
| More parallel reads use one EF context | Separate-context ownership proof, cancellation and error behavior; no concurrent shared-context queries. |
| Thumbnail optimization changes evidence | Retained original identity/hash, exact preparation, output geometry/orientation and fallback/refusal behavior. |
| Faster thumbnails consume more memory/provider capacity | Compare peak RSS/GC, queued source buffers, provider rate/429s and error rate; preserve or justify admission limits. |
| Warming moves cost into startup or intake | Measure readiness, intake latency, provider operations, cache bytes and expiry work; no hidden side effects. |
| New telemetry increases bill/loses privacy | Sampled allowlisted fields, existing cap, emitted-record inspection and bytes/headroom before/after. |
| Payload optimization changes security or UI | Compression threat review; glyph/license coverage; keyboard/focus/reflow checks; measured benefit. |
| Small sample hides remaining latency | Retain failures and cache/process identity, separate median/p95/max, compare matching runs and inspect live tails. |

No SKU increase, longer cache retention, new telemetry SDK or distributed
coordination is justified by current evidence. If a measured blocker requires
one, present the concrete alternative, expected recurring cost, operations
impact and acceptance benefit before adding it.

## 6. Completion record

For each package record: exact commit, changed behavior, verification/run links,
before/after measurements, cost evidence, review disposition and any remaining
limitation. Distinguish implemented, tested, deployed and live-accepted.

This plan is complete as a planning deliverable when independently reviewed and
its links/placement are checked. Performance implementation is complete only
when the agreed targets and security/functionality checks above are satisfied,
or a specific remaining limitation is explicitly accepted. Then stop.

### Implementation observations — 16 September 2026

Implementation base is unchanged at `5765a527a7729e606fe5683b3af3d47a17c708ff`,
on `perf/first-use-and-image-cache` in the separate `performance-next` worktree.
The original checkout's uncommitted plan and v27 material remain untouched.
The live version was rechecked as `e8efb19779baadc5ea46bd9c62e6c9c54740cac7`.

The real production Identity session supplied by the operator was used for
the read-only baseline below: desktop 1920×855, DPR 1, no throttling, normal
browser caching, fixed Office → Mine → Case A → Case B order. No process,
source cache or derived cache was reset. Case B has nine distinct thumbnail
addresses. These observations establish warm baseline only.

| Scenario | Valid LCP samples | Baseline p95 LCP | Paired p95 TTFB |
| --- | --- | --- | --- |
| Work Centre Office | 30 | 464 ms | 310 ms |
| Work Centre Mine | 30 | 1,096 ms | 958 ms |
| Case A | 30 | 672 ms | 544 ms |
| Case B | 30 | 464 ms | 291 ms |

The first 120 navigations retained 34 missing paint entries despite reporting
visible state. Explicitly bringing the tab forward restored paint reporting;
36 additional navigations supplied the missing observations. The table uses
the first 30 valid paints per route and their paired TTFB, never zero-filled
missing paints. Original samples and the supplement are retained separately.
No script errors or failed resource status codes were recorded. The
DOMContentLoaded double-frame marker is only a paint opportunity proxy, not
proof that sections or images are ready.

Private working evidence and the canonical shared host-slot pointer are in
`artifacts/performance/implementation-20260916/` in the original checkout.
The PR carries sanitized results; these ignored files are not portable links.

| Package | Implemented choice | Remaining evidence or decision |
| --- | --- | --- |
| P0 | Bounded authentication, workspace activation/result, Case subphase, Work Centre full/Refresh, shell and renderer-initialization spans in the existing sampled pipeline. | Controlled Linux first-use attribution, profiler captures, ingestion overhead/headroom and candidate comparison remain pending. |
| P1 | Successful validation no longer reissues cookies; current checks and sliding renewal remain. Protected Razor responses default to no-store independently of cookie renewal. | Candidate actual-cookie integration execution and browser cache reuse evidence pending. |
| P2 | Direct sections reuse the same-request Case-bound frame with custody scalars. Main Case GET does not resolve report generators. Dedicated preview GET preserves its established action and resolves only on that path. | Candidate regression execution and measured first-use gain pending; no claim that renderer initialization explains all prior delay. |
| P3 | Scaled plain decode, coordinated current renderer URL/cache/ETag identity and bounded same-representation miss coordination. Source copying occurs after decode admission. Provider/decode limits remain four/two. | Candidate regression execution; matched nine-image provider/cache bursts, RSS/GC and concurrent workload acceptance remain pending. |
| P4 | Closed palette no longer invokes scrollIntoView. An actual-script static shell fixture proves open/arrow/Escape/focus return. | Routed candidate browser coverage pending. Font subsetting is deferred without supported glyph and benefit evidence. Dynamic HTML compression is deferred: antiforgery/reflected content needs specific review and retained traces showed no LCP gain. |
| P5 | Independent source review and serialized verification are in progress. | No deployment or live acceptance has occurred. Concrete artifact/target approval remains required for release. |

No authorized matching Linux fixture was established. Do not substitute local
Windows/TestServer tests for Linux cold acceptance or infer provider costs
from a synthetic source. No additional environment, concurrency increase,
prewarming, ReadyToRun, cache-retention change or hosting change is selected.

The first frozen candidate, `a13e97373`, passed locked restore and Release
build (zero warnings/errors), Core (2,199 passed, 14 skipped), Architecture
(120 passed), documentation links and Markdown placement. Its focused
Integration run completed with 337 passed, four failed and none skipped.
Two failures were assertions that prohibited every response cookie on
an HTML form instead of specifically prohibiting Identity-ticket renewal.
Two shared a malformed PNG fixture: its RGBA scanline lacked a byte and its
IDAT checksum was invalid. These failures and the bounded diagnostic are
retained, not counted as passes. The corrected candidate requires reruns.
The orientation fixture is also strengthened to assert asymmetric pixel
positions, and cancellation is checked before and after native rendering.

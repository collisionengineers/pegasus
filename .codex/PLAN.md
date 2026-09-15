# Pegasus performance implementation plan

**Updated:** 15 September 2026

**Status:** Ready for implementation; no application changes delivered by this plan.

**Source reviewed:** `59ad1b3424071fd0eb018dd7f06aaaedc1c187b2`.

**Audited deployment:** Release 52, `38051586856eb2b4a00b964de842a2e7bcdee555`.

This temporary operator-requested plan replaces the performance report and
incorporates its confirmed review. Application source and infrastructure were
unchanged between those two revisions. Recheck affected source before starting;
record each implementation's actual baseline and candidate revisions.

## 1. Outcome and boundaries

Make navigation, Case sections and image viewing perform only the reads and
rendering they need. Correct refresh, count and image-cache defects alongside
the performance changes. Preserve accepted business policy, retained evidence,
authorization, editing and the supported operator journeys.

The governing owners are [FRD-12](../docs/frd/frd-12-operator-experience.md),
the [FRD index](../docs/frd/README.md), [CONTEXT](../CONTEXT.md), and the
[engineering policy](../docs/engineering.md). Current requirements take
precedence over incorrect existing behaviour. In particular, the Operations
badge must count all eligible failures, and direct and lazy Vehicle views must
show the same required assessment information.

Use existing Core policy owners, infrastructure adapters and Web presentation
methods. This work requires focused query contracts, not another cache,
background worker, section framework or telemetry platform. No database
migration is planned. Retain existing document-version and preparation-version
identities; update known callers and remove superseded query/controller paths.

This plan authorizes no production deployment, restart, reset, upload or
provider mutation. Future release operations use the existing
[release procedure](../.agents/skills/pegasus-release/SKILL.md) with the actual
candidate and targets. Reversible implementation and isolated verification
need no additional product-design decision.

### Evidence carried forward

The original audit reported 124 SQL dependency spans across an initial Case
page and its Vehicle/Valuation fragments; 3.9–9.8 second cold thumbnail reads;
and a discarded Work Centre refresh with 59 SQL spans. It also reported an
empty App Service queue, generally low SQL utilisation and 78–85% memory use.

These are observations, not reproduced benchmarks, fixed query budgets or
capacity guarantees. The 08:30–09:05 UTC measurement window overlaps the
08:37 test-estate reset recorded in [Operations](../docs/operations.md).
Do not combine pre-reset, empty-estate and repopulated observations into the
new baseline. The old Container Apps resources were subsequently removed.

The dated serving configuration is Linux B1 App Service, one Web instance,
Flex Consumption Worker, S0 SQL at 10 DTU, and UK South storage. Read back
current configuration before any cloud experiment. The intake five-second
target remains an intake target; this plan creates no navigation SLA.

## 2. Delivery order and change map

Implement and review each package against frozen inputs. W1 and W2 are
independent; W3 precedes section/prefetch comparisons. W4 and W5 overlap Case
Details, so integrate their shared edits through one owner.

| Package | Original findings | Changes and completion evidence |
| --- | --- | --- |
| W0 — Baseline | F9 and measurement limitations | Reproducible browser/server measurements, safe phase attribution and a fixed representative dataset. |
| W1 — Work Centre | F5 | Working automatic and manual refresh, fragment response, preserved operator state and truthful freshness. |
| W2 — Image identity | F3, F4 | Canonical existing URLs, bounded preparation lookup and safe cache behaviour across preparation races. |
| W3 — Case controller/assets | F2, F8 | One Case controller, preserved navigation/editing and Case-only stylesheet loading. |
| W4 — Case projections | F2 | Focused initial-page and Vehicle/Valuation/Files/Notes reads with equivalent required content and authorization. |
| W5 — Counts and batches | F1, F7 | Correct total badges, request-local reuse and bounded database-side projections for all listed consumers. |
| W6 — Retrieval and startup profile | F3, F6 | Attributed cold-image and first-use costs, with an evidence-backed recommendation for any further change. |
| W7 — Integrated proof | All | Functional regressions checked, comparable performance/cost evidence and required exact-candidate CI. |

## 3. Implementation packages

### W0 — Establish measurements without adding a browser SDK

Retain a content-safe evidence bundle under
`artifacts/performance/<baseline-sha>/<run-id>/`, with candidate comparisons
alongside it. Record timestamps, source/package identity, instance and hosting
configuration, role, dataset revision, browser/viewport/throttling, request
correlation, sampling settings and raw measurement/query outputs.

Use controlled authenticated browser captures for navigation, first paint,
section readiness and image completion. Freeze the named fixtures and their
data revision before either side of this mandatory comparison matrix:

| Journey | Measurements on both baseline and candidate |
| --- | --- |
| Work Centre, Office and Mine | Full navigation, manual refresh and automatic refresh; server reads, transfer and successful content/update-time application. |
| Cases list and populated Case | List and initial Case navigation; direct and lazy Vehicle, Valuation, Files and Notes readiness in both display modes. |
| Report and Files images | Same confirmed images viewed in both sections; cold original/thumbnail, original-cached, thumbnail-cached and browser-cached retrieval, plus crop/rotation invalidation. |
| Inbox, Accounts, Logs and Operations | Populated bounded pages and their count/batch reads, including the larger-data cases defined in W5. |
| Administration Health and Reports | First-use and warm navigation for the reported startup penalty and sequential reads. |

Capture thirty measured warm runs of each applicable journey after warm-up,
and five separate cold observations where cache/process state can be
controlled. Exercise preparation changes in isolated data and restore the same
identified fixture between comparisons. Report sample size,
median, p95 and variation; these samples do not establish an office-load SLA.
Separate first process use, original-cache miss, thumbnail-cache miss,
server-cache hit and browser-cache hit.

Add narrowly scoped server measurements through the existing telemetry
integration. Identify main-page versus fragment and the four section names
with allowlisted categories. Attribute thumbnail time to preparation lookup,
cache reads, provider/gate waits, verification, decoding and cache writes.
Measure both total elapsed time and phases; overlapping durations do not sum
to a sequential explanation. Disabled/uncomposed telemetry must not break
application behaviour.

Preserve Entra-only ingestion, same-origin CSP, existing sampling and the
shared 0.5 GB daily cap. Do not add an Application Insights browser SDK,
browser ingestion endpoint, raw SQL logging or an SDK migration in this
delivery. Request URLs, filenames, field values, tokens and document contents
must not be copied into new telemetry. Existing upload/callback redaction
must remain intact. Check emitted records, not only custom property names.

Record ingested bytes and cap headroom during the comparison. Browser captures
can also contain sensitive request data; retain only the necessary sanitized
evidence and do not publish raw captures containing credentials or content.

### W1 — Repair and narrow Work Centre refresh

Owners:
[refresh script](../src/Pegasus.Web/wwwroot/js/work-centre.js),
[page model](../src/Pegasus.Web/Pages/Index.cshtml.cs) and
[view](../src/Pegasus.Web/Pages/Index.cshtml).

- Separate an in-flight request guard from operator-interaction protection.
  Check interaction state both before fetching and before replacing content.
  Allow the current request to apply its own result.
- Block automatic replacement while any application dialog is open, a field
  is active, or a focused control inside the replacement region would be
  removed. Defer that refresh until a later eligible trigger. Preserve scroll
  position and rebind inserted controls once.
- Keep five-minute and focus-return refresh behaviour. Deduplicate simultaneous
  triggers; advance the successful-update timestamp only when content applies.
  Network errors, login redirects and malformed responses retain the last good
  display and do not claim success.
- Extract the Work Centre body into one shared partial. Add an authenticated
  `OnGetRefreshAsync` handler returning that partial through `PartialViewResult`.
  Initial navigation and refresh share the page-data loader. The fragment must
  bypass the rail-count filter and layout, and retain private/no-store caching.
- Carry scope, kinds, selected item, attention page, New cases page and the
  existing `since` divider through refresh. Exclude the one-shot `assign`
  instruction, so a dismissed assignment dialog cannot reopen automatically.
- Make the manual GET Refresh form submit `refresh=true`, `since` and
  `newPage` as well as its existing state. Keep its full-page, no-script path.
  Both manual and automatic refresh pass `markSeen: false`; ordinary initial
  page-one navigation retains the existing mark-seen behaviour.
- Distinguish successful, partial and failed section reads. On automatic
  refresh, retain a failed section's last good content/time and mark it stale;
  replace independently successful sections. A first read with no good data
  shows unavailable. Do not label a partially refreshed page fully current.
  Reuse server-rendered section status markers rather than inventing a new
  client-side data model. Emit content-safe refresh outcome telemetry.

**Proof:** apply changed content and timestamp; preserve filters and both page
positions; never advance New cases' seen boundary on refresh; do not reopen a
cancelled assignment dialog. Exercise fields, keyboard controls, shell dialogs,
simultaneous triggers, authentication expiry and independently failing sections.
Verify assignment and AI-job forms still submit with valid antiforgery and
operation/version values after replacement.

### W2 — Make image URLs and cache identity agree

Owners:
[existing URL methods](../src/Pegasus.Web/Pages/Cases/Details.Files.cs),
[Report partial](../src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml) and
[download endpoint](../src/Pegasus.Web/Pages/Cases/Documents/Download.cshtml.cs).

- Replace Report's independent URL construction with the existing
  `PreviewUrl`, `DownloadUrl` and `ThumbnailUrl` methods. Identical confirmed
  images in Report and Files must produce byte-identical addresses.
- Add an occurrence-scoped read to the existing
  `ICaseAssetPreparationQueries` port and its persistence adapter. Filter by
  Case and occurrence in SQL; return the current preparation/version together.
  A thumbnail must not load every preparation in the Case.
- Bind the thumbnail endpoint's optional `prep` value. Treat absent preparation
  as version 0. Resolve one preparation snapshot and use its version and
  rotation/crop consistently through conditional requests and rendering.
- Permit long-lived private thumbnail caching and 304 only when the requested
  preparation version matches that snapshot. For an absent or stale `prep`,
  return the current representation with `private, no-store` and no 304.
  This supported stale-page race requires no historical preparation store.
  Reject malformed/negative preparation values without cacheable content.
- Keep document-version IDs as source identity. ETags continue to identify the
  source hash plus rendered variant. Full preview/download remain original
  evidence; failed thumbnail rendering retains the existing non-cacheable
  original-image fallback and transient retry behaviour.
- Preserve authorization and confirmed-custody resolution before cache/304
  responses, plus provider ownership/version, length and hash verification
  where the existing verified-content path requires them.
- Retain current preparation-version increments for role/order-only changes.
  Their extra browser request is an accepted bounded cost for this delivery;
  do not add a second rendering-version field.

**Proof:** Report/Files browser reuse; crop and rotation invalidation; missing,
malformed and stale preparation URLs; save between HTML render and image fetch;
conditional requests; thumbnail failure; pending custody; wrong Case/version;
disabled or unauthorized account. A stale address must never acquire a new
week-long cached representation. Fresh snapshots after a save use the new URL.

### W3 — Give Case interaction one owner and scope its stylesheet

Make `case-workspace.js` the sole owner of Case Scroll/Tabs, section loading,
navigation and scroll tracking. Remove the overlapping Case controller from
`site.js`; retain generic shell binders and any still-required sticky sizing.
Consolidate layout state and sticky offsets around the server-backed Case
controller contract.

Preserve layout cookies, no-script navigation, section deep links, keyboard
movement, hidden current-section fields, unsaved drafts, lease heartbeats,
conflict handling and in-place action swaps. Mount each section and its binders
once; prevent an obsolete response from replacing newer section/workspace state.

Move the `case-workspace.css` link from the shared layout to Details' Styles
section. Inspect its selectors first: if a non-Case consumer needs a rule,
place that rule with the existing shared component styles rather than leaving
the complete Case stylesheet global.

Keep the current prefetch distance for the first comparison; do not change
loading policy while removing competing controllers. Retain the shared SVG
sprite, current font files/coverage, image encoding and compression policy in
this delivery. They are measured follow-ups, not automatic asset rewrites.

**Proof:** one navigation/load owner; direct links and refresh target the right
section; both display modes and editing states work; supported narrow widths,
200% zoom, keyboard/focus, forced colours and reduced motion remain usable.
Non-Case pages omit Case CSS and retain their shared controls/icons.

### W4 — Replace broad Case rendering reads with focused projections

Keep authorization in Core and implement projections in the existing
persistence query owner. Introduce explicit typed read results/use cases for
the page frame and four section bodies; do not populate a partial
`CaseDetails` with fabricated empty collections or add a configurable generic
section-query framework.

| Read | Required content |
| --- | --- |
| Initial page/frame | Case identity, workflow/version, active lease and access decisions, header/action state, and data actually consumed by initially rendered sections. |
| Vehicle | Accepted vehicle facts, required assessment vehicle fields, provenance and actor/source metadata, latest lookup observation, workflow/version and relevant access state. |
| Valuation | Accepted mileage, valuation cards, applied calculation, pending research and actor names; retain the existing editable preset/lease path. |
| Files | Document/version rows, upload links, custody, preparations/report selection, grouped Image intake galleries and render-only lease/access decisions. |
| Notes | Ordered history with the current actor-name enrichment and authorization. |

Use the same focused content readers for direct section rendering and fragment
rendering. Update partial view models, including Report's consumption of the
existing URL methods, without changing binding names or POST business policy.
Reuse the initial page's engineering workspace data when it already supplies
a rendered section; do not reload it through another projection.

Remove initial-page reads that supply only deferred bodies after checking
header, Report, dialogs and action dependencies. Do not remove document data
still needed by an initially rendered Report or broaden lazy loading to editable
sections. Files remains the only deferred section during an edit lease.

Fragments must not read/write cookie-backed TempData or acquire/renew a lease.
Retain Files' `X-Pegasus-Edit-Lease` render-only contract: validate against the
current holder/token/access snapshot, and keep POST authorization authoritative.
Missing records and access failures must retain the intended route semantics.

**Proof:** direct, lazy and prefetched views show equivalent required values,
especially Vehicle assessment/provenance and Valuation applied/pending state.
Verify Notes ordering/names, Files membership/custody/controls, missing records,
other/stale holders and no-cookie fragments. Assert that unrelated body queries
do not run, alongside browser readiness measurements.

### W5 — Correct totals and remove per-row database work

Extend the existing Core ports and infrastructure query owners with focused
count/batch operations. Keep the supported page bounds and deterministic order.
Batch IDs from the current bounded result; use bounded chunks when necessary.
Do not replace N queries with one unbounded join/materialization.

| Consumer | Direct change and retained semantics |
| --- | --- |
| Shared rail/Cases | Pass already-read counts from the handler to the filter through request-local state. Read only missing values on other PageResult paths, including validation failures. Never reuse across requests or actors. Keep the notification list as the single source for bell content/unread count. |
| Stage/Triage/Unidentified totals | Aggregate in SQL through current policy owners. Retain Awaiting-instruction association rules, Triage authorization and complete open-Unidentified semantics. Visible paging limits do not limit totals. |
| Operations badge | Count all retryable failed external work, including active-lease exclusion. Remove dependence on the 100-item mixed Operations display projection; that projection may remain bounded for display. |
| Accounts | Batch administrator-authorized credential statuses for the displayed account IDs. Retain configured/enabled/username/generation/version and disabled-account behaviour. Do not return protected secrets or parallelise the current scoped-context calls. Measure remaining decryption CPU separately. |
| Logs | Batch the distinct displayed Case references and AI-job labels through authorized, bounded reads. Avoid full case headers for reference-only display; retain missing-reference behaviour. |
| Operations failed rows | Batch the displayed failed-intake details and map by ID. Preserve retry eligibility, custody state, failure labels and missing-detail behaviour. |
| Case Files galleries | Apply the Case predicate in SQL before materializing Image intakes, then batch image membership for those intakes. Preserve durable submission groups, image-only filtering, input order and empty/missing results. |
| Inbox scope counts | Compute the seven independently defined counts together. Scopes overlap; retain each scope's mailbox, search, classification, destination, sent and dismissed predicates. |
| Work Centre/Reports/Operations reads | Reuse shared inputs and capture one time boundary per response. Parallelise only demonstrably independent, factory-context-backed reads; retain separately reportable failures. Leave shared-context or dependent operations sequential. |

Case-page counts and shell counts deliberately use the same request-local
result. Independent reads do not promise an atomic database snapshot. Keep
business predicates in their current Core owners and test database projections
against those predicates rather than preserving accidental display caps.

**Proof:** more than 100 mixed Operations candidates; upload links preceding
failures; active work leases; Unidentified/Triage/stage boundaries; invalid
PageResult paths; overlapping Inbox scopes; configured/disabled accounts;
missing log references; grouped galleries with unrelated estate data.
Query growth must depend on the bounded batches, not one read per displayed row.

### W6 — Profile cold images and first-use server work

After W2/W4/W5, measure the remaining thumbnail path in the existing
`CachedDocumentContentStore` and Box adapter. Record preparation/cache SQL,
provider calls and gate waits, original bytes, verification, decoded-image
allocation, rendering and cache writes. Test cold, original-cached,
thumbnail-cached and browser-cached states separately.

Preserve existing provider and decoder limits. Do not add prewarming, move
rendering into custody confirmation, raise concurrency, change cache retention
or introduce another cache in this delivery. These choices would change intake
latency and Worker/Blob/provider costs, and need a measured follow-up specifying
the post-confirmation boundary and best-effort failure semantics.

Profile a controlled fresh process and representative first requests to
separate JIT/EF compilation, rendering, GC, initialization and external waits.
Razor compilation is already enabled; leave it enabled.

Record cold and warm latency, startup-to-ready, peak/steady memory, GC,
attributed CPU/wait time and failures, with the actual profiling platform and
host. A local Windows profile alone does not prove Linux App Service
attribution. Deliver the profile and a recommendation; record unresolved
attribution explicitly rather than naming JIT as the cause.

W6 ends with that evidence and recommendation. It does not require a
ReadyToRun build, comparison host or publishing change. Keep ReadyToRun
disabled in the normal release. Any follow-up experiment must first name its
isolated equivalent Linux host, authorized targets/cost allowance and numeric
success/regression budgets. Do not provision a host implicitly to finish W6.

For that separate experiment, compare the same source, runtime, data and tier,
changing only Web publishing configuration. Supply the setting consistently
to locked restore and publish; retain Linux x64 framework-dependent packaging
and the workstation-native migration bundle. Measure package size and
build/deploy time alongside runtime performance. Keep experiment artifacts
separate, and leave Worker publishing, trimming and Native AOT unchanged.

## 4. W7 — Integrated verification and acceptance

Follow the [verification policy](../docs/engineering.md#verification-policy)
and [runbook](../docs/runbook.md). Before host execution, the primary records
one current verifier, host, frozen inputs and command scope in the operator
task context. All participating sessions share that pointer; the owner checks
active contexts/processes, runs sequentially and records idle before handoff.
Read-only source review can overlap. Retain failed/inconclusive results.

Use the three audited Case shapes, or equivalent isolated fixtures, plus
representative larger data at supported page limits. Include more than 100
Operations candidates and unrelated Image intakes. Exercise Administrator,
Engineer and User roles, Office/Mine, both Case display modes, read/edit,
direct/lazy routes and realistic denial/conflict states. Use approved isolated
data for writes and cold-cache resets; never alter or upload `corpus/`.

Inventory and extend existing query, persistence and Web tests, including
RailCounts, WorkCentre, CaseDetails, Operations, MailWorkspace, image
preparation and document-cache evidence. Add runtime JavaScript/browser
evidence for refresh, controller ownership and cache reuse; HTML-string tests
alone cannot establish those behaviours. Run affected checks first and the
required full CI once the shared script/filter/query changes are integrated.
Reuse qualifying exact-candidate evidence rather than rebuilding each package.

Acceptance requires:

- Every W1–W5 functional proof passes, including the corrected badge total and
  direct/lazy assessment content. No authorization, custody or lease check is
  removed to reduce queries.
- Work Centre refresh avoids layout/shared-count work; Report/Files use the
  same image addresses; one controller mounts sections; unused section reads
  and per-row query patterns are eliminated as specified.
- Baseline/candidate comparisons hold data, role, browser, cache state, runtime
  and tier constant, while recording the different source revisions. Include
  single-user and concurrent multi-user runs; record the exact active-session
  count and retain both results without claiming untested office capacity.
- Report elapsed time, readiness, SQL calls/rows, provider requests, transfer,
  CPU, memory/GC and errors. Reduced query counts alone are insufficient.
  Investigate any reproducible latency, memory or failure-rate regression;
  classify noisy comparisons as inconclusive rather than a pass.
- Observe telemetry volume/cap headroom and cache bytes/expired-entry backlog.
  If any experiment touches processing, also measure intake completion and
  Worker execution. The cleanup path handles only 50 candidates per invocation,
  and provider/decoder gates are process-local.
- Update affected canonical documentation and known callers with the final
  implementation; remove replaced code. Do not change dated deployed
  observations until a release actually occurs.

Implementation completion means W0–W7 evidence is recorded and required code
is integrated; production delivery additionally requires the normal release
procedure and actual post-release smoke. No green test or closed feature gate
substitutes for that distinction.

## 5. Cost and follow-up decisions

App Service B1 and provisioned S0 SQL charges do not fall merely because fewer
queries run. The first benefit is responsiveness and capacity headroom.
Report variable changes in telemetry ingestion, Blob operations/cache size,
Worker execution and any demonstrated provider charges; do not invent a
monetary saving without usage and billing evidence.

Keep these outside the initial implementation:

| Follow-up | Evidence needed before a separate change |
| --- | --- |
| Thumbnail preparation during processing | Provider/decode attribution, verified bytes available after durable confirmation, unchanged successful-custody outcome on render failure, and measured intake/Worker/storage cost. |
| Prefetch-distance changes | One-controller baseline and slow/fast scrolling readiness showing that a changed distance improves the tradeoff. |
| Font subsetting, sprite relocation, thumbnail encoding | Measured remaining payload/layout benefit, shared-consumer/glyph/CSP compatibility and image-quality proof. |
| Dynamic HTML compression | Material network benefit and a specific review of authenticated reflected/token-bearing responses. Retain the present policy meanwhile. |
| ReadyToRun experiment/release | W6 attribution justifying an experiment, a named authorized Linux host/cost allowance and success budgets, followed by a controlled comparison before accepting a restore/publish/package change. |
| Web/SQL tier or instance count | Remaining resource pressure or an explicit unmet target after application fixes, current inventory/pricing, and aggregate provider/connection effects. |

Stop after these delivery requirements pass. Remaining speculative optimizations
become evidence-backed follow-ups rather than expanding the current change.

### Technical references

- [ReadyToRun tradeoffs](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run)
- [ReadyToRun restore requirements](https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/6.0/publish-readytorun-requires-restore-change)
- [Efficient EF querying](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying)
- [Application Insights Entra restrictions](https://learn.microsoft.com/en-us/azure/azure-monitor/app/azure-ad-authentication#unsupported-scenarios)
- [Telemetry daily caps](https://learn.microsoft.com/en-us/azure/azure-monitor/logs/daily-cap)
- [Authenticated response compression](https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression?view=aspnetcore-10.0)

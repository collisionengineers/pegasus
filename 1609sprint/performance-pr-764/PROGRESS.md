# PR 764 performance progress report

Snapshot: 16 September 2026, approximately 12:58 UTC / 13:58 BST.
Temporary operator-requested handover. See [plan and aims](PLAN.md).

## Overall verdict

The confirmed product changes are implemented, committed and pushed in
[PR #764](https://github.com/collisionengineers/pegasus/pull/764).
Focused functional tests, source reviews and an offline routed UI check have
passed, with the qualifications below. The latest full CI is not green:
two SQL jobs timed out. Six-shard/test-split remediation is prepared locally
but has not yet been built, tested, committed or pushed.

The PR is open and unmerged. This task has performed no dev integration,
main promotion or production deployment. Performance acceptance remains open;
functional test passes are not proof of the required live latency gains.

## Source identities and locations

| Item | Value |
| --- | --- |
| PR title | Improve first-use Case paths and production thumbnail caching |
| PR base branch | `dev` |
| Implementation baseline | `5765a527a7729e606fe5683b3af3d47a17c708ff` |
| Branch | `perf/first-use-and-image-cache` |
| Committed local / current PR head | `e4fc0ea05762aab67dd6ec3c8aaf35a280d5f2aa` |
| Implementation worktree | `C:/Users/Alex/Documents/GitHub/pegasus-worktrees/performance-next` |
| Last production version observed by this task | `e8efb19779baadc5ea46bd9c62e6c9c54740cac7`, version `0.1.0-alpha.1` |
| New local delta | Six-shard CI plus Case test reorganization; no additional product change |

Production identity above is a retained observation, not a fresh cloud check
performed for this report. Other sessions' changes in the original checkout,
including Operations and v27 material, have not been incorporated or altered.

## Package-by-package progress

| Package | Implemented and evidenced | Still outstanding |
| --- | --- | --- |
| P0 | Allowlisted sampled workspace/authentication/shell/renderer/queue phases; real authenticated production warm baseline; telemetry composition and activation/result timing tests | Controlled Linux cold attribution/profiles, candidate comparison and measured telemetry/resource overhead |
| P1 | Successful validation suppresses validation-only renewal; protected Razor no-store independent of renewal; real Identity-cookie integration tests cover private 200/authorized 304, revocation, validation, sliding/idle/absolute lifetime behavior | Actual candidate authenticated browser cache reuse and deployed acceptance |
| P2 | Same-request Case-bound frame reuse for direct sections, custody scalar reuse and lazy report-service activation; mismatch/reuse/report-readiness tests passed in prior focused evidence | Matched first-use/section timing gains on the target environment |
| P3 | Scaled plain decoding, current renderer r2 URL/cache/ETag identity, same-key miss coordination and buffering after decode admission; corrected image/concurrency/cancellation tests passed | Matched nine-image cold/warm bursts, provider/Blob cost, RSS/GC and concurrent-workload acceptance |
| P4 | Hidden palette no longer scrolls; routed offline palette/navigation/image/edit-cancel/narrow-width checks passed | 200% zoom, forced colours, reduced motion and statistical candidate UI timing; font/compression work explicitly not selected |
| P5 | PR open; earlier complete-source/correction reviews found no blocker; retained local evidence and read-only release preflight | New split review/build/tests, green full CI, dev integration, release artifacts/approval and live acceptance |

## Commit trail

| Commit | Contribution |
| --- | --- |
| `c86358f39` | Timing attribution and implementation plan |
| `d35e2cab5` | Identity renewal/private-cache handling |
| `047af6a69` | Case frame reuse and lazy report activation |
| `b8f7b1b55` | Closed-palette scroll correction |
| `ab3f24896` | Frame validation/reuse tests |
| `1598d3475` | Scaled thumbnail decoding and miss coordination |
| `a13e97373` | Architecture documentation and first frozen verification candidate |
| `0a63f80b3` | Valid PNG fixtures, Identity-specific assertions, orientation and cancellation corrections |
| `911230ead` | Observe successful principal refresh rather than infer it from SQL reads |
| `e4fc0ea05` | Require and preserve the configured refresh callback; final prior local verification |

The latest CI-remediation source is not represented by a commit yet.

## Verification ledger, including failed attempts

| Candidate/run | Result |
| --- | --- |
| `a13e97373` restore/build | Locked restore and Release solution build passed; zero warnings/errors |
| `a13e97373` focused Integration | 341 total: 337 passed, four failed, none skipped; about 32m09s |
| `a13e97373` Core/Architecture | Core 2,199 passed and 14 skipped; Architecture 120 passed |
| `a13e97373` documentation | Links and placement passed |
| `0a63f80b3` build | Passed; zero warnings/errors |
| `0a63f80b3` corrected four-class run | 30 total: 29 passed, one failed; all four original failures corrected |
| `e4fc0ea05` affected build | Integration Release build passed; zero warnings/errors |
| `e4fc0ea05` lifetime test | One passed, none failed/skipped; preserved callback proves successful stamp validation and lifetime boundaries |
| `e4fc0ea05` documentation | Links passed for 381 files; placement passed |
| New six-shard/test-split source | Static evidence only; build, discovery and execution not yet run |

The original failures were two assertions banning every Set-Cookie header
instead of Identity renewal specifically, and two concurrency tests sharing
a malformed PNG. The PNG had a missing RGBA scanline byte and invalid IDAT
CRC; it was replaced with valid generated bytes. The remaining corrected-run
failure counted SQL user reads, which EF tracking can collapse. It was
replaced with observation of the preserved principal-refresh callback.
No failed attempt has been relabelled as a pass.

Representative exact tests in the retained evidence include:

- `WorkspaceRequestTimingFilterTests.TimingsEncloseActivationAndResultWithoutEmittingRouteValues`
- `ProductionCompositionTests.ProductionWebTelemetryEmitsAllowlistedDocumentTimingWithRequestCorrelation`
- `IdentityCookieLifetimeWebTests.ValidationDoesNotReissueAnIdentityCookieButSlidingExpirationStillDoes`
- `IdentityCookieDownloadWebTests.ACurrentThumbnailUsesPrivateCachingWithoutCookieRenewalAndRevalidatesOnlyForAnAuthorizedUser`
- `CaseSectionQueryValidationTests.RefusesAFrameWithEitherCaseIdentityMismatch`
- `CaseSectionQueryValidationTests.DirectSectionReadersReuseTheSuppliedFrame`
- `AssessmentReportDraftWebTests.CaseGetUsesMetadataReadinessWithoutOpeningPreviewOrRendering`
- `DocumentContentCacheTests.PlainThumbnailScalesAnOrientedLargeJpegToTheDisplayed480PixelEdge`
- `DocumentContentCacheTests.PlainThumbnailScalesTransparentPixelsOntoWhite`
- `DocumentContentCacheTests.ConcurrentSuccessfulThumbnailMissesFetchAndRenderOneCurrentVariant`
- `DocumentContentCacheTests.CancelledThumbnailWaiterDoesNotFetchWhileTheCurrentVariantRenders`
- `DocumentContentCacheTests.CancelledThumbnailSourceReadPropagatesCancellation`

Cancellation is checked before and after native rendering. This is not a
claim that an in-progress native decode is interruptible or deterministically
tested for mid-decode cancellation.

## Current full CI

[Run 35083151345](https://github.com/collisionengineers/pegasus/actions/runs/35083151345)
is completed at the current PR head. It is not running and not green.

| Check | Outcome |
| --- | --- |
| Unit | Passed, 5m07s |
| SQL 1 | Timed out, 45m01s; all 860 assigned rows completed: 858 passed, two skipped |
| SQL 2 | Timed out, 45m07s; 757 assigned rows, incomplete execution and no completed TRX |
| SQL 3 | Passed, 16m47s; 732 rows passed |
| Partition verification | Skipped after cancellation |
| Changes, documentation, local-development scripts, reference data | Passed |
| Infrastructure | Path-skipped for that head |

The [CI remediation report](../ci-six-shards/PROGRESS.md) contains the
31-minute CaseDetails bottleneck, exact pending changes and verification
handover. Existing PR prose saying CI is pending needs updating on resumption.

## Browser and performance observations

Real operator-authenticated production baseline: 1920x855, DPR 1, normal
cache, no throttling, fixed Office -> Mine -> Case A -> Case B order.
Process and caches were not reset. These are warm baseline p95 values,
not candidate measurements or controlled cold evidence.

| Route | Valid LCP samples | p95 LCP | Paired p95 TTFB |
| --- | --- | --- | --- |
| Work Centre Office | 30 | 464ms | 310ms |
| Work Centre Mine | 30 | 1,096ms | 958ms |
| Case A | 30 | 672ms | 544ms |
| Case B | 30 | 464ms | 291ms |

The initial 120 navigations retained 34 missing paint observations. A
foreground-tab supplement of 36 navigations supplied valid observations;
first 30 valid samples per route were used, never zero-filled. Case B had
nine thumbnail addresses. No recorded script/resource errors. A double-frame
DOMContentLoaded marker is only a paint opportunity, not meaningful readiness.

Offline candidate functional check at e4fc0ea05 reused an existing synthetic
fixture on Windows Release, Web only, DevelopmentOffline authentication:

- Scroll/Tabs and Vehicle, Valuation, Files, Notes and Report loaded.
- Three images rendered at 480x360 with matching Report/Files r2 URLs.
- Edit-mode image controls appeared; Cancel returned to read mode without save.
- Closed palette made zero scroll calls; open/ArrowDown/Escape/focus return
  behaved correctly.
- Widths 1920, 960 and 390 showed no page-wide horizontal overflow.
- No recorded JavaScript or failed resource-status errors.

The zoom shortcut did not change measurable scale, so 200% zoom is unverified.
Forced colours/reduced motion were not exercised. Invalid resize/click/stop
attempts were retained separately. Owned runtime and browser tab were stopped;
the user's production session was not modified. This does not prove actual
candidate Identity browser reuse, Linux performance or statistical timings.

## Release status and remaining gates

Read-only preflight previously checked the production account/targets and
unchanged migration tree. No final artifact manifest, Web/Worker packages or
approval packet has been produced for this candidate. No Azure writes or
release were performed by this task.

The expected release remains Web and Worker because Core/Infrastructure are
shared. Recheck final source, migrations and targets when packaging; do not
reuse old preflight as current authorization. The last recorded Web target
was `pegasus-prod-web-252ow37gij` in `rg-pegasus-prod`, Linux B1, UK South;
Worker was `pegasus-prod-worker-252ow37gij`.

Required next steps are the exact-head six-shard verification, then reviewed
dev integration, release packaging and fresh main/Azure approval. Linux cold,
real-cookie candidate browser reuse, matched image bursts/resource costs,
accessibility gaps and live acceptance must remain visible until evidenced
or explicitly accepted. No extra restart, purge or environment is authorized.

## Evidence locations and resumption

Original-checkout private evidence:

- `artifacts/performance/implementation-20260916/`: task context, baseline
  JSON/supplement, UI results, timeout logs/TRX, PR draft and local reviews.
- `artifacts/performance/lcp-20260916/`: earlier latency report and attribution.
- `artifacts/performance/operator-20260915/host-slot.json`: the sole host slot.

Implementation-worktree verification logs/TRX:

- `artifacts/performance/a13e97373/host-verification-20260916/`
- `artifacts/performance/0a63f80b3/host-verification-20260916/`
- `artifacts/performance/e4fc0ea05/host-verification-20260916/`

On resumption, start with the new test split's independent review, not another
production baseline collection. Freeze the source and obtain a fresh host
grant before local verification. Keep unrelated sessions' changes intact.
Do not publish raw cookies, business content, document URLs or local corpus.

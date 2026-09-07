# INTK-062 implementation report

## Result

Verified source frozen in .worktrees/intk-062 on INTK-062-public-upload-bound,
original base 783b537f189ead88553f940d03df0d1f9558ef75. Root supplied the
focused passing runtime checks below and authorized scoped commit/PR/Review.
No author build, test, capture, cloud call or deployment.

## Changed files

| File | Change |
| --- | --- |
| src/Pegasus.Web/Pages/Uploads/RequestUploadTransportFilter.cs | Early page authorization filter checks route token before form read; native size feature plus framework bounded whole-body/section buffering. |
| src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs | Applies filter before antiforgery; FromRoute token; POST reuses preflight public view. |
| tests/Pegasus.IntegrationTests/PublicUploadRetentionWebTests.cs | Eight focused cases using existing HTTP/SQL/custody fixtures and one small nonseekable observation stream. |
| docs/frd/frd-02-intake-and-source-identity.md | Clarifies pre-buffer enforcement and route identity acceptance; no policy limits changed. |

## Behavior and simplicity

Configured per-file limit plus 64 KiB finite multipart overhead bounds the
HTTP body; the native server feature is set before form read. Unknown,
expired and revoked link queries return404 without reading; declared excess
returns413 unread. ASP.NET FormFeature buffers the total body once and uses
file offsets into it, so a second file cannot independently consume another
file allowance. Missing/understated Content-Length cannot bypass actual-byte
buffer limits. Built-in form/antiforgery rejection remains the failure path,
not a new custom multipart parser, stream or upload framework.

The TypeFilter is only on the existing public Request page, order-2000 before
antiforgery. Program/global staff batch limits remain unchanged. POST query
count stays one by reusing the request-local public view; durable commands
still validate current request/session/custody authorization. Finalize keeps
its current per-token limiter before finalization work; its preflight token
read necessarily now precedes body/model binding and that limiter, and its
old comment was corrected. GET and completion behavior are unchanged.

FRD-02 is the existing owner; documentation skill kept the clarification in
its request-scoped upload section. No ADR, package, schema, runtime or UI
markup changed. No snapshots are required for unchanged markup.

## Evidence and required checks

Worker standalone git -c core.safecrlf=false diff --check: exit0.
At the initial freeze only caller checks had run; the later root runtime
results are recorded below. Read-only discovery
hit absent guessed IntakeWebApplicationFactory.cs and Status.cshtml.cs paths;
actual owners are IntakeWebTestSupport.cs and StatusCode.cshtml.cs.
No validation failure was hidden because no build/test was run.

Root-only first focused Integration filter:
FullyQualifiedName~PublicUploadRetentionWebTests.PublicTransport|FullyQualifiedName~QdosCustodialWebTests.PublicRequestUploadUsesOneCoreCommandAndPrgWithGenericCompletion|FullyQualifiedName~PublicUploadRetentionWebTests.PublicPageAddsReplacesFinalizesAndRefusesLaterBytes|FullyQualifiedName~PublicUploadRetentionWebTests.AReplacementNamingAnotherLinksOccurrenceIsRefused|FullyQualifiedName~PublicUploadRetentionWebTests.ALinkFromAnotherLimitsVersionRendersTheTypedRefusalAndWritesNothing

These include the current maximum-file success, header excess/unknown token
unread cases, unknown and misleading length aggregate streams, route/form
token isolation, existing query-count assertion, replacement/finalization and
limits-version refusal. Full existing PublicUploadRetentionWebTests and
PublicUploadSessionTests remain available if root finds a relevant gap; do
not start duplicate rails automatically.

Native transport enforcement is set but not run on a deployed host. The new
actual-stream tests exercise the real TestServer HTTP pipeline and count
consumed bytes rather than trusting headers. No performance/stress claim.

## Initial stop

Source and claim were retained for root verification without author builds or
PR. Root's subsequent verified handoff is recorded below.


## Root verification and authorized handoff — 2026-09-07

Environment: Windows, PowerShell 7, .NET 10, existing SQL/HTTP fixtures in
.worktrees/intk-062. Root was the sole heavy verifier. Exact commands:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~PublicUploadRetentionWebTests.PublicTransport|FullyQualifiedName~QdosCustodialWebTests.PublicRequestUploadUsesOneCoreCommandAndPrgWithGenericCompletion|FullyQualifiedName~PublicUploadRetentionWebTests.PublicPageAddsReplacesFinalizesAndRefusesLaterBytes|FullyQualifiedName~PublicUploadRetentionWebTests.AReplacementNamingAnotherLinksOccurrenceIsRefused|FullyQualifiedName~PublicUploadRetentionWebTests.ALinkFromAnotherLimitsVersionRendersTheTypedRefusalAndWritesNothing" --logger "trx;LogFileName=intk-062-focused.trx"
```

Root reports restore PASS exit0; Release build PASS exit0, 53.21 seconds,
zero warnings/errors; focused tests PASS exit0, 12 passed, zero failed/skipped,
77 seconds. No failed executable attempt in this cohort. The eight new public
transport tests and four existing request/custody tests ran once, without a
second rail. Core policy was unchanged; the earlier RequestUploadPolicyTests
suggestion was an incorrect candidate name, not an executed check.

TRX retained at tests/Pegasus.IntegrationTests/TestResults/intk-062-focused.trx.
Its counters were independently read back: total/executed/passed12 and all
failure, inconclusive and notExecuted counters0. Start22:33:13.3338282Z,
finish22:34:32.9251039Z; SHA256
A3DEBFD39C5E6F479D7AE4654625687664DEABE035AFC82D5053767C2D367AB4.
Root confirmed the exact expanded command above. No native deployed-server,
manual browser or performance claim is made.

Resumed packet ready with the exact existing root/branch, common Git and
single-claim census validated. Its current delivery base was
522e67f270ab4d6086d9fba04095988db3598888; original checked-out base stayed
783b537f189ead88553f940d03df0d1f9558ef75, not silently rebased. Packet's 41
unrelated stale-location warnings are controller-owned and were not repaired.
No ticket reference directory exists. One read-only discovery of that absent
directory returned exit1; it was not a verification/test failure.

Final author static diff check `git -c core.safecrlf=false diff --check` PASS
exit0. Root explicitly authorized [skip ci] for this scoped PR; required checks
must still be respected and final integrated release CI is separately owed.
Stop after push and Implementing→Review; independent review next, no author
self-review, merge, cleanup or deployment.

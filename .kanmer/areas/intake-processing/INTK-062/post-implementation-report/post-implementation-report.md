# INTK-062 implementation report

## Result

Source frozen, uncommitted, in .worktrees/intk-062 on
INTK-062-public-upload-bound at base783b537f189ead88553f940d03df0d1f9558ef75.
No build, test, capture, cloud call, PR or deployment performed by this lane.

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
Code caller checks only; runtime PASS is not claimed. Read-only discovery
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

## Stop

Retain lease/worktree frozen pending root verification and explicit next
handoff. Do not commit, PR, moveReview, self-review, merge or deploy now.

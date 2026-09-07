# Research — INTK-062: public upload transport bound

## Question

Why can anonymous public-link requests buffer more than the accepted file
limit, and which existing mechanism bounds bytes before antiforgery?

## Findings

- origin/dev 783b537f189ead88553f940d03df0d1f9558ef75: Program.cs:658-663 configures global FormOptions with IntakeEnvelopeLimits.MaximumBatchContentLength; public Request.cshtml.cs:70-108 reads IFormFile after antiforgery/model binding. PR675 full review finding 2 matches actual code.
- Request.cshtml declares /Uploads/{token}; one Upload file per POST. RequestUploadLimits and PublicView supply the configured per-file bound. Existing IGetRequestUpload (EfDocumentRequestStore.cs:1230 onward) rejects malformed/unknown/expired/revoked links without touching upload body. Refusal-only limits-version view must stay supported; final upload command rechecks authorization/session.
- ASP.NET FormFeature buffers files by default. With BufferBody enabled it uses one bounded whole-body buffer and file offsets into it, not a second file copy. BufferBodyLengthLimit bounds aggregate body; MultipartBodyLengthLimit bounds individual sections. Source: https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.0/src/Http/Http/src/Features/FormFeature.cs (185-187, 235-253). Official upload guidance: https://github.com/dotnet/AspNetCore.Docs/blob/main/aspnetcore/mvc/models/file-uploads.md.
- IHttpMaxRequestBodySizeFeature is native server enforcement; add it before reading. Content-Length alone cannot establish actual streamed size. Built-in bounded form buffering also works in TestServer where the server feature is absent. No custom stream or multipart framework is required.
- An early page authorization filter ordered before antiforgery is page-scoped and avoids broad Program changes. Bind Token from route only so body/query cannot substitute another request after preflight. Reuse the request-local public view for POST; durable commands retain all final checks.
- Existing PublicUploadRetentionWebTests has real SQL/token/page/antiforgery/custody fixtures and a partial class. Extend it with a small counting nonseekable test stream; no real 10 MiB/stress load needed since configured test limits are 1 MiB.
- Sources registry contains zero declarations. No cloud call, build or test has run during research.

## Implications

Use existing ASP.NET limits on the public page, with 64 KiB finite multipart
overhead beyond the configured single-file size. A declared oversize is 413
without reading; unknown/revoked/expired tokens are 404 without reading.
Stream/form excess is an ordinary bounded bad request from ASP.NET, with no
custody call. Keep global staff batch limits unchanged.

## Open questions

None; current user and root authorize this fix. Root alone verifies.

# Files — INTK-062

## Where the change lands

| Path | Why |
| --- | --- |
| src/Pegasus.Web/Pages/Uploads/RequestUploadTransportFilter.cs | New narrow early authorization filter on the existing public page; existing framework body/form limits and token query. |
| src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs | Attach early filter, route-only token binding, reuse request-local public view for POST. |
| tests/Pegasus.IntegrationTests/PublicUploadRetentionWebTests.cs | Existing real-page/SQL fixtures; bounded stream, early refusal, maximum supported file, token isolation assertions. |
| docs/frd/frd-02-intake-and-source-identity.md | Clarify pre-buffer transport acceptance in existing request-link section. |

## Context files

| Path | Constraint |
| --- | --- |
| src/Pegasus.Core/Documents/RequestUploadPolicy.cs | Existing accepted file/submission limits and token/session policy; do not duplicate business rules. |
| src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs | IGetRequestUpload token/expiry/revocation/refusal-only view; command still revalidates on durable upload. |
| src/Pegasus.Web/Program.cs | Global staff multipart limit is intentionally larger; absence gates and existing limiter stay unchanged. |
| src/Pegasus.Web/Pages/Uploads/Request.cshtml | One file per POST and current antiforgery form; no markup change or snapshot refresh. |

## Ripple effects

Only the existing public route receives the filter; no container/runtime/package
or schema change. Preserve finalize, replacement and custody assertions.

## Out of scope

Global staff/provider upload policies, limiter redesign, session policy,
custody storage, Program.cs, other active ticket files and live writes.

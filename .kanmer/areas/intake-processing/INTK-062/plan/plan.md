# Plan — INTK-062: bound public bodies before buffering

## Objective

Reject unsupported public upload bodies before unbounded multipart buffering,
using the existing route, token query and ASP.NET form/server limits.

## Starting state

origin/dev 783b537f189ead88553f940d03df0d1f9558ef75. Evidence:
research/research.md@73a7f7c8e3033e7b, files/files.md@d0d48ecd2a2c8aa9.
Existing page validates file length after form binding. EPIC-014 authorizes
this correction; no open user choice or extra approval remains.

## Governing docs

Meets FRD-02 request-link file/size/abuse, token isolation and nondisclosure
requirements. Clarify the existing acceptance paragraph with pre-buffer
enforcement under the user's explicit regression-fix authority. No ADR needed.

## Required changes

Attach a TypeFilter authorization filter at order -2000 on RequestModel,
before antiforgery. For POST only, query IGetRequestUpload using route token;
404 if unavailable without consuming body. Allow the existing refusal-only
view. Derive transport cap from that view's configured file limit plus 64 KiB
multipart overhead. Reject declared excess with 413. Set native max-request
body feature where available and a route-local FormFeature with BufferBody,
bounded BufferBodyLengthLimit and per-file MultipartBodyLengthLimit. This
framework buffer is shared with file offsets; add no custom production stream.
Store the public view in HttpContext.Features for this POST; handlers reuse
it and durable Core command still rechecks link/session authority. Bind Token
only FromRoute so form/query values cannot change the preflight identity.
Leave GET/completion rendering and operation/finalize/attempt logic unchanged.

## Expected files

| Action | Path | Purpose |
| --- | --- | --- |
| Add | src/Pegasus.Web/Pages/Uploads/RequestUploadTransportFilter.cs | Page-specific early token/body admission. |
| Modify | src/Pegasus.Web/Pages/Uploads/Request.cshtml.cs | Filter binding and request-local public view. |
| Modify | tests/Pegasus.IntegrationTests/PublicUploadRetentionWebTests.cs | Focused current HTTP/SQL fixture regressions. |
| Modify | docs/frd/frd-02-intake-and-source-identity.md | Pre-buffer acceptance clarification. |

## Do not modify

- src/Pegasus.Web/Program.cs
- src/Pegasus.Core/**
- src/Pegasus.Infrastructure/**
- docs/operator-notes.md
- docs/design/test-ui/**
- infra/**
- corpus/**

## Constraints

No new dependency, upload framework, UI markup, global policy or extra test
host. Existing public stream boundary justifies the one narrow filter.
Use configured test bounds, existing receipt/link/custody fixtures and small
non-domain test bytes. Root is sole heavy verifier; no build/test/capture/live
call by implementation lane. No retained review worktree mutation.

## Ordered steps

1. Add early filter and route-only token binding; reuse its public view for
   POST while preserving final command validation and GET completion.
2. Extend existing tests: unavailable token body is unread; advertised excess
   unread; actual unknown/understated length and multiple sections stop within
   finite buffer allowance with no custody/arrival; a maximum supported file
   plus normal multipart succeeds; form/query token cannot redirect scope.
   Retain existing revocation/expiry/cross-request/finalize assertions.
3. Clarify FRD-02, run static diff/caller check only, record exact root filter
   and freeze source awaiting coordinated verification.

## Acceptance checks

Production caller is /Uploads/{token} Razor page with TypeFilter preceding
antiforgery. Failed admission does not create occurrence or custody call.
Actual body reads are counted, not inferred from Content-Length; at most one
framework read chunk can cross the buffer threshold before rejection.
Valid maximum file and ordinary overhead still reach existing custody.
No claim that transport refusal is a case lifecycle change.

## Commands

Worker: git diff --check and static caller/route checks.
Root: existing Release --no-build integration filter
FullyQualifiedName~PublicUploadRetentionWebTests and Core/session filter
FullyQualifiedName~RequestUploadPolicyTests|FullyQualifiedName~PublicUploadSessionTests
as proportionate. No snapshot scope because Razor markup is unchanged.

## Failure and deviation rules

Record failures and do not weaken assertions. Report extra shared file or
schema/package requirements before editing; no silent transport framework.

## Stop condition

Freeze implementation/tests/docs and provide root filters for verification.
Keep lease/worktree. No build/test, commit, PR, Review move or live write
until root supplies verification and explicit next handoff.

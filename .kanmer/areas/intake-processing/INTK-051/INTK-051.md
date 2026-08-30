---
id: INTK-051
type: ticket
title: Activate INT-31 upload links in production under recorded interim limits
status: verifying
area: intake-processing
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-08-29T21:20:01.585Z'
  implementing: '2026-08-29T21:21:53.273Z'
  review: '2026-08-29T21:30:55.140Z'
  verifying: '2026-08-30T00:05:51.669Z'
labels:
  - INT-31
  - requires-live-approval
  - uploads
groups:
  - EPIC-011
links:
  - DELIV-037
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
prs:
  - '633'
archived: false
created: '2026-08-29T21:15:02.437Z'
updated: '2026-08-30T00:06:28.861Z'
---

## What

Configure the `DocumentRequests` section in `infra/modules/platform.bicep` so
document upload links compose in production, under the interim limits the
operator accepted on 2026-08-29, and give the anonymous surface the
transport-level bound the register claimed it already had.

Merged as PR **#633** into `dev` at `ce0bfbac`.

## Why this is not just a flag

Upload links were unavailable in production, and **no `Features:` flag controls
them**. `src/Pegasus.Web/Program.cs:247-250` composes the upload-link services
only when `DocumentRequests:AcceptedLimitsVersion` is non-empty, and production
set no `DocumentRequests` section at all.

The gate is all-or-nothing: **fifteen** entries are required together — the
seven `AllowedMediaTypes__N` no less than the eight scalars, because
`Program.cs:266-268` throws when the array binds to null. `LimitsVersion` must
match `AcceptedLimitsVersion` byte for byte (`:258-264`), and
`RequestUploadLimits`' constructor (`RequestUploadPolicy.cs:32-46`) rejects a
zero or negative bound.

**Corrected in review:** an earlier version of this ticket said a partial
configuration "throws at startup and crash-loops the container". **It does
not.** `RequestUploadLimits` is a lazily resolved factory singleton
(`DependencyInjection.cs:475`), nothing resolves it eagerly, and
`ValidateOnBuild` follows `IsDevelopment()` — false in Production. A missing or
misspelled key surfaces as a **500 on the first request** touching `/Uploads`, a
case's documents, or the Operations page. There is no fail-fast net, which is
why the binding was verified directly rather than trusted (see
`scratch/notes.md`).

## The accepted interim limits

`docs/open-decisions.md` recorded INT-31 as open, with an interim bound: "the
existing aggregate 10 MB intake limit; hashed 256-bit token; anonymous
`/Uploads/{token}` form; no case disclosure". The operator accepted this set as
an **interim activation, not a closure**:

| Setting | Value | Basis |
| --- | --- | --- |
| `LimitsVersion` / `AcceptedLimitsVersion` | `int-31-interim-v1` | names the interim set |
| `MaximumRequestBytes` | `10485760` | the recorded interim bound — `IntakeEnvelopeLimits.MaximumContentLength` exactly (`IntakeContracts.cs:13`) |
| `MaximumFileBytes` | `10485760` | one file may use the whole aggregate |
| `MaximumFileCount` | `10` | |
| `LifetimeHours` | `168` | 7 days, matching the chase cadence (CASE-17/18, MAIL-18) |
| `RateLimit` / window | `20` / `10` min | per **token** |
| `PublicUploadLink.RequestsPerClientPerMinute` | `30` | per **calling address** |
| `AllowedMediaTypes` | `application/pdf`, `image/jpeg`, `image/png`, `…wordprocessingml.document`, `application/msword`, `message/rfc822`, `application/vnd.ms-outlook` | the seven `MimeKitPdfPigOpenXmlIntakeSourceReader.DetectFormat` resolves (`:971-1014`) |

**Corrected in review:** the content-type list was wrong in both directions. It
admitted `text/plain`, which the reader classifies `Unsupported`, and omitted
`message/rfc822`, `application/msword` and `application/vnd.ms-outlook`, all of
which it reads — so an operator sending a link to a third party whose evidence
is a forwarded `.eml` or a legacy `.doc` would have had it refused with no
explanation.

**Corrected in review:** the class is `IntakeEnvelopeLimits`, not
`IntakeContracts`.

These are **not** the integration fixture's values
(`IntakeWebTestSupport.cs:128-141`, `integration-fixture-v1`, 1-hour lifetime,
1 MB per file). Those are test values and must not become production policy.

## The bound the register claimed but did not have

`open-decisions.md` recorded "per-token/per-IP rate" as settled. Only the
per-token bound existed, and it **cannot** bound a caller who holds no token:
`RequestUploadAttemptLimiter` partitions on the token digest, and
`RequestModel.OnPostAsync` answers `NotFound` for an unknown token before the
limiter is consulted.

That gap was unreachable while the gate was closed — the middleware
short-circuited `/Uploads` to 404 before a body was read. **Opening the gate
removed that short-circuit**, so Razor Pages' antiforgery filter now reads and
buffers the whole multipart body before rejecting, against a single replica at
cpu 1.0 / 2 GiB shared with headless Chromium.

`PublicUploadLink` adds the missing bound, partitioned by calling address as
staff sign-in, the MCP ingress and the Provider API already are, attached to the
one anonymous page by a Razor Pages convention so no authenticated page joins
the limiter. `UseRateLimiter` runs after routing and before endpoint execution,
so a refused caller is answered 429 without its body being read.

## Boundaries

- This does **not** close INT-31. One-time vs reuse and the revocation/expiry
  error contract remain undecided.
- Raising the per-file cap to 100 MB is [[INTK-052]] — a Core change, not
  configuration.
- A version change is **not yet a migration**: `RequestUploadPolicy.Authorize`
  throws on a `LimitsVersion` mismatch and `HasAcceptedLifetime` requires an
  exact lifetime match, so any version move invalidates every outstanding link.
  Harmless at zero links; settle before the second version.

## Verification

- [x] `Test-AzureDeploymentPlan.ps1 -Mode Local` exit 0
- [x] All fifteen settings present; the array binding proven empirically
      because there is no fail-fast net behind it
- [x] `LimitsVersion` and `AcceptedLimitsVersion` byte-identical
- [x] `open-decisions.md` records the activation as interim and still lists
      INT-31's unresolved parts, in the conditional tense until release 37 deploys
- [x] The per-address bound refuses, proven by
      `PublicRequestUploadRefusesAnAddressThatHoldsNoToken` and a discrimination
      check (removing the convention makes it fail `Assert.Contains`)
- [ ] After deployment, `/Uploads/{token}` composes and the Operations surface
      reports the active limits

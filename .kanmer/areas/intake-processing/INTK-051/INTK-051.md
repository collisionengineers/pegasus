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
updated: '2026-08-30T00:05:51.669Z'
---

## What

Configure the `DocumentRequests` section in `infra/modules/platform.bicep` so
document upload links compose in production, under the interim limits the
operator accepted on 2026-08-29.

## Why this is not just a flag

Upload links are unavailable in production today, and **no `Features:` flag
controls them**. `src/Pegasus.Web/Program.cs:241-250` composes the upload-link
services only when `DocumentRequests:AcceptedLimitsVersion` is non-empty, and
production sets no `DocumentRequests` section at all. The code comment says
this is deliberate, pending the INT-31 open decision.

The gate is stricter than one value. Once `AcceptedLimitsVersion` is set, the
factory at `Program.cs:250-278` requires **eight** settings and throws if any is
missing or inconsistent:

- `LimitsVersion` must exist and **exactly match** `AcceptedLimitsVersion`
  (`Program.cs:258-264`) — a mismatch throws at startup and crash-loops the
  Container App.
- `AllowedMediaTypes` must be a non-empty array (`Program.cs:266-268`).
- `LifetimeHours`, `MaximumFileCount`, `MaximumFileBytes`,
  `MaximumRequestBytes`, `RateLimit`, `RateLimitWindowMinutes` are all read and
  validated by `RequestUploadLimits`'s constructor
  (`src/Pegasus.Core/Documents/RequestUploadPolicy.cs:32-46`), which throws on
  zero or negative values.

So a partial configuration does not degrade — it takes the whole application
down.

## The accepted interim limits

`docs/open-decisions.md:71-75` records INT-31 as open — "exact token lifetime,
aggregate and per-file byte limits, file count, allowed content types,
per-token/per-IP rate, one-time vs reuse, and revocation/expiry error contract"
— with an **interim bound**: "the existing aggregate 10 MB intake limit; hashed
256-bit token; anonymous `/Uploads/{token}` form; no case disclosure".

The operator accepted this set on 2026-08-29 as an **interim activation, not a
closure of INT-31**:

| Setting | Value | Where it comes from |
| --- | --- | --- |
| `LimitsVersion` | `int-31-interim-v1` | names the interim set, so a later accepted set is a version change |
| `AcceptedLimitsVersion` | `int-31-interim-v1` | must match exactly |
| `MaximumRequestBytes` | `10485760` | the recorded interim bound — `IntakeContracts.MaximumContentLength` is the same 10 MB |
| `MaximumFileBytes` | `10485760` | a single file may use the whole aggregate |
| `MaximumFileCount` | `10` | |
| `LifetimeHours` | `168` | 7 days, matching the existing chase cadence (CASE-17/18, MAIL-18) |
| `RateLimit` | `20` | |
| `RateLimitWindowMinutes` | `10` | |
| `AllowedMediaTypes` | `application/pdf`, `image/jpeg`, `image/png`, `text/plain`, `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | exactly what the intake reader already handles (`MimeKitPdfPigOpenXmlIntakeSourceReader`) — no new file type enters the estate |

**These are not the integration fixture's values.** That fixture
(`tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs:128-141`) uses
`integration-fixture-v1` with a 1-hour lifetime and 1 MB per file; those are
test values and must not become production policy.

## Boundaries

- This does **not** close INT-31. One-time vs reuse and the revocation/expiry
  error contract remain undecided; record the activation in
  `docs/open-decisions.md` as interim rather than deleting the entry.
- No application code changes. Configuration and documentation only.
- Do not touch the `Features:` gates; upload links are not one of them.

## Verification

- [ ] `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` passes
- [ ] Every one of the eight settings is present in the bicep; a partial set
      crash-loops the host
- [ ] `LimitsVersion` and `AcceptedLimitsVersion` are byte-identical
- [ ] `docs/open-decisions.md` records the interim activation and still lists
      INT-31's unresolved parts
- [ ] After deployment, `/Uploads/{token}` composes and the Operations surface
      reports the active limits

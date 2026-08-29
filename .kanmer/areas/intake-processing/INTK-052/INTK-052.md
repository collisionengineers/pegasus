---
id: INTK-052
type: ticket
title: >-
  Raise the per-file upload cap to 100 MB without taking the multipart budget to
  2 GB
status: backlog
area: intake-processing
assignee: ''
profile: feature
labels:
  - INT-31
  - uploads
  - capacity
links:
  - INTK-051
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
archived: false
created: '2026-08-29T21:29:16.028Z'
updated: '2026-08-29T21:29:16.028Z'
---

## What

Raise the per-file upload cap from 10 MB to **100 MB**, because photographs are
routinely larger than 10 MB and are currently refused.

Operator instruction, 2026-08-29: *"100mb maximum - images are larger"*.

## Why this is a Core change, not configuration

[[INTK-051]] activated upload links in production with a 10 MB per-file bound.
**That bound cannot be raised in the upload-link configuration alone.**
`src/Pegasus.Core/Intake/DurableIntake.cs:320-330` bounds each channel
independently, and `IntakeSourceChannel.ManualUpload` — which is what an upload
link uses — maps to `IntakeEnvelopeLimits.MaximumContentLength`, a
`const int` of `10 * 1024 * 1024` at `IntakeContracts.cs:13`.

So configuring the upload link to accept 100 MB without changing Core would
accept a 50 MB image at the door and then have `DurableIntake` throw
"The intake source exceeds its channel's size limit." The file would be taken
from the sender and lost, which is worse than refusing it up front.

## The trap: the batch budget is derived, and it is global

```csharp
public const long MaximumBatchContentLength =
    (MaximumBatchFileCount * (long)MaximumContentLength) + MultipartOverhead;
```

`MaximumBatchFileCount` is 20, so today that is ~200 MB. And
`src/Pegasus.Web/Program.cs:588-593` feeds it straight into a **global**
`FormOptions.MultipartBodyLengthLimit`, which applies to every multipart request
in the application, the anonymous upload-link form included.

**A naive bump of `MaximumContentLength` to 100 MB therefore silently takes the
app-wide in-memory multipart ceiling from ~200 MB to 2 GB** — inside a Web
container that has 2 GiB shared with the in-process report renderer
(ADR-0028, and the reasoning recorded on `MaximumProviderApiEnvelopeLength`).
That is the whole difficulty of this ticket; the constant itself is one line.

## Approach

The recommended shape, which keeps memory exactly where it is today:

- `MaximumContentLength`: `10 MB` → `100 MB`.
- `MaximumBatchContentLength`: stop deriving it. Pin it to an explicit constant
  at roughly today's value (~200 MB) so the global multipart limit does not
  multiply.
- `MaximumBatchFileCount` stays 20.

This costs one documented invariant — the current comment says the batch budget
is "every file in the batch at its individual cap", and after this a 20-file
batch can no longer have every file at 100 MB. Rewrite that comment to state the
new rule honestly: one file up to 100 MB, one batch up to ~200 MB.

Also revisit, and decide explicitly rather than by accident:

- `Upload.cshtml.cs:33` renders the cap to operators via
  `OperatorLabels.FileSize` — it will follow the constant, but confirm the
  rendered string still reads sensibly.
- `ProviderSubmission.cs:250` rejects provider files above
  `MaximumContentLength`. Raising it raises the Provider API's per-file bound
  too. Is that wanted? `MaximumProviderApiEnvelopeLength` (30 MB) still caps the
  whole envelope, so the effective provider limit stays 30 MB — but the
  per-file check becomes non-binding, which may be fine or may be worth
  re-pointing at the envelope bound.
- `IntakeMcpTools.cs:164` uses it for the Automation channel.
- [[INTK-051]]'s upload-link limits (`DocumentRequests__MaximumFileBytes` and
  `__MaximumRequestBytes`, both 10485760 in `infra/modules/platform.bicep`) must
  be raised in the same change, or the upload link stays the binding constraint
  and nothing observable improves.
- `Kestrel`'s own request limits and the Container App ingress: confirm a 100 MB
  request actually reaches the app rather than being cut off upstream.

## Why it was not done in release 37

Put to the operator on 2026-08-29 with three options. The decision was **ship
the 10 MB interim limits now and do the 100 MB raise as its own ticket**, so a
Core constant change with container-memory implications does not ride a release
that is about to promote to `main`.

## Verification

- [ ] A ~100 MB file uploads through an upload link end to end and is retained,
      not refused by `DurableIntake`
- [ ] `MultipartBodyLengthLimit` is measured after the change and is **not** 2 GB
- [ ] Memory headroom checked against the 2 GiB Web container with the report
      renderer in process — a real measurement, not an assertion
- [ ] The rewritten comment on `MaximumBatchContentLength` states the actual rule
- [ ] `Upload.cshtml.cs`'s rendered cap reads correctly
- [ ] The Provider API's effective per-file bound is a decision, not a side effect

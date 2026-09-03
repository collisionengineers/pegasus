---
id: CASE-022
type: ticket
title: Deliver public upload links (INT-31) to the operator's accepted limits
status: preparing
area: case-reference-workflow
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-09-03T14:35:23.555Z'
labels:
  - found-during-qa
  - ui
  - design
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-23T15:19:54.445Z'
updated: '2026-09-03T14:35:23.555Z'
---

## What the operator saw

> *"**Issue 3** — No method to create an upload link on frontend at all seemingly."*

Correct, and not for the reason first assumed. The capability is composed as a
null implementation that **throws** (`UnavailableDocumentRequestStore`), `/uploads`
returns 404 in production, and a composition test pins it closed. Verified
against the deployed container: no `DocumentRequests__AcceptedLimitsVersion` is
set — only `Runtime__Profile` and `Features__AutomationMcp`. This was never a
missing button.

## The operator has now accepted the limits, 2026-08-24

> *"token lifetime - configurable upon generation. user enters expiration date
> or leaves open (permanent/until cancellation pegasus-side).*
>
> *file size: these limits are too light (10mb too small by far).*
>
> *content type: any standard files we would receive: images, documents, videos
> (rare but still happens), email file types*
>
> *most of this is over-engineering and assuming that our customers are going to
> send us a virus or something which is absurd.*
>
> *box is the destination storage as with all other storage/files/evidence."*

| Question | Answer |
| --- | --- |
| Token lifetime | Chosen per link at generation: an expiry date, **or open** — permanent until cancelled in Pegasus |
| One-time vs reuse | Reuse. A link lives until its expiry or cancellation |
| Revocation | Exists — "until cancellation pegasus-side" |
| Content types | Images, documents, videos, email files — the standard set |
| Destination | **Box**, like all other evidence |
| Byte limits | Far above 10 MB. Exact figure below |
| Rate limits | Over-engineering; not wanted |

## Two things the built policy cannot express

The `RequestUploadPolicy`/`RequestUploadLimits` code is complete and has been
waiting on these values. **Two of the answers contradict its design**, so this is
not a matter of supplying eight numbers.

**1. Per-link expiry is refused by construction.** `RequestUploadLimits` takes a
single global `Lifetime` (`TimeSpan`, validated `> Zero`), and
`HasAcceptedLifetime` rejects any link whose expiry is not *exactly*
`CreatedAtUtc + limits.Lifetime`. An operator-chosen date, and an open-ended
link, are both actively refused today. Making the expiry per-link is a change to
the policy contract.

**2. A rate limit is mandatory.** The constructor throws on a non-positive
`rateLimit`, so "no rate limiting" is not expressible either.

## The size ceiling is not where the constant says

Raising `IntakeEnvelopeLimits.MaximumContentLength` alone will not work.

- `Program.cs` sets `MultipartBodyLengthLimit` to `MaximumBatchContentLength`
  (20 files × 10 MiB + overhead ≈ **200 MiB**).
- **`MaxRequestBodySize` is configured nowhere** in `src/` or `infra/`, so
  Kestrel's ~30 MB default is the real ceiling. A request over that is refused
  before the multipart limit is ever consulted.

So the two limits already disagree, and the effective cap today is ~30 MB — below
anything that would carry a video. Container Apps ingress may impose its own; to
be established rather than assumed.

Precedent for a generous bound exists and is documented:
`MaximumMailboxContentLength` is **750 MB**, deliberately permissive, after a
16.69 MB QDOS forward was refused outright as `message_too_large`.

**Proposed, for correction rather than debate:** per file 250 MB, per request
1 GB, 50 files. These are bounds that stop a runaway request, not a judgement
about senders. The real constraint to establish at plan time is whether the
upload path streams to Box or materialises in memory — that, not a policy
number, decides what is safe.

## Box as destination closes the other open decision

`docs/open-decisions.md` § *Manual upload in a deployed environment* records that
ADR-0003 forbids a deployed upload route until **authenticated intake and
approved durable source custody** exist, and that only the first was met — the
upload path retaining assets *"in ignored local content-addressed storage… not
production Blob staging, Box custody, backup, or retention"*.

The operator's answer settles the custody half: **Box**, the same destination as
every other case file. An upload link is created against a case, and that case
already has a Box folder, so this reuses the existing case-document custody path
rather than inventing a second one. Confirm at plan time that the anonymous
upload route actually joins that path.

## Scope note

The dead upload-request controls at `_CaseDocuments.cshtml:136-167` belong to
this ticket, not [[DOCS-012]]. They stop being dead once this ships.

## Documents

`docs/open-decisions.md` item 1 under *QDOS alpha activation details* closes; the
accepted limits move to their canonical owner (FRD-05), per the register's own
rule that accepted decisions leave it. The § *Manual upload in a deployed
environment* contradiction is resolved for this route and should say so.

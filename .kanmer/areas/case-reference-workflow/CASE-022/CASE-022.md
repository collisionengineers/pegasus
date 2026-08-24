---
id: CASE-022
type: ticket
title: Make creating a public upload link findable
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - found-during-qa
  - ui
  - design
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-23T15:19:54.445Z'
updated: '2026-08-24T08:59:26.151Z'
---

## What the operator saw

> *"**Issue 3** — No method to create an upload link on frontend at all seemingly."*

They are right, and the reason is not the one this ticket originally gave.

## This is not a UI bug — the capability is not delivered

**Verified in code, in config, and against the deployed container.** Public
upload links (INT-31) are composed as a **null implementation that throws**:

```csharp
// DependencyInjection.cs:435-437 — the else branch
services.AddScoped<UnavailableDocumentRequestStore>();
services.AddScoped<ICreateRequestUploadLink>(provider =>
    provider.GetRequiredService<UnavailableDocumentRequestStore>());
```

`UnavailableDocumentRequestStore.cs:19` throws `DocumentRequestUnavailableException`.
The real store is composed only when `requestUploadLimitsFactory` is non-null,
and `Program.cs:203-210` leaves it null unless
`DocumentRequests:AcceptedLimitsVersion` is set.

| Check | Result |
| --- | --- |
| `appsettings.json` / `appsettings.Development.json` | no `DocumentRequests` section |
| `infra/` bicep | sets only `Runtime__Profile` and `Features__AutomationMcp` |
| **Deployed container app env** (`az containerapp show`, 2026-08-24) | **`Runtime__Profile=Production`, `Features__AutomationMcp=true` — and nothing else** |

So the gate is closed in the running product, not merely in the repo.

Two more consequences, both verified:

- **The public page 404s.** `Program.cs:919-937` returns 404 for `/uploads` (and
  `/requests` on the production profile) when the factory is null. Even a
  successfully minted link would point at a route that does not answer.
- **A test pins it closed.**
  `ProductionCompositionTests.ProductionProfileKeepsUploadLinksUnavailableWithoutAcceptedLimits`
  asserts the null store, *"so composing document custody must not activate
  anonymous upload links."*

Pressing the button today reaches `Custody.cshtml.cs:225-231`, which catches the
throw and reports the request unavailable. The lease requirement in
`_CaseDocuments.cshtml:8` is a real second-order discoverability problem, but it
is not why the operator cannot create an upload link.

## Why it is blocked rather than in progress

CLAUDE.md: *"A closed composition or feature gate is a disabled flag, not a
partially shipped feature. Do not ship, release, merge as delivered, claim, or
document a feature behind one as delivered."* Making the control **more
findable** while it cannot work is the worst of the available outcomes.

`docs/open-decisions.md` still holds eight unanswered questions for INT-31 —
token lifetime, per-file and aggregate byte limits, file count, allowed content
types, per-token and per-IP rate, one-time versus reuse, and the
revocation/expiry error contract. `docs/capabilities.md` marks INT-31
*"Allocated but non-blocking for `0.1.0-alpha.1`"*, and `open-decisions.md`
lists it as explicitly not on the path.

## The operator's choice

**(a) Remove the dead controls now.** The whole upload-request section
(`_CaseDocuments.cshtml:136-167`) renders in production offering an action that
cannot succeed, including the empty state *"No public upload request is
recorded. Availability is not assumed."* `docs/design/README.md` already forbids
that shape — a read-only section with nothing recorded and no available action
should be absent, not an empty panel. Small, honest, and it stops the surface
lying. INT-31 stays deferred.

**(b) Deliver INT-31.** A feature, not a fix, and it needs the eight limit
values answered first.

**Recommendation: (a) now, (b) as its own ticket when the limits are settled.**

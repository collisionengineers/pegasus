# Integration taxonomy + worked example

The vocabulary for *how* two projects can interlink, and one fully worked opportunity so the
abstract schema becomes concrete. When a Phase 2 agent proposes an opportunity, the `mechanism`
field comes from this list, and the worked example below is the bar for specificity.

## Integration mechanisms

How the connection is physically made. Pick the lightest one that delivers the value.

| Mechanism | What it is | When it fits | Typical effort |
|---|---|---|---|
| `deep-link` | One UI links to a specific record in another | Fastest possible interlink; no data movement | S |
| `file-handoff` | One drops a file (PDF, CSV) another picks up | Async, loosely-coupled, existing storage | S |
| `api-call` | One calls another's HTTP/RPC endpoint synchronously | Real-time read or action across a boundary | M |
| `event-webhook` | One emits an event another subscribes to | Decoupled "when X happens, do Y"; lead intake | M |
| `shared-contract` | Both import the same versioned schema | Two sides must agree on a payload shape | S–M |
| `shared-db-entity` | Both read/write the same record store | Tight coupling, one source of truth | M–L |
| `data-sync` | Periodic copy/ETL between stores | Reporting, warehouses, eventual consistency | M |
| `shared-library` | Extract common code into a package both depend on | Duplicated logic (validation, a state machine) | M |
| `shared-service` | Extract a capability into a service both call | Duplicated *infrastructure* (rendering, auth) | L |
| `sso-auth` | One identity/session across projects | Shared human users, staff portals | M–L |
| `shared-design-system` | One component/brand kit across UIs | Multiple front-ends, one brand | M |

A rule of thumb on coupling: prefer `deep-link` / `file-handoff` / `event-webhook` (loose) over
`shared-db-entity` (tight) unless there's a genuine single-source-of-truth requirement. Loose
couplings are cheaper to build, cheaper to reverse, and survive one side being rewritten.

## Seam types → which mechanisms they tend to unlock

| Seam type (from `linkage-method.md`) | Natural mechanisms |
|---|---|
| shared-entity | `shared-contract`, `event-webhook`, `api-call`, `shared-db-entity`, `deep-link` |
| external-system | `shared-service` (one wrapper), `shared-library` (one client), convergence of duplicate paths |
| interface-contract | `shared-contract` (version & publish it), `api-call` |
| producer-consumer | `api-call`, `shared-library`, `file-handoff` |
| cross-cutting-concern | `shared-service`, `shared-library`, `shared-design-system`, `sso-auth` |

## Shared-infrastructure patterns

When a seam has ≥3 members it's usually less "integrate A with B" and more "stop building the same
thing three times." Common shapes:

- **Consolidate duplicates** — N implementations of one capability → one service the others call.
  (e.g. three PDF renderers → one render service.)
- **Converge parallel paths** — N routes to the same external system → one shared client/gateway.
  (e.g. one project calls an API directly while another wraps it behind a gateway → route both
  through the gateway for one auth/rate-limit/caching story.)
- **Publish the shared contract** — an entity modelled separately in several places → one versioned
  schema package everyone imports.
- **One front door** — scattered auth → a single OAuth/identity layer all client-facing surfaces
  sit behind.
- **One design system** — multiple front-ends → a shared component/brand kit.

These become the §4 "Shared-infrastructure findings" and usually belong in **Wave 0** of the
roadmap, because building the shared foundation is what unblocks several downstream opportunities
at once.

## Worked example — website ↔ intake app (the specificity bar)

This is what "concrete" means. Note: nothing here is generic advice; every field names a real
entity, key, and file. (Drawn from collisionsuite — `collision-engineers-website` and
`collisionspike` — and used here only to illustrate the shape.)

> **Seam:** `seam-entity-case` — shared-entity, owned by `collisionspike`, correlation key `VRM`.
> The website captures enquiries; the spike owns the `Case` entity and a status state machine
> (`new_email → ingested → needs_review → ready_for_eva → eva_submitted`). Today a web enquiry is
> re-keyed by hand into the intake job sheet.

```jsonc
{
  "id": "OPP-website-spike-lead-intake",
  "title": "Website enquiry creates an intake Case automatically (joined by VRM)",
  "projects": ["collision-engineers-website", "collisionspike"],
  "direction": "producer->consumer",
  "mechanism": "event-webhook",          // + shared-contract for the payload
  "seam": {
    "type": "shared-entity",
    "name": "Case",
    "correlation_key": "VRM",
    "data_flowing": ["VRM", "claimant contact", "incident date", "instructing party", "images"]
  },
  "anchors": [
    { "project": "collisionspike", "path": "contracts/eva-payload.schema.json",
      "why": "defines the Case payload shape the lead must populate a subset of" },
    { "project": "collisionspike", "path": "CONTEXT.md",
      "why": "canonical Case + VRM definitions and the status state machine" },
    { "project": "collision-engineers-website", "path": "src/<contact-form-entity>",
      "why": "where the enquiry is captured today" }
  ],
  "smallest_viable_step": "Website contact form POSTs {VRM, contact, message} to one intake endpoint that creates a Case in the 'new_email'-equivalent state. No status read-back yet.",
  "impact": { "score": 5,
              "unlocks": "One tracked job from web-lead → intake → EVA → report; removes manual re-keying of enquiries." },
  "effort": { "size": "M",
              "drivers": ["website backend is hosted; needs an outbound webhook or API key",
                          "spike intake is email-triggered today; needs an HTTP entry point"] },
  "dependencies": ["A stable Case-create contract — a versioned subset of eva-payload.schema.json"],
  "risks": ["PII in transit from a hosted site",
            "duplicate cases if a web lead also arrives by email → needs VRM dedup"],
  "lifecycle_validity": { "ok": true, "note": "both ends active" },
  "analogous_pattern": { "summary": "CRM web-to-case: a public form creates a case object keyed by a stable identifier (Salesforce Web-to-Case, Zendesk).",
                         "citation": "<url from Phase 2 research>" },
  "confidence": 0.8,
  "status": "verified"
}
```

What makes it good, point by point — use this as a checklist when reviewing your own opportunities:
- It names the **shared entity** (`Case`) and the **join key** (`VRM`). Not "share data."
- Every claim has a **real file anchor**, including the contract the payload derives from.
- The **smallest viable step** is genuinely small — one POST, one state, no read-back — so it's
  buildable this week, not a quarter-long programme.
- **Impact and effort** are explicit and the effort **drivers** are real constraints (hosted
  backend, email-only entry), not guesses.
- **Risks** are specific and actionable (PII, dedup), and one even names the existing mitigation.
- It carries an **analogous pattern** so the recommendation rests on how this is done elsewhere.

A second, follow-on opportunity would be the read-back direction (`mechanism: api-call` or
`deep-link`): the website shows the claimant a live status pulled from the spike's state machine,
same `VRM` join. Sequencing these two — create first, read-back second — is exactly the kind of
dependency the roadmap captures.

The shape is domain-independent. The same card in an e-commerce cluster would read: storefront
emits an `event-webhook` that creates an `Order` (correlated by `order_no`) in the fulfilment
service, anchored to that service's `order.schema.ts` and the storefront's checkout handler, with a
follow-on `deep-link` from the order-confirmation email to a live tracking page. Cars, orders,
patients, tickets — the machinery is the same; only the discovered entity and key change.

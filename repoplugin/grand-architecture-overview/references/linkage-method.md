# Linkage method: seams, not pairs

This is the load-bearing idea of the skill. Get it right and the overview is sharp and
cheap to produce; get it wrong and you either drown in O(n²) comparisons or emit vague
"these two could share data" filler. Read this fully before authoring Phase 2.

## Why pairwise comparison is the wrong default

The naive approach to "how do my N projects connect?" is to compare every pair and ask
"could these two integrate?" That has three fatal problems:

1. **It doesn't scale.** N projects = N·(N−1)/2 pairs. 10 → 45, 20 → 190, 30 → 435. You'll
   blow the concurrency budget and most of those agents will return "no meaningful overlap."
2. **It's blind to multi-project structure.** The most valuable findings — *four* projects
   all touch the same external API, *three* all reimplement PDF rendering — are invisible to
   a pairwise lens, which only ever looks at two things at once.
3. **It invites hallucination.** An agent asked "could A and B integrate?" will *find* a way,
   because that's what it was asked. Most such findings are ungrounded.

## The seam model

Projects in a cluster don't connect to each other directly; they connect *through a shared
thing*. Call that shared thing a **seam**. There are five kinds:

| Seam type | What's shared | Example |
|---|---|---|
| **shared-entity** | A domain object two+ projects both model | `Case`, `Customer`, `Order`, `VRM` |
| **external-system** | A third-party system two+ projects both touch | the same API, the same storage, the same auth provider |
| **interface-contract** | A schema/contract one publishes and another consumes | a JSON Schema, a protobuf, an OpenAPI spec |
| **producer-consumer** | One project exposes an interface another already calls | a library → its consumer, a service → its client |
| **cross-cutting-concern** | A capability several reimplement independently | PDF rendering, auth/SSO, a design system, logging |

Every integration opportunity is born from exactly one seam. That's *why* opportunities come
out concrete: a seam already names the shared thing and the projects on it, so the agent's
job narrows from "invent a connection" to "given this real shared `Case` entity that these
three projects model, how should they interlink through it?"

## The algorithm

**Step 1 — Profile (Phase 1, O(n)).** One pass per project. The two fields that matter most
for seam-indexing are `owned_entities` (system-of-record) and `referenced_entities` (consumed
but not owned). Also `external_systems`, `data_contracts`, `interfaces_exposed/consumed`, and
the capabilities visible in `stack`/`extension_points`.

**Step 2 — Index seams (deterministic, in code).** `scripts/build-seam-index.mjs` groups the
profiles — no LLM, no tokens, no hallucination surface. The grouping rules:

- **shared-entity**: bucket every entity name (normalised — lowercase, singularise, strip
  spaces/underscores) across all projects' owned + referenced entities. Any bucket with ≥2
  *distinct* projects is a seam. The owner is the project that lists it under `owned_entities`.
- **external-system**: bucket `external_systems` strings. ≥2 projects → seam. (This is how
  "two projects both call DVLA/DVSA by different routes" surfaces.)
- **interface-contract**: bucket `data_contracts` file references (normalise by basename).
  ≥2 projects → seam.
- **producer-consumer**: for each project's `interfaces_exposed`, find any other project whose
  `interfaces_consumed` targets it. Each match is a directed seam.
- **cross-cutting-concern**: bucket capabilities. Derive capability tags from `stack`/`role`/
  `extension_points` with a small keyword map (render/pdf → "pdf-rendering"; auth/oauth/sso/
  entra → "auth"; design/theme/brand/ui-kit → "design-system"; etc.). ≥2 projects → seam.

Compute `weight` = number of **eligible** (live) members. `weight ≥ 2` → investigate.
`weight ≥ 3` → also flag as a **shared-infrastructure candidate** (the §4 findings).

This is the whole anti-blowup move: **O(n) profiling + O(seams) investigation**, and for any
real cluster `seams ≪ n²`. A 30-project cluster typically yields 8–15 seams, not 435 pairs.

**Step 3 — Investigate each seam (Phase 2 fan-out, O(seams)).** One agent per seam with
`weight ≥ 2`. The agent gets the seam plus the profiles of its members and proposes 2–4
concrete `IntegrationOpportunity` objects *through that seam*. Quality beats quantity — three
sharp opportunities are worth more than ten shallow ones.

**Step 4 — Bounded pairwise, only where it pays.** Seam-indexing can miss a connection that
isn't visible as a shared name — e.g. two projects that *should* share an entity but model it
under different names, or a latent opportunity from adjacency. So add a *small, bounded* set of
direct pairwise agents:
- **Focused mode**: the named projects (e.g. website × intake app) get a deep pairwise brief.
- **Full mode**: take the top-K most-central projects (highest seam-membership count, K ≈ 3–4)
  and run pairwise among just those. This catches cross-name connections among the hubs without
  reintroducing O(n²).

**Step 5 — Research the top seams (optional).** For the 2–3 highest-weight seams, spawn a
web-research agent: "How do analogous product suites solve this join?" (web-to-case lead intake;
a shared customer record across site + app + reporting; an API gateway as shared auth). Attach
the findings as `analogous_pattern` so recommendations rest on prior art, not invention.

## Worked trace (collisionsuite)

After profiling, `build-seam-index` produces seams like these (note the script normalises keys to
lowercase/singular, so the ids are `seam-entity-case`, not `seam-entity-Case`):

- `seam-entity-case` (shared-entity, owner `collisionspike`, members: website, collisionspike,
  collisionrenderer) → weight 3 → investigate **and** shared-infra candidate.
- `seam-extsys-dvla` and `seam-extsys-dvsa` (external-system, each with members collisionspike
  [direct] + dvla-dvsa-connector [wraps as MCP]) → weight 2 → investigate; together they surface the
  "two paths to the same APIs, converge them" opportunity. (External systems bucket per-string, so
  DVLA and DVSA are two seams, not one — combine them in the investigation if it reads cleaner.)
- `seam-concern-pdf-rendering` (cross-cutting, members: collisionrenderer, report-renderer,
  valuation-adverts-connector) → weight 3 → shared-infra finding "three PDF renderers, consolidate."
- `seam-producer-consumer-cedocumentmapper-to-collisionspike` (the mapper exposes a parser;
  collisionspike consumes it) → already an existing integration; the investigation refines, not invents.

Notice none of this required comparing the website to the renderer to the connector to the
research datasets pairwise. The seams did the routing.

The method is domain-agnostic — the same machinery on an e-commerce cluster would surface
`seam-entity-order` (owned by the checkout service, correlated by `order_no`, referenced by the
emailer and the analytics warehouse), `seam-extsys-stripe` (two services both calling Stripe →
converge on one billing client), and `seam-concern-design-system` (three storefronts → one component
kit). Nothing about the algorithm knows or cares that the first cluster was about cars.

## When seam-indexing under-delivers

If after Step 2 you have very few seams (a genuinely disconnected portfolio), that *is* the
finding — say so, and pivot the investigation to "what *single* shared foundation (one auth, one
customer record, one design system) would create the most seams that don't exist yet?" A cluster
with no seams isn't a failure of the method; it's a portfolio that hasn't been knit together, and
the highest-leverage move is proposing the first connective tissue.

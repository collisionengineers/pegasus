---
name: grand-architecture-overview
description: >-
  Map how a cluster of related/sibling projects fit together and find concrete,
  high-leverage ways they can integrate, interlink, share infrastructure, and
  benefit each other — then produce a "Grand Architecture Overview" with a cluster
  map, per-project capsules, shared-infrastructure findings, a ranked
  integration-opportunity register (each anchored to a real shared entity and a
  real file), and a dependency-sequenced roadmap. Use this whenever TWO OR MORE
  related projects are in scope — a monorepo-of-repos, a folder of sibling repos,
  a GitHub org, or a loose portfolio — and the user asks how they fit together,
  how to connect / link / integrate them, how to make them act as one product
  suite, where they overlap or could share infra, or asks for an architecture
  overview / portfolio strategy / integration plan across projects. Also triggers
  on "how could project A and project B interlink", "review my whole stack of
  projects", "where am I duplicating effort across these repos", and "update the
  architecture overview". Prefer this whenever the user asks how 2+ projects relate,
  connect, overlap, or could share — even without the words "architecture" or
  "integration". Do NOT trigger for ordinary multi-repo implementation work (e.g.
  "add this feature to both repos", "bump the dep in all three"): that touches several
  projects but isn't about how they fit together.
---

# Grand Architecture Overview

## What this is

You own a *cluster* of related projects — sibling repos, a monorepo-of-repos, a
GitHub org, a folder of half-finished apps. Individually you understand each one.
What you can't hold in your head is how they could act as **one coherent product
suite** instead of disconnected parts: where work and data should flow between
them, where they're duplicating infrastructure, and which connections are worth
building first.

This skill runs a **dynamic, fan-out workflow** that explores every project in
the cluster in parallel, finds the real seams where they could interlink, pressure-
tests those ideas, and synthesises a **Grand Architecture Overview** — a decision
artifact, not a catalogue.

The distinction matters. A catalogue (e.g. an `INDEX.md`) answers *"what are my
projects."* This skill answers the four questions a catalogue can't:

1. **How does work/data actually flow across the projects today?** (the as-is map)
2. **Where are the high-leverage seams to connect them?** (a ranked register of
   concrete integration opportunities, each anchored to a real shared entity and file)
3. **Where are we duplicating or fragmenting infrastructure?** (shared-infra findings)
4. **In what order should we act?** (a dependency-sequenced roadmap of thin slices)

If you find yourself just restating what the projects are, you have failed. The
value is entirely in 1–4.

## The mental model: seams, not pairs

The instinct when asked "how do these N projects connect?" is to compare every
pair. That is O(n²) — 30 projects is 435 pairs — and most pairs share nothing.
**Don't do that.** Instead, the projects connect to each other *through seams*:
a shared entity (a `Case`, a `Customer`, an `Order`), an external system both
touch (the same API, the same storage), a contract one publishes and another
consumes, a capability several reimplement (PDF rendering, auth, a design system).

So the workflow is: profile each project once (O(n)), then **index the seams**
(deterministic, in code), then investigate *each seam* (O(seams), and seams ≪ n²).
Every integration opportunity is born from a concrete seam, which is exactly why
the output is specific instead of mush. The seam method is the load-bearing idea
of this skill — read `references/linkage-method.md` before you build the fan-out.

## The five phases

Run these in order. Each is a barrier: finish and collect before starting the next.
Full schemas for every structured output are in `references/schemas.md` — read it
once before authoring the fan-out, and reuse the schemas verbatim.

**Phase 0 — Scout & scope (inline, no fan-out).** Discover the cluster *shape* and
*intent* before fanning out — you don't know how many projects there are or what
the user actually wants yet. Run `scripts/scout-cluster.mjs <cluster-root>` (or, if
Node isn't available, do the same discovery with your own tools). It walks the tree,
finds nested `.git` dirs, reads remotes, infers lifecycle (`active`/`archive`/
`on-hold` from path buckets — or `unknown`, with `last_commit` provided so you can judge),
infers role, and locates existing artifacts (an `INDEX.md`, a context store, a prior
overview). It emits a `ClusterManifest`. Treat its output as a best-effort starting point —
review the `unknown`-lifecycle projects and refine before fanning out. (For a **GitHub org** rather than a local tree, there's no remote-to-overview
shortcut: clone the org's repos under one parent dir first — `gh repo list <org> --limit 200` then
clone, or shallow-clone on demand — and point the scout at that dir; it labels the shape `github-org`
when the repos share one org.) Then classify the **run profile**:
- User named ≤3 specific projects ("how do the website and the intake app interlink") → **focused**: skip the broad review, do a deep two/three-way integration brief.
- "Review / overview / how do my projects fit together" → **full**: the whole workflow.
- A prior overview exists and the user says update/refresh → **update**: re-profile only projects whose git head moved, diff against the prior register.

Read any existing `INDEX`/context/constellation docs *now*. You are extending them,
not re-deriving them — the final overview must state explicitly what it adds beyond
what already existed (§8 of the template).

**Phase 1 — Profile each project in parallel (fan-out: one agent per project).**
Use read-only explorer agents. Each returns a `ProjectProfile`: purpose, domain,
stack, the entities it *owns* (is system-of-record for) vs *references*, the
interfaces it exposes vs consumes, external systems, personas, auth model, data
contracts, extension points, and the real files anchoring each claim. Profiling
depth is lifecycle-tiered: **active** projects get a full profile; **archived /
on-hold** get a light capsule plus `prior_art_notes` (patterns worth mining) and a
"do not integrate live" flag. The rubric is in `references/profiling-rubric.md`.

**Phase 2 — Index seams, then investigate them (fan-out: one agent per seam).**
First, deterministically: `scripts/build-seam-index.mjs profiles.json` turns the
array of profiles into a `Seam[]` (no LLM — pure grouping by shared entity / external
system / contract / producer→consumer match / cross-cutting capability). Then fan out
**one investigation agent per seam with ≥2 eligible members**, plus targeted pairwise
agents *only* for the focused projects or the top-K most-central projects (bounded,
never all pairs), plus optional web-research agents on the top 2–3 seams to find how
analogous product suites solve the same join. Each agent returns candidate
`IntegrationOpportunity` objects. See `references/linkage-method.md`.

**Phase 3 — Adversarially verify & rank (fan-out: one verifier per candidate).**
Each verifier needs read access (use a read-capable agent like `Explore`), because its job
is to *kill* the opportunity, in this order: **open each cited anchor file** — does it exist
and actually contain the claimed entity/contract/interface? (if not, the seam isn't real) Is a
join key named for a data/entity seam? Is either end archived/on-hold (hard reject)? Are the
stacks/hosting/auth actually compatible? Does it duplicate an integration or capability that
already exists? Is the effort estimate honest? Survivors get a
confidence score and explicit load-bearing assumptions. Rejections are *kept* (with a
kill reason) so a future update run won't re-propose them. Then rank deterministically
by `impact × effort_weight × confidence`.

**Phase 4 — Synthesise (inline, no fan-out).** Compose `ARCHITECTURE-OVERVIEW.md`
from the manifest, profiles, verified ranked opportunities, and shared-infra findings,
following `references/output-template.md` exactly. Build the mermaid cluster map
(solid edges = seams that exist today; dashed = proposed opportunities; node colour =
lifecycle). Topologically sequence the roadmap so shared foundations that unblock
multiple opportunities land in Wave 0. Write the `.json` companion alongside it. In
**update** mode, diff against the prior overview and fill the delta section.

## How to run the fan-out: Workflow tool vs. fallback

Check whether the **`Workflow`** tool is available.

- **If yes (preferred):** author a Workflow script from `references/workflow-script-template.md`.
  The fan-out is inherently dynamic — N is unknown until Phase 0 — which is exactly
  what `pipeline(items, …)` and `parallel(thunks)` are for, and the `schema` option
  gives you validated structured output for free. A skill instructing you to use
  Workflow is explicit opt-in, so this is sanctioned. Scout the cluster *inline first*
  (Phase 0), then pass the discovered project list into the script.
- **If no (fallback):** run the identical phases with parallel `Agent`/`Explore`
  calls. Fan-out = send N tool calls in one message; barrier = wait for all to return
  before the next phase. Use the *same* schemas from `references/schemas.md` — ask each
  agent to return JSON matching them. The phase logic is identical; only the harness
  differs.

Either way, **scale the fan-out to the cluster and the intent**: focused mode profiles
only the named projects; full mode tiers depth by lifecycle so a big archive doesn't
dominate cost; for very large N, batch Phase 1 rather than launching hundreds at once.

## Non-negotiable rules (these are what make the output good)

These four rules are why this produces sharp overviews instead of generic ones. Each
exists for a reason — hold the line on them.

1. **Every claim is anchored to a real file.** A profile says "owns the `Case` entity,
   defined in `CONTEXT.md`," never "handles cases." An opportunity cites ≥1 real file
   per project. The verifier re-checks anchors; anything ungrounded is dropped. This is
   the single best defence against hallucinated integrations.

2. **Every data/entity opportunity names the seam and the join key.** Not "they could
   share data" — that's a non-answer. It must be "share the `Case` entity, correlated
   by `VRM`, contract defined in `contracts/eva-payload.schema.json`." If you can't name
   the shared entity and the field that joins the two sides, you don't have an
   opportunity yet. The schema requires the seam block and an anchor; the **verifier**
   is the backstop that weakens or rejects anything missing a join key — don't rely on
   the schema alone, and don't route around the verifier.

3. **Respect lifecycle.** Never propose integrating an **archived or on-hold** project
   as a live target — it's superseded for a reason. But *do* mine archives for prior art
   (a state machine, a pricing model, a parser someone already wrote) and surface it as
   reusable. The manifest's `eligible_for_live_integration` flag and the verifier's
   `both_ends_active` check enforce this.

4. **Build on what exists; state your delta.** Read the existing `INDEX`/context/
   architecture docs in Phase 0 and make the overview *exceed* them. The final section
   must say plainly what this adds beyond them. Re-deriving the catalogue is failure.

## Quality bar, in one line

Every integration opportunity should be something the owner reads and thinks *"yes —
that specific connection, through that specific entity, and I can see the first
slice."* If it reads like advice you could give about any portfolio, cut it.

## Reference files

Read these as you reach the relevant phase — don't load them all upfront.

- `references/schemas.md` — every JSON schema (Manifest, Profile, Seam, Opportunity, Verdict, Roadmap). Read before authoring the fan-out.
- `references/linkage-method.md` — the seam-index algorithm and why pairwise is wrong. The crux of the skill.
- `references/profiling-rubric.md` — what a project profile must capture; per-stack hints; lifecycle tiers.
- `references/integration-taxonomy.md` — the catalogue of integration mechanisms and seam types, with a full worked example (website ↔ intake app).
- `references/output-template.md` — the exact `ARCHITECTURE-OVERVIEW.md` structure to fill in.
- `references/workflow-script-template.md` — an annotated Workflow script for all five phases, plus the plain-Agent fallback pattern.
- `scripts/scout-cluster.mjs` — deterministic cluster discovery → `ClusterManifest`.
- `scripts/build-seam-index.mjs` — deterministic `ProjectProfile[]` → `Seam[]`.
- `assets/overview-skeleton.md` — a copyable starting file the synthesis phase fills in.

## Two more traps (beyond the four rules above)

The four rules already guard against restating the catalogue, vague opportunities,
hallucinated seams, and integrating the dead. Two failure modes they don't cover:

- **Pairwise blowup.** Don't compare every pair of projects — it's slow and mostly empty.
  Index seams first (see `references/linkage-method.md`); this is structural, not optional.
- **One-and-done.** Always write the `.json` companion alongside the markdown, so the next
  run can *update* the overview instead of regenerating it from scratch.

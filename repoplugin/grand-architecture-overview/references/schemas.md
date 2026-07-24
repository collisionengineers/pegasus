# Schemas

The structured outputs that flow between phases. When using the `Workflow` tool,
pass the relevant schema as the `schema` option to `agent()` so output is validated.
In fallback mode, paste the shape into the subagent prompt and ask for matching JSON.

Keep IDs **stable and slug-based** (`OPP-website-spike-lead-intake`, `seam-entity-case`)
so an update run can diff this run against the last one by ID. Seam keys are normalised
(lowercase, singular), so the entity `Case` yields `seam-entity-case`.

## Contents
- [ClusterManifest](#clustermanifest) — Phase 0 output (from `scout-cluster.mjs`)
- [ProjectProfile](#projectprofile) — Phase 1 output (one per project)
- [Seam](#seam) — Phase 2 intermediate (from `build-seam-index.mjs`)
- [IntegrationOpportunity](#integrationopportunity) — Phase 2 output (the core object)
- [Verdict](#verdict) — Phase 3 output (one per candidate)
- [RoadmapItem & SharedInfraFinding](#roadmapitem--sharedinfrafinding) — Phase 4 synthesis

---

## ClusterManifest

Emitted by Phase 0. Defines scope for everything downstream. `eligible_for_live_integration`
is the lifecycle gate the verifier enforces. (Values below are illustrative.)

```jsonc
{
  "cluster_name": "collisionsuite",
  "root": "C:/Users/Alex/Documents/GitHub/collisionsuite",   // example path
  "shape": "monorepo-of-repos",        // monorepo-of-repos | flat-dirs | single-repo-multipackage | github-org
  "run_profile": "full",               // full | focused | update
  "focus_projects": [],                // names; populated only in focused mode
  "projects": [
    {
      "name": "collisionspike",
      "path": "active/collisionspike",
      "is_git": true,
      "remote": "github.com/collisionengineers/collisionspike",
      "lifecycle": "active",           // active | archive | on-hold | unknown
      "role": "app",                   // app | website | library | connector | skills | research | context | tool | unknown
      "eligible_for_live_integration": true,
      "size_hint": { "top_level_entries": 34, "top_level_docs": 9 },  // top-level only, from scout
      "last_commit": "2026-06-22"
    },
    {
      "name": "collisioncc",
      "path": "archive/collisioncc",
      "is_git": true,
      "lifecycle": "archive",
      "role": "app",
      "eligible_for_live_integration": false,
      "mine_as_prior_art": true
    }
  ],
  "existing_artifacts": [
    "INDEX.md",
    "collision-engineers-context/README.md",
    "active/collisionspike/docs/architecture/repo-constellation.md"
  ],
  "prior_overview": "collision-engineers-context/grand-architecture-overview.json",  // null on first run
  "context_store_dir": "collision-engineers-context"  // where to write the overview; null → cluster root
}
```

---

## ProjectProfile

Phase 1, one per project. The distinction between **owned** and **referenced** entities
is what makes seam-indexing work, so it's mandatory. Every interface/contract names the
file it's defined in. Formal schema (use as the `agent()` `schema`):

```json
{
  "type": "object",
  "required": ["name", "purpose", "domain", "stack", "lifecycle", "owned_entities",
               "interfaces_exposed", "interfaces_consumed", "external_systems", "key_anchors"],
  "properties": {
    "name":        { "type": "string" },
    "purpose":     { "type": "string", "description": "one sentence" },
    "domain":      { "type": "string" },
    "stack":       { "type": "array", "items": { "type": "string" } },
    "lifecycle":   { "enum": ["active", "archive", "on-hold", "unknown"] },
    "role":        { "type": "string" },
    "owned_entities": {
      "type": "array",
      "description": "entities this project is the system-of-record for",
      "items": {
        "type": "object",
        "required": ["name", "keys", "defined_in"],
        "properties": {
          "name":       { "type": "string" },
          "keys":       { "type": "array", "items": { "type": "string" }, "description": "candidate/correlation keys, e.g. VRM" },
          "defined_in": { "type": "string", "description": "real file path" }
        }
      }
    },
    "referenced_entities": { "type": "array", "items": { "type": "string" },
                             "description": "entities it consumes but does not own" },
    "interfaces_exposed": {
      "type": "array", "description": "how others can plug IN",
      "items": { "type": "object", "required": ["kind", "name", "anchor"],
                 "properties": { "kind": { "type": "string" },
                                 "name": { "type": "string" },
                                 "anchor": { "type": "string", "description": "file defining it" } } }
    },
    "interfaces_consumed": {
      "type": "array", "description": "what it calls OUT to",
      "items": { "type": "object", "required": ["kind", "target"],
                 "properties": { "kind": { "type": "string" },
                                 "target": { "type": "string" },
                                 "mode": { "type": "string", "description": "e.g. direct vs via-gateway" } } }
    },
    "external_systems":  { "type": "array", "items": { "type": "string" },
                           "description": "third-party systems touched, e.g. EVA, Box, DVLA" },
    "personas":          { "type": "array", "items": { "type": "string" } },
    "auth_model":        { "type": "string" },
    "data_contracts":    { "type": "array", "items": { "type": "string" }, "description": "schema/contract files" },
    "existing_integrations": { "type": "array", "items": { "type": "string" } },
    "extension_points":  { "type": "array", "items": { "type": "string" },
                           "description": "natural seams to plug into, e.g. a state machine, an HTTP entry" },
    "key_anchors":       { "type": "array", "items": { "type": "string" },
                           "description": "the handful of files that best evidence this profile" },
    "prior_art_notes":   { "type": ["string", "null"],
                           "description": "for archived/on-hold: reusable patterns + a do-not-integrate-live flag" }
  }
}
```

---

## Seam

Phase 2 intermediate, produced deterministically by `build-seam-index.mjs` — **not** by
an LLM. It's the inverted index over profiles: group projects by what they share.

```jsonc
{
  "id": "seam-entity-case",    // key normalised to lowercase/singular
  "type": "shared-entity",     // shared-entity | external-system | interface-contract | producer-consumer | cross-cutting-concern
  "key": "case",               // the entity / system / contract / capability the members share (normalised)
  "members": ["collisionspike", "collision-engineers-website", "collisionrenderer"],
  "owner": "collisionspike",   // for shared-entity: the project that owns it (null otherwise)
  "evidence": [
    { "project": "collisionspike", "anchor": "CONTEXT.md" },
    { "project": "collisionspike", "anchor": "contracts/eva-payload.schema.json" }
  ],
  "weight": 3,                 // count of eligible (live) members; ≥2 → investigate; ≥3 → also a shared-infra candidate
  "eligible_members": ["collisionspike", "collision-engineers-website", "collisionrenderer"]
}
```

How each `type` is computed (see `linkage-method.md` for the full algorithm):
- **shared-entity** — same/aliased entity name across `owned_entities` + `referenced_entities`.
- **external-system** — same string in two+ projects' `external_systems`.
- **interface-contract** — same contract/schema file in two+ projects' `data_contracts`.
- **producer-consumer** — one project's `interfaces_exposed` matches another's `interfaces_consumed`.
- **cross-cutting-concern** — same capability (PDF render, auth, design system) in ≥2 profiles.

---

## IntegrationOpportunity

The core object. Phase 2 emits candidates; Phase 3 sets `confidence` and `status`.

**Grounding is enforced in two layers, not just by the schema.** The schema makes the `seam` block and
at least one `anchor` structurally required — so a bare "they could share data" with no seam can't even be
emitted. But the schema alone can't know that a *data/entity* seam needs a join key, so the stronger rules —
**`seam.correlation_key` must be present for `shared-entity`/`interface-contract` seams, and there must be
≥1 anchor per project** — are enforced by the investigation prompt (which demands them) and re-checked by
the Phase 3 verifier (which weakens or rejects opportunities missing them). Treat those two rules as hard;
the verifier is the backstop. `impact.score`: **5** = closes the product spine or unblocks several other
opportunities; **3** = a solid standalone win; **1** = minor convenience.

```json
{
  "type": "object",
  "required": ["id", "title", "projects", "mechanism", "seam", "anchors",
               "smallest_viable_step", "impact", "effort"],
  "properties": {
    "id":       { "type": "string", "description": "stable slug, e.g. OPP-website-spike-lead-intake" },
    "title":    { "type": "string" },
    "projects": { "type": "array", "items": { "type": "string" }, "minItems": 2 },
    "direction":{ "enum": ["producer->consumer", "bidirectional", "shared-resource"] },
    "mechanism":{ "enum": ["shared-db-entity", "api-call", "event-webhook", "shared-library",
                           "shared-contract", "sso-auth", "shared-design-system", "data-sync",
                           "deep-link", "file-handoff", "shared-service"] },
    "seam": {
      "type": "object",
      "required": ["type", "name"],
      "properties": {
        "type":            { "enum": ["shared-entity", "external-system", "interface-contract",
                                      "producer-consumer", "cross-cutting-concern"] },
        "name":            { "type": "string" },
        "correlation_key": { "type": "string", "description": "REQUIRED for entity/data seams: the field that joins the two sides" },
        "data_flowing":    { "type": "array", "items": { "type": "string" } }
      }
    },
    "anchors": {
      "type": "array", "minItems": 1,
      "description": "real files proving the seam exists — ideally one per project",
      "items": { "type": "object", "required": ["project", "path", "why"],
                 "properties": { "project": { "type": "string" },
                                 "path": { "type": "string" },
                                 "why": { "type": "string" } } }
    },
    "smallest_viable_step": { "type": "string", "description": "the thin first slice that delivers value" },
    "impact": { "type": "object", "required": ["score", "unlocks"],
                "properties": { "score": { "type": "integer", "minimum": 1, "maximum": 5 },
                                "unlocks": { "type": "string" } } },
    "effort": { "type": "object", "required": ["size"],
                "properties": { "size": { "enum": ["S", "M", "L"] },
                                "drivers": { "type": "array", "items": { "type": "string" } } } },
    "dependencies":      { "type": "array", "items": { "type": "string" } },
    "risks":             { "type": "array", "items": { "type": "string" } },
    "lifecycle_validity":{ "type": "object",
                           "properties": { "ok": { "type": "boolean" }, "note": { "type": "string" } } },
    "analogous_pattern": { "type": "object",
                           "properties": { "summary": { "type": "string" }, "citation": { "type": "string" } } },
    "confidence":        { "type": "number", "minimum": 0, "maximum": 1, "description": "set in Phase 3" },
    "status":            { "enum": ["candidate", "verified", "weakened", "rejected"] }
  }
}
```

---

## Verdict

Phase 3, one per candidate. The verifier tries to falsify, then scores survivors. A clear
rejection with a reason is a *good* outcome — record it so update runs suppress it.

```json
{
  "type": "object",
  "required": ["opportunity_id", "verdict", "checks", "confidence"],
  "properties": {
    "opportunity_id": { "type": "string" },
    "verdict":        { "enum": ["verified", "weakened", "rejected"] },
    "checks": {
      "type": "object",
      "properties": {
        "seam_is_real":    { "type": "boolean", "description": "do the cited anchors actually support it?" },
        "both_ends_active":{ "type": "boolean", "description": "hard fail if an end is archived/on-hold" },
        "stack_compatible":{ "type": "boolean", "description": "hosting/auth/runtime feasibility" },
        "not_duplicate":   { "type": "boolean", "description": "doesn't reinvent an existing seam/infra" },
        "effort_realistic":{ "type": "boolean" }
      }
    },
    "corrected_effort": { "enum": ["S", "M", "L"] },
    "confidence":       { "type": "number", "minimum": 0, "maximum": 1 },
    "kill_reason":      { "type": ["string", "null"], "description": "populated when rejected → goes to the appendix" },
    "assumptions":      { "type": "array", "items": { "type": "string" }, "description": "load-bearing assumptions if it survives" }
  }
}
```

**Ranking after verification** (deterministic, in synthesis):
`rank_score = impact.score × effort_weight × confidence`, where `effort_weight` is
`S → 1.0, M → 0.6, L → 0.3`. Sort descending. Rejected items keep `status: "rejected"`
and drop out of the ranked register into the appendix.

---

## RoadmapItem & SharedInfraFinding

Built during synthesis (Phase 4), not by a fan-out agent.

```jsonc
// RoadmapItem — a sequenced unit of work
{
  "wave": 0,                                   // 0 = shared foundations that unblock multiple opportunities
  "realises": ["OPP-website-spike-lead-intake", "OPP-spike-renderer-handoff"],
  "title": "Publish a shared Case-create contract (subset of eva-payload.schema.json)",
  "prerequisites": [],
  "first_slice": "Extract the minimal Case-create fields into a versioned JSON Schema both sides import.",
  "done_signal": "Website and spike both validate against the same schema file."
}

// SharedInfraFinding — duplication/fragmentation worth consolidating
{
  "id": "INFRA-pdf-rendering",
  "capability": "PDF rendering",
  "carried_by": ["collisionrenderer", "report-renderer", "valuation-adverts-connector"],
  "evidence": [ { "project": "collisionrenderer", "anchor": "README.md" } ],
  "proposal": "Converge on collisionrenderer as the single render service; others call it.",
  "migration_risk": "Different stacks (.NET vs Python) — needs a stable HTTP contract first.",
  "from_seam": "seam-concern-pdf-rendering"    // shared-infra findings are auto-promoted from seams with weight ≥3
}
```

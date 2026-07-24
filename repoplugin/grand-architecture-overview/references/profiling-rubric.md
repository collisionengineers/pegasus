# Profiling rubric

How a Phase 1 agent turns one project into a `ProjectProfile`. The goal isn't a code tour —
it's to capture exactly the facts that seam-indexing and opportunity-finding need, and nothing
else. A profile is read by other agents, not humans, so it should be dense and file-anchored.

## The prime directive: owned vs referenced entities

The single most important thing a profile captures is **which domain entities this project is
the system-of-record for** (`owned_entities`) versus which it merely **consumes** (`referenced_entities`).
This is what lets seam-indexing decide who should publish and who should subscribe. Get the keys
right too — the `keys` array is where correlation keys like `VRM`, `customer_id`, `order_no` come
from, and those become the join keys in every data opportunity.

A project owns an entity if it defines the canonical schema, holds the source-of-truth store, or
is where that entity is created. It references an entity if it reads or displays one owned elsewhere.

## What to read, in priority order

Read docs first and treat code as confirming evidence — this keeps doc-heavy projects (a knowledge
base, a research repo) profilable and keeps you out of the weeds in code-heavy ones.

1. **Orientation**: `README`, `CLAUDE.md`/`AGENTS.md`, `CONTEXT.md`, any `docs/architecture/*`,
   ADRs. These usually state purpose, stack, and entities directly.
2. **Contracts & schemas**: `contracts/`, `*.schema.json`, OpenAPI/proto files, DB migrations,
   Dataverse/table definitions. These are the highest-signal source for entities and interfaces.
3. **Manifests**: `package.json`, `*.csproj`, `pyproject.toml`, `requirements.txt`, `*.sln`,
   workflow/automation definitions. Stack, dependencies, and sometimes consumed services.
4. **Entry points & config**: route files, function handlers, `*.config`, env templates,
   connector manifests. Interfaces exposed/consumed and external systems.

You do not need to read implementation files line by line. Stop when you can fill the profile with
real anchors — over-reading a single project starves the rest of the fan-out.

## Field-by-field

| Field | How to fill it |
|---|---|
| `purpose` | One sentence. What this project is *for*, not how it's built. |
| `domain` | The business/problem domain (e.g. "UK collision-claim case intake"). |
| `stack` | Languages, frameworks, platforms, datastores. Be specific (".NET 8 / WinUI", not "C# app"). |
| `owned_entities` | System-of-record entities + their keys + the file defining them. The most important field. |
| `referenced_entities` | Entities consumed but owned elsewhere. |
| `interfaces_exposed` | How others plug IN: HTTP endpoints, exported libraries, tables, MCP tools, events. Anchor each. |
| `interfaces_consumed` | What it calls OUT to, with `mode` (direct vs via-gateway) where it matters. |
| `external_systems` | Third-party systems touched (APIs, storage, auth providers). Same string across projects = a seam. |
| `personas` | Who uses it (operator, engineer, customer, admin). Shared personas hint at SSO/UX seams. |
| `auth_model` | How it authenticates (Entra ID, API key, OAuth, none). Feasibility input for the verifier. |
| `data_contracts` | Schema/contract files it owns or imports. Shared contracts are seams. |
| `existing_integrations` | Connections that already exist — so the overview refines them, not "discovers" them. |
| `extension_points` | The natural seams to plug into: a state machine, an unused HTTP entry, a webhook slot, a plugin system. |
| `key_anchors` | The handful of files that best evidence the whole profile. |
| `prior_art_notes` | Archived/on-hold only — see below. |

## Lifecycle tiers: how deep to profile

Profiling depth scales with lifecycle so a big archive doesn't dominate cost.

- **active** → full profile, every field. These are integration targets.
- **archive / on-hold** → light capsule (`name`, `purpose`, `domain`, `stack`, `lifecycle`) plus a
  populated `prior_art_notes`: *what reusable patterns live here* (a state machine, a pricing model,
  a parser, a methodology) and an explicit **"do not integrate live"** flag. Skip exhaustive
  interface/entity extraction — archived projects feed prior art, not live seams.

The reason: an archived project is superseded for a reason. Wiring a live app into it is almost
always wrong. But the *ideas* in it are often the cheapest available — someone already solved a
problem there. Capture the idea, flag the project as off-limits for live wiring.

## Per-stack hints

The cluster will be heterogeneous. Capability-oriented reading beats framework-specific reading,
but these pointers help:

- **.NET / C# (`*.csproj`, `*.sln`)** — entities in model classes / EF migrations; interfaces in
  controllers / minimal-API endpoints; external systems in `appsettings*.json`.
- **Python (`pyproject.toml`, FastAPI/FastMCP)** — entities in pydantic models / schemas; MCP tools
  and routes are the exposed interfaces; deps in the manifest.
- **JS/TS (`package.json`)** — entities in `types`/`models`/schema files; interfaces in route/API
  dirs; consumed services in client SDK imports and `.env` templates.
- **Power Platform / Dataverse** — entities are Dataverse tables; interfaces are Power Automate
  flows and custom connectors; external systems in connection references.
- **Cloudflare Workers** — entities in D1 schema / Durable Object classes; interfaces are the Worker
  routes; bindings in `wrangler.toml`.
- **Doc/markdown projects (a KB, research)** — there's no code, and that's fine. Entities and
  processes are described in prose; capture them as `referenced_entities` and note the project's
  role as a knowledge/context source. These often seam via the *context store*, not via APIs.

## Anti-patterns

- **Speculating about other projects.** A profile describes only *this* project, from *its* files.
  Cross-project reasoning happens in Phase 2, with all profiles in hand.
- **Unanchored claims.** "Handles authentication" with no file is noise. "Auth via Entra ID,
  configured in `appsettings.json`" is signal.
- **Code tourism.** Reading source for its own sake. Read to fill the profile, then stop.
- **Treating an archived prototype as a live target.** Set the flag; don't profile it as if it were active.

---
id: 2026-07-29-collision-brain-dotnet-rewrite
type: refactor
status: in_progress
risk: high
created: 2026-07-29
updated: 2026-07-29
issue: https://github.com/collisionengineers/pegasus/issues/22
pull_request: pending
baseline: pending exact head
target_release: workspace prototype parity
roadmap_horizon: Now
mode: development
supersedes: none
superseded_by: none
---

# Change: Rewrite Collision Brain in modern .NET 10

## Outcome

Replace the independently buildable `workspaces/ai-centre/services/collision-brain/`
TypeScript/Node prototype with a modern .NET 10 implementation. This is a clean
runtime cutover, not a Pegasus integration. The replacement must preserve the
workspace boundary and observable v1 contracts: four MCP tools, stateless
Streamable HTTP, stdio proxying, authenticated upload staging, asynchronous
ingestion, memory and PostgreSQL/pgvector repositories, memory/filesystem/S3
object stores, deterministic local embeddings, supported extraction formats,
citations, deletion/tombstones, export/import, benchmark, containers, and local
verification.

Implementation, caller proof, deployment, and acceptance remain separate evidence
states. This record stays in progress until exact-head review and the required
local evidence are captured.

## Authority and owners

- The approved implementation plan is the execution source of truth for this change.
- [Issue #22](https://github.com/collisionengineers/pegasus/issues/22) owns actionable work state.
- Repository documentation remains owned by [the documentation index](../index.md),
  [architecture](../architecture.md), [operations](../operations.md),
  [engineering](../engineering.md), and [source workspaces](../../workspaces/README.md).
- The workspace owns its package README, ADRs, runtime contracts, and verification.
- `Pegasus.Core` remains the business-policy owner; this source workspace is not an
  application caller or deployment unit.

## Scope

Included: .NET 10 project and test scaffold; domain ports and lifecycle service;
text, vector, extraction, token, repository, and object-store adapters; auth and
configuration; Minimal API, MCP, stdio, worker, observability, admin commands,
benchmark, parity tests, containers, CI, and affected workspace documentation.

Excluded or deferred: Pegasus integration or caller registration; production model,
embedding provider, corpus, or cloud target selection; deployment, live service
writes, production acceptance, answer generation, OCR, dormant provider/vector
capabilities, and unrelated repository jobs or projects. `corpus/`, `to-ingest/`,
`logs/`, `migrations/001_initial.sql`, benchmarks, and existing workspace provenance
remain protected; the untracked intake/comparison material is not modified.

## Fixed decisions and invariants

1. Target repository-pinned SDK `10.0.302` and `net10.0`; keep one package-local
   executable artifact with `api`, `worker`, `stdio`, `migrate`, `data-export`,
   `data-import`, `benchmark`, and internal `healthcheck` subcommands.
2. Preserve public MCP names, descriptions, annotations, structured content,
   mixed wire-key conventions, authentication roles, error codes/messages,
   timestamps, upload-token identity, embedding identity, data export schema,
   migration lineage, and lifecycle/tombstone semantics.
3. Keep direct SQL/Npgsql/pgvector for the lifecycle and search boundary. Do not
   introduce EF Core, a generic vector-store repository, Semantic Kernel, Agent
   Framework, preview ingestion ownership, or a second business-policy owner.
4. Docker remains optional local orchestration for PostgreSQL and object storage;
   .NET itself is not treated as a Docker dependency.
5. No dual runtime, compatibility shim, dead re-export, or npm CI path remains
   after parity evidence passes.

## Deferred-capability impact

The rewrite preserves seams and data identities without implementing deferred
capability. Provider/corpus/model selection, production caller integration,
Pegasus deployment, live cloud verification, OCR, answer generation, and formal
operator/management acceptance remain explicitly deferred. Existing document IDs,
content hashes, SQL tables and migration lineage, MCP tool identities, upload
references, embedding descriptor shape, and export schema are preserved so a later
accepted decision can activate a capability without silently changing this cutover.
No new top-level directory, project, store, runtime boundary, migration stream, or
deployment unit is introduced beyond the package-local replacement and its tests.

## Evidence and review

### Planned checks

- Locked restore, Release build, Release tests, benchmark parity, actual API/worker
  lifecycle smoke, published stdio smoke, and exact-head leftover/boundary review.
- PostgreSQL/pgvector integration only when an approved local endpoint exists;
  otherwise tests remain explicitly skipped and no live claim is made.
- Docker Compose verification only when Docker is available; volumes are not
  deleted without separate authorization.


### Completed local checks

- Release build passed for `CollisionBrain.slnx` after the final wire-format and connection-string changes.
- Release tests passed: 10 passed, 0 failed, 0 skipped, including lifecycle, upload-token, PostgreSQL URI, and shared-secret authentication contracts.
- The memory-driver synthetic benchmark passed with three documents, three queries, recall@k `1`, mean reciprocal rank `1`, and lower-camel embedding identity keys.
- The API smoke passed `/health`, MCP `tools/list`, MCP `write`, and MCP `lookup` (the immediate lookup correctly returned no matches while the queued document was still pending).
- The stdio proxy returned a valid JSON-RPC `initialize` response, and the worker host started under the memory profile.

### Explicitly unverified or pending

- PostgreSQL/pgvector and S3 remain unverified because no approved local target is available; Docker Compose was not run because `docker` is not installed on the workstation.
- Representative PDF/DOCX extraction and a labelled retrieval evaluation remain unverified.
- Implementation exact-head review, pull request, deployment/live verification, caller proof for Pegasus, and operator acceptance remain pending or out of scope.

## Outcome

The approved implementation and local verification are complete. Exact-head review, pull request delivery, and any deployment, caller, or acceptance evidence remain pending or out of scope.

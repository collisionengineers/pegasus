# Operations

This document is the canonical operational contract for Collision Brain. Functional requirements remain owned by [requirements.md](../../../../../docs/requirements.md).

Retained supporting contracts are [security](security.md),
[ADR-0001: provider boundaries](adr/0001-provider-boundaries.md), and
[ADR-0002: PostgreSQL/pgvector baseline](adr/0002-postgres-pgvector-baseline.md).

Provider comparison evidence and its first-party source register are retained in [provider evaluation](provider-evaluation.md).

## Evidence boundary

The latest repository evidence is dated **2026-07-29**.

Repository-present code, local verification, caller proof, hosted deployment, production selection, and acceptance are distinct states. Current evidence establishes the repository scope and the local checks recorded below; it does not establish a Pegasus caller, hosted deployment, or acceptance.

There is no evidence of:

- a real service caller, including a Pegasus caller;
- a hosted deployment;
- an approved provider, account, region, service, SKU, corpus, model, or cost cap;
- production selection or production acceptance;
- completion of the pilot acceptance scenarios or promotion evidence; and
- any hosted database or source-object custody, completed restore/deletion exercise, provider-resource cleanup, deployed telemetry, or live incident evidence.

Until those boundaries are crossed with recorded evidence, Collision Brain remains a prototype.

## Requirements

The functional scope is fixed. Deployment sizing is intentionally unfilled until a real pilot corpus is approved.

| Area | Fixed v1 requirement |
|---|---|
| Audience | Authenticated internal Collision Engineers users |
| Data classification | Non-sensitive knowledge only |
| Answering | Retrieval with citations; the caller generates the answer |
| Input | Pasted text, TXT, Markdown, HTML, text PDF, and DOCX |
| Upload limit | 25 MiB by default, configurable |
| Staged upload token | 15 minutes by default; configurable from 60 seconds through 24 hours; a dedicated secret is required in production |
| Transports | Streamable HTTP MCP and an equivalent stdio proxy |
| Roles | Reader, contributor, and administrator |
| Removal | Immediate content purge with a content-free tombstone |
| Portability | Provider SDKs remain behind adapters; runtime is OCI-compatible |

### Required before hosted-provider selection

The following must be measured, recorded, and approved:

- Initial and twelve-month document count, source bytes, extracted characters, expected chunks, and vector dimensions.
- Average and daily ingestion, peak concurrent queries, expected monthly queries, and egress.
- Lookup p50/p95 latency and ingestion-completion targets.
- Required region and data residency, plus permitted identity providers.
- Recovery point, recovery time, backup retention, and availability expectations.
- Prototype account, region, services and SKUs, expiry behaviour, projected cost, and hard spending cap.
- A representative synthetic or approved non-sensitive evaluation corpus with labelled queries.

The governing workspace-wide authority and cleanup contract is [ML operations](../../../ml-ops/README.md#external-experiment-approval-and-cleanup); this service procedure instantiates it. Before any hosted experiment, explicit authority must also name the provider, account or project, region, service and SKU, exact operations, data class, duration/expiry, spending ceiling and stop behavior, identity and secret-handling boundary, retained outputs, rollback source, and cleanup targets. Approval to evaluate a corpus or read public research does not authorize account creation, provisioning, paid calls, data transfer, or teardown.

The local feature-hash embedding is deterministic test infrastructure, not a production semantic model. Hosted model and provider selection remain blocked on the approved requirements, retrieval benchmark, provider comparison, and cost benchmark.

Free tiers may support a prototype. Production requires a fresh review of uptime, backups, inactivity behaviour, access control, observability, support, exit and migration cost, and an explicit monthly cap.

## Current repository evidence

The repository currently contains code for:

- Provider-neutral domain interfaces and ports for repositories, object storage, embeddings, and authentication.
- A PostgreSQL/pgvector repository and a deterministic in-memory repository for tests.
- Filesystem, in-memory, and S3-compatible object storage.
- Pasted-text and staged-file ingestion for TXT, Markdown, HTML, PDF, and DOCX.
- An asynchronous worker lifecycle, hybrid lookup, registry pagination, and purging removal.
- A streamable HTTP MCP endpoint and a stdio-to-HTTP proxy exposing the same four tools.
- OIDC, shared-secret, and local-development authentication modes.
- Export and import commands, SQL migrations, an OCI container, a Compose environment, and CI.

This is not proof that any real caller uses those capabilities or that their operational and acceptance criteria have passed.

### Runtime profiles

| Profile | Components | Evidence and limitation |
|---|---|---|
| In-memory | Deterministic in-memory repository, object store, and MCP path | Exercised by the offline/unit and in-memory MCP test suite. Suitable for local verification, not production evidence. |
| Local filesystem | Filesystem object store with traversal protection | Exercised by local unit tests with temporary directories. It is not hosted shared custody, backup, or recovery evidence. |
| PostgreSQL | PostgreSQL with pgvector; filesystem or S3-compatible shared source storage | Repository, migrations, and Compose definitions are present. PostgreSQL integration and the Compose runtime have not been verified on the current workstation. |
| S3-compatible | S3 object-store adapter source | No provider account, endpoint, bucket, credential, conformance result, or hosted caller is evidenced. |

Current proved storage is developer-local memory or temporary filesystem test state only. Implemented PostgreSQL and S3 adapters do not prove a hosted data service. A future hosted API and worker would have to share source storage through a proved persistent filesystem or the same proved S3-compatible endpoint and bucket.

## Configuration

The complete configuration contract is maintained in `.env.example`.

Provider adapters may add environment variables, but must not change MCP tool inputs or outputs. Provider-specific SDKs must remain behind adapters.

Supported authentication configurations are:

- OIDC tokens carrying issuer, audience, JWKS URL, subject, and an optional `roles` claim.
- A shared bearer secret behind an authenticated outer boundary.
- Local-development authentication for local use only.

Inject secrets through environment variables or mounted secret files. Secrets must never enter the container image, repository, logs, or telemetry.

## Local verification

Run from the service directory:

```powershell
dotnet restore .\CollisionBrain.slnx --locked-mode
dotnet build .\CollisionBrain.slnx --configuration Release --no-restore
dotnet test .\CollisionBrain.slnx --configuration Release --no-build
```

The memory-driver benchmark and API/worker/stdio smoke are local evidence. These
commands do not verify the PostgreSQL profile, hosted infrastructure,
representative document extraction, retrieval quality beyond the synthetic
control, or a real Pegasus caller.

The exact dated change record records command results, skipped external profiles,
and the independent review state.

No current check recorded here proves backup/restore, hosted purge, provider-resource cleanup, deployed observability, or incident handling.

## Not verified

The following remain unverified:

- PostgreSQL integration tests and the Docker Compose runtime, because Docker is not installed on the current workstation.
- PDF and DOCX extraction against a representative approved corpus.
- A labelled retrieval benchmark.
- Hosted embedding selection.
- Provider cost benchmarking.
- Any hosted deployment, because no provider, account, region, SKU, corpus, or cost cap has been approved.
- Any Pegasus integration or other real caller.
- Production readiness or acceptance.

## Provider-neutral deployment contract

A host is compatible only when it can satisfy this contract without changes to MCP or domain code.

### Runtime

The host must provide:

- OCI-compatible execution for one HTTP API and one worker that can run continuously or periodically.
- A .NET 10 Linux runtime image containing the published CollisionBrain artifact.
- Writable temporary storage.
- Outbound HTTPS.
- Graceful `SIGTERM` handling.
- Stable, authenticated HTTPS ingress that supports streaming responses.

### Data services

The host must provide:

- PostgreSQL with the pgvector extension.
- Permission to execute `migrations/*.sql`.
- Source storage shared by the API and worker through either:
  - a persistent filesystem; or
  - an S3-compatible endpoint and bucket.
- Backup and export access sufficient to run `dotnet ... data-export` and restore through `dotnet ... data-import`.

### Identity and secret handling

The host must provide either:

- OIDC with the configured issuer, audience, JWKS URL, subject, and optional roles claim; or
- a shared bearer secret protected by an authenticated outer boundary.

Secrets must be injected at runtime and remain absent from images, source control, logs, and telemetry.

### Operational gates

Before production content is accepted:

- Probe `/health`.
- Confirm logs redact content.
- Confirm tracing is OpenTelemetry-compatible.
- Test database and source-object recovery.
- Configure a budget alert and a hard spending cap where the provider supports one.
- Rehearse export and import into a fresh environment.

Failure to satisfy any gate prevents production acceptance; it does not justify changing tool schemas or domain contracts.

## Core operating behaviour

The following behaviours are required acceptance criteria, not currently accepted outcomes:

- A text or file write returns a pending state and job ID.
- The worker moves valid content to ready and malformed or empty content to failed.
- Lookup ignores documents that are not ready and returns correct, stable citations.
- HTTP and stdio expose identical tool schemas and safety annotations.
- A duplicate write returns the existing document without creating another job.
- View-all pagination remains stable across pages and does not expose complete document bodies.
- Reader, contributor, and administrator boundaries are enforced.
- Removal purges the source and chunks and retains only a content-free tombstone.
- Replaying an old job or rebuilding from active sources cannot resurrect removed content.
- Export and import restore active knowledge into a fresh environment.

## Backup, restore, and deletion procedures

These are acceptance procedures. They have not been completed against a current hosted target.

### Export and restore

1. Export active knowledge using the package-local `data-export` command.
2. Prepare a fresh compatible environment.
3. Provision PostgreSQL with pgvector and apply `migrations/*.sql`.
4. Provision source-object storage shared by the API and worker.
5. Restore using the package-local `data-import` command.
6. Verify active knowledge, stable citations, and source-object availability.
7. Record the backup and restore result for promotion evidence.

The host must grant sufficient access for both export and import. A repository or database backup alone is insufficient if corresponding source objects cannot also be recovered.

### Purge verification

1. Remove the selected document.
2. Verify that its source object and chunks are gone.
3. Verify that only a content-free tombstone remains.
4. Replay any old job associated with the document.
5. Rebuild from active sources.
6. Verify that neither action resurrects the removed content.
7. Retain the deletion proof as promotion evidence.

## Synthetic pilot

Use only non-sensitive material. Prepare a small corpus containing:

- exact terminology;
- synonyms and paraphrases;
- documents with overlapping subjects;
- metadata tags and sources;
- deliberately irrelevant material;
- embedded prompt-injection text, treated as ordinary evidence rather than instructions.

Create labelled queries with expected document and chunk citations. Include explicit no-answer cases.

Run the full ingestion lifecycle, both MCP transports, duplicate handling, pagination, role boundaries, purge behaviour, rebuild behaviour, and export/import restoration against this corpus.

## Hosted experiment cleanup

Cleanup is part of the experiment contract, not an optional follow-up and not implied by approval to run the benchmark.

1. Stop the API, worker, jobs, schedules, queues, model endpoints, and other experiment-only execution at the recorded expiry or earlier stop condition.
2. Export and hash only the approved result, configuration, price, restore, and deletion evidence.
3. Under separate exact-target cleanup approval, remove experiment copies of the corpus, source objects, vectors, chunks, database state, logs, backups, and disposable resources; never delete shared, predecessor, source, or unlisted resources.
4. Revoke experiment-only credentials and remove local secret copies without printing them.
5. Re-read the provider inventory, billing state, retention/deletion state, and scheduled work; record every residual resource, charge, retained backup/log, and provider-controlled expiry.
6. If cleanup cannot be verified, keep the route unpromoted, record the blocker and residual cost/data exposure, and do not represent the experiment as closed.

## Promotion evidence

Promotion requires all seven evidence groups:

1. A labelled retrieval report.
2. A provider comparison.
3. A cost calculation.
4. A successful backup and restore result.
5. Deletion and non-resurrection proof.
6. An approved target account, region, SKU, corpus, and cost cap.
7. A recorded cleanup result for every external target, or an explicit local-only/not-applicable record.

Without all seven, plus satisfaction of the deployment and operational gates, Collision Brain remains a prototype. None of this evidence is currently recorded as accepted, and there is explicitly no production or Pegasus acceptance.
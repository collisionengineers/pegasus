# RAG Pipeline

Independent Collision Brain source service

> **Source-workspace boundary:** This is an independently runnable source
> service with no Pegasus caller, deployment, selected provider, or application
> authority. Its code and commands are implementation/evaluation evidence only;
> `Pegasus.Core`, current operator authority, and an authorised human own every
> accepted fact, outcome, send, report issue, and approval.

When run in its own development environment, the service provides
provider-agnostic document ingestion and hybrid retrieval through its
package-local MCP interface. That interface is not a Pegasus integration seam.

The source service returns passages and citations; it neither generates nor
accepts a Pegasus answer.

## V1 capabilities

- `lookup` — hybrid full-text/vector retrieval with stable citations.
- `write` — queue pasted text or a securely staged file for ingestion.
- `view_all` — page through document metadata and processing state.
- `remove` — purge a document and retain a content-free audit tombstone.
- Authenticated `POST /uploads` endpoint for TXT, Markdown, text PDF, DOCX, and HTML.
- Streamable HTTP MCP endpoint plus an equivalent local stdio proxy.
- Provider boundaries for repository, object storage, embeddings, and authentication.

OCR, images, email containers, archives, and generated answers are intentionally outside v1.

## Local prerequisites

- Node.js 22 or newer.
- Docker with Compose for the supported local PostgreSQL/pgvector environment.

Docker is not needed for unit tests.

## Quick start with Docker

```powershell
Copy-Item .env.example .env
docker compose up --build
```

The API listens on `http://localhost:3000`, with MCP at `/mcp`. The Compose environment starts the
API, worker, and PostgreSQL/pgvector database and applies migrations before serving traffic.

## Run from the host

Start PostgreSQL/pgvector separately, then:

```powershell
npm ci
npm run db:migrate
npm run dev:api
```

In a second terminal:

```powershell
npm run dev:worker
```

For a stdio-only MCP client, build first and configure it to execute:

```powershell
node dist/stdio.js
```

Set `RAG_HTTP_URL` and `RAG_HTTP_BEARER_TOKEN` for the HTTP service the stdio adapter should proxy.

## Upload and ingest

`write` accepts pasted text directly. For files:

1. Upload a supported file to `POST /uploads` as multipart field `file`.
2. Pass the returned short-lived `upload_ref` to `write`.
3. Poll `view_all` until the document is `ready` or `failed`.

Only `ready` documents participate in `lookup`.

## Authentication

`AUTH_MODE` supports:

- `none` — local development only; refused when `NODE_ENV=production`.
- `shared-secret` — constant-time bearer-token verification.
- `oidc` — provider-neutral JWT validation using issuer JWKS and configured audience.

Roles are `reader`, `contributor`, and `admin`. OIDC roles are read from the token's `roles` claim.

## Verification

```powershell
npm run typecheck
npm test
npm run build
```

Run the synthetic retrieval-control benchmark without external services:

```powershell
$env:REPOSITORY_DRIVER='memory'
$env:OBJECT_STORE_DRIVER='memory'
npm run benchmark -- --input benchmarks/synthetic.json
```

See [architecture](docs/architecture.md), [requirements](./docs/operations.md), the
[deployment contract](./docs/operations.md), [current status](./docs/operations.md), and
the [provider research](./docs/provider-evaluation.md).

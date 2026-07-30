# Collision Brain

Independent Collision Brain source service, rewritten as a modern .NET 10 package.

> **Source-workspace boundary:** This is an independently runnable source service
> with no Pegasus caller, deployment, selected provider, or application authority.
> Its code and commands are implementation/evaluation evidence only; `Pegasus.Core`,
> current operator authority, and an authorised human own every accepted fact,
> outcome, report issue, and approval.

The service provides provider-agnostic document ingestion and hybrid retrieval
through four package-local MCP tools: `lookup`, `write`, `view_all`, and `remove`.
It returns passages and citations; it neither generates nor accepts a Pegasus answer.
OCR, images, email containers, archives, and generated answers remain outside v1.

## Local prerequisites

- Repository-pinned .NET SDK 10.0.302.
- Docker Compose is optional and provides the convenient PostgreSQL/pgvector profile.
- Memory drivers run without Docker or external services.

## Quick start

```powershell
Copy-Item .env.example .env
dotnet restore .\CollisionBrain.slnx --locked-mode
dotnet build .\CollisionBrain.slnx --configuration Release --no-restore
dotnet run --project .\src\CollisionBrain\CollisionBrain.csproj --configuration Release -- api
```

The API listens on `http://localhost:3000`, with MCP at `/mcp`. In a second
terminal, run the worker from the same published/build output:

```powershell
dotnet run --project .\src\CollisionBrain\CollisionBrain.csproj --configuration Release -- worker
```

The stdio proxy forwards JSON-RPC to `RAG_HTTP_URL` and writes protocol responses
to stdout only:

```powershell
dotnet run --project .\src\CollisionBrain\CollisionBrain.csproj --configuration Release -- stdio
```

## Docker profile

```powershell
docker compose up --build
```

Compose starts PostgreSQL/pgvector, the API, and the worker. The API applies the
ordered migrations before serving traffic. Docker remains orchestration, not a
.NET requirement; do not delete its named volumes without separate authorization.

## Upload and ingest

`write` accepts pasted text directly. For files:

1. Upload a supported file to `POST /uploads` as multipart field `file`.
2. Pass the returned short-lived `upload_ref` to `write`.
3. Poll `view_all` until the document is `ready` or `failed`.

Only `ready` documents participate in `lookup`. Supported files are TXT,
Markdown, HTML, text PDF, and DOCX; PDF handling is text-only and has no OCR.

## Configuration and authentication

The existing environment contract is retained in `.env.example`. Drivers are
`postgres`/`memory`, `filesystem`/`memory` (with an S3 adapter available for an
explicitly configured target), and `local-hash` embeddings. Local feature-hash
embeddings are deterministic prototype infrastructure, not a selected production
retrieval model.

`AUTH_MODE` supports `none` for local development, `shared-secret` with
`MCP_SHARED_SECRET`, and provider-neutral OIDC configuration. Roles are `reader`,
`contributor`, and `admin`; production refuses unauthenticated mode.

## Administration and benchmark

```powershell
dotnet run --project .\src\CollisionBrain\CollisionBrain.csproj -- migrate
dotnet run --project .\src\CollisionBrain\CollisionBrain.csproj -- benchmark --input benchmarks/synthetic.json
```

The synthetic benchmark uses the memory drivers and must report three documents,
three queries, 384-dimensional `local`/`feature-hash`/`1` embeddings, recall@k 1,
and mean reciprocal rank 1. Export/import commands use the versioned JSON bundle
contract.

## Verification boundaries

```powershell
dotnet restore .\CollisionBrain.slnx --locked-mode
dotnet build .\CollisionBrain.slnx --configuration Release --no-restore
dotnet test .\CollisionBrain.slnx --configuration Release --no-build
```

PostgreSQL/pgvector, S3, and container proof are reported only when their exact
local targets are available and exercised. This workspace remains implementation
and local-evaluation evidence; it is not deployment, caller proof for Pegasus, or
operator acceptance. See [architecture](docs/architecture.md),
[operations](docs/operations.md), [security](docs/security.md), and the
[provider evaluation evidence](docs/provider-evaluation.md).

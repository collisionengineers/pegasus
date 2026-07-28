# Current status

Updated: 2026-07-20

## Implemented

- Provider-neutral domain and ports for repository, object storage, embeddings, and authentication.
- PostgreSQL/pgvector repository plus deterministic in-memory repository for tests.
- Filesystem, in-memory, and S3-compatible object storage.
- Pasted text and staged file ingestion for TXT, Markdown, HTML, PDF, and DOCX.
- Asynchronous worker lifecycle, hybrid lookup, registry pagination, and purging removal.
- Streamable HTTP MCP endpoint and stdio-to-HTTP proxy exposing the same four tools.
- OIDC, shared-secret, and local-development authentication modes.
- Export/import commands, SQL migrations, OCI container, Compose environment, and CI.

## Verified locally

- `npm run typecheck`
- `npm test` — offline/unit and in-memory MCP tests pass.
- `npm run build`

## Not yet verified

- PostgreSQL integration test and Docker Compose runtime: Docker is not installed on the current
  workstation.
- PDF and DOCX extraction against a representative approved corpus.
- Retrieval benchmark, hosted embedding selection, and provider cost benchmark.
- Any hosted deployment, because no provider/account/region/SKU/corpus/cost cap has been approved.

## Research gates

Do not call the local feature-hash embedding a production semantic model. Hosted deployment and
model selection remain blocked on the requirements and benchmark in `docs/research/provider-matrix.md`.

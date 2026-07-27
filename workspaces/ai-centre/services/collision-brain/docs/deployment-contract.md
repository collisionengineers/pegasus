# Deployment contract

A host is compatible when it supplies all of the following without changing MCP or domain code.

## Runtime

- OCI-compatible container execution for one HTTP API and one continuously or periodically runnable
  worker.
- Node.js 22-compatible Linux image, writable temporary storage, outbound HTTPS, and graceful
  `SIGTERM`.
- Stable authenticated HTTPS ingress supporting streaming responses.

## Data services

- PostgreSQL with the pgvector extension and permission to run `migrations/*.sql`.
- Shared source storage visible to the API and worker through either:
  - a persistent filesystem; or
  - an S3-compatible endpoint and bucket.
- Backup/export access sufficient to run `data:export` and restore with `data:import`.

## Identity and secrets

- OIDC tokens with issuer, audience, JWKS URL, subject, and optional `roles` claim; or a shared bearer
  secret behind an authenticated outer boundary.
- Secret injection through environment variables or mounted secret files. Secrets never enter the
  image, repository, logs, or telemetry.

## Configuration

The full configuration contract is documented in `.env.example`. Provider adapters may add
environment variables, but must not change tool inputs or outputs.

## Operational gates

- Health checks against `/health`.
- Content-redacted logs and OpenTelemetry-compatible tracing.
- Database and source-object recovery test.
- Budget alert and hard spending cap where supported.
- Export/import rehearsal before accepting production content.

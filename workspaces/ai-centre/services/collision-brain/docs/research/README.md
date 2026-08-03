# Provider research

Checked on 2026-07-20.

This research supports a provider-agnostic reference deployment for the RAG pipeline. It does not
select a provider or authorise account creation, provisioning, deployment, or paid usage.

## Reports

- [Compute providers](compute-providers.md) — container and serverless hosts for the HTTP API and
  asynchronous ingestion worker.
- [Hosted embedding providers](embedding-providers.md) — managed text-embedding APIs and their
  public pricing.
- [Database providers](database-providers.md) — managed PostgreSQL/pgvector, dedicated vector
  services, integrated search services, and source-object storage considerations.
- [Cross-provider matrix](provider-matrix.md) — the common decision gate and shortlist to benchmark.

## Method

- Use first-party pricing, product, quota, region, and lifecycle documentation.
- Treat prices as a dated snapshot. All dollar figures are USD unless a report says otherwise;
  taxes, exchange rates, regional uplifts, support, observability, networking, and dependent
  services may be additional.
- Keep these materially different offers separate:
  - **ongoing free allowance** — recurs or remains available without a stated trial expiry;
  - **trial credit** — expires or is exhausted once;
  - **temporarily unbilled preview** — expected to become chargeable and not a free tier;
  - **paid** — requires a subscription, minimum spend, or metered billing.
- Record uncertainty rather than inferring a price hidden behind a calculator, account portal, or
  sales enquiry.
- Compare the whole stack. A free vector index still needs authoritative metadata, ingestion jobs,
  source-object storage, compute, authentication, logs, backups, and egress.

## Selection boundary

Free services are prototype candidates only. Before selecting a reference host, complete the sizing
worksheet in [provider evaluation](../provider-evaluation.md), run a labelled retrieval benchmark, document the
target account and region, set a hard spending cap where the provider supports one, and prove
export/import and deletion. Production requires a separate paid-tier, resilience, security, support,
and data-residency decision.


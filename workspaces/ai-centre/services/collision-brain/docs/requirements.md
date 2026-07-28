# Requirements and sizing worksheet

The functional scope is fixed; deployment sizing remains intentionally unfilled until a real pilot
corpus is approved.

## Fixed v1 requirements

| Area | Requirement |
|---|---|
| Audience | Authenticated internal Collision Engineers users |
| Data classification | Non-sensitive knowledge only |
| Answering | Retrieval with citations; caller generates the answer |
| Input | Pasted text, TXT, Markdown, HTML, text PDF, DOCX |
| Upload limit | 25 MiB by default, configurable |
| Transports | Streamable HTTP MCP and equivalent stdio proxy |
| Roles | Reader, contributor, administrator |
| Removal | Immediate content purge with a content-free tombstone |
| Portability | Provider SDKs stay behind adapters; OCI runtime |

## Required before hosted-provider selection

Record and approve:

- Initial and twelve-month document count, source bytes, extracted characters, expected chunks, and
  vector dimensions.
- Average/daily ingestion, peak concurrent queries, expected monthly queries, and egress.
- Lookup p50/p95 latency and ingestion completion targets.
- Region/data-residency requirement and permitted identity providers.
- Recovery point, recovery time, backup retention, and availability expectations.
- Prototype account, region, services/SKUs, expiry behaviour, projected cost, and hard spending cap.
- Representative synthetic or approved non-sensitive evaluation corpus and labelled queries.

## Promotion rule

Free tiers may support a prototype. Production requires a fresh review of uptime, backups, inactivity
behaviour, access control, observability, support, exit/migration cost, and an explicit monthly cap.

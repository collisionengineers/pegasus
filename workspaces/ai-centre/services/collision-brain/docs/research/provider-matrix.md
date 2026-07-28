# Cross-provider research matrix

Checked on: **2026-07-20**

This is the executive synthesis of the detailed [compute](compute-providers.md),
[embedding](embedding-providers.md), and [database](database-providers.md) reports. It does not
select or approve a hosted provider.

All dollar figures are public USD list prices before tax unless stated otherwise. Region, account,
currency, support, networking, logs, registry, backup and dependent-service charges can change the
total. A dated target-account quote is required before any billed pilot.

## What “free” means

| Category | Examples in this research | Planning treatment |
|---|---|---|
| Ongoing allowance | Cloud Run, Azure Container Apps, Neon, Supabase, Aiven, Qdrant, Pinecone Starter, Weaviate, Azure AI Search Free | Useful for synthetic prototypes; still subject to suspension, deletion, quota, backup and overage rules |
| Restricted evaluation access | Cohere Trial, Mistral Free | Non-production; data-use terms and account quotas can differ from paid service |
| Introductory allocation | Voyage first-token allocation, Jina new-user tokens | Model steady-state cost separately; confirm training/retention settings before approved content |
| New-account credit | Azure, Google Cloud, AWS, Railway trial | One-off budget, not a recurring free tier |
| Temporarily unbilled preview | Azure AI Search Serverless Developer | Expected to become chargeable; no production assumption |
| No current ongoing free compute | Fargate, Fly.io, DigitalOcean dynamic components, Cloudflare Containers | Price the smallest paid footprint rather than using legacy or static-site allowances |

## Compute shortlist for an OCI pilot

| Candidate | Free/lowest entry | API and worker fit | Main decision evidence |
|---|---|---|---|
| Google Cloud Run + Jobs | Ongoing CPU, memory and request allowances; no fixed service minimum at zero instances | Direct OCI service plus run-to-completion jobs; request ceiling up to 60 minutes | Cold start, database reconnection, target-region egress/logging and billing-account safeguards |
| Azure Container Apps + Jobs | Ongoing 180,000 vCPU-s, 360,000 GiB-s and 2M requests per subscription | Direct OCI app and first-class jobs; ingress timeout is 240 seconds | Reconnect behaviour, scale-from-zero, exact regional meters and log/network cost |
| Render | Free web service; paid web and worker start at $7/month each | Direct OCI; worker is first-class but not free | Unknown general HTTP duration/body limits, one-minute-class free wake-up and $14/month two-process floor |
| Railway | Free includes $1/month usage; Hobby minimum is $5/month including usage | Direct OCI services; optional sleep, but polling/telemetry can keep a worker awake | Fifteen-minute streaming ceiling, pre-emption, hard-limit behaviour and Amsterdam egress |
| Fly.io | Two VM-hours or seven-day trial only; smallest always-running example about $2/month | Direct OCI Machines/process groups; zero-running API is possible | Explicit worker wake-up, undocumented general request limit and region-specific storage/egress |
| AWS ECS/Fargate | No recurring Fargate allowance; usage priced per task | Conventional OCI service/tasks; strongest control but more infrastructure | ALB/network/public-IP baseline, no scale-to-zero Express service and target-region total |
| Cloudflare Containers | Workers Paid minimum currently $5/month | OCI image behind Worker/Durable Object control plane | Whether provider-specific orchestration and regional placement fit the portability boundary |
| DigitalOcean App Platform | Dynamic web and worker start at $5/month each | Direct OCI web, worker and jobs | Scale-to-zero availability is unclear/private-preview; assume roughly $10/month for web+worker |

App Runner is excluded from a greenfield shortlist while AWS documents it as unavailable to new
customers. Lambda and Workers-only deployments are architecture-changing function/isolate options,
not direct baselines for the current service and worker images.

## Hosted embedding shortlist

| Route | Free/introductory position | Public standard price | Main decision evidence |
|---|---|---:|---|
| OpenAI `text-embedding-3-small` / `-large` | No free token allowance confirmed | $0.02/M and $0.13/M input tokens | Corpus quality delta, dimensions/storage, retention controls and processing geography |
| Azure OpenAI / Microsoft Foundry | New-account credit only; no recurring allowance confirmed | Per-token, but exact model/region/deployment price requires target quote | Azure identity/network/region value versus account-specific price and quota |
| Gemini Developer API / Vertex AI | Developer API has quota-limited free mode; Vertex uses cloud billing | Gemini Embedding $0.15/M online; Vertex batch $0.12/M | Free-tier product-improvement use, route-specific controls and retrieval quality |
| Amazon Bedrock Titan V2 | AWS account credit only | Official launch price $0.02/M; current target-region meter must be rechecked | London availability, compact dimensions, quality and current regional price |
| Voyage 4 family | First 200M tokens advertised free on current models | $0.02/M to $0.12/M | Account data-use opt-out, zero-retention evidence and model-quality comparison |
| Mistral Embed | Limited unquantified Free evaluation mode | $0.10/M; 50% batch discount | EU-default processing, free-plan training terms and confirmed per-request limits |
| Cohere Embed 4 | Free non-production trial | Serverless numeric price not public on the reviewed page | Dated dashboard/quote, trial data terms and corpus quality |
| Jina embeddings | New-user 10M-token allocation | API metadata implies $0.02/M to $0.05/M; confirm commercially | Conflicting older page, US processing and dashboard price |
| Hugging Face | $0.10 monthly routed credit; dedicated endpoints are paid | Provider-dependent, or endpoints from $0.033/hour | Exact routed model/provider or dedicated hardware throughput and cold start |

The cheapest headline token price is not a selection result. Hold chunking and hybrid logic
constant, then compare relevant-chunk recall, MRR/NDCG, no-answer behaviour, query latency,
dimensions, re-index time, data terms and total stored-index cost. Every model or dimension change
creates a new embedding generation and requires a controlled full re-index.

## Data and search shortlist

| Shape/candidate | Free/lowest entry | Why retain it | Main decision evidence |
|---|---|---|---|
| Local PostgreSQL/pgvector | Local operational cost only | Canonical portability and recovery baseline | Corpus/index size, HNSW build, query latency and restore time |
| Neon Postgres | 0.5 GB + 100 CU-hours/project; Launch from usage, example about $15/month | Standard Postgres, scale-to-zero and export routes | Small storage ceiling, restore window and conflicting paid-egress documents |
| Supabase Postgres | 500 MB DB + 1 GB files; Pro from $25/month | Postgres/pgvector plus S3-compatible files | Free pause/no automatic backup, index overhead and coordinated DB/file export |
| Aiven PostgreSQL | 1 CPU/1 GB RAM/1 GB disk; Developer $5/month | Standard Postgres, pgvector and paid-region breadth | Assigned free region and ambiguous free/developer backup retention |
| Qdrant Cloud | 0.5 vCPU/1 GB RAM/4 GB disk | Open-source dedicated vector engine and migration tooling | Free deletion after four inactive weeks, separate metadata/source stores and measured hybrid gain |
| Pinecone Starter | 2 GB, 1M RU and 2M WU/month; Builder $20/month | Managed serverless vector/search comparator | US-only Starter, proprietary API, no Starter/Builder backup and outbound rebuild path |
| Weaviate Cloud | 100,000 objects/10 GB disk; Flex from $45/month | Open-source engine with native BM25F+vector fusion | One free collection, no free backup, HFresh/HNSW difference and measured quality |
| Zilliz/Milvus | 5 GB + 2.5M vCUs/month | Milvus-compatible hybrid comparator | vCU cost model and restricted outbound backup export |
| Azure AI Search | One ongoing 50 MB service; paid regional pricing | Native BM25+vector RRF and integrated Azure option | Free limitations/inactivity conflicts, no native backup and separate source of truth |
| Cloudflare Vectorize | Official pages conflict on Free eligibility; Paid starts with Workers | Low-cost vector-dimension model | Resolve free eligibility, no documented native BM25 leg and proprietary export/rebuild |

MongoDB Atlas and Upstash remain documented alternatives in the detailed report. They are not
first-line baselines here: Atlas changes the core transactional model, while Upstash has unresolved
first-party contradictions around free index count and hybrid availability.

Original source files remain behind the S3-compatible `ObjectStore`. The database report compares
R2, Backblaze B2, Supabase Storage, Wasabi and Amazon S3; the same adapter calls must pass against
the selected store.

## Architectures to benchmark

### A. Portable single-database baseline

```text
OCI API + job worker
        |
managed PostgreSQL: documents, jobs, chunks, FTS and pgvector
        |
S3-compatible source-object store + hosted embedding adapter
```

This is the comparison baseline because it satisfies transactional lifecycle state and hybrid
retrieval without a second search service.

### B. Split search comparator

```text
OCI API + job worker
        |
transactional PostgreSQL -------- dedicated vector/search index
        |                                  |
S3-compatible source store         rebuildable chunks/vectors
        \-------------- hosted embedding adapter --------------/
```

This shape progresses only if measured retrieval quality, latency or scale justifies the extra
service, network path, reconciliation, deletion and recovery work.

## Decision gate

Before selecting a reference deployment:

1. Complete the corpus, traffic, region, retention, RPO/RTO and budget inputs in
   [requirements](../requirements.md).
2. Use only synthetic or separately approved non-sensitive content.
3. Benchmark at least two direct OCI hosts, two managed PostgreSQL options and representative
   low-cost and higher-quality hosted embeddings.
4. Add a dedicated vector/search comparator only after the PostgreSQL baseline is measured.
5. Exercise write → process → lookup → list → remove, retry after interruption, idle suspension,
   citation correctness and metadata filtering.
6. Prove source/database export, fresh-local import, full index rebuild and deletion without stale
   job resurrection.
7. Save current target-account, region/SKU, quota and price evidence; distinguish hard stops from
   budget alerts.
8. Reassess a paid production tier separately for SLA, backup, support, private access, data
   residency, observability and an explicit monthly spending cap.

No hosted provider should be selected from its free allowance or model reputation alone.


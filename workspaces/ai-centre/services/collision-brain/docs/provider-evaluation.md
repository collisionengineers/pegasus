# Provider Evaluation Evidence

> **Status:** Comparative evidence only. No provider is selected, deployed, activated, technically accepted, approved for production, or represented as a Pegasus caller. No paid test, Azure deployment, or provider choice is evidenced here.

## Evidence identity and authority

| Field | Value |
|---|---|
| Evidence owner | **Collision Brain — Provider Evaluation Evidence Owner** (single evidence-record role; no named individual was supplied) |
| Evidence-owner date | **2026-07-20** |
| Evidence status | Dated research consolidation for controlled experiments only |
| Decision authority | Pegasus.Core and authorised humans retain all business, spending, activation, acceptance, and provider-selection decisions |
| Price date and currency | Public USD list-price snapshots checked **2026-07-20**, before tax unless stated otherwise |
| Checksums | None supplied for the source evidence |
| Snapshot limitations | No archived pages, response checksums, target-account quotations, or guarantees that prices and limits remain current after **2026-07-20** |
| Content boundary | Experiments use synthetic or separately approved non-sensitive labelled content only |
| Whole-stack boundary | Compute, database, embeddings, object storage, authentication, networking, logging, backups, recovery, and egress must be assessed together |

## Source register

| Source | Title and date | Evidence retained |
|---|---|---|
| [`research/README.md`](#evidence-and-offer-rules) | **Provider research**, checked **2026-07-20** | First-party-source method, offer classifications, sizing/benchmark requirements, and decision boundary |
| [`research/compute-providers.md`](#compute-evidence) | **Compute hosting providers**, checked **20 July 2026** | OCI/API/worker capabilities, runtime and ingress limits, regions, allowances, prices, security boundaries, and compute tests |
| [`research/database-providers.md`](#database-vectorsearch-and-object-storage-evidence) | **Database, vector database and object-storage providers**, checked **2026-07-20** | PostgreSQL, vector/search, object-storage, export, backup, recovery, lifecycle, limits, and prices |
| [`research/embedding-providers.md`](#hosted-embedding-evidence) | **Hosted embedding-provider research**, checked **20 July 2026** | Hosted embedding routes, prices, dimensions, limits, batch behavior, data terms, provenance, and migration gates |
| Additional embedding source date | Jina API documentation published **29 June 2026** | Newer Jina limits and model metadata; older-page conflicts remain unresolved |
| [`research/provider-matrix.md`](#architecture-and-reproducibility-contract) | **Cross-provider research matrix**, checked **2026-07-20** | Controlled architecture hypotheses, shortlist, reproducibility matrix, and whole-stack decision gates |
| [`requirements.md`](#inputs-that-must-be-frozen-and-recorded) | Referenced sizing worksheet; no contents distilled here | Must be completed before a billed or provider-hosted experiment |

Official URLs below are those retained in the supplied distillations. Compute and embedding distillations described first-party sources but did not retain their individual URLs; absence of a URL here is not independent verification.

## Evidence and offer rules

| Rule | Treatment |
|---|---|
| Ongoing free allowance | Recurs or remains available without a stated trial expiry; it does not imply a complete free stack |
| Trial credit | One-time, expiring, or exhaustible; never amortised as a permanent discount |
| Temporarily unbilled preview | Expected to become chargeable and not treated as an ongoing free tier |
| Paid | Subscription, minimum spend, or metered billing is required |
| Unknown price | Record as unknown rather than infer from a calculator, portal, metadata field, or sales page |
| Complete cost | Include region/SKU uplift, registry, builds, ingress/front door, IP/load balancer, network transfer, logs, metrics, backups, database, object storage, embeddings, support, and tax/currency effects |
| Hard spending control | Distinguish a provider-enforced stop from an alert or automation input; destructive behavior must be tested in a disposable account |
| Capacity statement | Vendor estimates and published quotas are not workload measurements or target-account guarantees |

# Architecture and reproducibility contract

## Fixed system boundaries

| Area | Required invariant |
|---|---|
| API | Linux OCI image, Node.js 22, authenticated public Streamable HTTP MCP, low/intermittent initial traffic, and sessions that may exceed ordinary request duration |
| Worker | Independent document-ingestion process that extracts, chunks, embeds, and indexes; survives client disconnect and API termination; retries safely and idempotently |
| Job provenance | Commit a durable PostgreSQL job record before invoking any provider queue, trigger, task, or worker |
| Source files | Originals exist only through `ObjectStore`; local or attached compute filesystems are bounded scratch and never authoritative |
| Transactional state | Documents, jobs, chunks, metadata, deduplication state, audit tombstones, and source hashes remain in PostgreSQL through repository interfaces |
| Search state | A dedicated vector/search index, if tested, is derived, disposable, and fully rebuildable from authoritative data |
| Upload path | Prefer short authenticated staging or direct object-store upload using a short-lived reference; do not assume compute ingress can proxy large files safely |
| Connectivity | Outbound TLS to PostgreSQL, object storage, and hosted embeddings where used |
| Portability | Core application code does not require provider discovery, identity, queue, trigger, or wake SDKs; those remain deployment adapters |
| Request lifecycle | Client disconnect, API shutdown, instance replacement, or scale-down cannot cancel committed ingestion |
| Scratch | Every supported document format has explicit disk and memory bounds |
| Observability | Logs and traces omit raw queries, document bodies, source content, credentials, and vectors where they could disclose content |
| Local baseline | Contract, deletion, recovery, and embedding tests remain runnable with local Docker Compose and a deterministic local embedding provider |

## Controlled architecture hypotheses

| Shape | Controlled hypothesis | Continuation rule |
|---|---|---|
| Portable single database | PostgreSQL + `pgvector` + PostgreSQL FTS may satisfy lifecycle and hybrid retrieval with less operational complexity | Treat as the comparison baseline until corpus-scale quality, latency, index-build, restore, and recovery measurements exist |
| Split search | Transactional PostgreSQL + dedicated vector/search adapter may improve quality, latency, or scale | Do not begin before the PostgreSQL baseline; continue only if measured benefit justifies added networking, backup, reconciliation, deletion, and rebuild work |
| Source/search separation | Source binaries remain in S3-compatible object storage while search state remains derived | Require provider-neutral source/database export, fresh-local import, full rebuild, and deletion without stale-job resurrection |
| Hosted embeddings | Price alone cannot predict retrieval quality or total index impact | Hold corpus, chunking, hybrid logic, filters, and reranking constant; any model/version/dimension/task change creates a new embedding generation |
| Portable compute | Conventional OCI services/jobs should preserve the application contract more directly than function/isolate control planes | Stop a route if provider-specific lifecycle state becomes authoritative or local recovery cannot be reproduced |

## Inputs that must be frozen and recorded

| Category | Reproducibility inputs |
|---|---|
| Requirements | Corpus size/composition, traffic profile, target account and region, retention, availability target, RPO/RTO, and experiment budget |
| Corpus | Versioned, approved, non-sensitive labelled documents containing representative terminology, abbreviations, near-duplicates, long sections, no-answer cases, and multilingual examples only if required |
| Documents | File GB, monthly ingress/egress, document count, average extracted characters, supported formats, and chunk count |
| Retrieval | Fixed chunking, FTS languages, hybrid logic, metadata filters, citation fields, candidate count, `topK`, reranking, query set, and relevance judgments |
| Vectors | Count, dimensions, metadata bytes/chunk, index type and parameters, generation identifier, normalisation, task/input type, tokenizer/version, and truncation policy |
| Runtime | API/worker connection and concurrency pattern, execution duration, target region, cross-region traffic, cold-start conditions, and database pooling/reconnection |
| Embedding account | Route, immutable model/version, RPM, TPM, daily cap, concurrency, billing state, processing region, training/retention controls, and date observed |
| Failure behavior | Timeouts, 429, transient 5xx, connection reset, malformed response, batch timeout, duplicate delivery, partial failure, oversized input, and stuck-worker limit |
| Recovery | Backup retention, restore-test frequency, export/import duration, rebuild duration, deletion behavior, availability target, RPO, and RTO |
| Cost | Initial corpus, monthly changes, query tokens, one full re-embedding reserve, measured usage, standard/batch rates, endpoint minimums, storage, egress, networking, logs, metrics, backups, and dependencies |
| Comparable compute shapes | One-month idle API; fixed light-use lookup/job set; failure case with retry loop, oversized document, or stuck worker up to its configured execution limit |
| Comparator coverage | At least two direct OCI hosts, two managed PostgreSQL options, and representative low-cost and higher-quality hosted embeddings |

# Compute evidence

## Runtime, worker, ingress, storage, and region observations

| Platform | Runtime and worker capability | Material limits | Storage and region observations |
|---|---|---|---|
| **Google Cloud Run services + Jobs** | Standard containers; services scale to zero; Jobs run containers to completion. IAM, secrets, scheduling, event delivery, and definitions are Google-specific. | Service request max **60 min**; default **5 min** unless raised; HTTP/1 request **32 MiB**; HTTP/2 server requests differ; non-streaming response **32 MiB**; up to **1,000 concurrent requests/instance**, resource dependent. Job task timeout up to **168 h** and **10,000 tasks/execution**, quota permitting. Runtime may allow four minutes to begin listening; measured cold starts are absent. | Writable filesystem is non-persistent, memory-backed, and consumes instance memory. London `europe-west2` and other European locations are listed; exact service/job eligibility is account/region dependent. |
| **Azure Container Apps + Jobs** | Conventional containers; HTTP apps plus manual, scheduled, or event-driven Jobs with timeout, retry, parallelism, and replica controls. KEDA, managed identity, secrets, ingress, and triggers are Azure-specific. | HTTP/1.1, HTTP/2, WebSocket, and gRPC; request timeout **240 s**. Long ingestion cannot depend on an open request. Platform retries do not understand document lifecycle semantics. | Replica-scoped ephemeral storage disappears when the replica stops and depends on vCPU. Azure Files is persistent but provider-specific and unnecessary for authoritative originals. UK South and West Europe are listed; features, zones, and profiles vary. |
| **AWS ECS/Fargate** | Standard API services and run-to-completion tasks. Express Mode creates ECS/Fargate, ALB, IAM, networking, and observability; low-end default is **1 vCPU/2 GiB** with at least one desired task. | No intrinsic Fargate HTTP ceiling; ALB/proxy/application determine timeout, upload, and streaming. Linux billing begins at image pull with **1 min minimum**. | Includes **20 GiB** ephemeral storage; additional ephemeral storage is billable. Region, architecture, rates, and capacity vary; target-region evidence is absent. |
| **AWS Lambda** | Container image packages a Lambda handler, not a conventional server/worker. API needs an adapter; ingestion must be bounded or split into resumable events. | Invocation max **15 min**. Response streaming up to **200 MiB**, throttled after the first **6 MiB**; front-door and regional availability require confirmation. | Function lifecycle is not a conventional long-running worker; target-region streaming/front-door support is unproved. |
| **AWS App Runner** | Existing-customer pricing remains published, but AWS documentation says the service is unavailable to new customers and receives no new features. No direct job product. | Older evidence indicates provisioned memory remains charged while idle. | Greenfield activation is blocked while the documented new-customer restriction remains. |
| **Render** | Prebuilt Docker web services; first-class paid background workers. | HTTP/2 and WebSockets documented. General provider-wide request-duration and upload-body maxima were not established. Open streams can close during instance replacement; free wake can take about one minute. | Free filesystem is ephemeral; free services cannot use persistent disks. Frankfurt was the listed European region; no UK region was listed. |
| **Railway** | Conventional API and worker containers. Optional Serverless sleeps after more than **10 min** without outbound traffic; pools, polling, or telemetry can prevent sleep. | HTTP and SSE/WebSocket max **15 min**; keep-alive **60 s**; headers **32 KiB**. Reconnection is required. Free deployments have lower priority and may be suspended under paid demand. | Free limits include **1 GiB** ephemeral storage and **0.5 GB** volume. A volume limits a service to one replica and adds deployment constraints. Amsterdam was the listed European region. |
| **Fly.io Machines** | Standard OCI images; API/worker process groups or Machines. Fly Proxy may stop/start Machines with zero minimum running Machines. A worker does not wake merely because a PostgreSQL job exists; explicit orchestration, schedule, queue, private service, or always-on Machine is required. | HTTP/2 and configurable idle timeout documented; no provider-wide request-duration or body-size maximum was established. | Root filesystem is ephemeral. Volumes are tied to one Machine/physical server and are not automatically replicated; snapshots are not application backup. London, Amsterdam, and Frankfurt listed; capacity varies by shape and region. |
| **Cloudflare Workers** | JavaScript isolate with Node compatibility, not the repository’s ordinary OCI runtime; requires port and parser-compatibility review. | Free CPU **10 ms/invocation**; paid requests up to **5 min CPU**. HTTP wall time has no fixed limit while connected, but CPU/body limits apply. Cron/Queue wall max **15 min**. Bodies: **100 MB Free/Pro**, **200 MB Business**, **500 MB Enterprise**. Memory **128 MiB**; compressed script **3 MiB Free / 10 MiB Paid**. | Document parsing suitability is unproved. Global placement rather than a conventional selected region leaves residency and database proximity unresolved. |
| **Cloudflare Containers** | Linux images controlled through Worker/Durable Object-style routing and lifecycle. Authorisation, identity, placement, and job triggering materially couple the design to Cloudflare. | Triggering Worker/Queue may have a **15 min** orchestration limit. Safe container continuation after returned, failed, retried, or duplicate triggers is unproved. Smallest current type: fractional vCPU, **256 MiB RAM, 2 GB disk**. | Disk follows container lifecycle and cannot be authoritative. Regional placement controls and stored-data location require target-account review. |
| **DigitalOcean App Platform** | Linux AMD64 images; first-class web, worker, and job components. | **4 GiB** non-persistent filesystem/container; **600 s** upload timeout; deployment jobs default **30 min**, configurable within documented bounds. HTTP autoscaling applies to web services, not workers. | Persistent volumes cannot attach to App Platform components. London, Frankfurt, and Amsterdam facilities exist for parts of the portfolio; exact component/shape availability is unproved. |

## Compute prices and allowances checked 2026-07-20

| Platform | Dated public evidence | Price and allowance caveats |
|---|---|---|
| **Cloud Run** | Request-based monthly allowance: **2M requests, 180,000 vCPU-s, 360,000 GiB-s**. Instance-based: **240,000 vCPU-s, 450,000 GiB-s**. Listed **1 GiB/month** free North America outbound transfer. Example rates: **$0.000018/vCPU-s**, **$0.000002/GiB-s**; **100 ms** rounding. New-account trial **$300/90 days**. | Ongoing allowance requires active billing; overage is charged. Zero instances remove fixed service compute but not necessarily registry, builds, logs, domains, networking, connectors, or databases. Rates are not target-region quotes. |
| **Azure Container Apps Consumption** | Subscription grant: **180,000 vCPU-s, 360,000 GiB-s, 2M HTTP requests/month**. Retail Prices API snapshot: UK South/West Europe active **$0.000034/vCPU-s**, **$0.000004/GiB-s**; idle **$0.000004/vCPU-s**, **$0.000004/GiB-s**; requests **$0.40/M UK South**, **$0.56/M West Europe**. New-account credit **$200**, time-limited. | Snapshot, not quote. Zero replicas have no replica-resource charge; Jobs bill start-to-completion. Networking, logs, dedicated profiles, GPUs, and support services are separate. |
| **ECS/Fargate** | US East Linux/x86 examples: **$0.000011244/vCPU-s** or **$0.04048/vCPU-h**; **$0.000001235/GB-s** or **$0.004446/GB-h**; storage above 20 GiB **$0.0000000308/GB-s**. Always-on **1 vCPU/2 GiB** example ≈ **$35.55/720 h** before ALB. New accounts: initial **$100**, potentially another **$100**; free plan at most **6 months** or until exhausted. | No ongoing Fargate compute allowance. Region, architecture, ALB, public IPv4/NAT, ECR, logs, and transfer materially affect cost. Express Mode retains at least one API task. |
| **Lambda** | Monthly allowance **1M requests, 400,000 GB-s**. Example overage: **$0.20/M requests**, **$0.0000166667/GB-s** in first x86 duration tier. | Low idle cost does not prove compatibility; handler/front-door adapters and resumable workflow changes are required. |
| **Render** | Free web **512 MiB/0.1 CPU**, **750 instance-hours/workspace/month**. Paid web/private/worker starts **$7/month** for **512 MiB/0.5 CPU**, second-prorated. API + worker floor **$14/month**. Hobby bandwidth **5 GB**, then **$0.15/GB**; build minutes **500**, then **$5/1,000 min**; disk **$0.25/GB-month**. | Free excludes background workers, private services, and one-off jobs; web sleeps after **15 min** without inbound HTTP/WebSocket. Workspace/service allowance interaction must be rechecked. |
| **Railway** | Free **$1/month included usage**; Hobby **$5/month with $5 included**; Pro **$20/month with $20 included**; one-off trial **$5**. Approximate: **$10/GB-month RAM**, **$20/vCPU-month**, **$0.05/GB egress**, **$0.15/GB-month volume**; per-minute compute. | Pools, polling, and telemetry may defeat sleep. Hard usage limits can stop workloads, but outage behavior and uncovered charges require destructive testing. |
| **Fly.io** | Trial **2 VM hours or 7 days**, whichever first; trial Machines stop after **5 min**. No ongoing legacy free VM allowance for new accounts. `shared-cpu-1x`: 256 MiB ≈ **$2.02/30 days**, 512 MiB ≈ **$3.32**, 1 GiB ≈ **$5.92**. Stopped root storage **$0.15/GB/30 days**; volumes **$0.15/GB-month**; snapshots **$0.08/GB-month**, first 10 GB free; Europe/North America egress **$0.02/GB**; private cross-region **$0.006/GB**. | Region affects rates. Stopping removes CPU/RAM charges, not retained storage; worker wake orchestration may introduce baseline cost. |
| **Cloudflare Workers** | Free **100,000 requests/day**, **10 ms CPU/invocation**. Paid minimum **$5/month**, including **10M requests** and **30M CPU-ms**; overage **$0.30/M requests**, **$0.02/M CPU-ms**. Workers egress listed as uncharged. | Free supports lightweight adapter work only; ingestion compatibility is unproved. |
| **Cloudflare Containers** | Included in **$5 Workers Paid**: **25 GiB-h RAM, 375 vCPU-min, 200 GB-h disk**. Overage **$0.0000025/GiB-s**, **$0.000020/vCPU-s**, **$0.00000007/GB-s disk**. Europe/North America egress **$0.025/GB**, with **1 TB included**. | Worker requests, Durable Objects, queues, and logs may add charges. Sleeping ends active compute but not all disk/control-plane costs. This is not free container hosting. |
| **DigitalOcean App Platform** | Dynamic web and worker each start **$5/month** for **1 shared vCPU/512 MiB**, each including **50 GiB** outbound transfer. API + continuous worker ≈ **$10/month**. Additional transfer **$0.02/GiB**. Per-second billing with **1 min minimum**. | Only static sites have a free tier. Jobs bill during execution; builds, logs, and support services are additional. |
| **AWS App Runner** | Existing-customer pricing page remains available, including provisioned-memory and active-CPU pricing. | Published price does not prove new-customer availability; availability documentation blocks a greenfield experiment until changed. |

## Compute compatibility classification

| Classification | Routes | Meaning |
|---|---|---|
| Direct OCI benchmark shape | Cloud Run services/Jobs; Azure Container Apps/Jobs; ECS/Fargate; Render web/worker; Railway services; Fly.io Machines/process groups; DigitalOcean web/worker/jobs | Runtime comparability only; not acceptance or suitability |
| Architecture-changing experiment | Lambda; Cloudflare Workers; Cloudflare Containers | Requires explicit approval of handler/isolate/control-plane and resumable-orchestration changes before experimentation |
| Blocked greenfield activation | AWS App Runner | Do not activate while AWS documents the new-customer restriction |

# Database, vector/search, and object-storage evidence

## Data architecture capabilities

| Capability | Evidence boundary |
|---|---|
| PostgreSQL authority | Can hold document/job lifecycle, chunks, deduplication constraints, tombstones, FTS indexes, and vectors transactionally; original binaries remain in object storage |
| PostgreSQL retrieval | `pgvector` supports exact, HNSW, and IVFFlat search; PostgreSQL FTS and vector results can be fused in SQL |
| PostgreSQL portability | Requires testing extension versions, functions, generated columns, index-build memory, pooled/unpooled connections, dump/restore, logical replication, and restore performance |
| Dedicated search | Must remain a rebuildable downstream index; authoritative state, source hashes, and originals remain elsewhere |
| S3-compatible storage | Limit adapter use to bucket, put/get/head/delete, required multipart upload, presigned transfer, content hash/ETag, encryption controls, and lifecycle operations |
| Compatibility caveat | “S3 compatible,” “durable,” “managed,” or “retained” does not prove complete protocol behavior, backup, restore, or off-provider recovery |

## Managed PostgreSQL

| Provider and official source | Price/allowance checked 2026-07-20 | Search, limits, export, backup, and lifecycle |
|---|---|---|
| [Neon](https://neon.com/pricing) | Free: **0.5 GB**, **100 CU-hours/month/project**, up to **2 CU**, **5 GB** public transfer, six-hour/**1 GB** change-history window. Launch **$0.106/CU-hour + $0.35/GB-month**; vendor example ≈ **$15/month** for intermittent 1 GB. Scale **$0.222/CU-hour + $0.35/GB-month**. | PostgreSQL FTS + `pgvector`; documented `pg_dump`/`pg_restore`, `pgcopydb`, and logical replication. Exit must use unpooled connectivity and preserve extensions, indexes, functions, and generated FTS columns. Free transfer exhaustion stops compute until cycle reset or upgrade. Branch archival/rehydration and scale-to-zero may delay first access. Free restore history is not off-provider backup. Paid-transfer conflict: [network guide](https://neon.com/docs/introduction/network-transfer) says 100 GB then **$0.10/GB**; [June 2026 announcement](https://neon.com/blog/more-data-transfer-on-paid-plans) says 500 GB from **2026-06-01**. Project count needs account confirmation. |
| [Supabase](https://supabase.com/pricing) | Free/project: **500 MB DB**, **1 GB files**, **5 GB uncached + 5 GB cached egress**; at most two active free projects. Pro from **$25/month**, including **$10** compute credit; **8 GB DB**, **100 GB files**, **250 GB uncached + 250 GB cached egress**. Overage: DB **$0.125/GB**, files **$0.0213/GB-month**, uncached **$0.09/GB**, cached **$0.03/GB**. Seven-day PITR adds **$100/month**. | PostgreSQL FTS + documented `pgvector` RRF. Dump/restore and S3-compatible files provide an exit only when DB/files are exported together and reconciled. Free DB becomes read-only above 500 MB; indexes count. No automatic Free backup/PITR; external dump recommended. Free projects pause after one inactive week. After >90 paused days Studio restore is unavailable, though archives may remain downloadable until deletion. Storage has no object versioning; deletion is permanent. |
| [Aiven for PostgreSQL](https://aiven.io/docs/products/postgresql/concepts/pg-free-tier) | Free: one service/type/organisation, **1 CPU, 1 GB RAM, 1 GB disk**. Developer **$5/month**: 1 CPU, 1 GB RAM, 8 GB. Hobbyist from **$12**, Startup **$75**, Business with standby **$180**; region/cloud affects paid totals. | `pgvector`, PostgreSQL FTS, and `pg_dump`/`pg_restore`. Free has one node, `max_connections=20`, assigned region/cloud, and no VPC, static IP, integration, fork, or connection pool. Inactive services may be powered off after warning and reactivated. Recovery is ambiguous: Free page says backups included, but retention tables omit Free/Developer and give Hobbyist no retained backup. Free cannot fork a restore; tested external dumps are required. |
| [Crunchy Bridge](https://www.crunchydata.com/products/crunchy-bridge) | No ongoing free database shown; exact price is calculator-driven by region/configuration. | Standard PostgreSQL across AWS, Azure, and GCP; pooling and cross-cloud recovery documented. Retained only as a paid technical comparator; no free-pilot evidence. |

## Dedicated vector/search services

| Provider and official source | Price/allowance checked 2026-07-20 | Retrieval, limits, backup/export, and lifecycle |
|---|---|---|
| [Qdrant Cloud](https://qdrant.tech/documentation/cloud/create-cluster/) | Free: one non-dedicated node, **0.5 vCPU, 1 GB RAM, 4 GB disk**; vendor estimate ≈ one million 768-d vectors, dependent on payload/index/quantisation/replication. Standard meters CPU, memory, disk, backup, and optional inference; no stable minimum published. | Dense/sparse named vectors, filters, RRF/DBSF fusion; not a complete text engine with query analysers/non-vector ranking. Free supports manual API snapshots/restores but no automatic backup/DR. Suspends after one unused week and deletes after four inactive weeks unless reactivated. Open-source/self-hostable; snapshots can use S3-compatible storage subject to minor-version compatibility. Migration tooling supports multiple engines. PostgreSQL/object storage remain authoritative. |
| [Pinecone](https://docs.pinecone.io/reference/api/database-limits) | Starter **$0**: **2 GB, 1M read units, 2M write units, 5M embedding tokens/model/month**, up to five serverless indexes, AWS `us-east-1`; exhausted allowances block operations. Builder **$20/month**; Standard **$50 minimum**; Enterprise **$500 minimum**. Separate Standard trial **21 days/$300**, once/organisation. | Dense+sparse single-index or client-fused separate indexes; weighting/normalisation is explicit. Starter/Builder have no backups. Standard backups remain in the same project/cloud/region, may omit about 15 minutes of writes, and do not support current full-text/document-schema indexes. Parquet import is ingress, not outbound export. Preserve provider-neutral text, metadata, vectors, or deterministic regeneration inputs. |
| [Weaviate Cloud](https://weaviate.io/pricing) | Free: one cluster/user, **100,000 objects, 1 GB memory, 10 GB disk**, one collection, up to three tenants, **2,000 managed-embedding requests/day**. Flex from **$45/month**; usage from **$0.00465/M vector dimensions**, **$0.12/GiB storage**, **$0.0264/GiB backup**, subject to minimum. Transfer was temporarily free. | BM25F + vector hybrid with relative-score/rank fusion and `alpha`. Free uses HFresh; paid required to select HNSW. Free has no backup, replication, or SLA; suspends after seven inactive days while preserving data. Open-source/self-hostable. Backup supports S3/GCS/Azure and cross-provider restoration, but an external authoritative source remains required. |
| [Zilliz Cloud / Milvus](https://docs.zilliz.com/docs/free-trials) | Ongoing Free: one cluster/organisation, **5 GB, 2.5M vCUs/month**, up to five collections; vendor estimate ≈ one million 768-d vectors. Separate work-email trial **$100/30 days**. Serverless read/write **$4/M vCUs**, six-vCU minimum/read, plus storage/transfer/backup/audit. Example storage—not universal—**$0.025/GB-month** in AWS `us-east-1`. | BM25 sparse fields, multiple dense/sparse paths, weighted/RRF fusion. Milvus SDK/open-source engine reduce but do not remove migration risk. Credit-backed trial clusters freeze/recycle at expiry and delete after 30 days without payment; distinct from ongoing Free. Paid suspended clusters continue storage charges. General import/migration exists, but customer-object-storage backup export is Private Preview for Dedicated Enterprise. Exact-plan outbound export must be demonstrated. |
| [Upstash Vector](https://upstash.com/pricing/vector) | Free **$0**: **10,000 query/update operations/day, 1 GB, 1,536 max dimensions, 100 namespaces, 200M vector-dimensions**. PAYG **$0.40/100k operations + $0.25/GB storage**; 200 GB same-region bandwidth then **$0.03/GB**. Fixed **$60/month** for up to 1M operations/day plus storage. | Current docs describe dense+sparse hybrid and server-side fusion with optional BGE-M3/BM25 embeddings. Official pages conflict on sparse availability, one free database versus up to ten indexes, and older statements that hybrid/replication were unsupported. No reviewed evidence establishes user-controlled backup/export or replication. Console validation and provider-neutral export/rebuild are mandatory. |

## Integrated document/search services

| Provider and official source | Price/allowance checked 2026-07-20 | Capability and recoverability |
|---|---|---|
| [MongoDB Atlas](https://www.mongodb.com/docs/atlas/tutorial/deploy-free-tier-cluster/) | M0: one free cluster/project, **512 MB including indexes**, shared compute, indicated ceiling **100 operations/s**. Flex **$0.011/hour**, capped at **$30/month**, 5 GB and daily snapshots. Dedicated from **$0.08/hour**, ≈ **$56.94/month** before transfer/add-ons. | Native full-text + vector hybrid fusion. M0 has no backups and pauses after 30 inactive days; Search indexes rebuild and remain temporarily unavailable on resume. Making Atlas authoritative would replace the PostgreSQL persistence model, not merely swap a search adapter. |
| [Azure AI Search](https://learn.microsoft.com/azure/search/search-try-for-free) | Free: one service/subscription, **50 MB, three indexes, three indexers, three data sources**. Dedicated bills hourly/Search Unit and cannot pause; deletion stops cost. Basic usually 15 GB/partition, but prices are portal/region dependent. Serverless Developer preview was temporarily unbilled and available only in West Central US, Switzerland North, and Japan East, with at least 30 days’ notice before billing. | Native BM25 + vector parallel search with RRF. Embeddings, semantic ranking, and enrichment may add charges. Free cannot scale and lacks managed identity, IP firewall, private endpoint, and zones. Indexes have no native backup/restore; deleted indexes/services are unrecoverable and require full rebuild. Serverless preview has no SLA and cannot migrate to/from other pricing models. |
| [Cloudflare Vectorize](https://developers.cloudflare.com/vectorize/platform/pricing/) | **April 2026** page: Workers Free includes **30M queried and 5M stored dimensions/month**; Paid includes **50M queried and 10M stored**, then **$0.01/M queried dimensions** and **$0.05/100M stored dimensions**; no Vectorize egress charge. Workers Paid base **$5/month**. | Nearest-neighbour search, namespaces, and metadata filters; no native BM25/full-text leg established. Limits: **1,536 dimensions, 10M vectors/index, 10 KiB metadata/vector**, up to ten metadata indexes. A **July 2026** Workers page says paid-only while also showing Free/Paid allowances; `$0` use is unproved until target-account creation/query succeeds without upgrade. |

## Object storage

| Store and official source | Price evidence checked 2026-07-20 | Limits, lifecycle, and portability |
|---|---|---|
| [Cloudflare R2 Standard](https://developers.cloudflare.com/r2/pricing/) | **10 GB-month, 1M Class A, 10M Class B/month** free; then **$0.015/GB-month**, **$4.50/M Class A**, **$0.36/M Class B**; direct egress free. | S3-compatible with documented omissions; exact adapter calls require conformance testing. |
| [Backblaze B2](https://www.backblaze.com/cloud-storage/pricing) | First **10 GB** always free; **$6.95/TB-month**; mostly free transactions; egress free up to 3× average stored data, then **$0.01/GB**. | S3-compatible, Signature V4 documented, no minimum storage duration; account is tied to one region. |
| [Supabase Storage](https://supabase.com/docs/guides/storage/pricing) | **1 GB** per Free project. Pro includes **100 GB**, then **$0.0213/GB-month**; transfer uses Supabase allowances/rates. | S3 protocol compatible but no object versioning; deletion is permanent. Billing/lifecycle are coupled to the Supabase project. |
| [Wasabi](https://docs.wasabi.com/docs/may-2026-wasabi-pricing) | **May 2026** pricing evidence: 30-day trial up to 1 TB; no ongoing free allowance. From **July 2026**, **$7.99/TB-month** with 1 TB monthly minimum. | S3-compatible with **90-day minimum storage duration**; headline per-TB price understates small or frequently deleted corpus cost. |
| [Amazon S3](https://aws.amazon.com/s3/pricing/) | New-account credits up to **$200**; Free plan lasts six months; credits expire within 12 months. No permanent new-account storage allowance established. | Canonical S3 behavior and broad integrations. Storage class, requests, retrieval, and transfer require a declared-region estimate. |
| Azure Blob / Google Cloud Storage | No dated regional amount was captured. | Valid future adapters, especially colocated with compute/search, but not substitutes for proving the initial portable S3 adapter. |

# Hosted embedding evidence

## Capabilities, limits, prices, and operational observations

`$x/M` means USD per million input tokens. Ten-million-token examples are marginal illustrations after free allocations, not forecasts.

| Route/model | Price/free evidence checked 2026-07-20 | Limits and dimensions | Batch/operations and caveats |
|---|---|---|---|
| **OpenAI API** — `text-embedding-3-small`, `text-embedding-3-large` | **$0.02/M**, **$0.13/M**; 10M examples **$0.20**, **$1.30**. No free tokens confirmed; a `Free` rate-limit class does not prove free usage. | **8,192 tokens**; default **1,536/3,072 dimensions**. Microsoft records up to **2,048 array inputs** for the family; direct and Azure limits may evolve separately. | `/v1/embeddings` Batch: 24-hour window, advertised **50% discount**, up to **50,000 inputs/batch**. Batch is for ingestion/re-index, not online lookup. No test occurred; direct limits require validation. Ordinary direct API use does not prove UK processing. |
| **Azure OpenAI / Microsoft Foundry** — `text-embedding-3-*` | Per 1,000 tokens; no stable numeric public meter captured. New-account **$200/30 days** is introductory credit. | **8,192 tokens**; **1,536/3,072** defaults; **2,048 array inputs**; **300,000 aggregate input-array tokens**; dimension parameter supported. | Quota depends on subscription, region, model, and deployment. Tier-0 Global Standard example for small: 1M tokens/min and 1,000 requests/10 s, not an account guarantee. Exact access, price, quota, and geography are unproved; no subscription/deployment/paid test occurred. |
| **Gemini Developer API** — `gemini-embedding-001`, `gemini-embedding-2` | Quota-limited free tier; paid **$0.15/M**, **$0.20/M**; batch **$0.075/M**, **$0.10/M**. Free excludes batch. | Input windows **2,048/8,192 tokens**; **128–3,072 dimensions**; 768/1,536/3,072 recommended for Embedding 2, recorded stable from **April 2026**. | RPM, TPM, and daily cap vary by model/tier/project and must be captured from AI Studio. UK availability does not prove UK-only processing or retention. |
| **Vertex AI Gemini Embedding** | Online **$0.15/M**; batch **$0.12/M**. Older non-Gemini routes: **$0.000025/1K characters online**, **$0.00002/1K batch**. | Google Cloud identity, quota, billing, and data controls differ from Gemini Developer API. | Character- and token-priced routes require measured corpus conversion; Developer API and Vertex are not interchangeable profiles. |
| **Amazon Bedrock** — `amazon.titan-embed-text-v2:0` | Official launch evidence **$0.00002/1K tokens = $0.02/M**; 10M example **$0.20**. Eligible post-July-2025 accounts: $100 plus possible additional $100; free account ends after six months or credit depletion. | Up to **8,192 tokens or 50,000 characters**; **1,024 default**, 512/256 dimensions; float/binary output; request/min throttling. `eu-west-2` availability listed; no Global/Geo inference listed. | On-demand/provisioned throughput. Launch price is not a captured London meter. Access, quota, quality, and compact-dimension effect are unproved. More than 100 languages are preview; AWS warns cross-language query/passage quality may be suboptimal. |
| **Cohere API** — Embed 4 | Free trial is evaluation/non-production, normally **1,000 API calls/month**. No numeric current serverless price captured. Dedicated Model Vault: Small **$4/hour or $2,500/month**; Medium **$5/hour or $3,250/month**. | **128K context**; up to **96 texts/images/request**; 256/512/1,024/1,536 dimensions, default 1,536; **2,000 inputs/minute**. | No comparable batch price captured. Dedicated rates are not serverless proxies. A dated dashboard price or quote is missing; multimodality is immaterial unless required and tested. |
| **Voyage AI** — Voyage 4 family | First **200M tokens** advertised free for lite/4/large; standard **$0.02/M, $0.06/M, $0.12/M**; 12-hour batch discount **33%**. Context/code models first 200M at $0.18/M; selected domain/code models first 50M at $0.12/M. Files API **$0.05/GB-month**, 30-day retention. | **32K context**; **1,024 default** dimensions with 256/512/2,048 options; up to **1,000 texts/request**. Aggregate limits: lite 1M tokens, 4 320K, large/code-3 120K. Quantised output available. | Basic: **2,000 RPM** and 16M/8M/3M TPM for lite/4/large. Payment method required for first paid tier even while free tokens remain. Introductory allocation does not establish privacy suitability; unused free tokens may be void after data-use opt-out. Files API would duplicate source content without an evidenced need. |
| **Mistral AI** — `mistral-embed`, `codestral-embed` | **$0.10/M**, **$0.15/M**; advertised batch discount **50%**. Limited Free mode exists but allowance is unquantified. | `mistral-embed` outputs **1,024 dimensions**. Array input and model-dependent dimensions/data types supported. | Limits are account-specific. Durable context and maximum input-array limits were not established. Documentation conflicts on payment activation before key use; do not attach a card or incur a charge solely to resolve research unless separately authorised. |
| **Jina AI** — v5 nano/small, v4 | New users **10M free tokens**. `/v1/models` metadata implies nano **$0.02/M**, small/v4 **$0.05/M**; this is inferred from machine-readable fields, not a prose price promise. | Nano: **8,192 context/768 dimensions**; Small: **32,768/1,024**; V4: **32,768/2,048**. Documentation published **29 June 2026**: Free **500 RPM/1M TPM/5 concurrency**; Tier 1 500 RPM/10M TPM/50; Tier 2 5,000 RPM/100M TPM/500. | Current metadata lists US processing. Dashboard/invoice confirmation is missing. Older page conflicts with 100 RPM, 100K TPM, concurrency 2, and non-commercial V4 terms. Newer docs/metadata are operational inputs, but API terms, downloaded-weight licence, and price remain to be confirmed. |
| **Hugging Face routed inference** | Free users **$0.10/month** credit; PRO **$2/month**; Team/Enterprise **$2/month per seat**; underlying-provider rate with no stated HF markup. | Context, dimensions, region, and behavior depend on selected model/provider; this is a routing/billing layer, not a model. | At $0.02/M, $0.10 illustrates about 5M tokens; at $0.10/M, about 1M. Availability, minimums, credits, and data terms are route-dependent. Custom keys may move billing/execution directly to the provider. |
| **Hugging Face dedicated endpoints** | Small CPU examples: AWS **$0.033/hour**, Azure **$0.060/hour**, Google Cloud **$0.050/hour**, billed per running minute. | User-selected model/hardware; suitable GPUs cost more. Scale-to-zero introduces cold starts. | Exact model, hardware, region, replicas, throughput, latency, and cold-start distribution require benchmark. Hourly infrastructure price does not establish token economics or capacity. |

## Embedding data terms and security boundaries

| Route | Evidence-backed data term | Configuration-dependent or unproved boundary |
|---|---|---|
| OpenAI API | Inputs/outputs are not used for training by default; abuse-monitoring retention up to 30 days; approval-based Modified Abuse Monitoring or Zero Data Retention exists. | Residency and regional processing depend on eligible account/endpoint; automatic UK residency is not proved. |
| Azure OpenAI | Microsoft states prompts, outputs, and embeddings are not exposed to OpenAI/other model providers or used for training without permission. | Regional, Data Zone, and Global processing differ; deployment region, abuse monitoring, access, and account settings require evidence. |
| Gemini Developer API | Free-tier content is used to improve products; paid-tier content is not. | UK service availability does not prove UK-only processing or retention. |
| Vertex AI | Separate Google Cloud identity, billing, quota, and data-control route. | Exact project controls and processing region were not captured. |
| Bedrock | AWS describes controls preventing durable request/response storage and states model providers do not receive prompts/completions. | Knowledge-base, logging, batch, and storage behavior must be checked separately from synchronous inference. |
| Cohere | Trial inputs/outputs may be used for research, development, and model improvement; trial guidance warns against personal data. Opt-out and approved zero-retention options exist for qualifying configurations. | Effective account settings and retention were not evidenced. |
| Voyage | Submitted content may be used for training by default. Administrator opt-out enables zero-day retention but requires a payment method and may void remaining free tokens; prior content retains its prior treatment. | No account-level opt-out evidence was captured. |
| Mistral | Free-plan data may be used for training with opt-out; Scale PAYG is stated not to use data for training. ZDR requires Scale, approval, and stateless APIs and excludes batch/files/stateful features. EU data centres are documented as default. | Effective account setting, subprocessors, endpoint, and ZDR approval were not captured. |
| Jina | Jina states API inputs/outputs are not used for training. | Current metadata lists US processing; UK residency is not evidenced. |
| Hugging Face | Routed inference inherits selected provider/model behavior; dedicated endpoints inherit selected cloud/model/configuration. | No generic route-level region, retention, or training assertion is valid without pinning the route. |

## Embedding provenance invariants

| Invariant | Required behavior |
|---|---|
| Credential isolation | Isolate credentials or use workload identity; never place credentials in logs, traces, or persisted job errors |
| Raw-content protection | Record usage counts, request IDs, retry information, and batch state only; do not log raw queries or document bodies |
| Provenance | Persist route, immutable model/version, dimensions, task/input type, normalisation, tokenizer/version, truncation policy, and generation ID |
| Generation isolation | Never combine query/chunk vectors from different models, dimensions, task modes, or incompatible normalisation |
| Validation | Reject non-finite vectors and unexpected dimensions before persistence |
| Truncation | Reject, explicitly permit, or explicitly record truncation; never silently inherit a provider default |
| Migration | Build a complete parallel generation, benchmark it, switch reads atomically, and retain rollback metadata that contains no deleted source content |
| Deletion | Delayed batches and stale jobs cannot recreate deleted documents |
| Local reproducibility | Contract and deletion tests use deterministic local embeddings without network access or account credit |

# Security, recovery, export, and deletion controls

| Boundary | Required evidence |
|---|---|
| Authentication | Exercise authenticated Streamable HTTP and upload staging through the actual deployed front door |
| Encryption | TLS for PostgreSQL, object storage, and embedding endpoints; record provider-side encryption settings used |
| Region/residency | Record compute, database, object, embedding, backup, and log locations; global placement is not evidence of residency |
| Sensitive content | Free/trial routes that may train on content receive synthetic/non-sensitive data only unless effective account controls are independently evidenced |
| Backup | Demonstrate restore; provider-native durability, snapshots, PITR, retention, or suspension do not by themselves prove recoverability |
| Off-provider recovery | Preserve provider-neutral source/database exports and deterministic index-regeneration inputs |
| Export | Ingress/import does not prove outbound export; test exact tested plan, API, extensions, functions, generated columns, vectors, metadata, and files |
| S3 conformance | Test only the actual adapter calls, including multipart and presigned operations where required |
| Deletion | `remove` eliminates vectors, chunks, metadata, jobs as appropriate, and source objects; retries, delayed batches, and reconciliation cannot resurrect content |
| Logs | Verify document bodies, raw queries, credentials, and protected metadata are absent; measure destination, retention, and cost |
| Portability | Export, redeploy on local Docker Compose, import, resume/retry outstanding jobs, and rebuild search without provider control-plane state |
| Spending | Record explicit experiment cap and whether thresholds alert, automate shutdown, block work, or still permit charges |

# Benchmark and cost model

## Required measurements

| Area | Metrics |
|---|---|
| Retrieval quality | Relevant-chunk and citation recall@K, MRR and/or NDCG, no-answer behavior, metadata-filter correctness |
| Retrieval performance | p50/p95 lookup latency; p50/p95/p99 query-embedding latency from intended application region |
| Indexing | Corpus/index size, build time, full rebuild/re-index time, memory, storage, and index parameters |
| Compute lifecycle | Cold-start distribution including registry pull and DB recovery, ingestion duration, API/worker retry behavior, request reconnect, scratch/memory bounds |
| Embedding operations | Synchronous query latency, batch ingestion throughput/turnaround, quota headroom, partial failures, and tokenisation/truncation results |
| Database lifecycle | Suspension/reactivation, pooled/unpooled reconnection, dump/export, restore, extension/index/function restoration, and post-restore query validation |
| Deletion/recovery | Complete `write → process → lookup → list → remove`, interruption retry, stale-job handling, fresh-local import, and complete rebuild |
| Cost | Full projected monthly total for declared workload and failure case, including all dependent-service categories |

```text
billable embedding tokens =
    initial corpus tokens
  + expected monthly new/changed document tokens
  + expected monthly query-embedding tokens
  + one full-corpus re-embedding reserve

total retrieval-platform cost =
    compute API and worker/jobs
  + ingress/front door/load balancer/public IP
  + registry and builds
  + PostgreSQL compute and storage
  + object storage and operations
  + embedding tokens or endpoint minimum
  + vector/search compute and storage
  + batch/file storage
  + backup and restore storage
  + network egress and cross-region traffic
  + logs, traces, and retained metrics
  + support, tax, currency, and regional uplift
```

| Cost rule | Required treatment |
|---|---|
| Free allocation | Show separately from steady-state cost |
| Introductory credit | Do not treat as a permanent discount |
| Batch | Use only for ingestion/re-index; online `lookup` remains synchronously priced and benchmarked |
| Character billing | Convert only from measured corpus character/token counts |
| Dedicated endpoints | Use measured throughput, uptime, replicas, and cold starts; hourly price alone is insufficient |
| Target quote | Requote exact account, region, SKU, architecture, quota, and date before any billed conclusion |
| Failure cost | Price retry loops, oversized documents, and stuck workers through configured execution ceilings |

# Evaluation, activation, pass, and stop gates

## Activation and continuation gates

| Gate | Pass evidence |
|---|---|
| Requirements | Corpus, traffic, account, region, retention, RPO/RTO, availability target, and budget are complete |
| Experimental data | Content is synthetic or separately approved; training use, retention, deletion, processing region, and logging settings are recorded |
| Free-SKU activation | Target account exposes the claimed SKU; claimed blocking behavior at exhaustion is demonstrated rather than inferred |
| Direct compatibility | OCI route preserves the portable API/worker shape, or architecture-changing adaptations are explicitly authorised |
| Baseline | PostgreSQL/`pgvector` baseline has measured quality, latency, index-build, restore, and rebuild results |
| Comparator | Dedicated search begins only after baseline measurement and uses the same corpus, embeddings, chunking, filters, and judgments |
| MCP contract | Local stdio and authenticated Streamable HTTP expose the same tools, structured errors, and embedding-failure semantics |
| Ingestion lifecycle | Durable job ID is returned before processing; client disconnect and immediate API shutdown do not prevent completion |
| Retry/idempotency | Worker termination, timeout, duplicate delivery, and partial failure produce one correct final lifecycle/index state |
| Streaming/reconnect | Sessions are tested across **240 s Azure**, **15 min Railway**, configured Cloud Run boundaries, and every unknown-limit front door |
| Scratch | Every supported format, including an unusually large document, remains within configured disk and memory bounds |
| Scale-from-zero | Registry pull, wake, PostgreSQL reconnection, and latency distribution are measured; not merely container start |
| Provider trigger | Cloudflare container behavior survives returned/failed/duplicate triggers; Fly.io worker wake is proved when work exists only in PostgreSQL |
| Embedding conformance | Limits, dimensions, Unicode, malformed/empty input, throttling, timeouts, deterministic errors, vector validation, and truncation pass |
| Embedding quality | Corpus-specific recall, rank, no-answer, latency, and storage effects are measured; no progression on price alone |
| Recovery | Backup/restore and fresh-local import succeed; complete index rebuild and post-restore queries pass |
| Portability | Provider-neutral export, local Docker Compose redeployment, outstanding-job recovery, and at least one alternative embedding adapter succeed |
| Deletion | Vectors, metadata, jobs as applicable, and source objects are removed; retries/batches cannot resurrect them |
| Cost | Current target-account/region/SKU price, complete charge categories, explicit cap, and hard-stop behavior are recorded |
| Split-search continuation | Measured retrieval quality, latency, or scale benefit justifies the added service and reconciliation/recovery burden |
| Decision boundary | Comparable results are delivered to the evidence owner; no provider activation or business choice follows automatically |

## Stop or reject conditions

| Condition | Required action |
|---|---|
| Local or attached provider storage is needed for correctness | Stop and restore `ObjectStore`/repository authority |
| Provider trigger is the only durable job record | Stop; commit PostgreSQL state before triggering |
| Client disconnect or API shutdown cancels committed ingestion | Stop; separate request and ingestion lifecycles |
| Retry/duplicate delivery creates duplicate or resurrected state | Stop; correct idempotency before cost/performance work |
| Request/body/stream limits remain unknown after synthetic ingress tests | Do not advance the runtime beyond experiment |
| Silent embedding truncation, wrong dimensions, non-finite vectors, or mixed generations occur | Stop and correct adapter/provenance handling |
| Online lookup depends on batch processing | Stop the route |
| Required training, retention, region, or deletion control is unavailable | Do not send approved internal content |
| Raw content, queries, credentials, or protected data enter telemetry | Stop and correct observability |
| Export, restore, complete rebuild, or deletion cannot be reproduced | Stop the route |
| Provider-specific control-plane identifiers/state become domain requirements | Stop and restore the portability boundary |
| Cost omits ingress, network, logs, registry, worker execution, storage, backup, or dependent services | Treat cost result as invalid |
| Budget alert is represented as a hard cap without destructive testing | Treat cost containment as unproved |
| Peak quota lacks evidenced headroom or retry budgets violate bounded latency | Stop progression |
| Dedicated search shows insufficient measured benefit over PostgreSQL | Stop split-search progression |
| App Runner remains unavailable to new customers | Do not activate a greenfield experiment |
| Mistral research would require attaching a card or incurring charges without separate authority | Do not proceed |

# Conflicts and unknowns requiring resolution

## Compute

| Issue | Evidence status and required resolution |
|---|---|
| Render HTTP limits | General request-duration and upload-body maxima were not found. Unknown does not mean unlimited; test the actual front door. |
| Fly.io HTTP limits | HTTP/2 and idle timeout are documented, but no provider-wide duration/body maximum was established; test actual ingress. |
| DigitalOcean scale-to-zero | Documentation is inconsistent: navigation/limits mention inactivity or Scale to Zero while release notes call it private preview. Treat as unknown/private preview and assume one running web instance until the target account proves otherwise. |
| Cloudflare trigger/container lifetime | Safe continuation after Worker return, failure, retry, or duplicate delivery is unproved; perform lifecycle interruption tests. |
| Cloudflare placement/residency | Regional placement, stored-data location, and database proximity remain unresolved. |
| ECS/Fargate ingress | Limits depend on selected ALB/proxy/application; capture the complete front-door configuration. |
| App Runner availability | Pricing remains online while availability documentation blocks new customers; availability governs activation. |
| Region/account availability | Feature eligibility, quotas, capacity, and exact shapes are unproved for every target account/region. |
| Cold starts | No real workload distribution is measured for any provider. |
| Cross-provider egress | Database, object-store, and embedding latency/cost remain unknown until concrete placements are chosen. |
| Observability cost | Log, trace, and metric volumes are unmeasured. |
| Spending controls | Most evidence establishes alerts or automation inputs, not guaranteed prevention of charges. Railway documents a stopping limit, but outage and uncovered-charge behavior remain unproved. |

## Database, search, and storage

| Issue | Evidence status and required resolution |
|---|---|
| Neon paid transfer | Official guide says 100 GB included then $0.10/GB; June 2026 announcement says 500 GB from 2026-06-01. Inspect target-account billing limits. |
| Aiven recovery | Free page says backups included; retention tables omit Free/Developer and say Hobbyist has no retained backup. Obtain exact behavior and prove restore or rely on tested external dumps. |
| Upstash hybrid eligibility | Pricing says sparse is “coming soon”; docs/changelog say hybrid arrived January 2025; older FAQ says unsupported. Verify target-account creation/query. |
| Upstash free count | Pricing labels one free database; FAQ says up to ten indexes. Confirm the enforced console/API limit. |
| Upstash recovery/replication | Reviewed evidence does not establish user-controlled export, backup, or replication. Perform provider-neutral export and clean rebuild. |
| Azure semantic ranking | Free guide says unsupported; tier table says it runs on Free but is unsuitable for large workloads. Test the exact SKU/API. |
| Azure Free retention | One guide says non-expiring; other pages allow deletion after prolonged inactivity under regional constraints. Maintain rebuild inputs and do not infer indefinite retention. |
| Cloudflare Vectorize Free access | April 2026/current introduction support Free; July 2026 Workers pricing says paid-only while showing Free allowances. Prove no-upgrade index creation/use in the target account. |
| Zilliz outbound portability | General export paths exist, but direct customer-storage backup export is Private Preview for Dedicated Enterprise. Demonstrate complete outbound export on the tested plan. |
| S3 compatibility | No store is proved compatible with every call; run adapter conformance on the exact selected API subset. |
| Azure Blob/GCS price | No dated regional amount was captured; obtain exact colocated-region prices before comparison. |

## Embeddings

| Issue | Evidence status and required resolution |
|---|---|
| Azure numeric price | No stable exact public amount captured. Obtain dated calculator/portal evidence for subscription, currency, region, model, and deployment type. |
| Azure access/quota/geography | Published examples are not account guarantees and no deployment occurred. Capture account availability, quota, region, and effective controls. |
| Cohere serverless price | No numeric PAYG Embed 4 price was captured. Obtain dated authenticated pricing or written quote; do not infer from Model Vault. |
| Gemini free quota | Project/model limits are console-dependent. Capture RPM, TPM, daily cap, and billing state. |
| Mistral free quota/key activation | Exact allowance and payment requirement conflict. Verify only with an authorised no-charge account. |
| Mistral batch limits | Context and array maxima were not established. Obtain model-specific documentation or test before choosing batch sizes. |
| Jina limits and V4 terms | Older product page conflicts with newer 29 June 2026 docs and metadata. Use newer operational limits, then confirm dashboard price, API terms, and downloaded-weight licence. |
| Jina price units | USD/M values are inferred from metadata fields, not confirmed billing terms. Capture dashboard or invoice-rate evidence. |
| Titan V2 regional price | Numeric evidence is an official launch publication, not a current London meter. Capture target-region SKU and access. |
| Hugging Face routed behavior | Cost, region, dimensions, retention, and training depend on the exact route. Pin provider/model/settings. |
| Hugging Face dedicated economics | Hourly examples do not establish throughput, latency, or token economics. Benchmark exact model/hardware/region/replicas. |
| Free/trial privacy controls | Plan names do not prove effective opt-out, ZDR, processing region, or retention. Record account-level settings and date before approved content. |
| Retrieval quality | No provider has corpus-specific quality evidence. Execute the controlled labelled benchmark. |
| Latency/resilience | No provider has measured p50/p95/p99, throughput, cold-start, or recovery evidence. Test from the intended application region. |

# Evidence outcome and explicit non-proofs

| Outcome | Evidence statement |
|---|---|
| Established comparison input | Conventional OCI services and run-to-completion jobs most directly match the API/worker shape; functions, isolates, and provider-specific container control planes require architectural changes. |
| Established comparison input | PostgreSQL/`pgvector` is the portable comparison baseline, not a selected database or deployment. |
| Established comparison input | Dedicated search adds network, reconciliation, deletion, backup, and rebuild obligations and requires measured benefit. |
| Established comparison input | Source-object storage remains necessary regardless of compute or search provider. |
| Established comparison input | No reviewed platform establishes a universally free, directly portable API plus reliable worker plus database, embeddings, object storage, networking, logs, and recovery stack. |
| Established comparison input | Local and attached provider filesystems are ephemeral, lifecycle-bound, or placement-constrained and cannot be authoritative. |
| Not proved | No provider is selected, deployed, provisioned, activated, technically accepted, approved, production-ready, affordable in a target region, compliant with residency requirements, or a Pegasus caller. |
| Not proved | No free tier is suitable beyond a controlled synthetic/non-sensitive experiment. |
| Not proved | Public allowance, credit, quota, capacity estimate, or headline price establishes target-account eligibility, sustained workload fit, total monthly cost, or hard cost containment. |
| Not proved | Absence of a documented request limit means unlimited upload or streaming. |
| Not proved | Scale-to-zero means reliable worker wake, acceptable cold starts, or successful database recovery. |
| Not proved | “Durable,” “managed,” retained, suspended, or non-expiring storage establishes backup, restore, or indefinite retention. |
| Not proved | Import/ingress capability establishes outbound export. |
| Not proved | Provider-native backup establishes off-provider recovery. |
| Not proved | Open-source availability eliminates managed-service migration risk. |
| Not proved | PostgreSQL portability guarantees extension, function, index, generated-column, restore, or performance equivalence. |
| Not proved | Any embedding route has superior retrieval quality, latency, reliability, data terms, or total cost for the controlled corpus. |
| Not proved | Any dedicated search service outperforms the PostgreSQL baseline enough to justify its additional complexity. |
| Required next action | After Pegasus.Core and authorised humans approve a bounded experiment, run one identical synthetic benchmark under the gates above and retain measurements, target-account settings, dated regional prices, exports, restore results, and deletion evidence in this single owner-controlled record. |
# Database, vector database and object-storage providers

Checked on: **2026-07-20**

Status: research input only. No hosted provider is selected, provisioned or approved by this
document.

## Scope and interpretation

This report compares services that could support the RAG pipeline's documents, chunks, ingestion
jobs, metadata, vectors and original files. It covers:

- portable managed PostgreSQL with `pgvector` and PostgreSQL full-text search;
- managed vector/search services that would sit behind a repository adapter;
- integrated document/search alternatives; and
- S3-compatible object stores for uploaded source files.

All prices below are vendor list prices in **USD**, before tax, unless stated otherwise. Regional
rates, marketplace billing, support, private networking, data transfer and currency conversion can
change the total. “Free” means an ongoing no-charge allowance only where the vendor says so; trial
credits are labelled separately. Dynamic calculators and console-only prices are not turned into
false precision.

The key architectural distinction is:

- **PostgreSQL option:** one portable transactional store can own document state, jobs, chunks,
  audit tombstones, full-text indexes and vectors. Original binaries still belong in object
  storage.
- **Dedicated vector/search option:** PostgreSQL or another transactional database is still needed
  for authoritative document/job/audit state. The vector service is a rebuildable search index,
  not the sole source of truth.

That distinction matters more than a small difference in vector-storage list price.

## Headline comparison

| Service | Ongoing free allowance | Lowest published paid entry or basis | Native hybrid search | Portability and principal risk |
|---|---|---|---|---|
| Neon Postgres | 0.5 GB and 100 CU-hours per project; 5 GB public egress | Launch: $0.106/CU-hour + $0.35/GB-month; vendor example is about $15/month for intermittent 1 GB | Yes, through PostgreSQL FTS + `pgvector` | Standard Postgres dump/restore and logical replication; current paid-egress documents conflict |
| Supabase Postgres | Two active projects; each has 500 MB database, 1 GB files and 5 GB uncached + 5 GB cached egress | Pro from $25/month, including $10 compute credit for one Micro instance | Yes, documented FTS + `pgvector` RRF pattern | Standard Postgres plus S3-compatible storage; free projects pause and lack automatic backups |
| Aiven for PostgreSQL | One free service per organisation; 1 CPU, 1 GB RAM, 1 GB disk | Developer $5/month; Hobbyist from $12/month | Yes, using PostgreSQL FTS + `pgvector` | Standard Postgres and broad paid-region choice; free region is assigned and backup wording is ambiguous |
| Qdrant Cloud | 0.5 vCPU, 1 GB RAM, 4 GB disk, one node | Standard is hourly resource-based; exact configuration price is calculator/console driven | Dense+sparse fusion, plus text filters | Open-source engine and migration tooling reduce exit risk; metadata/job/source stores remain separate |
| Pinecone | Starter: 2 GB, 1M RU and 2M WU per month, five indexes | Builder $20/month flat; Standard $50/month minimum | Dense+sparse and document full-text patterns | Proprietary API and Starter is AWS `us-east-1`; no backups on Starter or Builder |
| Weaviate Cloud | One free cluster: 100,000 objects, 1 GB memory, 10 GB disk | Flex starts at $45/month | Native BM25F + vector fusion | Open-source engine and standard backup formats help; free tier has one collection and no backups |
| Zilliz Cloud | One free cluster: 5 GB, 2.5M vCUs/month, five collections | Serverless $4 per million read or write vCUs, plus storage/transfer | BM25/sparse+dense with weighted or RRF fusion | Milvus compatibility helps, but outbound backup export is restricted and some managed features are proprietary |
| Upstash Vector | One free database is shown, with 1 GB total data and 10,000 operations/day | $0.40/100,000 operations + $0.25/GB-month; fixed $60/month option | Dense+sparse hybrid is documented | Proprietary service; official pricing and FAQ pages disagree on index count and sparse/hybrid availability |
| MongoDB Atlas | M0: 512 MB and up to 100 operations/second | Flex $0.011/hour, capped at $30/month; Dedicated from $0.08/hour | Native full-text + vector fusion | Changes the core data model from Postgres; Atlas Search lifecycle and costs remain service-specific |
| Azure AI Search | One 50 MB service per subscription | Dedicated is hourly per Search Unit; Serverless Developer preview is temporarily unbilled | Native BM25 + vector RRF | Search index is explicitly downstream and has no native backup; multiple current documentation conflicts require confirmation |
| Cloudflare Vectorize | **Conflicting official statements**: Vectorize says 30M queried and 5M stored dimensions/month on Free; Workers says paid-only | Workers Paid is $5/month, then $0.01/M queried dimensions and $0.05/100M stored dimensions above included usage | No native keyword-ranking path documented | Proprietary vector index, 1,536-dimension ceiling; do not count on free eligibility until confirmed in the target account |

## Managed PostgreSQL and `pgvector`

### Why PostgreSQL is the portability baseline

PostgreSQL supplies transactions, foreign keys, lifecycle state, deduplication constraints, full-text
search and standard export tools. `pgvector` adds exact search and HNSW/IVFFlat approximate indexes.
A hybrid query can run full-text and vector retrieval separately and fuse ranks in SQL. Supabase
publishes a concrete [hybrid FTS + `pgvector` RRF implementation](https://supabase.com/docs/guides/ai/hybrid-search);
the SQL pattern is portable to other PostgreSQL hosts that expose the required extension.

This does not make every managed host identical. Extension version, index build memory, connection
limits, maintenance policy and restore features still need conformance tests. It does mean the
schema and primary migration path remain standard PostgreSQL rather than a vendor-only vector API.

### Neon

Neon's current [pricing page](https://neon.com/pricing) describes an ongoing Free plan with no
credit card: 0.5 GB storage, 100 CU-hours per month **per project**, compute up to 2 CU (about 8 GB
RAM), scale-to-zero after five idle minutes, a six-hour/1 GB change-history restore window and
5 GB public network transfer. The page currently advertises up to 100 projects, but that unusually
large project count should be confirmed in the target organisation rather than treated as usable
production capacity.

The paid Launch plan is usage based at $0.106/CU-hour and $0.35/GB-month; Neon gives an indicative
$15/month example for an intermittent 1 GB workload. Scale is $0.222/CU-hour and $0.35/GB-month.
The same page lists `pg_vector`, autoscaling, connection pooling, read replicas and multi-AZ storage
as platform capabilities.

Material caveats:

- The Free plan stops the compute until the next cycle or upgrade if its 5 GB public-transfer
  allowance is exceeded, according to Neon's
  [network-transfer guide](https://neon.com/docs/introduction/network-transfer).
- Official paid-transfer information is not internally settled: that guide says paid plans include
  100 GB then charge $0.10/GB, while a June 2026
  [Neon announcement](https://neon.com/blog/more-data-transfer-on-paid-plans) says the allowance
  increased to 500 GB from 1 June 2026. Confirm the allowance in the actual billing console.
- Inactive branches can be archived and automatically rehydrated, adding a first-access delay;
  this is not deletion. Free compute scale-to-zero is also suspension, not a production uptime
  guarantee.
- The short Free restore window is useful for development but is not an off-provider backup.

Exit is comparatively straightforward: Neon documents `pg_dump`/`pg_restore`, `pgcopydb` and
logical-replication migration paths in its
[migration guide](https://neon.com/docs/import/migrate-intro). A provider-exit test should use the
unpooled connection and include the vector extension, indexes, functions and generated full-text
columns.

### Supabase

Supabase's [current pricing](https://supabase.com/pricing) gives each Free project:

- a 500 MB Postgres database on shared CPU/500 MB RAM;
- 1 GB of file storage;
- 5 GB uncached and 5 GB cached egress;
- no automatic database backup or PITR; and
- pause after one week of inactivity, with at most two active Free projects.

The database enters read-only mode above the 500 MB database quota even though the underlying disk
is 1 GB, as the [database-size guide](https://supabase.com/docs/guides/platform/database-size)
explains. Indexes, including HNSW, count towards that small database allowance.

Pro starts at $25/month. It includes $10 of compute credit (enough for one Micro instance), 8 GB
disk per project, 100 GB file storage, 250 GB uncached and 250 GB cached egress, and daily database
backups retained for seven days. Published overages include $0.125/GB for database disk,
$0.0213/GB-month for files, $0.09/GB uncached egress and $0.03/GB cached egress. PITR starts at an
additional $100/month for seven days.

Material caveats:

- Supabase explicitly recommends that Free users create their own `db dump` and off-site backup;
  see [Database Backups](https://supabase.com/docs/guides/platform/backups).
- A project paused for more than 90 days can no longer be restored in Studio. The current
  [recovery guide](https://supabase.com/docs/guides/troubleshooting/restore-project-after-90-days-pause)
  says a database backup and storage archive can be downloaded and moved into a new project, until
  the project itself is deleted.
- Supabase Storage is [S3 protocol compatible](https://supabase.com/docs/guides/storage/s3/compatibility),
  but does not implement S3 object versioning; deletion is permanent.
- Free auth/storage quotas are bundled with the database even if this application does not use
  Supabase Auth. That is convenient for a pilot, but application code should depend on the
  repository's own OIDC and object-store ports.

Supabase documents both PostgreSQL full-text search and the combined
[hybrid-search SQL pattern](https://supabase.com/docs/guides/ai/hybrid-search). Standard
`pg_dump`/`pg_restore` and the S3-compatible file interface provide credible exit paths, provided
database and files are exported together and object metadata is reconciled.

### Aiven for PostgreSQL

Aiven's [Free PostgreSQL tier](https://aiven.io/docs/products/postgresql/concepts/pg-free-tier) is
described as indefinite, without a credit card: a single node with 1 CPU, 1 GB RAM and 1 GB disk.
Only one free service of each type is allowed per organisation. There is no VPC, static IP,
integration, fork or connection pool, `max_connections` is 20, no SLA applies, and the user cannot
choose the cloud or region. Aiven may power off a never-used or inactive free service after warning;
it can be powered on again.

The [Aiven pricing page](https://aiven.io/pricing) lists Developer at $5/month with 1 CPU, 1 GB RAM
and 8 GB storage, and Hobbyist from $12/month. Startup begins at $75/month; Business, which adds a
standby, begins at $180/month. Actual paid price varies by cloud and region.

Aiven explicitly documents [pgvector support](https://aiven.io/docs/products/postgresql/howto/use-pgvector)
and standard [`pg_dump`/`pg_restore` migration](https://aiven.io/docs/products/postgresql/howto/migrate-pg-dump-restore).
PostgreSQL's built-in FTS remains available, so the portable hybrid SQL design applies.

There is a backup ambiguity to resolve before pilot use. The Free-tier page says “Backups” are
included, while the current
[backup-retention table](https://aiven.io/docs/products/postgresql/concepts/pg-backups) starts at
Hobbyist and gives Hobbyist no retained backup; it does not state the Free or Developer restore
window. The free service also cannot fork a restore. Treat recoverability as unproven and maintain a
tested external dump until Aiven confirms the exact plan behaviour.

### Other production PostgreSQL comparator

[Crunchy Bridge](https://www.crunchydata.com/products/crunchy-bridge) is worth retaining as a paid
production comparator because it is standard Postgres across AWS, Azure and GCP, includes
connection pooling, backup storage and network ingress/egress in its base usage price, and supports
cross-cloud recovery. It does not publish an ongoing free database on the reviewed product page,
and exact region/configuration pricing is calculator driven, so it is not a free-pilot candidate.

## Dedicated vector databases

### Qdrant Cloud

The ongoing Qdrant Free cluster is one non-dedicated node with 0.5 vCPU, 1 GB RAM and 4 GB disk,
without a card. Qdrant estimates roughly one million 768-dimensional vectors, although payload,
indexes, quantisation and replication change real capacity. Free clusters are suspended after one
week unused and **deleted after four weeks of inactivity** unless reactivated; see
[Create a Cluster](https://qdrant.tech/documentation/cloud/create-cluster/).

Free has manual snapshots/restores via API but no automatic backup or disaster recovery. Standard
adds dedicated resources, backup/DR, scaling and SLA; its
[billing model](https://qdrant.tech/documentation/cloud/pricing-payments/) is hourly CPU, memory,
disk, backup and optional inference usage rather than a published minimum monthly fee.

Qdrant supports dense and sparse named vectors, text filters and server-side RRF/DBSF
[hybrid queries](https://qdrant.tech/documentation/search/hybrid-queries/). It does not aim to
replace a full general-purpose text engine: its own
[FAQ](https://qdrant.tech/documentation/faq/qdrant-fundamentals/) distinguishes vector/sparse
fusion and text filtering from non-vector ranking functions and query analysers.

Portability is comparatively good for a dedicated service:

- the database engine is open source and self-hostable;
- snapshots can use S3-compatible storage, subject to minor-version restore compatibility; and
- the [Qdrant Migration Tool](https://qdrant.tech/documentation/migrate-to-qdrant/) streams
  Qdrant, Pinecone, Weaviate, Milvus, `pgvector` and other sources with resume support.

The application must still retain authoritative document metadata, ingestion jobs, source hashes
and audit tombstones in PostgreSQL, and original files in object storage.

### Pinecone

The current Starter plan is $0/month. Official
[database limits](https://docs.pinecone.io/reference/api/database-limits) provide 1 million read
units, 2 million write units and 5 million embedding tokens per model per month. Starter has at most
five serverless indexes and 2 GB total data, all in AWS `us-east-1`; current limits can be checked
against the [downgrade requirements](https://docs.pinecone.io/guides/organizations/manage-billing/downgrade-billing-plan).
When an allowance is reached operations are blocked rather than automatically billed.

Published plan entry points are documented in
[Understanding cost](https://docs.pinecone.io/guides/manage-cost/understanding-cost):
Builder is $20/month flat, Standard has a $50/month minimum and Enterprise has a $500/month
minimum. Moving from Starter to Standard means previously free indexes become billable immediately.
The Standard trial is separate: [21 days and $300 credit](https://docs.pinecone.io/guides/get-started/quickstart),
one per organisation.

Pinecone supports single-index dense+sparse search and separate-index/client-fusion designs. Its
[hybrid-search guide](https://docs.pinecone.io/guides/search/hybrid-search) warns that sparse/BM25
and dense scores require explicit normalisation/weighting. The newer document schema can combine
full-text fields and vector fields, but several related features remain constrained or previewed.

The largest operational caveat is recovery. Pinecone's
[backup documentation](https://docs.pinecone.io/guides/manage-data/backups-overview) says:

- backups are unavailable on Starter and Builder;
- Standard backups remain in the same project, cloud and region;
- records written within about 15 minutes may not be captured; and
- full-text/document-schema indexes are not supported by the current backup feature.

Bulk import from S3, GCS or Azure Blob uses Parquet and is useful for ingress, but
[import constraints](https://docs.pinecone.io/guides/index-data/import-data) are not an outbound
export mechanism. Keep a provider-neutral copy of chunk text, metadata and vectors (or the ability
to regenerate them) outside Pinecone.

### Weaviate Cloud

Weaviate's [pricing page](https://weaviate.io/pricing) now advertises one Free cluster per user,
free forever without a card:

- 100,000 objects;
- 1 GB memory and 10 GB disk;
- one collection and up to three tenants;
- 2,000 managed-embedding requests per day; and
- no backup, replication or SLA.

After seven days of inactivity the free cluster is suspended but its data is preserved, according
to [cluster lifecycle documentation](https://docs.weaviate.io/cloud/manage-clusters/create).
Free uses the cost-optimised HFresh profile; paid is needed for HNSW selection.

Flex begins at $45/month and adds replication, a 99.5% availability target and seven-day backups.
Published Flex usage starts at $0.00465 per million vector dimensions, $0.12/GiB storage and
$0.0264/GiB backup, subject to the $45 minimum. Current pricing marks data transfer as free only for
a promotional period.

Weaviate's [hybrid search](https://docs.weaviate.io/weaviate/concepts/search/hybrid-search) combines
BM25F and vector results with configurable relative-score or rank fusion and an `alpha` weight. The
engine is open source, self-hostable, and its
[backup format](https://docs.weaviate.io/deploy/configuration/backups) supports S3, GCS and Azure
backends plus restore between storage providers. That reduces engine lock-in, but the managed free
tier itself has no backup, so the application must retain a separate source of truth.

### Zilliz Cloud / Milvus

Zilliz provides both an ongoing Free cluster and separate trial credit. The
[Free cluster](https://docs.zilliz.com/docs/free-trials) gives one cluster per organisation,
5 GB capacity, 2.5 million vCUs each month and up to five collections; Zilliz estimates about one
million 768-dimensional vectors. Separately, a work-email sign-up can receive $100 credit for
30 days for one Serverless or Dedicated cluster. When that trial expires the paid clusters are
frozen/recycled and are permanently deleted after a further 30 days without a payment method. That
deletion policy applies to the credit-backed trial, not the ongoing Free cluster.

For Serverless, the current
[cost guide](https://docs.zilliz.com/docs/serverless-cluster-cost) prices both read and write usage
at $4 per million vCUs, plus storage, transfer, backup and optional audit logs. Each read has a
minimum six-vCU charge. Storage continues while a cluster is suspended and is region/type
dependent; a current example uses $0.025/GB-month for performance-optimised AWS `us-east-1`, but
this is an example rather than a universal rate. Free and Serverless limits are detailed in the
[limits page](https://docs.zilliz.com/docs/limits).

Milvus/Zilliz supports BM25 sparse fields, multiple dense/sparse vector paths and weighted or RRF
[hybrid ranking](https://docs.zilliz.com/docs/hybrid-search-rankers). Milvus is open source and the
managed service exposes Milvus-compatible SDKs, which lowers query-API lock-in.

Exit is less even than ingress. Zilliz provides import and migration from Milvus, PostgreSQL,
Pinecone, Qdrant and search engines, and its [data import/export index](https://docs.zilliz.com/docs/data-import-export)
documents export methods. However, direct export of backup files to customer object storage is
currently [Private Preview for Dedicated Enterprise](https://docs.zilliz.com/docs/export-backup-files).
Before selection, demonstrate a full outbound export using the exact intended plan, not merely
Zilliz-to-Zilliz migration.

### Upstash Vector

Upstash's current [Vector pricing](https://upstash.com/pricing/vector) shows:

- Free: $0, 10,000 query/update operations per day, 1 GB total data, 1,536 maximum dimensions,
  100 namespaces and 200 million vector-dimensions;
- Pay as You Go: $0.40 per 100,000 operations, $0.25/GB storage, no idle compute charge, 200 GB
  same-region bandwidth included then $0.03/GB; and
- Fixed: $60/month for up to one million operations per day, with the same $0.25/GB storage rate.

Upstash now documents [dense+sparse hybrid indexes](https://upstash.com/docs/vector/features/hybridindexes)
with server-side fusion and optional hosted BGE-M3/BM25 embeddings.

The first-party pages are inconsistent. The pricing comparison labels sparse vectors “coming
soon”, while the hybrid documentation and
[changelog](https://upstash.com/docs/vector/overall/changelog) say sparse/hybrid indexes arrived
in January 2025. The pricing page also labels the allowance “1 Free DB” while its FAQ on the same
page says up to ten indexes can be created for free. Treat both index count and hybrid eligibility
as console-confirmation items.

The reviewed official material did not establish a user-controlled backup/export format or
production replication behaviour. Upstash's older FAQ also says hybrid search and replication are
unsupported, contradicting the current hybrid documentation. This documentation drift makes a
tested export/rebuild drill mandatory and gives the service higher exit risk than an open-source
engine.

## Integrated document/search alternatives

### MongoDB Atlas Vector Search

Atlas M0 is an ongoing
[free cluster](https://www.mongodb.com/docs/atlas/tutorial/deploy-free-tier-cluster/) with one
cluster per project. Current [limits](https://www.mongodb.com/docs/atlas/manage-clusters/) are
512 MB total data plus indexes, shared compute and no backups. MongoDB's
[pricing page](https://www.mongodb.com/pricing) adds an indicated ceiling of 100 operations per
second. Free clusters automatically pause after 30 inactive days and can be resumed, but Search
indexes are rebuilt and unavailable for a period after resume; see
[pause behaviour](https://www.mongodb.com/docs/atlas/pause-terminate-cluster/).

Flex is $0.011/hour, up to $30/month, with 5 GB and daily snapshots. Dedicated starts at
$0.08/hour (about $56.94/month) before transfer or add-ons. Atlas supports AWS, GCP and Azure;
data-transfer cost varies by provider and region.

Atlas now supports native
[full-text + vector hybrid fusion](https://www.mongodb.com/docs/vector-search/hybrid-search/hybrid-search-overview/).
It is attractive if MongoDB is already the operational source. For this repository it would replace
the existing PostgreSQL domain persistence rather than simply swap a vector adapter, so it carries
a larger implementation and migration cost. Free has no backup, and Atlas Search/Vector Search
capacity, lifecycle and billing remain service-specific even where MongoDB data itself can be
dumped.

### Azure AI Search

Microsoft's [free-tier guide](https://learn.microsoft.com/azure/search/search-try-for-free) says one
Free service per subscription, always free, with 50 MB storage. The
[limits page](https://learn.microsoft.com/azure/search/search-limits-quotas-capacity) adds three
indexes, three indexers and three data sources; Free is shared, cannot scale, and may be deleted
after extended inactivity when a region is constrained. Managed identity, IP firewall, private
endpoint and availability zones are unavailable on Free.

Azure AI Search has two paid models:

- Dedicated is charged hourly per Search Unit (`replicas × partitions`) and cannot be paused; the
  service must be deleted to stop cost. Basic provides 15 GB per partition in most regions.
  Microsoft says a Basic service uses roughly one-third of a new-account $200/30-day credit, but
  actual regional rates are exposed through the Azure pricing calculator/portal rather than a
  stable global number.
- Serverless Developer is a consumption-priced public preview, currently unbilled during the
  initial preview. Microsoft explicitly says this temporary deferral will end after at least
  30 days' notice. It has no SLA, is limited to West Central US, Switzerland North and Japan East,
  and cannot migrate to or from other pricing models.

Native [hybrid search](https://learn.microsoft.com/azure/search/hybrid-search-overview) runs BM25
full-text and vector queries in parallel and merges them with RRF. Vector search itself has no
additional feature charge, but embeddings, semantic ranking and enrichment can add separate costs.

Two current Microsoft documentation conflicts should remain open:

- the free-tier guide says Free does **not** support semantic ranking, while the
  [tier feature table](https://learn.microsoft.com/azure/search/search-sku-tier#feature-availability-by-tier)
  says semantic ranker runs on Free but is unsuitable for large workloads;
- the same guide calls Free non-expiring, while service-limit and tier pages say prolonged
  inactivity can lead to deletion. “Non-expiring” must not be interpreted as retained indefinitely.

Azure explicitly treats an index as downstream data. There is
[no native index backup/restore](https://learn.microsoft.com/azure/search/search-faq-frequently-asked-questions#can-i-move,-backup,-and-restore-indexes);
official samples copy retrievable fields, but a deleted index/service cannot be recovered. The
authoritative database and object store must therefore be sufficient to rebuild the complete index.

### Cloudflare Vectorize

The April 2026 [Vectorize pricing page](https://developers.cloudflare.com/vectorize/platform/pricing/)
says Workers Free includes 30 million queried vector dimensions per month and five million stored
vector dimensions, and explicitly says the free tier will always permit prototyping. Workers Paid
includes 50 million queried and ten million stored dimensions, then charges $0.01 per million
queried dimensions and $0.05 per 100 million stored dimensions. Vectorize does not charge egress.

However, Cloudflare's newer July 2026
[Workers pricing page](https://developers.cloudflare.com/workers/platform/pricing/#vectorize) says
“Vectorize is currently only available on the Workers paid plan” while displaying both Free and
Paid allowances in the table. The current
[Vectorize introduction](https://developers.cloudflare.com/vectorize/get-started/intro/) also says
Free and Paid are supported. These are irreconcilable first-party statements. Do not model a $0
pilot until the target Cloudflare account can actually create and use the index without upgrading.

Other relevant limits are 1,536 dimensions, ten million vectors per index, 10 KiB metadata per
vector and at most ten metadata indexes; see
[Vectorize limits](https://developers.cloudflare.com/vectorize/platform/limits/). Vectorize provides
nearest-neighbour search, namespaces and metadata filters, but the reviewed product documentation
does not provide a native BM25/full-text ranking leg. Hybrid keyword/vector retrieval would need a
second Cloudflare service or application-side fusion, increasing both coupling and test scope.

## Object storage for original documents

The application's `ObjectStore` should use a deliberately small S3-compatible subset: bucket,
put/get/head/delete, multipart upload if needed, presigned upload/download, content hash/ETag,
server-side encryption controls and lifecycle rules. “S3 compatible” does not mean every S3
operation is implemented, so adapter conformance must test the exact calls used.

| Store | Ongoing free or trial | Paid list basis | Portability and caveats |
|---|---|---|---|
| Cloudflare R2 Standard | 10 GB-month, 1M Class A and 10M Class B operations each month | $0.015/GB-month; $4.50/M Class A; $0.36/M Class B; direct egress free | S3-compatible with documented omissions; attractive for a small portable pilot |
| Backblaze B2 | First 10 GB always free | $6.95/TB-month; mostly free transactions; egress free up to 3× average stored data then $0.01/GB | S3-compatible; no minimum duration, but account is tied to one region |
| Supabase Storage | 1 GB with each Free project | Pro includes 100 GB then $0.0213/GB-month; egress uses Supabase allowance/rates | S3-compatible but no object versioning; coupled billing/lifecycle with the Supabase project |
| Wasabi | 30-day trial, up to 1 TB; no ongoing free tier | $7.99/TB-month from July 2026, with a 1 TB monthly minimum | S3-compatible and no API/egress fee under policy, but 90-day minimum storage duration makes it poor for a tiny/churning corpus |
| Amazon S3 | New-account programme is credit based, not an ongoing storage allowance: up to $200, Free plan six months, credits expire within 12 months | Region, storage class, requests, retrieval and transfer are itemised | Canonical S3 behaviour and broadest integrations; price and egress require a region-specific estimate |

Evidence and material detail:

- R2 [pricing](https://developers.cloudflare.com/r2/pricing/) includes no egress charge and its
  [S3 compatibility matrix](https://developers.cloudflare.com/r2/api/s3/api/) explicitly lists
  implemented and missing operations.
- B2 [pricing](https://www.backblaze.com/cloud-storage/pricing) has no minimum storage duration and
  the first 10 GB free; its
  [S3 API reference](https://www.backblaze.com/docs/en/cloud-storage-call-the-s3-compatible-api)
  documents supported calls and Signature V4.
- Supabase's [storage pricing](https://supabase.com/docs/guides/storage/pricing) is bundled at the
  organisation plan level, not an isolated bucket bill.
- Wasabi's [July 2026 pricing notice](https://docs.wasabi.com/docs/may-2026-wasabi-pricing),
  [1 TB minimum](https://docs.wasabi.com/v1/docs/how-does-wasabis-monthly-minimum-storage-charge-work)
  and [30-day trial limit](https://docs.wasabi.com/docs/trial-data-limit) must all be included in
  comparisons; the headline per-TB rate alone is misleading.
- AWS changed its new-customer programme in July 2025; the current
  [S3 pricing page](https://aws.amazon.com/s3/pricing/) describes credits rather than a permanent
  free S3 allocation for new accounts.

Cloud-provider-native Azure Blob and Google Cloud Storage remain valid future adapters, particularly
when compute and search are colocated with them. They are not substitutes for testing the initial
portable S3 protocol adapter, and their regional operation/egress prices should be evaluated with a
declared deployment region.

## Decision framework

### 1. Fix the workload before comparing monthly totals

Record these inputs in the cost worksheet:

- source-file GB and monthly file ingress/egress;
- document count, average extracted characters and chunk count;
- vector count, dimensions and bytes of metadata per chunk;
- monthly writes/re-indexes, lookup queries, candidate count and `topK`;
- required FTS languages, filters and citation fields;
- database connection/concurrency pattern for API and worker;
- region and cross-region traffic;
- RPO/RTO, backup retention, restore-test frequency and availability target.

Provider unit models are not directly comparable: Neon charges CU-hours, Pinecone RU/WU, Zilliz
vCUs, Vectorize dimensions and provisioned services charge clock time. A monthly estimate without
the workload above is not meaningful.

### 2. Benchmark two architectural shapes

Use the same approved, non-sensitive labelled corpus:

1. **Portable single-database shape:** managed PostgreSQL + `pgvector` + PostgreSQL FTS +
   S3-compatible object storage.
2. **Split search shape:** transactional PostgreSQL + dedicated vector/search adapter +
   S3-compatible object storage.

Measure recall@k, MRR/NDCG, p50/p95 latency, index build/rebuild time, index size, connection
behaviour after idle suspension, and total projected monthly cost. The split shape must justify its
extra database, network, backup, deletion and reconciliation work with measured retrieval or scale
benefit.

### 3. Apply non-negotiable pilot gates

A candidate passes the free pilot gate only if:

- the target account shows the stated free SKU and a card/upgrade cannot silently create overage;
- no genuine case or personal data is used;
- region and data-processing terms are acceptable for the synthetic corpus;
- suspension and reactivation have been exercised;
- the full write → process → lookup → list → remove lifecycle passes;
- an external export exists and a fresh local environment can rebuild the index;
- deletion removes vectors, metadata and source objects and old jobs cannot resurrect them; and
- backup/restore behaviour is proven rather than inferred from “durable” storage.

### 4. Apply production gates separately

Free tiers are not production recommendations. Before any paid selection, document the target
account, provider, region, SKU, corpus estimate and hard monthly spending cap, then obtain explicit
approval. Verify:

- SLA/topology and maintenance behaviour;
- automated backup, PITR or scheduled snapshot retention and a restore drill;
- private networking/identity requirements;
- logs, metrics, audit events and support response;
- egress and cross-region disaster-recovery cost;
- extension/engine versions and upgrade policy; and
- a timed export/import migration to an alternative provider.

## Research outcome

The evidence supports PostgreSQL/`pgvector` as the **comparison baseline**, because it can satisfy
the repository's transactional and hybrid-retrieval needs without changing its public MCP
contracts. It does not select Neon, Supabase, Aiven or any other host.

Dedicated vector/search services remain valid benchmark candidates, but their headline free
allowances omit at least one required production concern: authoritative job/metadata state,
recoverable backups, source-object storage, region choice, or a demonstrated outbound export.
Cloudflare Vectorize, Azure AI Search and Upstash in particular require clarification of conflicting
official documentation before their free tiers can be treated as dependable.

No winner should be selected until the labelled retrieval benchmark, target-region price estimate,
restore test and provider-exit drill have all passed.

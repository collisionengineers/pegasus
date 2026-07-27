# Compute hosting providers

Checked: **20 July 2026**

## Purpose and scope

This report compares compute platforms capable of hosting the RAG pipeline's two runtime roles:

1. an authenticated, public Streamable HTTP MCP API; and
2. an asynchronous ingestion worker which extracts, chunks, embeds and indexes documents.

The intended deployment unit is a standard Linux OCI image. PostgreSQL, object storage and
embedding services are assessed separately; their prices are not included here. The local
filesystem is scratch space only.

This is research, not a provider selection. No account, region, SKU or spending commitment is
authorised by this document.

All prices below are public list prices in USD unless stated otherwise. Taxes, support, container
registry, build minutes, logs, metrics, public IPv4, load balancing, database, object storage and
network transfer can be separate charges. Prices and regional availability must be quoted again
for the target account and region before a pilot.

## Workload assumptions used for comparison

The comparison assumes:

- one Node.js 22 API container, initially with low and intermittent traffic;
- Streamable HTTP responses which may remain open for more than a conventional short API request;
- authenticated uploads staged directly into object storage rather than retained on container
  filesystems;
- a worker which can take longer than an HTTP request, must retry safely and must not be killed
  merely because the originating client disconnected;
- outbound TLS connections to PostgreSQL, an object store and potentially a hosted embedding API;
- no requirement for provider-specific service discovery, identity or queue SDKs in the core
  application;
- a preference for scale-to-zero during a non-sensitive prototype, but not at the expense of a
  reliable ingestion path; and
- production decisions based on measured traffic, ingestion duration, cold-start behaviour,
  retrieval latency and total cost rather than headline free allowances.

## Comparison framework

Each platform is assessed against the same questions.

| Area | Questions |
|---|---|
| Runtime fit | Can the unmodified OCI images run, and is Node.js 22 supported without a provider-specific execution model? |
| API fit | Does the front door support HTTP streaming, and what request, response and connection limits apply? |
| Worker fit | Is there a job, task or always-on worker model independent of the API request lifecycle? |
| Idle cost | Can the API and worker reach zero billable compute, and what wakes them? |
| Free status | Is the allowance ongoing, a time-limited credit, or unavailable to new customers? |
| Minimum paid shape | What is the smallest credible paid footprint before database and network costs? |
| Storage | Is local disk ephemeral, and can persistent storage be attached without harming portability? |
| Portability | How much deployment, queue, identity and networking logic is provider-specific? |
| Operations | What regional, quota, observability, billing and failure-mode work remains for a pilot? |

## Headline comparison

| Provider and product | Free or trial position | Idle and minimum paid position | Worker model | OCI portability | Principal issue to test |
|---|---|---|---|---|---|
| Google Cloud Run services and jobs | Ongoing request/CPU/memory allowances with an active billing account; separate new-account credit | Service can scale to zero; usage-priced above allowances | Cloud Run Jobs | High | Cold database connections, 60-minute request ceiling and regional eligibility |
| Azure Container Apps and jobs | Ongoing monthly Consumption allowance; separate Azure free-account credit | Consumption apps can scale to zero; usage-priced above allowances | Container Apps Jobs | High | 240-second ingress timeout and target-region retail price |
| AWS ECS on Fargate | No ongoing Fargate compute allowance; new-account credits are time-limited | Per-second task billing; an Express Mode API has at least one desired task | One-off or scheduled Fargate tasks; ECS services | High | Baseline ALB/network cost and operational complexity |
| AWS Lambda | Ongoing request and GB-second allowance | Scales to zero; usage-priced | Event invocation, maximum 15 minutes | Low for this service | Requires a Lambda adapter and cannot run a conventional long worker |
| AWS App Runner | Closed to new customers | Existing customers pay for provisioned memory even when idle | No distinct job product | Medium | Not available for a new deployment |
| Render | Free web service only; 750 free instance-hours per workspace | Free web sleeps; paid web and worker start at $7/month each | Paid background worker or cron job | High | Worker is not free; free cold start can approach one minute |
| Railway | $1/month resource allowance on Free; separate small trial credit | Optional Serverless sleep; Hobby minimum $5/month including usage | Ordinary service/container | High | Background traffic can prevent sleep; streaming requests are capped at 15 minutes |
| Fly.io | Very short new-account trial; no ongoing free allowance for new users | Machines can auto-stop; smallest quoted shared VM is about $2/month if always running | Separate process group or Machine | High | Worker wake-up orchestration and volume placement |
| Cloudflare Workers and Containers | Workers Free exists; Containers require Workers Paid, currently $5/month minimum | Containers can sleep; paid plan includes some container usage | Queue/Workflow/Worker-controlled container | Medium | Worker/Durable Object control plane creates material provider coupling |
| DigitalOcean App Platform | Free tier is for static sites, not API or worker compute | Web and worker components start at $5/month each | First-class worker or job | High | Scale to zero is documented as private preview, not a general assumption |

The table deliberately does not name a winner. A provider can be attractive for a nearly idle API
yet unsuitable for long ingestion, or operationally simple while having a higher fixed minimum.

## Google Cloud Run

### Product fit

[Cloud Run services](https://cloud.google.com/run/docs/overview/what-is-cloud-run) run standard
container images behind a managed HTTPS endpoint. A service can scale to zero. Separate
[Cloud Run Jobs](https://cloud.google.com/run/docs/create-jobs) run containers to completion,
making them the natural ingestion-worker shape rather than keeping work alive after an API
response.

The deployment remains reasonably portable: the image and application protocol are conventional,
while service/job definitions, IAM, secrets, scheduling and event delivery are Google-specific.

### Free and paid position

Cloud Run's [pricing page](https://cloud.google.com/run/pricing) currently documents two CPU
allocation models.

- Request-based billing includes 2 million requests, 180,000 vCPU-seconds and 360,000
  GiB-seconds per month across the billing account.
- Instance-based billing includes 240,000 vCPU-seconds and 450,000 GiB-seconds per month.
- The page also lists 1 GiB/month of free outbound transfer from North America.
- Usage is rounded to 100 milliseconds. Rates vary by region and CPU allocation model; the public
  page currently shows example instance-based rates of $0.000018 per vCPU-second and $0.000002 per
  GiB-second in its displayed base region.
- Jobs use the same resource-metering family. The actual task duration and parallelism therefore
  matter more than the number of submitted documents.

The allowance is an ongoing Google Cloud Free Tier, but it is not an anonymous or no-billing
service. Google's [Free Cloud Features documentation](https://cloud.google.com/free/docs/free-cloud-features)
says the project must be linked to an active billing account and usage above the allowance is
charged. A new account can separately receive $300 of trial credit for 90 days; that trial is not
the ongoing Cloud Run allowance and should not be treated as a production budget.

There is no fixed paid monthly minimum for a service at zero instances, but supporting services
can create one. For example, logging, a custom domain path, network egress, a VPC connector or a
database with its own minimum remain billable.

### API and worker constraints

Cloud Run's [quota and limit table](https://cloud.google.com/run/quotas) gives:

- a maximum service request duration of 60 minutes;
- a 32 MiB HTTP/1 request limit, while HTTP/2 server requests do not have that same limit;
- a 32 MiB non-streaming response limit, with streamed/chunked responses treated differently;
- up to 1,000 concurrent requests per instance, subject to CPU, memory and application limits; and
- job task timeouts up to 168 hours and up to 10,000 tasks per execution, subject to quota.

The [container runtime contract](https://cloud.google.com/run/docs/container-contract) describes
an in-memory, non-persistent writable filesystem. Files consume the instance's memory and disappear
with the instance. Originals therefore still belong in the `ObjectStore`; temporary parsing files
must be size-limited.

Streaming HTTP is supported, but any client session approaching 60 minutes needs reconnection and
idempotent recovery. The documented [request timeout](https://cloud.google.com/run/docs/configuring/request-timeout)
defaults to 5 minutes and must be explicitly raised where justified.

Cold starts are workload-dependent. The runtime contract allows up to four minutes for an instance
to start listening, although a well-built Node image should normally start much faster. A pilot
must measure cold start plus database connection establishment, not only container start time.

### Regions, transfer and operations

Cloud Run lists London (`europe-west2`) and several other European locations on its
[locations page](https://cloud.google.com/run/docs/locations). The current page also contains
eligibility wording around some locations, so the target account must confirm the exact service
and job availability rather than relying on the name appearing in a price table.

Outbound transfer is charged using Google Cloud network rates, with some same-region Google service
traffic exempt as described on the pricing page. Database and object-store placement therefore
affect total cost.

Pilot checks:

- verify Streamable HTTP over HTTP/2 through the chosen custom-domain and authentication path;
- test a request exceeding the default five-minute timeout and a client reconnect;
- prove that a submitted job survives API scale-down and client disconnect;
- test connection-pool recovery after scaling from zero;
- quote service, job, logging, registry and egress prices in the target region; and
- set budget alerts, while recognising that an alert is not by itself a hard spending stop.

## Microsoft Azure Container Apps

### Product fit

[Azure Container Apps](https://learn.microsoft.com/azure/container-apps/overview) runs container
applications and supports HTTP-driven scaling. [Container Apps Jobs](https://learn.microsoft.com/azure/container-apps/jobs)
supports manual, scheduled and event-driven work with configurable timeouts, retries, parallelism
and replica counts. That split maps cleanly to the API and ingestion roles.

The images and application remain portable, but Container Apps environment definitions, KEDA
scalers, managed identities, secrets, ingress and job triggering are Azure-specific deployment
concerns.

### Free and paid position

Azure's [Container Apps billing documentation](https://learn.microsoft.com/azure/container-apps/billing)
and [pricing page](https://azure.microsoft.com/pricing/details/container-apps/) give the Consumption
plan a monthly subscription-level free grant of:

- 180,000 vCPU-seconds;
- 360,000 GiB-seconds; and
- 2 million HTTP requests.

This is an ongoing service allowance, distinct from the Azure free account's time-limited $200
credit. A Consumption app at zero replicas has no active or idle replica resource charge. Jobs are
billed from replica start until completion.

The marketing pricing page renders regional values dynamically. The official
[Azure Retail Prices API](https://learn.microsoft.com/rest/api/cost-management/retail-prices/azure-retail-prices)
returned the following public Consumption prices on 20 July 2026:

| Region queried | Active vCPU | Active memory | Idle vCPU | Idle memory | Requests |
|---|---:|---:|---:|---:|---:|
| UK South | $0.000034/vCPU-second | $0.000004/GiB-second | $0.000004/vCPU-second | $0.000004/GiB-second | $0.40/million |
| West Europe | $0.000034/vCPU-second | $0.000004/GiB-second | $0.000004/vCPU-second | $0.000004/GiB-second | $0.56/million |

These are a dated retail snapshot, not a quote. The
[UK South API query](https://prices.azure.com/api/retail/prices?currencyCode=USD&%24filter=serviceName%20eq%20%27Azure%20Container%20Apps%27%20and%20armRegionName%20eq%20%27uksouth%27%20and%20skuName%20eq%20%27Standard%27%20and%20type%20eq%20%27Consumption%27)
and [West Europe API query](https://prices.azure.com/api/retail/prices?currencyCode=USD&%24filter=serviceName%20eq%20%27Azure%20Container%20Apps%27%20and%20armRegionName%20eq%20%27westeurope%27%20and%20skuName%20eq%20%27Standard%27%20and%20type%20eq%20%27Consumption%27)
show the underlying meters. Dedicated workload profiles, GPUs, networking, logs and supporting
services are separate.

### API and worker constraints

Container Apps [ingress](https://learn.microsoft.com/azure/container-apps/ingress-overview#protocol-types)
supports HTTP/1.1, HTTP/2, WebSocket and gRPC. The documented HTTP request timeout is 240 seconds.
That is materially shorter than Cloud Run's configurable maximum and Railway's 15-minute edge
limit. The MCP client and server must therefore tolerate reconnects, and long ingestion must return
a job identifier rather than hold the request open.

The jobs product is a strong fit for asynchronous ingestion. A manual execution can be triggered
after a durable job record is committed; a scheduled or event-driven execution can drain work
independently. Retry behaviour still needs application-level idempotency because platform retry
does not understand document lifecycle semantics.

Container Apps [storage mounts](https://learn.microsoft.com/azure/container-apps/storage-mounts)
provide replica-scoped ephemeral storage whose capacity depends on vCPU allocation and disappears
when the replica stops. Azure Files can be mounted persistently, but that is an Azure-specific
adapter and is not required for originals already stored through `ObjectStore`.

### Regions, transfer and operations

Container Apps is available in multiple European regions, including UK South and West Europe, but
feature, zone and workload-profile availability can differ by region. Network transfer, Log
Analytics or other telemetry destinations and private networking can dominate a small compute bill.

Pilot checks:

- run an MCP streaming/reconnect test across the 240-second boundary;
- test manual job execution, duplicate delivery, timeout and retry;
- confirm whether a queue scaler is needed or database-backed polling is sufficient;
- quote the exact Consumption meters and log-retention configuration in the approved region;
- measure scale-from-zero latency with the chosen registry and database; and
- configure budget alerts and deployment limits, without describing them as a guaranteed hard cap.

## Amazon Web Services

AWS has several superficially relevant products. They are not interchangeable, and App Runner's
current availability change materially alters older comparisons.

### ECS Express Mode and AWS Fargate

[ECS Express Mode](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/express-service-overview.html)
is AWS's current simplified path to a web container. It creates an ECS service on Fargate plus an
Application Load Balancer, IAM, networking and observability resources. There is no separate
Express Mode fee, but each underlying resource is billed.

The [Express Mode defaults and limits](https://docs.aws.amazon.com/AmazonECS/latest/developerguide/express-service-work.html)
include a 1 vCPU/2 GiB task, one desired task at the low end and up to 20 tasks. Consequently, an
Express Mode API is not a scale-to-zero proposition. A conventional ECS design can run independent
Fargate tasks to completion for ingestion and keep an ECS service for the API.

[Fargate pricing](https://aws.amazon.com/fargate/pricing/) is per requested vCPU, memory, operating
system/architecture and additional ephemeral storage, from image-pull start until task termination.
Linux tasks are billed per second with a one-minute minimum and include 20 GiB of ephemeral
storage. The official page's US East Linux/x86 example rates are approximately:

- $0.000011244 per vCPU-second, or $0.04048 per vCPU-hour;
- $0.000001235 per GB-second, or $0.004446 per GB-hour; and
- $0.0000000308 per additional GB-second of ephemeral storage above the included 20 GiB.

Rates differ by region and architecture. An always-on 1 vCPU/2 GiB task at those example rates is
roughly $35.55 for a 720-hour month before the Application Load Balancer, public IPv4, logs, image
registry and transfer. This is an illustration, not a target-region quote.

Fargate does not have an ongoing compute free tier. AWS's current
[Free Tier plans](https://docs.aws.amazon.com/awsaccountbilling/latest/aboutv2/free-tier-plans.html)
offer eligible new customers an initial $100 of credit, with up to a further $100 available through
activities. The free plan lasts at most six months or until credits are exhausted. This is a trial
budget, not free Fargate capacity. The
[Free Tier FAQ](https://docs.aws.amazon.com/awsaccountbilling/latest/aboutv2/free-tier-FAQ.html)
should be checked for the target account's eligibility and payment method.

Fargate is a high-portability runtime for both images. There is no Fargate-specific HTTP request
timeout for a running task; limits instead come from the Application Load Balancer, reverse proxy
and application. The chosen front door's idle timeout, upload size and streaming behaviour must be
configured and tested. ECS gives substantial control but also requires the most networking,
load-balancer, task-definition, autoscaling and IAM operation in this comparison.

### AWS Lambda

[Lambda pricing](https://aws.amazon.com/lambda/pricing/) has an ongoing allowance of 1 million
requests and 400,000 GB-seconds per month. The public x86 example rate above the allowance is
$0.20 per million requests and $0.0000166667 per GB-second for the first duration tier. Lambda
scales to zero and can be effective for short event-driven extraction steps.

It is not a direct host for this repository's standard server and worker processes. A container
image is a packaging format for a Lambda handler, not an arbitrary long-running container. Lambda's
[quota documentation](https://docs.aws.amazon.com/lambda/latest/dg/gettingstarted-limits.html) and
[timeout configuration](https://docs.aws.amazon.com/lambda/latest/dg/configuration-timeout.html)
limit an invocation to 15 minutes. The API would require an HTTP/Lambda adapter and ingestion would
need to be bounded or split into resumable events.

[Lambda response streaming](https://docs.aws.amazon.com/lambda/latest/dg/configuration-response-streaming.html)
supports responses up to 200 MiB, with bandwidth throttled after the first 6 MiB, but availability
and the HTTP front door must be confirmed per region. Lambda is therefore an architecture-changing
alternative to benchmark only if its low idle cost outweighs the extra adapter and workflow.

### AWS App Runner is not a new-customer option

Older provider comparisons often recommend App Runner. AWS now states that
[App Runner is unavailable to new customers](https://docs.aws.amazon.com/apprunner/latest/dg/apprunner-availability-change.html)
and will receive no new features. Existing customers can continue to use it, and AWS directs new
customers towards ECS Express Mode.

The [App Runner pricing page](https://aws.amazon.com/apprunner/pricing/) remains online and still
shows provisioned-memory and active-CPU pricing. Its presence must not be mistaken for new-account
availability. App Runner should be excluded from a greenfield shortlist unless AWS changes that
published restriction.

### AWS pilot implications

Pilot checks for an ECS/Fargate design:

- quote Fargate, Application Load Balancer, NAT/public IPv4, ECR, logs and transfer together;
- compare an always-on API service with a deliberately orchestrated zero-task architecture;
- prove task retry and idempotency for worker failures;
- test Streamable HTTP through the actual load balancer, including its idle timeout;
- keep AWS event and IAM wiring outside the core application; and
- treat AWS Budgets as notification/automation input, not an assumed absolute billing ceiling.

## Render

### Free and paid position

Render's [pricing page](https://render.com/pricing) provides a free 512 MiB/0.1 CPU web-service
instance and paid web, private-service and background-worker instances from $7/month for
512 MiB/0.5 CPU. Paid instances are prorated by the second. A Hobby workspace is $0/month plus
resource usage.

The [free-instance documentation](https://render.com/docs/free) states:

- 750 free instance-hours per workspace per calendar month;
- only web services, not background workers or private services, receive free compute;
- a free web service spins down after 15 minutes without inbound HTTP or WebSocket traffic;
- waking can take about one minute;
- the filesystem is ephemeral and persistent disks are unavailable on free services;
- free services do not include one-off jobs, scaling, private networking or SSH; and
- a workspace without a payment method is suspended rather than charged when certain free limits
  are reached.

The free API can therefore support synthetic exploration, but it cannot also supply a reliable free
background worker. A paid starter API plus worker has a $14/month compute floor before database,
disk and bandwidth.

The pricing page includes 5 GB/month of outbound bandwidth on Hobby, followed by $0.15/GB, and
500 build minutes followed by $5 per 1,000 minutes. A persistent disk is $0.25/GB-month. The exact
allowance and workspace plan must be rechecked because workspace and service charges interact.

### Runtime, API and worker fit

Render [web services](https://render.com/docs/web-services) can run a prebuilt Docker image and
expose a conventional public service. Background workers are a first-class paid service. This
preserves the application's runtime model and makes operations simpler than assembling ECS
components.

Render supports HTTP/2 and [WebSocket connections](https://render.com/docs/websocket). Its
WebSocket page does not set a fixed maximum connection duration but warns that connections close
when an instance is replaced. No clear general HTTP request-duration or upload-body maximum was
located in the official documentation reviewed for this report. Those limits are **unknown** and
must be tested with the current Streamable HTTP implementation before shortlisting.

Paid disks can persist files, but originals should still use `ObjectStore`. A disk-bound service
also has scaling and placement implications. A worker that polls a database remains continuously
billable; scheduled or queue-driven jobs can be cheaper if the workload is bursty.

### Regions and operations

Render currently lists Oregon, Ohio, Virginia, Frankfurt and Singapore on its
[regions page](https://render.com/docs/regions). Frankfurt is the available European choice listed;
there is no UK region on that page.

Pilot checks:

- confirm HTTP streaming, request duration and upload limits with support or a synthetic test;
- measure the one-minute-class free cold start and database reconnection;
- price a paid background worker, not only the free API;
- test replacement of an instance while a client stream is open;
- verify private networking and region compatibility with the selected database; and
- confirm whether the account exposes a genuine hard spending limit before relying on one.

## Railway

### Free and paid position

Railway's [plan documentation](https://docs.railway.com/pricing/plans) lists:

- Free at $0/month with $1/month of included resource usage;
- Hobby at $5/month with $5 of included resource usage; and
- Pro at $20/month with $20 of included usage.

A new-account trial can separately provide $5 of one-off credit. It is not the recurring Free
allowance. Railway documents card payment for paid subscriptions; eligibility and verification
status can alter trial resource limits.

The [resource pricing page](https://docs.railway.com/pricing) currently lists approximately:

- $10 per GB-month of memory;
- $20 per vCPU-month;
- $0.05 per GB of egress;
- $0.15 per GB-month of volume storage; and
- compute metered by the minute.

Free plan limits include one replica, 0.5 GB memory, 1 vCPU, 1 GB ephemeral storage and a 0.5 GB
volume. Railway warns in its [deployment reference](https://docs.railway.com/deployments/reference)
that free deployments have lower priority and may be suspended when paid demand consumes capacity.
That is suitable for a disposable prototype, not an availability promise.

Railway provides a [hard usage limit](https://docs.railway.com/pricing/cost-control) which can stop
workloads when reached. This is one of the clearer documented spending controls in this set, but
the resulting outage and any non-covered charges still need to be understood before use.

### Scale-to-zero, API and worker fit

Railway's optional [Serverless mode](https://docs.railway.com/deployments/serverless) sleeps a
service after more than ten minutes without outbound network traffic and wakes it on incoming
traffic. A database pool, telemetry exporter, queue poll or any other outbound traffic can prevent
sleep. Wake-up includes a cold-boot delay, and Railway says serverless workloads are lower priority
and may rarely be rebuilt.

This has two consequences:

- the API can sleep only if its background connections become genuinely quiet; and
- a conventional polling worker is expected to remain active and billable.

Railway runs conventional containers, so the API and worker require little application adaptation.
Its [public-network specifications](https://docs.railway.com/networking/public-networking/specs-and-limits)
support HTTP/1.1 and WebSockets, with a 15-minute maximum HTTP request, 60-second keep-alive,
32 KiB header limit and documented connection/RPS limits. Railway's
[SSE and WebSocket guidance](https://docs.railway.com/guides/sse-vs-websockets) says both streaming
forms have a 15-minute maximum and should reconnect.

### Volumes, regions and operations

Railway [volumes](https://docs.railway.com/volumes/reference) attach to a service but restrict it to
one replica and introduce deployment constraints. Originals should therefore use external object
storage; a volume is at most scratch/cache or a small operational aid.

Railway currently lists California, Virginia, Amsterdam and Singapore on its
[regions page](https://docs.railway.com/deployments/regions). Amsterdam is the European option.

Pilot checks:

- verify the MCP client's reconnect behaviour at the 15-minute edge cutoff;
- observe whether PostgreSQL pooling and telemetry prevent API sleep;
- compare an always-on worker with an externally triggered short-lived service;
- test free-plan pre-emption and recovery using synthetic data;
- set and deliberately validate a low hard usage limit in a disposable account; and
- price egress between the Amsterdam service, database, object store and embedding endpoint.

## Fly.io

### Free and paid position

Fly.io's [free trial](https://fly.io/docs/about/free-trial/) is deliberately short: two total VM
hours or seven days, whichever comes first. Trial Machines auto-stop after five minutes. Adding a
payment method ends the trial and begins usage billing. New accounts do **not** receive the legacy
ongoing free VM allowances sometimes quoted in older comparisons.

Fly's [pricing page](https://fly.io/docs/about/pricing/) says normal organisations require a card
and are billed for actual resources. The displayed smallest `shared-cpu-1x` example in the default
region is:

- 256 MiB at $0.00000078/second, $0.0028/hour or about $2.02 for 30 days;
- 512 MiB at about $3.32 for 30 days; and
- 1 GiB at about $5.92 for 30 days.

These are example list prices and vary by region. Stopped Machines cease CPU and RAM charges but
retain root-filesystem charges, currently $0.15/GB per 30 days. Persistent volumes are
$0.15/GB-month and snapshots $0.08/GB-month with the first 10 GB of snapshots free. The page
currently lists outbound transfer from Europe and North America at $0.02/GB and private
cross-region transfer at $0.006/GB.

### Scale-to-zero, API and worker fit

Fly Proxy can [automatically stop and start Machines](https://fly.io/docs/launch/autostop-autostart/).
New applications default to automatic start/stop and can be configured with zero minimum running
Machines. An inbound request starts an existing stopped Machine; the proxy does not create a new
one. The wake delay depends on image and application startup.

Fly runs standard OCI images and supports separate process groups or Machines for API and worker.
Its [long-running task blueprint](https://fly.io/docs/blueprints/long-running-tasks) recommends
splitting web and worker processes. A worker with no public HTTP traffic will not automatically wake
merely because work appeared in PostgreSQL. It needs an always-on Machine, a private Flycast
service, scheduled Machine start, queue integration or explicit API/control-plane orchestration.
That wake path is the main design question for a bursty ingestion workload.

Fly services support TCP, HTTP and HTTP/2 routing. No provider-wide request-duration or body-size
maximum was found in the official documentation reviewed here. Fly configuration exposes an
[HTTP idle-timeout setting](https://fly.io/docs/reference/configuration/) and gives configuration
examples, but this is not evidence of an unlimited Streamable HTTP session. Limits are therefore
**unknown** until tested through Fly Proxy with the intended upload and streaming pattern.

### Storage, regions and operations

[Fly Volumes](https://fly.io/docs/volumes/overview) are local to one physical server, belong to one
Machine and are not automatically replicated. Daily snapshots are not a substitute for the
application's backup design. Root filesystems are ephemeral. The portable `ObjectStore` remains the
correct place for source documents.

Fly advertises a broad region set, including London, Amsterdam and Frankfurt, on its pricing and
region documentation. Capacity can differ by Machine size and region.

Pilot checks:

- prove zero-Machine wake-up and measure the first MCP request latency;
- choose and test an explicit worker wake mechanism;
- establish current request, response and streaming limits with a synthetic load;
- test a Machine restart while a client is streaming;
- keep volume-local state out of ingestion correctness; and
- quote Machine, root filesystem, volumes, snapshots and inter-provider egress in the target region.

## Cloudflare Workers and Containers

Cloudflare offers two related but architecturally different runtimes. Workers alone is not an OCI
host. Containers can run images, but a Worker and Durable Object control their lifecycle.

### Workers free and paid position

[Workers pricing](https://developers.cloudflare.com/workers/platform/pricing/) currently provides:

- Free: 100,000 requests per day and 10 milliseconds of CPU time per invocation;
- Paid: a $5/month minimum including 10 million requests and 30 million CPU milliseconds; and
- overage at $0.30 per million requests and $0.02 per million CPU milliseconds.

Cloudflare says Workers does not charge egress. The Free allowance is useful for a lightweight
edge adapter, not for document parsing or embedding orchestration that consumes meaningful CPU and
memory.

The [Workers limits](https://developers.cloudflare.com/workers/platform/limits/) include 128 MiB
memory, a 3 MiB compressed script on Free and 10 MiB on Paid. Paid requests can use up to five
minutes of CPU time, although the default is lower; an HTTP request has no fixed wall-clock
duration while the client remains connected. Cron and Queue invocations have a 15-minute wall
limit. Request bodies are capped by the Cloudflare account plan, currently 100 MB on Free/Pro,
200 MB on Business and 500 MB on Enterprise.

Workers is a JavaScript isolate with a Node compatibility layer, not the repository's normal
Node/OCI runtime. Hosting the whole service there would require an explicit port and parser
compatibility review.

### Containers position

[Cloudflare Containers](https://developers.cloudflare.com/containers/) run Linux container images
but are available only on Workers Paid. The Worker calls and manages a container instance through a
Durable Object-style control plane.

The [Containers pricing page](https://developers.cloudflare.com/containers/pricing/) currently
includes, within the $5 Workers Paid subscription:

- 25 GiB-hours of memory;
- 375 vCPU-minutes; and
- 200 GB-hours of disk.

Overage is listed at $0.0000025 per GiB-second, $0.000020 per vCPU-second and $0.00000007 per
GB-second of disk. Europe/North America Internet egress is shown at $0.025/GB with 1 TB included.
Worker requests, Durable Object operations and logs can add separate charges. Containers can sleep
and cease active compute, but their provisioned transient disk and control-plane calls remain part
of the cost model.

The smallest current instance type provides a fractional vCPU, 256 MiB memory and 2 GB disk.
Container [image size is constrained by the selected instance disk](https://developers.cloudflare.com/containers/platform-details/image-management/).
The disk follows container lifecycle and must not hold the source of truth.

### Portability and worker implications

The image itself is portable, but the hosting model is less so:

- a Worker must route, authorise and start the container;
- Durable Object identity and lifecycle influence instance placement;
- Queues, Workflows or another Worker invocation are the natural job triggers; and
- conventional independent database polling does not take advantage of sleep and may not match the
  intended lifecycle.

A queue consumer's 15-minute wall limit also applies to the orchestration step even if the container
can continue according to its own lifecycle. The exact semantics for a job outliving its triggering
Worker need a failure test, not an assumption.

Cloudflare markets the platform as globally placed rather than offering a conventional selected
single compute region. That can reduce edge latency but complicates strict data residency and
database proximity. Regional placement controls and the location of stored document data must be
reviewed for the selected account.

Pilot checks:

- decide whether the Worker/Container/Durable Object coupling is acceptable under the portability
  requirement before doing performance work;
- test Streamable HTTP, authentication and request-body limits through the Worker front door;
- prove the ingestion container's lifecycle after its trigger returns, retries or is duplicated;
- measure latency to the selected regional database;
- quote Workers, Containers, Durable Objects, Queues, logs and egress together; and
- do not describe the $5 plan as free container hosting.

## DigitalOcean App Platform

### Free and paid position

DigitalOcean's [App Platform pricing](https://docs.digitalocean.com/products/app-platform/details/pricing/)
has a free tier for static sites. Dynamic web services and workers are paid. The smallest listed
web-service and worker size is currently $5/month for 1 shared vCPU and 512 MiB, with 50 GiB of
outbound transfer. Components are billed per second with a one-minute minimum.

A continuously deployed API plus worker therefore starts at roughly $10/month before database,
object storage and other services. Additional outbound transfer is listed at $0.02/GiB. Jobs run
and bill only for their execution, which can reduce the worker cost when ingestion is genuinely
bursty.

### Runtime, API and worker fit

App Platform supports Linux AMD64 container images and has first-class web-service, worker and job
components. This is a direct fit for the two repository images with comparatively little
application adaptation.

The [platform limits](https://docs.digitalocean.com/products/app-platform/details/limits/) document:

- 4 GiB of non-persistent local filesystem per container;
- no persistent-volume attachment for App Platform components;
- a 600-second upload timeout;
- deployment-job timeouts defaulting to 30 minutes and configurable within documented bounds; and
- HTTP-request autoscaling for web services, but not workers.

The official documentation references an inactivity or Scale-to-Zero feature in some limits and
navigation. However, DigitalOcean's [release notes](https://docs.digitalocean.com/release-notes/)
describe App Platform Scale to Zero as a private preview. The evidence is internally inconsistent
enough that general access must be treated as **unknown/private preview**, not a production
capability. Standard cost estimates should assume at least one running web instance.

No App Platform persistent disk means originals must use `ObjectStore`, which aligns with the
portable design. Large parser images should also be checked against the documented image-size and
deployment constraints.

### Regions and operations

DigitalOcean lists App Platform availability across several datacentre regions on its
[availability page](https://docs.digitalocean.com/products/app-platform/details/availability/),
including London, Frankfurt and Amsterdam facilities for parts of its portfolio. Exact component,
feature and size availability must be verified in the chosen region.

Pilot checks:

- confirm that both dynamic web and worker components are available in the target European region;
- assume no general scale to zero unless the target account proves otherwise;
- test Streamable HTTP across platform ingress and the 600-second upload boundary;
- compare an always-on worker with per-execution jobs;
- validate image size and temporary extraction within the 4 GiB filesystem; and
- quote component, build, log and transfer costs together.

## Cross-provider technical constraints

### API request and streaming limits

| Platform | Documented edge/runtime constraint relevant to MCP |
|---|---|
| Cloud Run | Maximum request 60 minutes; HTTP/1 request 32 MiB; HTTP/2 server requests avoid that HTTP/1 limit |
| Azure Container Apps | HTTP request timeout 240 seconds; HTTP/1.1, HTTP/2, WebSocket and gRPC supported |
| ECS/Fargate | Container has no intrinsic request timeout; selected load balancer/proxy configuration governs it |
| Lambda | 15-minute invocation; special response-streaming API and front-door constraints |
| Render | HTTP/2 and WebSockets documented; general request-duration/body limit not established |
| Railway | HTTP request and SSE/WebSocket session maximum 15 minutes |
| Fly.io | HTTP/2 and configurable idle timeout; general request-duration/body limit not established |
| Cloudflare | Workers HTTP wall time tied to client connection, but CPU and plan body limits apply |
| DigitalOcean | 600-second upload timeout; broader Streamable HTTP limit requires a pilot |

Unknown means no sufficiently clear first-party limit was found during this review. It does not
mean unlimited.

The upload endpoint should remain a short, authenticated staging operation. Large objects are
better uploaded directly to object storage with a short-lived reference, as already proposed, than
proxied through any of these compute front doors.

### Worker execution choices

| Worker pattern | Best-supported products in this comparison | Cost/portability observation |
|---|---|---|
| Run-to-completion job | Cloud Run Jobs, Container Apps Jobs, Fargate tasks, DigitalOcean jobs | Strong match for bursty ingestion; trigger and retry wiring is provider-specific |
| Always-on queue consumer | ECS/Fargate, Render worker, Railway service, Fly Machine, DigitalOcean worker | Simplest application flow; creates a fixed or continuously metered baseline |
| Serverless function | Lambda, Cloudflare Workers/Queues | Lowest idle cost; 15-minute ceilings and provider execution models can force workflow changes |
| HTTP-triggered sleeping container | Cloud Run service, Container Apps app, Railway Serverless, Fly Machine, Cloudflare Container | Good for API; worker wake-up and duplicate delivery need an explicit durable design |

The portable domain should record jobs in PostgreSQL before triggering provider execution. A
provider trigger is then an optimisation for waking workers, not the sole record that work exists.
This preserves retries and migration even when a queue adapter changes.

### Local storage is not a selection criterion

Every candidate provides ephemeral or lifecycle-bound local storage. Even products with attachable
volumes impose placement, scaling or replication constraints. The source file and durable job state
must remain behind `ObjectStore` and `DocumentRepository`; local disk is only bounded scratch
space. This removes a false advantage from platforms advertising a cheap disk.

## Shortlisting without selecting a provider

The next phase should use gates rather than a single weighted score.

### Gate 1: direct runtime compatibility

Retain for a direct OCI benchmark:

- Cloud Run services plus Jobs;
- Azure Container Apps plus Jobs;
- ECS/Fargate tasks and service;
- Render web plus background worker;
- Railway services;
- Fly.io Machines/process groups; and
- DigitalOcean App Platform web plus worker/job.

Treat as architecture-changing experiments:

- Lambda, because it requires a handler/front-door adapter and bounded invocations; and
- Cloudflare Workers/Containers, because a Worker and Durable Object control the container.

Exclude App Runner for a greenfield deployment while AWS's new-customer restriction remains.

This grouping is about technical comparability, not preference.

### Gate 2: prototype cost truth

For each retained platform, calculate three measured shapes:

1. **Idle API:** zero user requests for a month, including registry, logs, public ingress and
   minimum instances.
2. **Light use:** a defined number of lookup requests plus a small set of ingestion jobs.
3. **Failure case:** a retry loop, unusually large document or stuck worker up to the configured
   hard execution limit.

Do not compare only the API's free allowance:

- Render has no free background worker.
- Railway's worker traffic can defeat Serverless sleep.
- Fly.io's ongoing free allowance is not available to new accounts.
- DigitalOcean dynamic components are paid.
- Fargate compute has no ongoing free allowance and Express Mode adds an always-on task plus ALB.
- Cloudflare Containers start with the paid Workers plan.
- Cloud Run and Container Apps have genuine ongoing allowances, but database, network and logs can
  still charge.

### Gate 3: operational and portability proof

A candidate passes only after the pilot demonstrates:

- the same MCP tool contract and structured errors as local stdio;
- authenticated Streamable HTTP with reconnect and idempotency;
- a completed ingestion after API shutdown and client disconnect;
- safe retry after worker termination;
- bounded scratch-disk and memory use for every supported format;
- database connection recovery after scale-to-zero;
- logs and traces without document bodies or raw queries;
- export/import and redeployment on the local Docker Compose baseline;
- a current target-region price sheet including network and observability; and
- an account-level spending control whose failure behaviour is understood.

## Questions that remain provider- and account-dependent

The following cannot be resolved reliably from public headline pricing:

- target account eligibility for free credit and region-specific features;
- current capacity for the selected CPU/memory shape in a specific region;
- organisation, payment-card and identity verification requirements;
- custom-domain, OIDC/JWT and private-networking path;
- exact HTTP streaming and upload behaviour where the official limit is unclear;
- database and embedding egress when services sit in different providers or regions;
- build, registry, logs, traces and retained metric volume;
- cold-start distribution rather than a single best-case measurement;
- support response and outage recovery expectations; and
- whether a budget control is an alert, a workload stop or a true prevention of further charges.

These are exit criteria for a synthetic pilot, not assumptions to fill with estimates.

## Research conclusion

There is no universal free container platform for both halves of this workload. Cloud Run and
Azure Container Apps publish meaningful ongoing consumption allowances and first-class jobs.
Developer platforms can reduce operational work, but their free offerings often exclude workers
or provide very small credits. ECS/Fargate offers the most conventional AWS container model but
with a higher baseline and more infrastructure. Fly.io is inexpensive at small sizes but no longer
free for new accounts. Cloudflare's low-idle model is technically interesting while introducing
the greatest control-plane coupling among the container candidates.

The correct next action is a small, identical synthetic benchmark across a deliberately limited
shortlist, followed by a complete target-region cost calculation. This report does not authorise
that shortlist, account creation or deployment.

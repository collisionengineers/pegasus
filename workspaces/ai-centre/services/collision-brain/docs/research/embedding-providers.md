# Hosted embedding-provider research

Checked: **20 July 2026**

This note compares hosted text-embedding routes suitable for the RAG pipeline. It covers OpenAI,
Microsoft Foundry/Azure OpenAI, Google Gemini API and Vertex AI, Amazon Bedrock, Cohere, Voyage AI,
Mistral AI, Jina AI and Hugging Face. It does not select a provider.

Prices below are public USD list prices unless stated otherwise. Taxes, exchange rates, negotiated
contracts, cloud marketplace uplift and data-transfer charges are excluded. A price shown as
`$x/M` means dollars per million input tokens. The worked 10-million-token figures are marginal
list cost after any free allocation has been exhausted; they are not a workload forecast.

## Executive findings

- “Free” is not one comparable category. Google has a quota-limited Gemini Developer API free
  tier; Cohere and Mistral have evaluation modes; Voyage and Jina advertise introductory token
  allocations; Azure and AWS offer time-limited new-account cloud credits; and Hugging Face gives
  a very small recurring routed-inference credit. OpenAI's model pages show a `Free` rate-limit
  class but do not establish a free token allocation. None should be assumed to be a free
  production service.
- The cheapest published standard text-embedding prices in this comparison start around
  **$0.02/M tokens**. At that price, embedding ten million tokens is $0.20 before storage,
  retrieval, orchestration and re-indexing costs. The price difference between credible routes
  may matter less than retrieval quality, data terms, dimensionality, regional processing and
  operational reliability at a small corpus size.
- Free and trial data terms are materially different. Google says free-tier Gemini API content is
  used to improve products; Cohere may use trial content for model improvement; Mistral says Free
  plan content may be used for training; and Voyage's default terms permit training use unless an
  account administrator opts out. This repository's synthetic/non-sensitive pilot constraint is
  therefore necessary, not merely precautionary.
- An embedding provider is not interchangeable by changing an endpoint. Model, version,
  dimensionality, tokenisation, truncation, task type and normalisation affect stored vectors and
  retrieval. A provider/model change requires a controlled full-corpus re-embedding and a
  side-by-side quality gate.
- Batch routes can reduce initial-corpus and re-indexing cost, but they must not be used for the
  online `lookup` query path. OpenAI, Google, Voyage and Mistral publish batch discounts; the
  exact submission format, turnaround and retention differ.
- Public pricing is incomplete in some cases. The Azure page exposes the charging unit but did not
  render stable numeric meter prices without account/region context. Cohere's current public page
  exposes trial and dedicated-deployment prices but not a numerical serverless Embed 4
  pay-as-you-go rate. These are procurement checks, not values to infer from another route.

## Comparable snapshot

| Hosted route | Representative current model(s) | Free access found | Public standard input price | Marginal cost for 10M tokens | Important technical shape |
|---|---|---:|---:|---:|---|
| OpenAI API | `text-embedding-3-small`; `text-embedding-3-large` | No free token allowance confirmed | $0.02/M; $0.13/M | $0.20; $1.30 | 8,192-token request; 1,536 and 3,072 default dimensions |
| Azure OpenAI / Microsoft Foundry | Azure deployments of `text-embedding-3-small` and `-large` | New Azure account credit only; no recurring Azure OpenAI allowance confirmed | Charged per 1,000 tokens; numeric price is region/deployment/contract dependent and was not stably exposed | Obtain from target-region calculator | 8,192 tokens; 1,536/3,072 dimensions; up to 2,048 array inputs |
| Gemini Developer API | `gemini-embedding-001`; `gemini-embedding-2` | Quota-limited free tier | $0.15/M; $0.20/M | $1.50; $2.00 | 2,048 and 8,192 input tokens respectively; 128–3,072 dimensions |
| Google Vertex AI | Gemini Embedding | No recurring production inference allowance confirmed | $0.15/M online; $0.12/M batch | $1.50 online; $1.20 batch | Google Cloud SKU, billing and data controls, separate from Gemini Developer API |
| Amazon Bedrock | Titan Text Embeddings V2 | Introductory AWS account credit, not a recurring Bedrock tier | $0.02/M in AWS's official Titan V2 launch pricing; verify target-region meter | $0.20 | 8,192 tokens or 50,000 characters; 1,024/512/256 dimensions |
| Cohere API | Embed 4 | Free trial, non-production; normally 1,000 calls/month | No numeric serverless Embed 4 price on the current public page | Quote/dashboard required | 128K context; up to 96 inputs; 256/512/1,024/1,536 dimensions |
| Voyage AI | `voyage-4-lite`; `voyage-4`; `voyage-4-large` | First 200M tokens advertised free, subject to account/data-term caveats | $0.02/M; $0.06/M; $0.12/M | $0.20; $0.60; $1.20 after allocation | 32K context; 256/512/1,024/2,048 dimensions |
| Mistral API | `mistral-embed`; `codestral-embed` | Limited Free mode for testing; allowance not publicly quantified | $0.10/M; $0.15/M | $1.00; $1.50 | `mistral-embed` is 1,024-dimensional; current public context/batch-input limits need confirmation |
| Jina AI API | `jina-embeddings-v5-text-nano`; `-small` | New users receive 10M tokens | Model metadata implies $0.02/M; $0.05/M | $0.20; $0.50 after allocation | 8K/768 dimensions; 32K/1,024 dimensions; current API metadata lists US processing |
| Hugging Face routed inference | Provider- and model-dependent | $0.10/month for free users; higher account plans receive more credit | Underlying provider price, with no Hugging Face markup | Model/provider-dependent | A routing and billing layer, not one embedding model |
| Hugging Face Inference Endpoints | User-selected model on dedicated CPU/GPU | Scale-to-zero can remove idle compute cost; no permanent free endpoint | From $0.033/hour for the smallest AWS CPU example | Utilisation-dependent | Dedicated infrastructure; hardware fit and cold-start benchmark required |

The OpenAI model specifications in the snapshot align with the models Microsoft documents for its
Azure route. OpenAI's own current model pages provide the direct API prices; Azure's separate
deployment and billing terms must not be replaced with those prices.

## Provider findings

### OpenAI API

The current OpenAI model pages list
[`text-embedding-3-small`](https://developers.openai.com/api/docs/models/text-embedding-3-small)
at **$0.02/M tokens** and
[`text-embedding-3-large`](https://developers.openai.com/api/docs/models/text-embedding-3-large)
at **$0.13/M tokens**. Both pages show a `Free` rate-limit class of 100 requests/minute,
2,000 requests/day and 40,000 tokens/minute, but the same pages still price input tokens. No
official free token balance or recurring credit was found, so the safe budget assumption is paid
usage.

OpenAI's Batch API supports `/v1/embeddings`, has a 24-hour completion window, and is advertised at
a 50% discount. The
[Batch reference](https://platform.openai.com/docs/api-reference/batch/object?api-mode=responses)
limits one embedding batch to 50,000 embedding inputs. This is attractive for initial ingestion
and re-indexing, not query-time embedding.

Microsoft's current specification for the same model family records an 8,192-token request limit,
default dimensions of 1,536 for small and 3,072 for large, and at most 2,048 inputs in an array.
The application should nevertheless discover or validate limits at adapter start-up because the
direct OpenAI and Azure routes can evolve independently.

OpenAI says API inputs and outputs are not used to train its models by default. Its
[data-control documentation](https://platform.openai.com/docs/models/default-usage-policies-by-endpoint)
describes default abuse-monitoring retention of up to 30 days and approval-based Modified Abuse
Monitoring or Zero Data Retention. Data residency and regional processing are eligibility- and
endpoint-dependent; non-US residency can have additional account requirements. The embedding
adapter must not imply that the ordinary direct API is automatically UK-resident.

Operational conclusion: the direct API has unusually transparent unit pricing and a cheap small
model, but no confirmed free token tier and no default UK processing guarantee. The quality delta
between small and large must be measured on this corpus before paying 6.5 times the input price
and storing twice the default vector width.

### Microsoft Foundry / Azure OpenAI

Azure offers deployments of the same `text-embedding-3` family behind Azure resource, identity,
network and regional controls. Microsoft's
[model specification](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure)
lists:

| Model | Maximum request | Default dimensions | Maximum array inputs |
|---|---:|---:|---:|
| `text-embedding-3-small` | 8,192 tokens | 1,536 | 2,048 |
| `text-embedding-3-large` | 8,192 tokens | 3,072 | 2,048 |

The
[Azure embeddings REST reference](https://learn.microsoft.com/en-us/rest/api/aifoundry/azureopenai/embeddings)
also records an aggregate ceiling of 300,000 tokens across one input array and confirms that the
dimension parameter is available for `text-embedding-3` and later models.

The [Azure OpenAI price page](https://azure.microsoft.com/en-gb/pricing/details/azure-openai/)
describes Standard pay-as-you-go embedding billing per 1,000 tokens and separates Global, Data
Zone and Regional deployment types. Its public static representation did not expose stable
numeric embedding prices on the checked date; values depend on region, currency and agreement.
The correct acceptance check is a saved target-account calculator or portal quote for the exact
region and deployment type. OpenAI-direct prices are not a valid substitute.

Azure's public new-account offer includes **$200 credit for 30 days**. That is general
introductory Azure credit, not a recurring free Azure OpenAI tier, and service/model availability
can still be gated.

Microsoft states that prompts, outputs and embeddings are not made available to OpenAI or other
model providers and are not used to train models without permission. Its
[data-privacy documentation](https://learn.microsoft.com/en-us/azure/foundry/responsible-ai/openai/data-privacy)
distinguishes Regional, Data Zone and Global processing and describes abuse monitoring.
[Regional availability](https://learn.microsoft.com/en-us/azure/foundry/foundry-models/concepts/models-sold-directly-by-azure-region-availability)
varies by model and deployment type. A desired geography is therefore a deployment selection,
not an intrinsic property of “Azure”.

Quota is assigned by subscription, region, model and deployment type. Microsoft's
[quota page](https://learn.microsoft.com/en-us/azure/foundry/openai/quotas-limits?view=foundry-classic)
currently gives a Tier 0 Global Standard example for `text-embedding-3-small` of one million
tokens/minute and 1,000 requests per ten seconds, but that example is not a guarantee for a new
subscription or regional deployment.

Operational conclusion: Azure is the clearest route here when existing Azure identity, private
networking and region controls are requirements, but price and quota must be evidenced for the
specific account/region/deployment. No Azure subscription, model deployment or paid test was
performed for this research.

### Google Gemini Developer API and Vertex AI

Google operates two commercially distinct routes that must remain separate in configuration and
costing.

For the Gemini Developer API, the
[Gemini pricing page](https://ai.google.dev/gemini-api/docs/pricing) lists:

| Model | Free tier | Paid standard | Paid batch |
|---|---:|---:|---:|
| `gemini-embedding-001` | Free, subject to limits | $0.15/M | $0.075/M |
| `gemini-embedding-2` | Free, subject to limits | $0.20/M for text | $0.10/M for text |

The free tier does not include batch processing. Google's
[embedding guide](https://ai.google.dev/gemini-api/docs/embeddings) gives
`gemini-embedding-001` a 2,048-token input window and `gemini-embedding-2` an 8,192-token window.
Both support flexible 128–3,072 output dimensions, with 768, 1,536 and 3,072 recommended for
Embedding 2. The model guide records Embedding 2 as stable from April 2026.

The free tier has a consequential data condition: Google's price table states that free-tier
content **is used to improve products**, whereas paid-tier content is not. The
[billing guide](https://ai.google.dev/gemini-api/docs/billing/) says free and paid services are
available in the EEA, UK and Switzerland, but geography availability should not be read as a
promise that all processing and retention remain in the UK.

Google does not publish one durable free-tier request number in the general
[rate-limit guide](https://ai.google.dev/gemini-api/docs/rate-limits). Limits are measured using
requests/minute, tokens/minute and requests/day, apply per project, vary by model/tier, and the
active numbers are shown in AI Studio. The pilot must capture the actual project quota rather than
copy a documentation example.

The separate
[Vertex AI generative AI pricing page](https://cloud.google.com/vertex-ai/generative-ai/pricing)
lists Gemini Embedding at **$0.00015 per 1,000 input tokens** online ($0.15/M) and
**$0.00012 per 1,000** in batch ($0.12/M). Older text-embedding SKUs excluding Gemini are billed
per character at $0.000025 per 1,000 characters online and $0.00002 in batch. A character-billed
and token-billed result cannot be compared without measuring the actual corpus.

Operational conclusion: the Developer API is a useful synthetic-data quality and integration
probe because of its genuine no-charge mode. Paid Gemini API and Vertex should be treated as
separate adapters or at least separate deployment profiles because billing, identity, quotas and
data controls differ.

### Amazon Bedrock

Amazon Titan Text Embeddings V2 is Bedrock's first-party text option. The
[current model documentation](https://docs.aws.amazon.com/bedrock/latest/userguide/titan-embedding-models.html)
specifies:

- model ID `amazon.titan-embed-text-v2:0`;
- up to 8,192 tokens or 50,000 characters;
- 1,024 dimensions by default, with 512 and 256 options;
- float and binary output forms;
- on-demand and provisioned throughput; and
- request-per-minute rather than token-per-minute throttling.

AWS's official
[Titan V2 launch pricing](https://aws.amazon.com/blogs/machine-learning/get-started-with-amazon-titan-text-embeddings-v2-a-new-state-of-the-art-embeddings-model-on-amazon-bedrock/)
states **$0.00002 per 1,000 tokens**, equivalent to **$0.02/M**. The general
[Bedrock pricing page](https://aws.amazon.com/bedrock/pricing/) is region-specific. Because the
numeric evidence is an official launch publication rather than a captured current London meter,
selection must re-check the exact target-region SKU.

The current
[Titan V2 model card](https://docs.aws.amazon.com/bedrock/latest/userguide/model-card-amazon-titan-text-embeddings-v2.html)
lists in-region availability including `eu-west-2` (London) and Standard pay-per-token inference.
It does not list Global or Geo inference for Titan V2. The model supports more than 100 languages
in preview, but AWS warns that cross-language queries and passages can give suboptimal results.
Language behaviour therefore belongs in the retrieval benchmark.

Under AWS's post-July-2025 new-account programme, eligible new customers receive $100 in credits
and can earn a further $100 through activities including Bedrock; the free account plan ends after
six months or when credits are depleted. The
[AWS announcement](https://aws.amazon.com/about-aws/whats-new/2025/07/aws-free-tier-credits-month-free-plan/)
describes introductory account credit, not a permanent Bedrock embedding tier.

Bedrock's
[data-protection documentation](https://docs.aws.amazon.com/bedrock/latest/userguide/data-retention.html)
supports policies that prevent durable request/response storage and says model providers do not
receive customer prompts and completions. Configuration differs by API and feature, so knowledge
base, logging and batch storage must be checked independently of synchronous inference.

Operational conclusion: Titan V2 combines a low published unit price, selectable compact vectors
and London in-region inference. That does not make it a default: target-account access, current
regional price, task quality and the effect of lower dimensions still require evidence.

### Cohere

Cohere's
[pricing page](https://cohere.com/pricing) gives free Trial API access and says trial use is for
learning and prototyping, not production or commercial use. It points production users to
pay-as-you-go API keys but does not expose a numeric serverless Embed 4 token price in the current
static page.

The same page does expose dedicated Model Vault prices for Embed 4:

- Small: **$4/hour or $2,500/month**;
- Medium: **$5/hour or $3,250/month**.

Those dedicated figures are not a proxy for serverless token pricing and are unlikely to be
economic for a small pilot.

Cohere's
[rate-limit documentation](https://docs.cohere.com/v2/docs/rate-limits) gives Embed a limit of
2,000 inputs/minute on both trial and production keys, while trial keys are normally limited to
1,000 API calls/month. Its
[Embed API reference](https://docs.cohere.com/reference/embed) accepts up to 96 texts or images in
one request and supports 256, 512, 1,024 and 1,536 dimensions, with 1,536 as default for Embed 4.
Cohere describes Embed 4 as having a 128K context window, multilingual coverage across more than
100 languages and multimodal capability. This service only needs text, so multimodal price or
quality should not receive extra weighting.

Cohere's
[privacy notice](https://cohere.com/privacy) says trial inputs and outputs may be used for
research, development and model improvement, and warns against personal data in trial
environments. Its
[enterprise data commitments](https://cohere.com/enterprise-data-commitments) describe opt-out,
approved zero-data-retention and private/cloud deployment options for enterprise arrangements.

Operational conclusion: Cohere is technically credible and its evaluation rate limit is clear,
but a fair production-cost comparison needs a dated serverless quote or authenticated pricing
record. Trial data remains synthetic.

### Voyage AI

Voyage's
[pricing documentation](https://docs.voyageai.com/docs/pricing) is unusually explicit:

| Model | Introductory free allocation | Standard price | 12-hour batch price |
|---|---:|---:|---:|
| `voyage-4-lite` | First 200M tokens | $0.02/M | 33% discount |
| `voyage-4` | First 200M tokens | $0.06/M | 33% discount |
| `voyage-4-large` | First 200M tokens | $0.12/M | 33% discount |
| `voyage-context-3` / `voyage-code-3` | First 200M tokens | $0.18/M | 33% discount |
| `voyage-finance-2` / `voyage-law-2` / `voyage-code-2` | First 50M tokens | $0.12/M | 33% discount |

Voyage's Files API is priced at $0.05/GB-month and retains files for 30 days. This pipeline should
send embedding requests directly rather than duplicate originals into a provider file store
unless a benchmark demonstrates a concrete need.

The
[embedding documentation](https://docs.voyageai.com/docs/embeddings) gives the Voyage 4 family a
32K context window and 1,024 default dimensions, with 256, 512 and 2,048 options. A request can
contain at most 1,000 texts, but total request token limits differ: one million for
`voyage-4-lite`, 320,000 for `voyage-4`, and 120,000 for `voyage-4-large` and `voyage-code-3`.
Quantised output types are also available.

At the current Basic tier, Voyage's
[rate-limit table](https://docs.voyageai.com/docs/rate-limits) lists 2,000 requests/minute across
the Voyage 4 family, with 16M tokens/minute for lite, 8M for 4 and 3M for large. A payment method is
required to enter the first paid tier even while free tokens remain applicable.

The free allocation carries a material governance qualification. Voyage's
[FAQ](https://docs.voyageai.com/docs/faq) and [terms](https://www.voyageai.com/tos) say submitted
content may be used for training by default. An administrator can opt out, which enables
zero-day retention, but a payment method is required and Voyage may void remaining free tokens.
Content submitted before the opt-out remains governed by the earlier setting.

Operational conclusion: Voyage offers the largest published introductory allocation and several
retrieval-oriented models, but that headline is not an unconditional privacy-safe free tier.
Enable and evidence the desired data setting before any approved corpus, and price the workload
both with and without remaining free tokens.

### Mistral AI

Mistral's [API pricing page](https://mistral.ai/pricing/api/) lists:

- `mistral-embed` at **$0.10/M input tokens**;
- `codestral-embed` at **$0.15/M input tokens**; and
- batch processing at a **50% discount**.

`mistral-embed` produces 1,024-dimensional vectors according to Mistral's
[embedding cookbook](https://docs.mistral.ai/resources/cookbooks/mistral-embeddings-embeddings).
The current general
[Embeddings API reference](https://docs.mistral.ai/api/endpoint/embeddings) supports a string or
array of strings plus output dimension and data type where a model permits them. It does not
clearly expose a durable context limit and maximum input-array size for `mistral-embed`; these must
be tested or confirmed through model-specific documentation before setting ingestion batch size.

Mistral's current
[rate-limit guidance](https://help.mistral.ai/en/articles/698531-why-am-i-hitting-api-rate-limits-and-how-do-i-increase-them)
describes a default, limited Free mode for testing and prototyping, with exact organisation limits
shown in the console, and a Scale pay-as-you-go tier. Some older quick-start material still says
payments must be activated before API keys are enabled. Treat free API-key availability and its
allowance as an account-level check rather than a guaranteed quota.

Mistral says its
[Free plan may use input and output data for model training](https://help.mistral.ai/en/articles/347617-do-you-use-my-user-data-to-train-your-artificial-intelligence-models),
with an opt-out available, while Scale pay-as-you-go data is not used for training.
[Zero Data Retention](https://help.mistral.ai/en/articles/347612-can-i-activate-zero-data-retention-zdr)
requires Scale, approval and stateless APIs; it does not cover batch, files or other stateful
features. Mistral's current known-limitations documentation says API inference is served from EU
data centres by default, but subprocessors and an explicitly selected US endpoint require
separate review.

Operational conclusion: Mistral has transparent paid pricing, EU-default service and a meaningful
batch discount. Its unquantified Free allowance and trial training terms make it an integration
probe rather than a dependable free production choice.

### Jina AI

Jina's current [API documentation](https://api.jina.ai/docs), published 29 June 2026, says new
users receive **10 million free tokens**. Its current tier table lists:

| Tier | Requests/minute | Tokens/minute | Concurrency |
|---|---:|---:|---:|
| Free | 500 | 1M | 5 |
| Tier 1 | 500 | 10M | 50 |
| Tier 2 | 5,000 | 100M | 500 |

The live first-party
[`/v1/models` metadata](https://api.jina.ai/v1/models) exposes per-input-token `prompt` values.
Interpreting its OpenRouter-compatible pricing field in USD gives:

| Model | Raw per-token value | Equivalent | Context | Dimensions |
|---|---:|---:|---:|---:|
| `jina-embeddings-v5-text-nano` | 0.00000002 | $0.02/M | 8,192 | 768 |
| `jina-embeddings-v5-text-small` | 0.00000005 | $0.05/M | 32,768 | 1,024 |
| `jina-embeddings-v4` | 0.00000005 | $0.05/M | 32,768 | 2,048 |

This conversion is an inference from the API metadata schema, not a prose price promise. A dated
dashboard screenshot or invoice-rate confirmation is required before production budgeting.

Jina's older [product page](https://jina.ai/en-US/embeddings/) still shows lower free rate limits
of 100 requests/minute, 100,000 tokens/minute and two concurrent requests, and describes
`jina-embeddings-v4` as free under a non-commercial research licence. Those statements conflict
with the newer API documentation and current model metadata. Use the newer operational limits,
do not assume V4 is free for commercial API use, and confirm both API terms and any downloaded
weights licence separately.

Jina says API requests, inputs and outputs are not used for training. The current model metadata
lists the available data centre as `US` for the examined embedding models. This is not a
UK-resident route on the evidence reviewed.

Operational conclusion: Jina offers a useful introductory allocation, compact current models and
public machine-readable capabilities. The lagging product page, inferred price units and US-only
metadata are explicit pre-production checks.

### Hugging Face

Hugging Face offers two different hosted patterns.

#### Routed Inference Providers

The
[Inference Providers pricing page](https://huggingface.co/docs/inference-providers/pricing) says
free users receive **$0.10 of monthly credits**, subject to change. PRO accounts receive $2/month,
and Team/Enterprise organisations receive $2/month per seat. Beyond those credits, pay-as-you-go
uses the underlying provider's rate and Hugging Face says it adds no markup.

This is a routing, authentication and billing layer across multiple providers, not a single
embedding service with one price, context limit, region or data policy. The selected model and
provider determine those properties. A custom provider key may also move billing and contractual
responsibility directly to that provider.

The recurring free credit is genuine but tiny: at $0.02/M it would cover roughly five million
tokens if such a routed embedding combination were available at that exact price; at $0.10/M it
would cover one million. Provider availability, minimum charges and changing credit values make
this an illustration, not a guaranteed allowance.

#### Dedicated Inference Endpoints

The
[Inference Endpoints price page](https://huggingface.co/docs/inference-endpoints/pricing) lists
small CPU examples from **$0.033/hour on AWS**, **$0.060/hour on Azure** and **$0.050/hour on
Google Cloud**, billed per minute while the endpoint is running. An active subscription and
payment card are required. Suitable GPU instances cost more.

Endpoints can scale to zero and therefore stop compute billing while idle, at the cost of a cold
start. This is infrastructure pricing: it says nothing about how many embedding tokens a selected
model and hardware pair can process per second. Even a cheap CPU endpoint can cost more than a
serverless token API if it stays warm, and may fail latency targets for a larger model.

Operational conclusion: routed inference is helpful for a broad low-volume bake-off and the
dedicated route provides stronger model portability. Neither produces a comparable cost without
pinning the exact model, provider, hardware, region, minimum replicas and measured throughput.

## Cross-provider implementation requirements

The provider-neutral `EmbeddingProvider` port should capture more than `embed(text)`:

- distinguish online query embedding, online document embedding and offline/batch document
  embedding;
- carry the provider route, immutable model ID/version, output dimensions, task/input type,
  normalisation setting and tokenizer/version into indexed-document metadata;
- enforce the model's per-item token limit, per-request item limit and aggregate token limit before
  calling the provider;
- make truncation an explicit rejected, allowed or recorded policy rather than silently accepting
  provider defaults;
- expose usage counts, provider request ID, retry-after information and batch-job state without
  logging raw queries or document bodies;
- handle 429 and transient 5xx responses with bounded exponential back-off, jitter and an ingestion
  retry budget while keeping online lookup latency bounded;
- validate that every vector has the configured dimension and finite numeric values before
  persistence;
- support a deterministic local test provider so contract tests and deletion tests never depend on
  network or credit;
- isolate API credentials and allow workload identity where a cloud route supports it;
- declare the configured data region, retention/training mode and whether the account setting has
  been independently evidenced; and
- refuse to combine chunks and query vectors from different model identities or dimensions.

A change to provider, model revision, task type or dimension creates a new embedding generation.
The migration procedure should build a parallel index, run the retrieval benchmark, switch reads
atomically, and retain enough metadata to roll back without retaining deleted source content.

## Cost model to take into the pilot

For each candidate, estimate:

```text
initial corpus tokens
+ expected monthly new/changed document tokens
+ expected monthly query-embedding tokens
+ re-embedding reserve
= monthly billable embedding tokens

token cost
+ minimum endpoint/throughput cost
+ batch or file storage
+ network egress
+ vector-database cost
+ observability
= total retrieval-platform cost
```

The estimate should include at least one full-corpus re-embedding during the pilot. “First N
tokens free” must be shown separately from the steady-state cost, and introductory cloud credit
must not be amortised as a permanent discount. Query tokens are normally small relative to corpus
ingestion, but query calls are latency-sensitive and cannot rely on offline batch discounts.

## Evidence gaps and contradictions

| Issue | Required resolution before provider selection |
|---|---|
| Azure public page did not render a stable numeric embedding price | Save calculator/portal evidence for the exact subscription, currency, region, model and deployment type. |
| Cohere current public page omitted a numeric serverless Embed 4 pay-as-you-go price | Obtain a dated dashboard price or written quote; do not infer it from dedicated Model Vault pricing. |
| Gemini and Mistral active free quotas are console/account dependent | Capture the created synthetic-pilot project's effective RPM, TPM, daily cap and billing status before testing. |
| Mistral documentation differs on whether a payment method is needed before API-key use | Verify with a no-charge account only if approved; do not attach a card or incur a charge as part of research. |
| Jina's older product page conflicts with newer API documentation on limits and V4 pricing | Treat `/docs` and `/v1/models` as the current operational source, then confirm commercial terms and dashboard price. |
| AWS's readily accessible numeric Titan V2 evidence is an official launch publication | Verify the current target-region Bedrock meter and model access before budgeting. |
| Hugging Face routed pricing is provider/model-dependent | Pin an exact routing combination and data policy; benchmark dedicated endpoint hardware separately. |
| Free/trial training-use controls differ from paid controls | Record account-level opt-out/ZDR evidence and effective date before any approved non-synthetic data. |

## Evaluation framework

No provider is selected by this research. Candidates should progress through the following gates.

### 1. Data and contractual gate

- Confirm that the corpus remains non-sensitive for the prototype.
- Record controller/processor roles, training-use setting, abuse-monitoring retention, deletion
  mechanism, subprocessors and the actual processing region.
- Reject a route for approved internal content if its required opt-out or retention setting cannot
  be evidenced at account level.
- Treat free/trial and paid plans as separate data-processing products where their terms differ.

### 2. Technical conformance gate

- Prove both document and query task modes through the provider-neutral adapter.
- Test documented maximum item size, array size, total-token size, dimension choices, Unicode,
  empty input, malformed input, timeouts, throttling and deterministic error mapping.
- Verify that HTTP and stdio MCP behaviour remains identical when the embedding adapter fails.
- Prove that provider credentials and raw content do not enter logs, traces or persisted job
  errors.

### 3. Retrieval-quality gate

- Build a versioned, synthetic or approved evaluation set with representative terminology,
  abbreviations, near-duplicates, long sections and no-answer queries.
- Hold chunking, hybrid-search logic and reranking constant while comparing providers.
- Measure citation recall, relevant-chunk recall at K, mean reciprocal rank, no-answer behaviour
  and metadata-filter correctness.
- Compare smaller and larger dimensions where offered; include database size and query latency,
  not only semantic score.
- Test multilingual behaviour only if it is a real requirement.

### 4. Performance and resilience gate

- Measure p50/p95/p99 online query-embedding latency from the intended application region.
- Measure ingestion throughput using both synchronous and discounted batch routes.
- Exercise 429, 5xx, connection reset, batch timeout and partial-failure recovery.
- Record cold-start behaviour for scale-to-zero or dedicated endpoints.
- Confirm quota headroom at projected peak rather than relying on a vendor maximum.

### 5. Cost gate

- Price the measured corpus and query load at standard, batch and steady-state rates.
- Separate recurring free allowances, one-off allocations and introductory account credits.
- Include at least one full re-index, vector width/storage impact, egress, observability and any
  always-on endpoint minimum.
- Record target account, region/SKU, corpus estimate and a hard spending cap before any billed
  benchmark.

### 6. Portability and exit gate

- Export document/chunk metadata without provider-specific identifiers being required by the
  domain.
- Rebuild the index using deterministic local embeddings and at least one alternative hosted
  adapter.
- Verify parallel-generation cutover and rollback.
- Confirm that removing a document prevents it being recreated by a delayed provider batch result.

The reference hosted provider should be selected only after these gates produce comparable
quality, data-governance, operational and cost evidence. A free tier can justify inclusion in the
synthetic pilot; it cannot, by itself, justify the production decision.

# Collision Engineers — AI/ML Strategy
## 00 · Executive Summary

*Prepared: July 2026. Audience: firm owner / decision-maker. Reading time: ~10 minutes.*
*This is the front door to an 8-document suite. A one-page reading guide is at the end.*

---

## Bottom line up front

Your archive is a **genuinely valuable AI asset** — not because it is big, but because your finished reports are already the "right answers." Every historical case is a worked example of *instruction + photos → expert assessment*. That is a supervised training set most firms could never assemble, and it is yours.

But the highest-value move is **not** "train a model." It is to **turn report production into a schema-driven, AI-assisted, engineer-signed pipeline** that cuts the time an engineer spends per report. At £100–£200 a report, your binding constraint is engineer hours, not compute. Halving touch-time is worth **far more** than any model you could train, and it is achievable in weeks with tools that need no training at all.

Training your own **owned, downloadable model weights** (your explicit requirement) is worthwhile — but as a *second* wave, layered on top, and only where measurement proves a frontier API isn't good enough. When you do train, you fine-tune open-weights models (Apache-2.0 / MIT) that you own outright. You never train from scratch, and you never bake money into the model.

**One urgent, non-AI action first:** the reference library contains a spreadsheet of manufacturer-portal usernames and passwords in plain text (`Manufacturers.ods`). Rotate those credentials and remove the file from any shared/synced location this week, before anything else.

---

## Your five questions, answered

### 1. Is any of this data useful for AI training? — **Yes, substantially.**

The decisive property of your data is that **your historical reports are gold-standard labels**. The intellectual work an engineer does — deciding repair vs total loss, the salvage category, the parts and operations list, the labour hours, the valuation, the roadworthiness call, the written opinion — is all captured in the finished report, paired with the exact inputs (instruction + photos) that produced it.

That makes your archive a ready-made **supervised dataset** for the assessment task, plus:
- a **house-style corpus** (your engineers' comments and report voice),
- a **proprietary argumentation corpus** (diminution rebuttals, PAV responses, query defences — your competitive moat), and
- a set of rare **"gold signals"** worth more than their number suggests: original-vs-amended report pairs (what changed after strip-down), client-vs-audit pairs (independent figure vs agreed-with-repairer figure), and internal valuation-tool screenshots showing how an Engineer's Value was derived.

Full inventory, including what's dangerous in it, is in **`01-data-asset-assessment.md`**.

### 2. Can it train a model *from scratch*? — **No. Neither feasible nor necessary.**

Training a competitive language or vision model from random initialisation needs on the order of **trillions of tokens** and **hundreds of millions of images**. Your corpus is roughly **10–50 million tokens** of reports and **tens of thousands of images** — four to six orders of magnitude short. A from-scratch model would cost far more and be far worse than a free, existing open model.

The only place "from scratch" is defensible is a **small photo-quality classifier head** (e.g., "is this a VIN shot / odometer / whole-vehicle photo?") — and even there you train a small head on top of a *pretrained* vision backbone, not from zero. Full reasoning and the arithmetic are in **`03-training-strategy.md`**.

### 3. Can it *fine-tune* a model? — **Yes. This is the recommended training path.**

Fine-tuning adapts an existing open-weights model to your task and voice using your labelled cases. It is cheap (hundreds of pounds per training run), and — critically for you — you **own the resulting weights** and can download and keep them forever. We recommend fine-tuning on **Apache-2.0 / MIT** base models so ownership is clean and irrevocable.

Fine-tuning is **gated behind evaluation**: we build a scored test set first, measure what a frontier API can already do, and only fine-tune where it demonstrably falls short (most likely: multi-photo damage reading, your house-style comments, and rebuttal drafting). Targets, recipes and model choices are in **`03-training-strategy.md`**.

### 4. What use cases are possible? — **A portfolio of 20+, ranked and sequenced.**

Beyond the four you named (engineer copilot, knowledge/RAG assistant, vision analysis, intake automation), the archive and email access unlock: a QA "report-linter" second-reader; rebuttal / PAV / diminution drafting from your own playbooks; damage-consistency & photo-reuse fraud screening (your instructions already ask engineers to check this); fee/cashflow automation against your 89-day terms; vehicle-ID & mileage verification via **free government APIs**; and analytics products from thousands of historical cases (cost benchmarks by make/model/damage area). New **revenue lines** are plausible too — diminution-at-scale screening of your own closed book, and a "supplementary-damage risk" score productised from your amended-report pairs. The full ranked portfolio with build order is in **`02-use-case-portfolio.md`**.

### 5. How would you go about it? — **A schema-centric, human-in-the-loop pipeline, built in waves.**

The architecture in one breath: **parse each case into a structured Case Record → let AI pre-fill the fields (with confidence + sources) → compute every £ figure in code from dated rate tables → render the report from a template → the engineer reviews, corrects, and signs → the corrections feed the next round of training.** Frontier APIs do the heavy lifting on day one; owned fine-tuned models are layered in where evaluation justifies it; the engineer is always the author of record. The pipeline is in **`04-data-pipeline-and-governance.md`**; the staged plan and costs in **`06-roadmap-and-costs.md`**.

---

## The strategic thesis: where your moat actually is

Large, well-funded players (Tractable, Solera/Audatex Qapter, CCC) have spent hundreds of millions on photo→estimate damage AI with vastly more images than you will ever have. **Do not try to out-build them at commodity damage detection.** They also don't compete with you: none produces an *independent, CPR Part 35, court-compliant expert opinion*.

Your moat is the **expert-opinion layer**:
- **Independence and court-compliance** — the statement of truth, the duty to the court, the engineer who holds the opinion.
- **Proprietary judgement** — your diminution and PAV playbooks, your query-rebuttal library, your SOPs, your house style.
- **A data flywheel** — every report your engineers correct makes your owned models better, and nobody else has your cases.

Invest AI effort *there*. Treat commodity damage detection as something to consume (via a frontier API) rather than something to build.

---

## What we will NOT do (and why)

| Ruled out | Why |
|---|---|
| Train a model from scratch | 4–6 orders of magnitude short on data; expensive and worse than free open models. |
| Fine-tune a **closed** model (GPT/Gemini/Claude) | You'd never own the weights — the exact risk you told us to avoid. Open weights only. |
| Put money/valuations **in the model weights** | Rates and values drift; baked-in numbers rot. £ is computed at render time from dated tables. |
| Index or train on **per-job licensed** third-party content (Thatcham, OEM manuals) | Explicit no-copy licences; use per-case only, never in a persistent store. See `01`. |
| **Autonomous** report issuance | CPR 35 requires a human expert to hold and sign the opinion. AI drafts; the engineer decides. |
| **License your raw data** to third parties | Riddled with PII + third-party copyrighted content + client confidentiality; it also sells your moat. |

---

## The value model (why this pays)

Your economics are driven by **engineer touch-time per report**, not software cost.

- **Illustrative:** if AI-assisted drafting reduces engineer time from ~45–90 min/report to ~15–30 min, that is ~30–60 min saved per report. At ~150–200 reports/month and a loaded engineer cost of £40–80/hr, that's on the order of **£4,000–£12,000/month of freed capacity** — which is either lower cost or more reports at the same headcount.
- Against that, the **running AI cost is noise**: frontier-API processing is roughly **£0.10–0.50 per case** (tens of pounds a month at your volume). Owned-model serving on scale-to-zero GPU is ~**£15–40/month**.
- So the fine-tuned model is **not** a cost-saving play — frontier APIs are already cheap. Owning weights buys you **continuity** (no vendor deprecation or price shock), **portability**, **court-reproducibility** (pinned weights + fixed settings = the same output every time), and **margin if you scale or white-label**.

*(All figures are planning ranges to be re-quoted at build time; full model in `06-roadmap-and-costs.md`.)*

---

## Budget envelope and gates

Staged so early, cheap wins fund later, bigger bets. Nothing large is spent before value is proven.

| Wave | What | Rough spend | Gate to proceed |
|---|---|---|---|
| **0** | Security & governance (credential rotation, access control, start DPIA) | ~£0–2k (mostly time) | — do first, unconditionally |
| **1** | Deterministic Case Record + renderer, intake automation, QA-linter, vehicle/mileage lookups | ~£8–25k engineering + ~£tens/mo API | Measured touch-time reduction on real cases |
| **2** | RAG knowledge assistant, engineer copilot, rebuttal/PAV/DV drafters | ~£10–25k engineering + API | Engineers accept a majority of drafted fields/text |
| **3** | Owned-weights fine-tunes (VLM + house-style), fraud/consistency, analytics | ~£5–20k (GPU + engineering), *gated* | Frontier baselines underperform on the eval set |
| **4** | Products: diminution-at-scale, benchmarks | commercial case-by-case | Legal review + demonstrated internal accuracy |

**Indicative Year-1 all-in: ~£30–80k**, front-loaded into Waves 1–2 (the parts that pay for themselves). The critical path is **not** money — it's **~150–250 hours of engineer time** to build and check the golden dataset. Protect that time.

---

## Immediate next actions (first two weeks)

1. **Rotate the OEM-portal credentials** in `Manufacturers.ods`; move secrets to a password manager; delete the file from shared/synced storage and from git history. *(Wave 0 — see `07`.)*
2. **Start the DPIA** and add a data-use clause to engagement terms so future cases are cleanly usable. *(See `04`.)*
3. **Confirm the real numbers** that size everything: how many closed cases, what fraction are amended, email-archive volume. *(Feeds `05`/`06`.)*
4. **Green-light Wave 1**: the Case Record schema + deterministic renderer + intake parsing. This is the foundation every use case shares and it pays back on its own.

---

## How to read this suite

| Doc | Read it for | Primary audience |
|---|---|---|
| `00-executive-summary.md` | The decisions and the money (this doc) | Owner |
| `01-data-asset-assessment.md` | What you hold, what it's worth, what's dangerous | Owner + build team |
| `02-use-case-portfolio.md` | Every use case, ranked, with build order | Owner + product |
| `03-training-strategy.md` | From-scratch verdict, fine-tuning, model choices | Technical |
| `04-data-pipeline-and-governance.md` | The pipeline, the schema, GDPR/security | Technical + compliance |
| `05-evaluation-framework.md` | How we measure before we trust a model | Technical |
| `06-roadmap-and-costs.md` | Waves, milestones, £ ranges, staffing | Owner + build team |
| `07-risk-register-and-compliance.md` | Risks, CPR-35 AI policy, incident response | Owner + compliance |

---

## Decision log (choices this suite locks in)

- **D1 — No from-scratch training.** Data volume forbids it; unnecessary given open models.
- **D2 — Fine-tune open weights only (Apache-2.0/MIT).** Clean, permanent ownership; satisfies the "downloadable weights" constraint.
- **D3 — Schema + deterministic renderer at the centre.** Reports are ~70% template; arithmetic is code, never a model.
- **D4 — Money and valuations live in versioned tables, never in weights.** Directly answers the temporal-drift concern.
- **D5 — Human-in-the-loop is mandatory and permanent.** CPR 35; the engineer authors and signs; AI assists.
- **D6 — Frontier APIs first, owned models second (gated on evaluation).** Cheapest path to value; own weights where it's proven worthwhile.
- **D7 — Three-tier licensing enforced.** Proprietary → train/index; purchased/public → lookup only; per-job licensed → ephemeral only.
- **D8 — Security & governance are Wave 0.** Credential rotation, PII pseudonymisation, DPIA precede any model work.

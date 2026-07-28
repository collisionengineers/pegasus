# Prioritised Opportunity Matrix

## Executive conclusion

The data is useful across ingestion, search, vision, assessment support, valuation, drafting, QA and operations. The strongest starting opportunities do not require training a new foundation model. They create a reliable case graph, enforce evidence provenance and solve measurable workflow problems with rules, retrieval and small pretrained models.

The recommended order is:

1. data rights, ingestion and case assembly;
2. deterministic QA, analytics and approved-knowledge retrieval;
3. remote-evidence completeness and vehicle/OCR assistance;
4. report and correspondence drafting from accepted facts;
5. damage localisation and estimate/valuation support;
6. multi-image assessment and supplement-risk research;
7. carefully bounded decision support;
8. no general-purpose training from scratch.

## Scoring method

Scores are directional and should be replaced with measured baselines during discovery.

- **Value:** likely operational, quality or product benefit; 1 low to 5 high.
- **Data readiness:** how directly current/history data supports the task; 1 poor to 5 strong.
- **Technical feasibility:** maturity and fit of available techniques; 1 uncertain to 5 strong.
- **Risk:** privacy, professional, safety or independence consequence; 1 low to 5 high.
- **Tier:** recommended investment order, where A is first.

## Opportunity matrix

| Opportunity | Value | Data readiness | Feasibility | Risk | Tier | Recommended technique |
|---|---:|---:|---:|---:|---|---|
| Corpus inventory and analytics | 5 | 4 | 5 | 2 | A | ETL, rules, BI |
| Inbox-to-case assembly | 5 | 4 | 4 | 3 | A | extraction, embeddings, classifiers |
| Artifact extraction/deduplication | 5 | 4 | 5 | 2 | A | parsers, OCR, perceptual hashes |
| Automated report/estimate QA | 5 | 4 | 5 | 2 | A | rules first, anomaly ranking later |
| Approved-knowledge search/RAG | 5 | 4 | 5 | 2 | A | hybrid retrieval with citations |
| Remote evidence completeness | 5 | 3 | 4 | 2 | A | view/quality classifiers |
| Vehicle identifier OCR/conflict checks | 4 | 3 | 4 | 2 | A | OCR plus validation/lookups |
| Report drafting from accepted facts | 5 | 4 | 4 | 3 | B | templates, RAG, constrained LLM |
| Evidence-request correspondence | 4 | 4 | 5 | 2 | B | rules/RAG, human send |
| Query/dispute response drafting | 4 | 3 | 4 | 3 | B | issue mapping, RAG, LLM |
| Workflow/deadline forecasting | 4 | 3 | 4 | 2 | B | rules, tabular/survival model |
| Damage component detection | 4 | 2 | 4 | 3 | B | pretrained detector fine-tuning |
| Damage segmentation | 4 | 1 | 3 | 3 | C | pretrained segmenter fine-tuning |
| Comparable valuation ranking | 4 | 3 | 4 | 3 | B | retrieval, learning-to-rank |
| Estimate normalisation/line mapping | 4 | 3 | 4 | 3 | B | taxonomy, text classifier |
| Repair-operation proposals | 4 | 2 | 3 | 4 | C | constrained multimodal model |
| Total-loss triage | 4 | 3 | 4 | 4 | C | calibrated tabular/multimodal model |
| Salvage-range anomaly detection | 3 | 2 | 3 | 3 | C | time-aware quantile model |
| Visible safety-indicator prompts | 5 | 1 | 3 | 5 | C | detector plus conservative rules |
| Multi-image evidence-grounded draft | 5 | 2 | 3 | 4 | C | pipeline/VLM with citations |
| Supplement/hidden-damage risk | 4 | 1 | 3 | 4 | C | temporal risk model |
| Independence-pressure review signals | 3 | 2 | 3 | 4 | C | text classifier plus human review |
| Autonomous final assessment | 3 | 1 | 2 | 5 | D | not recommended |
| Image-to-exact-price model | 2 | 1 | 2 | 5 | D | not recommended |
| General foundation model from scratch | 1 | 1 | 1 | 4 | D | not justified |

## Tier A — Build the foundation and immediate controls

### Data/case foundation

Without a canonical case graph, later models will leak evidence across matters and versions. This work also creates value by making the archive searchable and auditable.

### QA

Identity, arithmetic, version and citation checks can prevent defects with little model risk. They also generate high-quality correction labels.

### Retrieval

The `documents` corpus is best used as governed, dated knowledge rather than copied into model weights. Retrieval makes sources visible and updateable.

### Evidence completeness and OCR

These tasks improve remote submissions before professional reasoning begins. They are narrower, measurable and suitable for pretrained vision fine-tuning.

## Tier B — Controlled productivity systems

### Drafting

Generate report or email text only after facts have been accepted and sources resolved. Templates and retrieval should precede fine-tuning.

### Damage localisation

Start with component/view classification and coarse boxes. The likely initial limitation is labels, not image volume alone.

### Valuation and estimate normalisation

These tasks benefit from historical reasoning, but current market, method and price sources remain external.

### Workflow forecasting

Good inbox/case timelines can support transparent queues and deadline risk without touching the professional opinion.

## Tier C — Higher-value research with higher consequence

Multi-image assessment, repair-operation proposals, supplement risk, total-loss triage and safety prompts could become differentiating products. They require:

- a materially larger labelled corpus;
- evidence-time reconstruction;
- rare-case challenge sets;
- calibrated abstention;
- engineer-facing provenance;
- controlled shadow trials;
- higher release thresholds.

They should not block earlier value.

## Tier D — Do not target

### General model from scratch

Thousands of cases are domain-rich but many orders of magnitude below contemporary foundation-model pretraining. Training random weights would spend substantially more to achieve a worse general model than adapting a strong pretrained one.

### Autonomous final assessment

The data contains incomplete remote evidence, later amendments and professional judgement. An unsupervised final decision would hide uncertainty and create unacceptable professional and operational risk.

### Direct image-to-price

Prices depend on current external data, repair methods, rates and vehicle identity. A direct model would learn historical and client correlations rather than a defensible calculation.

## Product opportunities beyond internal automation

Subject to rights and client agreements, the same capabilities could support:

- a guided remote-evidence portal;
- white-labelled evidence-completeness API;
- independent estimate/valuation QA;
- audit workbench for engineering teams;
- explainable damage-evidence packs;
- anonymised cohort benchmarking;
- specialist model licensing;
- domain evaluation datasets;
- repair-query knowledge assistant.

External products require a separate re-identification, contractual and liability assessment.

## Portfolio recommendation

Fund one foundation programme and three linked pilots rather than many isolated proofs of concept:

- **Foundation:** rights, canonical case graph, ingestion, benchmark and governed knowledge.
- **Pilot 1:** case assembly plus deterministic QA.
- **Pilot 2:** remote evidence quality/view guidance.
- **Pilot 3:** RAG plus controlled report/query drafting.

Treat damage localisation and multi-image assessment as a research track that consumes the same governed data and evaluation infrastructure.

## Conclusion

The opportunity is broad, but the sequencing matters more than model novelty. The fastest defensible route creates clean case/evidence infrastructure and review controls first, then adds pretrained, task-specific models where the benchmark proves incremental value.

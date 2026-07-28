# Collision Engineers AI and Machine Learning Report Library

**Prepared:** 19 July 2026  
**Scope:** Remote, image-based independent vehicle-damage assessment  
**Source reviewed:** an external private snapshot formerly described as `ml-ops/data/private/raw`
**Status:** Historical opportunity assessment and delivery research. This library is evidence, not
Pegasus product, architecture, data-custody, or runtime authority. `Pegasus.Core`, root `design/`,
and `workspaces/report-renderer` retain their canonical ownership; private corpus and complete
Box/Outlook archives remain external under the ignored `corpus/` boundary.

## Executive conclusion

Collision Engineers has a commercially valuable multimodal dataset. The value does not come from photographs or reports in isolation. It comes from the relationship between:

1. incoming instructions;
2. vehicle and incident facts;
3. remotely supplied images and supporting documents;
4. engineer findings;
5. valuation and repair calculations;
6. original, audited and amended report versions;
7. correspondence, challenges and outcomes; and
8. the domain-reference material used to support professional judgement.

Thousands of similarly complete cases could support production-grade specialist AI systems. They are not enough to train a competitive general-purpose language or vision foundation model from random weights. The recommended strategy is to combine:

- deterministic extraction and calculation;
- permission-aware retrieval over approved reference material;
- fine-tuned vision and multimodal models;
- small task-specific classifiers and regressors;
- controlled report and correspondence drafting; and
- meaningful engineer review with evidence-level provenance.

The best first product is a **remote-assessment evidence system**: it checks whether the supplied photographs are sufficient, identifies what is visible, requests missing views, structures possible damage, and ties every suggestion back to its source. Exact repair prices, valuations and professional conclusions should remain grounded in current external data and approved by an engineer.

## What was found in the sample

The retained local snapshot contains 298 working files. The source inventory also recorded two ZIP
archives that duplicated extracted material; those archives are not present in the current snapshot:

- 115 files in eight case folders;
- 84 standalone case images;
- 20 case PDFs;
- seven completed assessment cases, covering four total-loss and three repairable outcomes;
- one partial/evidence-gathering case;
- 183 domain-reference files;
- original, audit and amended versions of one assessment;
- instructions, emails, estimates, fee notes and repairer-supplied evidence;
- manufacturer repair information, valuation guidance, salvage material, SOPs, diminution material, query responses and training resources.

The images have no retained EXIF metadata. Report PDFs are visually readable and generally text-bearing, but several contain malformed font encodings that defeat basic PDF parsers. Images, email attachments and report-embedded copies can represent the same underlying evidence, so deduplication is mandatory.

The sample also demonstrates why sender role matters. Incoming material can contain commercial pressure, incomplete evidence or an opposing party's position. Those items are useful context and risk signals; they must not be treated as Collision Engineers-authored ground truth.

## Report index

### 01 — Data readiness

- [Sample corpus inventory](01-data-readiness/01-sample-corpus-inventory.md)
- [Canonical case data model](01-data-readiness/02-canonical-case-data-model.md)
- [Ingestion, extraction and deduplication](01-data-readiness/03-ingestion-extraction-deduplication.md)
- [Annotation and label taxonomy](01-data-readiness/04-annotation-and-label-taxonomy.md)
- [Data governance, privacy and licensing](01-data-readiness/05-data-governance-privacy-and-licensing.md)

### 02 — Training strategy

- [Training from scratch: feasibility](02-training-strategy/01-from-scratch-feasibility.md)
- [Fine-tuning strategy](02-training-strategy/02-fine-tuning-strategy.md)
- [RAG and knowledge engineering](02-training-strategy/03-rag-and-knowledge-engineering.md)
- [Model selection, compute and cost](02-training-strategy/04-model-selection-compute-and-cost.md)
- [Evaluation and benchmark design](02-training-strategy/05-evaluation-and-benchmark-design.md)

### 03 — Vision and multimodal systems

- [Remote evidence quality and guided capture](03-vision-and-multimodal/01-remote-evidence-quality-and-guided-capture.md)
- [Vehicle identification and OCR](03-vision-and-multimodal/02-vehicle-identification-and-ocr.md)
- [Damage detection, localisation and segmentation](03-vision-and-multimodal/03-damage-detection-localisation-and-segmentation.md)
- [Multi-image assessment and evidence grounding](03-vision-and-multimodal/04-multi-image-assessment-and-evidence-grounding.md)
- [Supplement and hidden-damage risk](03-vision-and-multimodal/05-supplement-and-hidden-damage-risk.md)

### 04 — Assessment, estimating and valuation

- [Repair-plan and estimate assistance](04-assessment-estimating-and-valuation/01-repair-plan-and-estimate-assistance.md)
- [Total loss, roadworthiness and salvage](04-assessment-estimating-and-valuation/02-total-loss-roadworthiness-and-salvage.md)
- [Valuation and PAV assistance](04-assessment-estimating-and-valuation/03-valuation-and-pav-assistance.md)
- [Report generation, style and expert opinion](04-assessment-estimating-and-valuation/04-report-generation-style-and-expert-opinion.md)

### 05 — Email and workflow intelligence

- [Inbox ingestion, triage and case assembly](05-email-and-workflow-intelligence/01-inbox-ingestion-triage-and-case-assembly.md)
- [Correspondence, response and dispute assistance](05-email-and-workflow-intelligence/02-correspondence-response-and-dispute-assistance.md)
- [Workflow forecasting and independence controls](05-email-and-workflow-intelligence/03-workflow-forecasting-and-independence-controls.md)

### 06 — Governance, quality and deployment

- [Automated QA, consistency and anomaly detection](06-governance-quality-and-deployment/01-automated-qa-consistency-and-anomaly-detection.md)
- [Human oversight, active learning and feedback](06-governance-quality-and-deployment/02-human-oversight-active-learning-and-feedback.md)
- [Security, deployment and local model ownership](06-governance-quality-and-deployment/03-security-deployment-and-local-model-ownership.md)
- [Knowledge currency, drift and change control](06-governance-quality-and-deployment/04-knowledge-currency-drift-and-change-control.md)
- [Analytics, benchmarking and product insights](06-governance-quality-and-deployment/05-analytics-benchmarking-and-product-insights.md)

### 07 — Roadmap and pilots

- [Prioritised opportunity matrix](07-roadmap-and-pilots/01-prioritised-opportunity-matrix.md)
- [Phased delivery plan](07-roadmap-and-pilots/02-phased-delivery-plan.md)
- [Pilot specifications](07-roadmap-and-pilots/03-pilot-specifications.md)
- [Decision register and open questions](07-roadmap-and-pilots/04-decision-register-and-open-questions.md)

## Common design principles

Every proposed system in this library follows the same principles:

- **Case-level, not image-level, truth.** Images belong to a claim, evidence event and report version.
- **Remote evidence is bounded evidence.** The system must distinguish visible facts, supplied facts, inferences and unknowns.
- **Retrieval for changing knowledge.** Current repair methods, valuation guidance, prices and legal material should be retrieved and cited, not memorised in model weights.
- **Models propose; controlled systems calculate.** Arithmetic, VAT, thresholds and live prices belong in deterministic services.
- **The engineer remains responsible.** AI output must be reviewable, editable and attributable to supporting evidence.
- **Abstention is a valid outcome.** “Insufficient evidence” is preferable to an unsupported confident answer.
- **Portable evidence.** Promotion research may identify checkpoints and exports, but binaries remain
  in an approved external artifact store; this repository tracks manifests, hashes and evaluation results.
- **Use bounded source authorisation deliberately.** Approved extracts remain externally held and
  preserve provenance, source roles, client boundaries, licences, and deletion lineage.

## Primary external references

- [ICO guidance on AI and data protection](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/artificial-intelligence/guidance-on-ai-and-data-protection/)
- [ICO purpose-limitation guidance](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/data-protection-principles/a-guide-to-the-data-protection-principles/purpose-limitation)
- [Civil Procedure Rules Part 35](https://www.justice.gov.uk/courts/procedure-rules/civil/rules/part35)
- [Practice Direction 35](https://www.justice.gov.uk/courts/procedure-rules/civil/rules/part35/pd_part35)
- [DVLA Vehicle Enquiry API](https://developer-portal.driver-vehicle-licensing.api.gov.uk/apis/vehicle-enquiry-service/v1.2.0-vehicle-enquiry-service.html)
- [Hugging Face object-detection guidance](https://huggingface.co/docs/transformers/tasks/object_detection)
- [Hugging Face multimodal fine-tuning guidance](https://huggingface.co/docs/trl/main/training_vlm_sft)
- [UK government report on Copyright and Artificial Intelligence](https://www.gov.uk/government/publications/report-and-impact-assessment-on-copyright-and-artificial-intelligence/report-on-copyright-and-artificial-intelligence)


# Fine-Tuning Strategy

## Executive conclusion

Fine-tuning is the main training opportunity for the Collision Engineers corpus. It allows strong pretrained models to learn remote vehicle-assessment concepts from a comparatively modest, specialist dataset.

The recommended programme uses separate models or adapters for separate tasks. It does not fine-tune one model on the entire archive.

## Fine-tuning families

### Image classification

Candidate tasks:

- view classification;
- capture-quality flags;
- full-vehicle versus close-up;
- evidence sufficiency;
- impact-region classification;
- repairable/total-loss risk as a research signal;
- identifier or odometer image classification.

Recommended starting point:

- compact pretrained MobileNet/MobileViT for mobile or fast server inference;
- ResNet as a stable benchmark;
- DINO-style ViT features for stronger fine-grained representation.

Image classification needs one or more labels per image. Report-level outcomes should not be copied blindly onto every image in the case.

### Object detection

Candidate targets:

- vehicle body components;
- lamps, wheels and tyres;
- dent/scuff/crack/deformation regions;
- plates, VIN labels and odometer displays;
- visible broken or displaced parts.

Models such as DETR, RT-DETR or D-FINE can be fine-tuned using COCO-style bounding boxes. The Hugging Face object-detection guide demonstrates the required image, category and bounding-box structure. [Hugging Face object detection](https://huggingface.co/docs/transformers/tasks/object_detection)

Start with common components and damage classes. A large sparse taxonomy will produce poor per-class learning and difficult evaluation.

### Segmentation

Segmentation can delineate:

- damaged paint area;
- deformed panel region;
- cracked glazing;
- wheel-rim damage;
- visible scratch or paint transfer.

SAM/SAM2-style models can be adapted from boxes or points, but masks are expensive to create. Segmentation should follow a successful detector and target only use cases where boundaries affect workflow or measurement.

### Language-model fine-tuning

Suitable tasks:

- converting extracted case facts into a controlled narrative;
- matching Collision Engineers tone and terminology;
- classifying query type;
- producing structured JSON from instructions or reports;
- drafting approved response styles;
- rewriting an engineer's notes into report-ready prose.

Training targets should include only:

- Collision Engineers-authored and approved output;
- the evidence available at the time;
- structured source facts;
- approved reference citations where appropriate;
- explicit uncertainty and abstention examples.

Exclude:

- incoming repairer or solicitor language as target text;
- opposing-party opinions as if adopted;
- signatures and disclaimers;
- outdated templates;
- reports superseded for factual error unless labelled as negative examples.

Parameter-efficient fine-tuning such as LoRA is preferred initially. It reduces compute and makes it easier to maintain client-, task- or style-specific adapters without modifying every base weight.

### Vision-language fine-tuning

This is the most strategically interesting but highest-risk route.

Possible inputs:

- multiple case images;
- structured vehicle facts;
- instruction summary;
- current reference excerpts.

Possible outputs:

- visible-component list;
- damage observations;
- evidence sufficiency;
- supporting image IDs;
- missing views;
- candidate repair actions;
- uncertainty;
- structured report draft.

Current tooling supports supervised fine-tuning of multimodal models with single or multiple images. [Hugging Face multimodal SFT](https://huggingface.co/docs/trl/main/training_vlm_sft)

The target should be structured evidence, not an unconstrained final expert opinion.

## Recommended training stages

### Stage 1 — Baselines without fine-tuning

For each task:

- deterministic baseline;
- pretrained zero-shot or frozen-embedding baseline;
- prompt-only language/VLM baseline;
- current human performance and time.

Fine-tuning is justified only if it improves a product metric.

### Stage 2 — Small, clean supervised cohort

Use a deliberately curated dataset with:

- stable schema;
- reviewed labels;
- case-level splits;
- diverse sources and vehicle types;
- explicit negative and insufficient-evidence cases;
- documented rights.

The first run should prove the pipeline and metric, not maximise dataset size.

### Stage 3 — Learning curve

Train on progressively larger subsets, for example:

- 10%;
- 25%;
- 50%;
- 100%.

Plot performance by data volume and class. This reveals whether more annotation or a better taxonomy is the limiting factor.

### Stage 4 — Error-directed expansion

Add data for observed failures:

- glare and dark images;
- uncommon panels;
- vans, taxis, motorcycles or prestige vehicles if in scope;
- hybrids and EVs;
- prior damage;
- subtle damage;
- mixed or incomplete evidence;
- unusual source formats.

### Stage 5 — Shadow deployment

The model runs on real work but cannot alter or send a report. Collect:

- engineer acceptance;
- edits;
- missed findings;
- unsupported suggestions;
- time saved;
- abstention quality;
- source and vehicle subgroup performance.

### Stage 6 — Controlled assistance

Enable only outputs that meet acceptance gates, with audit logging and rollback.

## Training-data construction

Every example should identify:

- task;
- case ID;
- evidence cutoff;
- input artifact IDs;
- source roles;
- target author/reviewer;
- assessment version;
- label confidence;
- split;
- rights state.

For a multi-image case model, use message-style records such as:

```json
{
  "case_id": "pseudonymous-id",
  "images": ["artifact-01", "artifact-02", "artifact-03"],
  "vehicle": {"make": "FORD", "model": "S-MAX"},
  "task": "produce_evidence_grounded_damage_observations",
  "target": {
    "findings": [
      {
        "component": "left_front_wing",
        "damage": "deformation",
        "supporting_images": ["artifact-02"],
        "confidence": "high"
      }
    ],
    "missing_evidence": ["left_front_wheel_closeup"]
  }
}
```

## Preventing harmful fine-tuning

Fine-tuning can make a model sound authoritative without making it factually reliable. Controls should include:

- evidence IDs required for every factual finding;
- no monetary output unless inputs and calculation source are explicit;
- no invented manufacturer method;
- no unsupported roadworthiness opinion;
- forced abstention when evidence thresholds fail;
- negative examples containing inappropriate client influence;
- reviewer-facing confidence and limitations;
- test cases designed to tempt hallucination.

## Separate knowledge from behaviour

Fine-tune for:

- output structure;
- terminology;
- task policy;
- tone;
- classification;
- evidence-grounding behaviour.

Retrieve at runtime:

- current manufacturer methods;
- current valuation guidance;
- salvage rules;
- client-specific SOPs;
- rates and prices;
- legal or regulatory material;
- changing template clauses.

This prevents stale facts from becoming embedded in weights.

## Recommended initial fine-tunes

1. View and image-quality classifier.
2. Component/damage detector for a limited taxonomy.
3. Multi-image evidence-grounding adapter producing structured findings.
4. Report-style adapter operating on engineer-approved structured facts.
5. Email-intent classifier and approved-response drafting adapter.

## Conclusion

Fine-tuning should be a staged, evidence-led programme. The most valuable models will learn Collision Engineers' task structure and language while continuing to retrieve changing professional knowledge and leaving final judgement with the engineer.


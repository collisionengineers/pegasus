# Multi-Image Assessment and Evidence Grounding

## Executive conclusion

A Collision Engineers assessment is a case-level, multi-image reasoning task. A model should analyse the entire evidence set, but every proposed finding must identify which image or document supports it and whether it was directly visible, externally supplied or inferred.

The target product is an evidence-grounded structured draft, not an autonomous final report.

## Why single-image training is insufficient

One case may include:

- general vehicle views;
- multiple close-ups of one impact;
- odometer or identifier images;
- unrelated damage;
- repairer estimates;
- images received later;
- report-embedded duplicates.

No single photograph necessarily shows:

- the affected panel in context;
- the full damage extent;
- adjacent components;
- vehicle identity;
- all information used in the report.

A model trained as `one image → final assessment` will learn weak correlations and source artefacts.

## Proposed input package

```yaml
assessment_input:
  case_id:
  evidence_cutoff:
  vehicle_facts:
  instruction_facts:
  images:
    - image_id:
      source_role:
      received_at:
      view:
      quality:
  documents:
    - artifact_id:
      document_type:
      extracted_fields:
  approved_reference_passages:
```

The evidence cutoff prevents later information from leaking into an earlier assessment.

## Proposed output package

```yaml
assessment_draft:
  evidence_sufficiency:
  vehicle_identity_consistency:
  observations:
    - component:
      damage:
      supporting_image_ids:
      observation_status: visible | reported | inferred
      confidence:
      limitations:
  possible_relatedness:
  missing_evidence:
  candidate_operations:
    - operation:
      supporting_observation_ids:
      method_reference_ids:
      confidence:
  contradictions:
  abstentions:
```

Free-form narrative should be generated only after this structure is accepted or corrected.

## Evidence grounding

Every factual sentence in a later draft should map to:

- one or more image regions;
- an instruction/document passage;
- an external lookup result;
- a repairer-reported later finding;
- a calculation;
- a retrieved approved source;
- an explicit engineer inference.

The interface should allow the engineer to click a finding and see its evidence.

## Model architecture options

### Pipeline architecture

1. image quality/view classifiers;
2. component/damage detector;
3. document and email extractor;
4. evidence-set aggregator;
5. language model for structured synthesis;
6. rules and retrieval;
7. engineer review.

Advantages:

- interpretable components;
- cheaper inference;
- easier evaluation;
- local replacement of one model.

### End-to-end VLM

A multimodal language model receives images and text and produces the structure.

Advantages:

- flexible reasoning;
- fewer hand-built interfaces;
- handles variable evidence.

Risks:

- unsupported findings;
- image-count/context limits;
- poor localisation;
- harder debugging;
- greater compute and privacy exposure.

Recommended approach: pipeline-first, with a VLM used as a constrained synthesiser and benchmarked against an end-to-end research model.

## Training examples

Use engineer-approved case records:

- input evidence available at that date;
- structured findings;
- supporting image IDs;
- missing evidence;
- engineer uncertainty;
- corrected model proposals over time.

Include:

- incomplete cases;
- conflicting images;
- no visible damage;
- unrelated damage;
- multiple impacts;
- images from another vehicle;
- later amendments.

## Context management

Large cases may exceed model image limits. Use:

- duplicate removal;
- view/quality filtering;
- component grouping;
- high-resolution crops plus context thumbnails;
- hierarchical summaries;
- retrieval of only applicable references.

Never discard an image solely on low model relevance without retaining it for engineer access.

## Uncertainty

The model should distinguish:

- confidence that a region is damaged;
- confidence in component identity;
- confidence that damage is incident-related;
- confidence that evidence is sufficient;
- confidence in a proposed operation.

One overall confidence score hides materially different risks.

## Evaluation

Finding-level metrics:

- supported-finding precision;
- supported-finding recall;
- evidence-citation accuracy;
- missed-evidence requests;
- contradiction detection;
- abstention quality;
- engineer acceptance/edit rate.

Case-level metrics:

- report preparation time;
- amendment/supplement rate;
- audit correction rate;
- engineer trust;
- performance by source and vehicle type.

## Interface requirements

- evidence gallery grouped by view/component;
- finding-to-image links;
- original-resolution viewing;
- approve/edit/reject controls;
- reason codes for corrections;
- visible source and timestamp;
- clear distinction between visible and reported findings;
- no hidden automatic insertion into a signed report.

## Conclusion

Multi-image assessment is feasible when the output is an evidence map. The key innovation is not merely allowing a model to see several photographs; it is forcing the model and interface to show exactly what supports each proposed conclusion.


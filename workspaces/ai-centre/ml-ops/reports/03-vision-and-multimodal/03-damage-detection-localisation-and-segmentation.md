# Damage Detection, Localisation and Segmentation

## Executive conclusion

The image archive can fine-tune models to identify visible vehicle components and damage regions. Reports alone are insufficient labels: targeted bounding-box and, later, mask annotation is required.

The first production objective should be evidence navigation and pre-labelling for engineers—not autonomous repair decisions.

## Task decomposition

### Component detection

Identify:

- bumper;
- bonnet;
- wing;
- door;
- sill;
- quarter/side panel;
- lamp;
- grille;
- glass;
- wheel;
- tyre;
- tailgate/boot;
- roof and pillars where visible.

Component detection provides context for damage detection and makes outputs easier to review.

### Damage detection

Initial classes:

- scratch/scuff;
- paint transfer;
- dent;
- crease/deformation;
- crack/split;
- broken/missing;
- displaced/misaligned;
- glass damage;
- wheel-rim damage;
- tyre damage.

Use an `unknown_visible_anomaly` class cautiously; it may help triage but can become a catch-all that hides taxonomy problems.

### Segmentation

Potential uses:

- precise visual overlay;
- damaged-area measurement research;
- distinguishing a long scratch from the whole panel;
- engineer annotation acceleration.

Segmentation does not reveal depth, material compromise or hidden damage. A precise mask is not a repair decision.

## Training architecture

Recommended staged architecture:

1. component detector;
2. damage detector using component context;
3. optional segmentation refinement;
4. rule/learned association to propose a component-damage record;
5. engineer confirmation.

Alternatively, one detector can predict combined classes such as `rear_bumper_scuff`, but the class count grows quickly and limits transfer. Separate component and damage attributes are more maintainable.

## Data preparation

Each image should include:

- case and evidence-event ID;
- image source;
- view;
- capture-quality labels;
- component boxes;
- damage boxes/masks;
- relatedness where professionally reviewed;
- annotation confidence;
- whether damage is visible or only reported elsewhere.

Avoid:

- assigning every report component to every image;
- treating all report operations as visually observable;
- using amended findings as labels for original images without review;
- including exact duplicates across splits.

## Annotation workflow

1. Prepopulate likely components from a pretrained detector.
2. Show report-derived candidate component names.
3. Annotator confirms visible components.
4. Engineer labels damage and causal relatedness on selected cases.
5. Quality reviewer checks difficult examples.
6. Active learning prioritises uncertain and diverse images.

Use COCO-compatible box data for detector portability. [Hugging Face object-detection guidance](https://huggingface.co/docs/transformers/tasks/object_detection)

## Model candidates

Benchmark at least:

- a compact real-time detector for lower-cost inference;
- a transformer detector such as RT-DETR/D-FINE/DETR;
- frozen DINO-style features with a task head;
- a promptable segmentation model for annotation assistance.

Selection should depend on:

- per-class precision/recall;
- high-resolution detail;
- latency;
- exportability;
- licence;
- confidence calibration;
- performance on poor evidence.

## Output contract

```yaml
visible_damage_candidate:
  image_id:
  component:
  component_box:
  damage_type:
  damage_region:
  confidence:
  quality_limitations:
  model_version:
  requires_engineer_confirmation: true
```

The word `candidate` is deliberate.

## Related versus unrelated damage

Damage causation is not an ordinary image-classification problem. It may depend on:

- stated accident circumstances;
- impact direction;
- damage pattern;
- pre-incident evidence;
- vehicle history;
- engineering judgement.

A model may provide a consistency score or retrieve comparable evidence, but an engineer should decide relatedness.

## Severity

Avoid vague labels such as light/moderate/severe until Collision Engineers defines:

- what is being measured;
- whether the label applies to one defect or the whole impact;
- observable criteria;
- relationship to repair action;
- treatment of poor images.

Ordinal severity models can be explored after agreement is measurable.

## Evaluation

Measure:

- per-component and per-damage precision/recall;
- mAP/mAR;
- false positives on undamaged panels;
- missed safety-relevant visible damage;
- performance by view and image quality;
- unknown-vehicle rejection;
- engineer correction rate;
- time saved finding relevant images;
- cross-source and temporal robustness.

Include negative images and unrelated/pre-existing damage.

## Product uses

Low-risk:

- highlight candidate damaged regions;
- group images by component;
- search similar visible damage;
- prepopulate annotation;
- create an evidence contact sheet.

Medium-risk:

- draft structured damage observations;
- suggest missing views;
- suggest likely operations for review.

High-risk:

- autonomous causation;
- autonomous roadworthiness;
- exact repair-versus-replace;
- final estimate directly from detections.

## Recommended pilot

Start with three common impact regions and a limited component/damage taxonomy. Train and evaluate a detector on an untouched case-level test set. Deploy only as an engineer-facing overlay and record corrections.

## Conclusion

Damage detection is feasible and valuable when framed as evidence localisation. The model should make the remote image set easier to inspect and structure, while professional interpretation remains a separate, reviewable stage.


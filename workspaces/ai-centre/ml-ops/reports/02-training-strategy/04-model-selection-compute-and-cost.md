# Model Selection, Compute and Cost Strategy

## Executive conclusion

Collision Engineers should select the smallest model that meets a measured product requirement and can be exported into locally controlled storage. Model choice should follow dataset and benchmark design, not precede it.

Hosted GPU services may be used as temporary compute, but no promoted model should exist only behind a provider endpoint.

## Task-to-model mapping

| Task | Baseline | Likely production family |
|---|---|---|
| PDF/email extraction | Rules plus general OCR/parser | Layout-aware document model only for unresolved fields |
| View/quality classification | MobileNet/ResNet | MobileNet, MobileViT or compact ViT |
| Component/damage detection | Pretrained detector | D-FINE, RT-DETR, DETR or equivalent exportable detector |
| Segmentation | Box-driven foundation segmenter | SAM2-style adapted mask decoder or specialised segmenter |
| Similarity/duplicates | Perceptual hash + frozen embeddings | Domain-adapted vision embeddings if needed |
| Multi-image findings | Prompted VLM | Parameter-efficient fine-tuned VLM |
| Report style | Template + prompted LLM | Small/medium instruction model with LoRA if justified |
| Email routing | Rules + embeddings | Gradient boosting or small language encoder |
| Risk/forecasting | Simple statistical baseline | Calibrated gradient-boosted model |
| Knowledge assistant | Keyword search | Hybrid retrieval + reranker + grounded LLM |

## Vision model tiers

### Edge/mobile tier

Use for live capture guidance:

- blur/exposure;
- vehicle-present;
- coarse viewpoint;
- framing/readiness.

Requirements:

- low latency;
- small package;
- predictable memory use;
- ONNX or platform-native export;
- acceptable performance without a network connection.

### Server vision tier

Use for:

- detailed component detection;
- damage localisation;
- image embeddings;
- batch evidence analysis.

This tier can use larger input resolution and stronger backbones while remaining exportable.

### Multimodal reasoning tier

Use for:

- combining several case images;
- mapping observations to structured findings;
- explaining missing evidence;
- drafting evidence-linked text.

This is the most expensive and should be invoked after cheaper classifiers and extraction have narrowed the task.

## Language model tiers

### Deterministic templates

Best for:

- report headers;
- totals;
- standard declarations;
- fixed fee-note content;
- known calculation language.

### Small local instruction model

Best for:

- rewriting notes;
- extracting structure;
- classifying correspondence;
- producing controlled, short drafts.

### Larger hosted or local model

Best for:

- complex multi-document synthesis;
- nuanced dispute responses;
- multi-image assessment research.

Sensitive live data should be supplied only under approved deployment controls.

## Compute stages

### CPU

Suitable for:

- hashing and deduplication;
- document parsing;
- classical ML;
- small embedding batches;
- ONNX inference;
- evaluation orchestration.

### Single commodity GPU

Suitable for:

- compact image classifier fine-tuning;
- small/medium object detectors;
- LoRA language-model training;
- frozen-encoder experiments.

### Larger single GPU

Suitable for:

- high-resolution detection;
- SAM-style adaptation;
- quantised VLM fine-tuning;
- multi-image inference.

### Multi-GPU

Should be considered only after:

- a smaller model has a proven quality ceiling;
- the dataset and metric justify scale;
- production economics are understood.

## Cost model

Every experiment should budget:

1. data engineering;
2. annotation and engineer review;
3. compute;
4. storage;
5. evaluation;
6. security and governance;
7. integration;
8. ongoing monitoring and retraining.

Annotation and domain review are likely to cost more than early GPU experiments. Cheap training on poor labels does not produce a low-cost system.

Track cost per:

- labelled image;
- reviewed case;
- training run;
- accepted model suggestion;
- minute of engineer time saved;
- prevented report error;
- avoided evidence follow-up.

## Experiment discipline

Each run should declare:

```yaml
experiment:
  task:
  dataset_release:
  model_and_base_revision:
  licence:
  image_or_context_size:
  hyperparameters:
  random_seed:
  hardware:
  duration:
  compute_cost:
  metrics:
  artifact_hashes:
  decision:
```

Stop conditions should be defined before a large run. Examples:

- no meaningful improvement over the baseline;
- unacceptable unsupported-finding rate;
- poor performance on a source holdout;
- inference cost exceeds product budget;
- no engineer-time benefit.

## Local artifact package

Every promoted model should include:

- original framework checkpoint;
- portable export where feasible;
- tokenizer/image processor;
- label map and schema;
- base model revision;
- licence files;
- training configuration;
- dataset and split manifest;
- evaluation report;
- calibration thresholds;
- known limitations;
- SHA-256 hashes;
- offline smoke test;
- rollback version.

## Provider strategy

Provider-neutral rules:

- use private jobs and storage;
- pass secrets through an approved secret store;
- do not publish data or checkpoints by default;
- export results immediately;
- verify offline inference;
- record region and processor;
- delete ephemeral job data according to policy;
- avoid provider-specific features that prevent migration unless the value is explicit.

## Model-selection scorecard

Score candidate models on:

- task accuracy;
- calibration and abstention;
- source/vehicle subgroup robustness;
- inference latency;
- memory and package size;
- exportability;
- base licence;
- training reproducibility;
- vulnerability to unsupported outputs;
- operational cost;
- maintainability.

Accuracy alone is not sufficient.

## Conclusion

The recommended architecture uses compact models for frequent deterministic tasks and invokes larger multimodal models only where they add measurable value. This minimises cost, improves auditability and preserves local control.


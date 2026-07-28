# Training Models From Scratch: Feasibility

## Executive conclusion

“Train a model from scratch” can mean several different things. The answer for Collision Engineers is:

- **General-purpose language foundation model:** not realistic or justified.
- **General-purpose vision or vision-language foundation model:** not realistic with thousands of reports.
- **Small specialist neural model:** technically feasible, but usually inferior to transfer learning.
- **Classical task-specific model:** feasible and potentially valuable.
- **Self-supervised domain adaptation:** feasible as a later experiment, normally beginning with a pretrained backbone.

The strategic objective should be owning the task-specific model and its artifacts, not insisting that every weight starts random.

## Why foundation training is unsuitable

Modern foundation models are trained at a scale far beyond the expected archive:

- OpenAI's CLIP research used approximately 400 million image-text pairs.
- Meta reports that DINOv3 used 1.7 billion images; even the original DINO proof of concept used around one million images.

References:

- [Learning Transferable Visual Models From Natural Language Supervision](https://cdn.openai.com/papers/Learning_Transferable_Visual_Models_From_Natural_Language_Supervision.pdf)
- [DINOv3](https://ai.meta.com/research/dinov3/)

Thousands of Collision Engineers reports could yield tens or hundreds of thousands of images. That is a strong domain corpus, but it lacks the scale and diversity required to learn robust general visual concepts or language from random initialisation.

Foundation training would also introduce:

- high compute cost;
- complex distributed training;
- tokenizer and data-curation requirements;
- greater memorisation risk;
- difficult safety evaluation;
- poor general knowledge compared with existing models;
- a long research cycle before any operational value appears.

## What can be trained from scratch

### Tabular classifiers and regressors

Once reports are structured, Collision Engineers can train models such as gradient-boosted trees, random forests, calibrated logistic regression or small neural networks for:

- repairable versus total-loss probability;
- supplement/amendment risk;
- likelihood that more evidence will be required;
- estimate variance or reserve range;
- valuation-dispute likelihood;
- likely turnaround delay;
- email routing;
- report-QA anomaly scores.

Thousands of cases can be useful for these tasks, provided the labels are stable and leakage is controlled.

### Small image classifiers

A compact convolutional model can be trained from scratch for:

- blur or exposure classification;
- document-versus-vehicle image classification;
- a small number of viewpoint classes;
- presence of an odometer or registration plate.

However, a pretrained MobileNet, MobileViT, ResNet or vision transformer will normally converge faster and generalise better. Training from scratch should be a benchmark, not the default.

### Domain vocabulary and extraction models

Small sequence taggers can be trained to extract:

- references;
- registrations;
- vehicle details;
- costs;
- report headings;
- parts and operations.

Rules and pretrained language encoders may still outperform a scratch model, especially when formats change.

### Retrieval ranking

A small reranker could be trained from Collision Engineers relevance judgements. This is a realistic “owned model” opportunity because the task is narrow: rank approved passages for a query. It does not require creating a general language model.

## Self-supervised domain learning

Unlabelled vehicle photographs can be used for self-supervised representation learning. The model learns that augmented views of the same image—or images from the same evidence set—should have related representations.

Possible benefits:

- better similarity search over damaged-vehicle images;
- domain-adapted features for low-label tasks;
- duplicate and outlier detection;
- improved downstream classification.

Risks:

- learning source, bodyshop or camera artefacts instead of engineering concepts;
- memorising repeated vehicles or templates;
- limited gain over strong pretrained representations;
- expensive experimentation without a defined downstream metric.

Recommended approach:

1. benchmark a frozen pretrained encoder;
2. fine-tune or adapt it on labelled tasks;
3. try domain self-supervision only if the benchmark shows a representation gap;
4. compare gains on an untouched case-level test set.

## Defining model ownership correctly

Collision Engineers can fully own a model even when it starts from open or commercially licensed pretrained weights, subject to the base licence.

Ownership and control should mean:

- the fine-tuned checkpoint is stored locally;
- the training code and exact configuration are retained;
- the base model and licence are recorded;
- the dataset release is reproducible;
- a portable inference export such as ONNX is produced where feasible;
- hashes and manifests prove artifact identity;
- offline tests demonstrate that the model runs without the training provider;
- no provider-only endpoint is the sole usable copy.

This is more valuable than an underperforming random-weight model trained solely to claim that it was built “from scratch”.

## Decision criteria

Train from scratch only when all are true:

- the task is narrow and clearly labelled;
- a simple baseline is appropriate;
- pretrained models offer no material benefit or create unacceptable licensing constraints;
- the dataset is sufficiently large and balanced;
- the evaluation set represents real deployment;
- the compute and maintenance cost is proportionate.

Fine-tune when:

- the task requires visual or language understanding;
- labels are limited;
- pretrained representations are available under acceptable terms;
- fast iteration and strong generalisation matter.

Use no learned model when:

- rules or arithmetic fully determine the answer;
- current external data is authoritative;
- an auditable lookup is safer and simpler;
- the task is too rare to evaluate.

## Recommended scratch-model experiments

The most defensible early experiments are:

1. gradient-boosted supplement-risk model;
2. calibrated evidence-insufficiency classifier;
3. email intent router;
4. report-total anomaly detector;
5. small image-quality baseline compared with a pretrained classifier.

## Conclusion

Collision Engineers can train useful models from scratch, but those models should be narrow. For vision, language and multimodal assessment, fine-tuning or adapting pretrained models is the technically and commercially sound route. Model ownership should be secured through portable artifacts and reproducible training, not random initialisation.


# ML operations

> **Current authority:** this file is the single current owner of experiment, data, evaluation,
> training, pipeline, and model-registry contracts for this workspace.
>
> `Pegasus.Core` and authorised humans own all business facts, professional conclusions, decisions,
> permissions, thresholds, approvals, and activation choices. Nothing in this area creates or
> overrides those facts or decisions.

This area contains reproducible, locally controlled ML experiments and their supporting data,
pipeline, evaluation, training, and artifact records. It does not define a production model,
business workflow, or autonomous decision-maker.

## Contract precedence

1. This README defines the current ML-operations contract.
2. Versioned manifests instantiate the contract for a specific dataset, experiment, evaluation, or
   artifact.
3. Files under `datasets/`, `pipelines/`, `training/`, `evaluation/`, and `registry/` (none of
   which exist yet — see the layout table) may add implementation detail once created but must not
   weaken or redefine this contract.
4. Historical strategy and research material may support a current hypothesis only when its dated
   evidence identity, checksum status, limitation, and current experiment link are recorded below.
   It is not runtime, product, architecture, custody, or approval authority.

## Layout and custody

| Location | Purpose | Repository treatment |
|---|---|---|
| Repository-root `corpus/ai-centre/` | Approved private source inputs | Ignored, immutable or versioned, access-controlled, and never committed |
| `reports/` | Dated, qualified evidence records (currently the 01-data-readiness sample-corpus inventory) | Tracked; the only ml-ops content besides this README today |
| `datasets/` | Dataset recipes, schemas, taxonomies, synthetic fixtures, manifests, and cards | Intended; tracked when created |
| `pipelines/` | Deterministic inventory, extraction, OCR, hashing, deduplication, lineage, minimisation, annotation, splitting, and deletion code | Intended; tracked when created |
| `training/` | Configuration-driven training entry points and environment definitions | Intended; tracked when created |
| `evaluation/` | Baselines, sealed-suite definitions, challenge slices, calibration, abstention, human-review protocols, and regression gates | Intended; tracked when created |
| `registry/` | Model cards, reviewed promotion manifests, licences, inference contracts, artifact hashes, expiry, and rollback instructions | Intended; tracked when created |
| Repository-root `artifacts/` | Generated datasets, run outputs, reports, logs, metrics, and other payloads | Generated; not source authority |
| Approved external artifact store | Binary checkpoints, adapters, exports, processors, and large immutable outputs | Referenced by immutable hash; not committed |

Complete source-system archives must not be copied into Git or treated as an implicit training
dataset. An approval for one bounded purpose, source, or experiment does not imply approval for any
other use.

The [19 July 2026 sample-corpus inventory](reports/01-data-readiness/01-sample-corpus-inventory.md) is retained as qualified historical evidence of the 300-file source review and its stated limitations. It is not permission to reopen, modify, publish, or derive new claims from the private corpus.

## No dormant runtime

This workspace contains explicit commands, configuration, fixtures, and evidence records only.

- No daemon, scheduled training job, live mailbox reader, model endpoint, background indexer,
  autonomous agent, or file watcher is authorised by this contract.
- Every execution must be deliberately started with an experiment or pipeline manifest.
- A run must stop when its declared command completes or fails.
- Hosted resources must not remain active merely because configuration exists in this repository.
- Registry promotion records evidence and approval state; they do not deploy or activate a model.
- Live reads, writes, sends, recipient selection, case mutation, signing, or external side effects
  require a separately approved implementation owned outside this contract.

## Shared technical invariants

1. **The learning unit is a case at a point in time.** An image, message, or final report is not an
   independent truth record.
2. **Evidence is immutable and versioned.** Improved extraction creates a derivative; it never
   overwrites the source artifact.
3. **Source role is mandatory.** Instructions, third-party assertions, references, lookups,
   calculations, authored findings, and approved outputs remain distinguishable.
4. **Time is part of provenance.** Inputs must be limited to evidence available at the target
   `evidence_cutoff_at`.
5. **Unknown is a valid state.** `unknown`, `not_visible`, `not_assessable`,
   `insufficient_evidence`, and abstention must not be coerced into positive or negative labels.
6. **Models propose; deterministic systems calculate.** Arithmetic, tax, totals, thresholds,
   effective-dated tables, and accepted scenario calculations are reproducible outside model
   weights.
7. **Changing knowledge is versioned.** Volatile references are retrieved or configured with
   identity, effective dates, and supersession state rather than treated as timeless memorised
   facts.
8. **Every material finding is traceable.** A finding must resolve to exact artifacts, regions,
   source passages, structured lookups, calculations, approved references, or an explicitly
   identified human inference.
9. **Splits are constructed after deduplication.** Every artifact, version, receipt, and derivative
   associated with a case remains in the same split.
10. **Evaluation is case-, time-, and source-aware.** Random image or message splits are diagnostic
    only and cannot establish generalisation.
11. **The sealed holdout is never tuned on.**
12. **No capability claim exists without a dated evaluation record and immutable hashes.**
13. **No business fact or decision is inferred from model confidence, correspondence wording,
    historical correlation, or registry status.**

# Experiment ownership

## Lifecycle

Every concrete experiment must have one manifest and one current state:

```text
design
  → approved_to_build
  → data_ready
  → baseline_complete
  → training_or_method_complete
  → sealed_evaluation_complete
  → shadow_evaluation_complete
  → promoted | held | stopped | superseded
```

A state transition requires evidence appropriate to the transition. Missing approval, provenance,
hashes, or required evaluation causes the transition to fail closed.

```yaml
experiment:
  id:
  title:
  category:
  task:
  state:
  owner:
  authorised_purpose_id:
  approval_ids: []
  hypothesis:
  output_boundary:
  dataset_release:
  split_manifest:
  evidence_cutoff_rules:
  baselines: []
  primary_metrics: []
  subgroup_definitions: []
  challenge_suite:
  calibration_measures: []
  abstention_rule:
  preregistered_thresholds:
  stop_conditions: []
  code_revision:
  environment_lock:
  artifact_hashes: []
  opened_at:
  evaluated_at:
  decision:
    outcome: promoted | held | stopped | superseded
    decided_at:
    decision_owner:
    evidence_record:
```

Unstarted designs are hypotheses, not evidence that a capability works. A stopped experiment retains
only the records needed to explain the current decision, prevent repeated invalid work, or support a
still-current successor experiment.

## External experiment approval and cleanup

Collision Brain provider experiments also use the provider-specific [evaluation pass and stop rules](../services/collision-brain/docs/provider-evaluation.md#pass--stop-rules) and [hosted cleanup procedure](../services/collision-brain/docs/operations.md#hosted-experiment-cleanup). Those evidence and procedure owners instantiate this workspace-wide experiment contract; they do not replace it.

An experiment manifest's `approval_ids`, purpose, output boundary, stop conditions, code/environment identity, artifact hashes, and decision record must remain sufficient to identify the exact authorised run. Before a run uses a hosted model, provider, account, project, subscription, external storage, or billed service, the approval must additionally name the exact target, region, service/model/SKU, operations, input class, duration and expiry, spending ceiling and stop behavior, identities, retained outputs, rollback source, and cleanup targets. Dataset or experiment approval alone does not authorize provisioning, external transfer, paid use, deployment, or deletion.

Every externally backed run retains a cleanup record linked from its manifest or decision evidence:

1. stop experiment-only processes, jobs, schedules, endpoints, and queues at completion, failure, expiry, or a stop condition;
2. retain and hash only approved artifacts and evidence;
3. revoke experiment-only credentials and remove local secret copies without logging them;
4. under separate exact-target cleanup approval, remove only named disposable external copies and resources, never `corpus/`, source evidence, shared services, predecessor resources, or unlisted targets;
5. verify the final external inventory, scheduled work, billing state, provider retention/deletion state, backups/logs, residual resources, and charges; and
6. record every residual and provider-controlled expiry.

A run may be marked stopped while cleanup is pending, but it cannot be promoted or represented as closed until cleanup is verified or the authorised owner explicitly records the remaining obligation.

## Current experiment categories

### A. Data, extraction, case assembly, and deterministic QA

| ID | Experiment | Hypothesis and baseline | Primary evidence | Gate |
|---|---|---|---|---|
| A1 | Corpus inventory and canonical case graph | Deterministic inventory and multi-signal linking can create an auditable case/event graph. Compare with manual reconstruction. | Link precision/recall, attachment recall, version accuracy, duplicate audit, timeline completeness, cross-case errors | Stop on an incorrect automatic merge, overwritten source, untracked artifact, or irreconcilable chronology |
| A2 | Tiered document and message extraction | Format-specific parsing with plausibility checks and OCR fallback is more reliable than one generic parser. Compare parser-only, renderer-assisted, and OCR paths. | Decode success, field accuracy, page/attachment recall, text plausibility, coordinates, total reconciliation, classified failure rate | Failed or implausible extraction remains quarantined and cannot enter training |
| A3 | Historical message-to-case assembly | Exact and fuzzy signals can associate bounded, read-only messages and attachments with cases without cross-case leakage. Compare exact rules with retrieval or classifier assistance. | Case-match precision/recall, catastrophic merge count, source-role accuracy, duplicate/supersession accuracy, review time | Low-confidence matches remain unassigned; any cross-case disclosure or mutation stops progression |
| A4 | Deterministic QA linter | Identity, version, evidence-cutoff, arithmetic, citation, attachment, and required-field rules can catch known defects with acceptable reviewer burden. | Precision/recall by defect, material-defect escape rate, alerts per case, reviewer disposition and time | Learned anomaly ranking cannot begin until the rule baseline and alert burden are acceptable |
| A5 | Learned anomaly ranking | Contextual ranking can find incremental defects beyond deterministic rules. | Incremental material defects, alert precision, calibration, contributing features, source/time stability | Suggestions only; stop if burden rises without incremental detection or explanations are not reviewable |
| A6 | Structured payload and deterministic rendering | Accepted structured facts plus deterministic calculations can reproduce internally consistent outputs. | Schema validity, calculation reconciliation, stable rendering hashes, source-link resolution | Stop or narrow scope if required variation cannot be represented without untracked free text |

### B. Vision and multimodal evidence

| ID | Experiment | Hypothesis and baseline | Primary evidence | Gate |
|---|---|---|---|---|
| B1 | View and capture-quality classification | Compact models can identify view, blur, darkness, glare, obstruction, context/detail sufficiency, duplicates, and incomplete evidence. Compare deterministic checks, frozen embeddings, scratch compact models, and transfer learning. | Macro F1, per-flag precision/recall, missing-view recall, false-ready rate, calibration, source/device/time slices | Stop if incomplete evidence is frequently marked ready, guidance removes useful context, or abstention is unreliable |
| B2 | Identifier OCR and case consistency | OCR plus validation and structured case facts can expose registration, VIN, odometer, and mixed-vehicle conflicts better than visual identity alone. | Exact-string accuracy, character error rate, unit accuracy, top-K alternatives, conflict recall, false-conflict rate, low-quality abstention | Never silently correct observed text; ambiguity or conflict requires review |
| B3 | Component and visible-damage detection | Separate component and damage labels can support reviewable localisation. Compare an unadapted pretrained detector with task adaptation. | Per-class precision/recall, mAP/mAR, undamaged-region false positives, poor-evidence performance, source/time/vehicle slices | Boxes precede masks; no operation, severity, causation, or hidden-condition conclusion follows from a box |
| B4 | Targeted segmentation | Masks add measurable value only where boundary precision improves a defined downstream metric. Compare with boxes, points, and unadapted segmentation assistance. | Mask quality, annotation time, overlay usefulness, downstream metric gain | Do not continue if masks add burden without incremental value |
| B5 | Multi-image evidence grounding | A pipeline or constrained multimodal method can produce structured findings, contradictions, and evidence requests linked to exact artifacts. Compare deterministic extraction, prompt-only methods, and pipeline-first aggregation. | Supported-finding precision/recall, citation correctness, unsupported-finding rate, contradiction detection, missing-evidence recall, calibration, edit/reject rate | Every finding must declare `visible`, `reported`, or `inferred`; unsupported or post-cutoff findings fail |
| B6 | Supplement or amendment risk | Time-valid case variables may predict the probability and family of later change. Compare simple statistics, calibrated tabular models, and image features only after the tabular baseline. | Brier score, calibration, risk-coverage, temporal holdout performance, change-family analysis | Stop if later evidence cannot be excluded, chronology is incomplete, or “no known change” cannot be separated from confirmed no change |
| B7 | Scratch versus transfer benchmark | Random initialisation may be adequate only for a narrow task with balanced labels. | Matched learning curves, generalisation, calibration, run duration, source/time holdouts | General-purpose language, vision, or multimodal foundation training from random initialisation is out of scope |
| B8 | Self-supervised vision adaptation | Domain adaptation may improve representations only where a frozen encoder has a measured gap. | Untouched downstream metrics, duplicate/outlier retrieval, source holdout, memorisation checks | Stop if gains are negligible or derive from source, camera, template, or repeated-vehicle artifacts |

### C. Assessment, estimate, valuation, and controlled narrative research

| ID | Experiment | Hypothesis and baseline | Primary evidence | Gate |
|---|---|---|---|---|
| C1 | Estimate-line normalisation | Language-assisted mapping can align source wording to a canonical component/operation taxonomy. Compare rules and embedding similarity. | Mapping precision/recall, side/component errors, source-role preservation | Original wording and source remain available; low-confidence mappings require review |
| C2 | Repair-operation proposals | Accepted observations and applicable references may support constrained operation proposals. Compare rules, retrieval, and simple multilabel models. | Operation precision/recall, unsupported-operation rate, safety-related omission rate, evidence citation | Proposal only; missing current methods or sources causes abstention, not invention |
| C3 | Total and dependency anomaly checks | Deterministic arithmetic plus contextual anomaly ranking can detect inconsistent totals, duplicate lines, and missing dependencies. | Arithmetic defects prevented, genuine-anomaly precision, alert burden, incremental value over rules | Learned output must not replace or obscure deterministic calculations |
| C4 | Economic-outcome triage | Time-valid structured features may rank cases for review. Compare transparent statistical and calibrated tabular baselines. | Calibration, false-positive/false-negative profile, risk-coverage, temporal and subgroup performance | Triage evidence is evaluated separately from calculation and does not establish an outcome |
| C5 | Visible safety-indicator prompts | Vision may flag visible concerns or missing views for escalation. | Recall of adjudicated visible concerns, false-reassurance rate, citation accuracy, abstention, rare-condition slices | Absence of a flag is never evaluated or represented as affirmative clearance |
| C6 | Salvage range and anomaly research | Effective-dated historical records may support ranges and outlier review. | Interval coverage, temporal drift, outlier precision, segment performance | Current sourced inputs remain explicit; no stale or source-free amount may be generated |
| C7 | Comparable ranking and valuation anomaly research | Learning-to-rank may prioritise current sourced comparables and expose mismatches. | Relevance at `k`, stale/duplicate/derivative mismatch detection, interval coverage, temporal residuals | No image-to-exact-price target; historical amounts are not current facts |
| C8 | Controlled narrative adapter | Accepted structured facts, locked calculations, and cited references may be rendered into concise prose. Compare deterministic templates, retrieval plus prompting, and adaptation only if a measured gap remains. | Factual consistency, unsupported assertions, citation correctness, uncertainty preservation, copied third-party language, edit/reject rate | Unsupported text is removed or marked incomplete; fluent output cannot compensate for missing provenance |
| C9 | Claim-to-source validation | Automated checks can verify that report statements resolve to evidence, calculation, or explicit inference. | Source resolution, temporal leakage detection, contradiction recall, source-role accuracy | Unresolved contradictions remain visible and block progression where material |

### D. Correspondence and workflow intelligence

| ID | Experiment | Hypothesis and baseline | Primary evidence | Gate |
|---|---|---|---|---|
| D1 | Message purpose and intent routing | Narrow, reviewed categories may outperform keyword rules. | Macro F1, source holdout, PII leakage, error by message role | Incoming wording remains a sourced statement and cannot become an accepted fact |
| D2 | Evidence-grounded correspondence drafting | Retrieval of accepted facts and approved examples may produce useful shadow drafts. Compare templates and retrieval-only prompting before adaptation. | Question coverage, factual consistency, citation accuracy, unsupported assertions, wrong-version/case errors, edit/reject rate | No sending, recipient selection, attachment selection, commitment, or opinion change |
| D3 | Turnaround and queue-risk forecasting | Time-aware event models may improve calibration over stage-median and due-date rules. | Calibration/error, overdue recall, blocker visibility, segmented errors, value over baseline | Internal handling and external waiting remain separate; stop if simple rules are not beaten |
| D4 | Missing-evidence prediction | Instruction-time evidence may predict likely missing documents or views. | Precision/recall, lead time, false prompts, evidence-completion effect | Later case information must not enter features |
| D5 | Amendment or query prediction | Evolving event data may identify cases requiring additional review. | Precision, calibration, reviewer acceptance, temporal/source slices | Review candidate only; no automatic case alteration |
| D6 | Outcome-linked language signals | A separately governed classifier may surface relevant passages for contextual review. | Reviewer agreement, false-signal rate, source/outcome correlations, reliance monitoring | Signal only; it must not infer intent, misconduct, truth, or a required decision |

### E. Retrieval, training methods, feedback, and drift

| ID | Experiment | Hypothesis and baseline | Primary evidence | Gate |
|---|---|---|---|---|
| E1 | Permission- and date-aware retrieval | Metadata filtering, hybrid retrieval, and reranking can improve applicable-source discovery. | Recall@K, top-result applicability, citation correctness, stale-source rate, conflict detection, no-result rate | Permission, client, date, and applicability filtering occurs before semantic ranking |
| E2 | Retrieval reranker | Reviewed relevance judgments may improve ranking over keyword, semantic, and hybrid baselines. | Recall@K, applicability, citation correctness, source/time holdouts | Stop if the reranker overfits wording or increases stale/wrong-scope retrieval |
| E3 | Parameter-efficient adaptation | Adaptation may improve a narrow task only after a simpler or prompt-only baseline shows a measured ceiling. | Sealed-set gain, calibration, regressions, portability, offline reproduction | No base family is preselected; full-model escalation requires evidence that smaller approaches are insufficient |
| E4 | Governed active learning | Representative plus hard-case sampling may improve a challenger without contaminating evaluation. | Challenge and ordinary-case results, correction recurrence, label agreement, calibration | Test cases and near duplicates are excluded; accepted model output is not ground truth without review metadata |
| E5 | Knowledge-currentness controls | Effective-dated retrieval and deterministic configuration may reduce stale-reference errors. | Stale/no-result rates, unsupported citations, adversarial stale examples, regression results | Update retrieval or rules before retraining when the model task has not changed |
| E6 | Drift and recalibration | Monitoring may distinguish input, label, outcome, and retrieval drift. | Distribution shifts, unknown/OCR failure, confidence, abstention, edits, retrieval failures, source/time slices | Recalibrate only where ranking remains useful; retrain only for demonstrated representation drift; redesign if the task changed |
| E7 | Portability and offline equivalence | A complete bundle can reproduce inference and evaluation without a provider endpoint being its sole usable copy. | Hash verification, rebuild result, offline smoke test, export equivalence | Promotion fails if export, reconstruction, dependency locking, or smoke testing fails |

# Dataset contract

## Source and use-state boundary

Every source family and artifact must carry a use state assigned through an approval owned by
`Pegasus.Core` or an authorised human:

```text
approved_training
approved_evaluation
retrieval_only
operational_reference_only
aggregate_only
pending
prohibited
```

The pipeline records this state; it does not infer or grant it. `pending`, absent, expired, or
conflicting approval fails closed.

Raw source material is evidence and recovery storage, not a training dataset. Purpose-specific
datasets are generated from the governed case graph using explicit selection and transformation
rules.

## Canonical case-at-time model

| Entity | Required fields and constraints |
|---|---|
| Case | Pseudonymous `case_id`, open/close times, status, references, incident date, assessment mode, source/client boundary, retention class, rights/use state |
| Vehicle | Tokenised identifiers where needed, make/model/derivative/body, registration year, powertrain, odometer with source, history indicators; raw identifiers excluded unless the approved task requires them |
| Instruction | Receipt time, sender role, requested outputs, stated circumstances, deadlines, original source artifact; requests and assertions are not adopted facts |
| Artifact | Immutable ID, case, type, filename, MIME type, SHA-256, perceptual hash where relevant, byte length, source system/role, parent message, duplicate relations, storage/access class |
| Evidence event | Occurrence time, receipt time, engineer-seen time where available, event type, source role, grouped artifact IDs, supersession state |
| Image observation | Exact artifact version, view, visible regions, quality, identifiers, annotations, relatedness, annotator, confidence |
| Assessment | Version/type, creation time, `evidence_cutoff_at`, parent version, author/reviewer role, approval state, outcome fields, uncertainty |
| Finding | Component/location, observation or claim, provenance class, supporting and contradicting artifacts, visible/reported/inferred state, confidence, review state |
| Repair operation | Component/action, operation family, method reference, supporting finding IDs, proposal/approval state |
| Estimate | Currency, source/effective date, labour/rate, parts, paint, specialist, subtotal, tax, total, and deterministic rule version |
| Valuation | Valuation date, source evidence, retrieved/effective dates, adjustments, rationale, and approval state |
| Message | Thread/direction, sender/recipient roles, sent/received time, purpose, body artifact, attachment IDs, adoption-as-fact state |
| Knowledge item | Source identity, publisher, domain, effective dates, retrieval date, scope, rights/use state, supersession, review owner, content hash |

Names, addresses, contacts, and raw external identifiers belong in a restricted identity layer.
Pseudonymous identifiers are used for analytics and general training. Transformations such as
masking, redaction, OCR, thumbnails, or crops retain parent lineage.

## Event and provenance classes

| Class | Meaning |
|---|---|
| `visual_observation` | Direct support from identified image regions |
| `documented_fact` | Stated in supplied documentation |
| `external_lookup` | Returned by an identified structured or reference source |
| `reported_later_finding` | Supplied after the original evidence cutoff |
| `human_inference` | Explicit professional interpretation by an authorised human |
| `third_party_assertion` | A sourced claim not independently adopted |
| `calculated_value` | Deterministic result from recorded inputs and rule versions |
| `model_proposal` | Experimental output awaiting review |

A model may receive multiple classes as context, but each output claim must preserve its supporting
class and links. A more recent message does not automatically supersede an earlier fact. Conflicting
values coexist until an authorised resolution is recorded.

## Example-level training record

```yaml
example:
  example_id:
  task:
  case_id:
  evidence_cutoff_at:
  input_artifact_ids: []
  source_roles: []
  assessment_version:
  target:
  target_author:
  target_reviewer:
  approval_state:
  label_confidence:
  rights_state:
  split:
  parent_dataset_release:
  transformation_lineage: []
```

Eligible targets are reviewed outputs aligned with evidence available at the cutoff. Incoming
language, signatures, boilerplate, unsupported findings, later evidence, and superseded errors are
excluded or explicitly labelled for a separate task.

Model-generated material remains labelled as model-generated and cannot re-enter training as
human-authored truth without review and approval metadata.

## Artifact entry gate

An artifact may enter a dataset only when:

- its source and authorised purpose are identified;
- extraction succeeded or the failure is explicitly classified;
- malware and secret checks passed;
- case linkage meets the declared confidence threshold or was confirmed;
- exact, visual, and semantic duplicate relationships were resolved;
- source role, timestamps, and access/use state are present;
- version and evidence-event relationships are known where applicable;
- personal data was minimised to the task;
- required labels pass schema validation;
- deletion and correction lineage is sufficient to rebuild affected releases.

## Dataset release manifest

```yaml
dataset_release:
  name:
  version:
  built_at:
  purpose:
  authorised_purpose_id:
  source_snapshot:
  source_snapshot_hash:
  query_or_selection_rules:
  selection_code_revision:
  included_case_ids_hash:
  included_artifact_manifest_hash:
  schema_version:
  taxonomy_version:
  redaction_version:
  annotation_version:
  pseudonymisation_key_version:
  duplicate_cluster_manifest:
  split_manifest:
  file_hashes:
  measured_statistics:
    observation_period:
    counts_after_deduplication:
    distributions:
    missingness:
  known_limitations:
  deletion_lineage_version:
  approval_ids: []
```

Counts and distributions belong only in dated, immutable data cards. They must not be copied into
this README as evergreen corpus claims.

## Deduplication and split contract

Splits are built once, registered centrally, and consumed by training jobs. Jobs must not regenerate
their own partitions.

1. Freeze and hash the authorised source snapshot.
2. Compute cryptographic hashes for byte identity.
3. Compute conservative visual similarity for resized or recompressed images.
4. Identify semantic document relations such as exact duplicate, template-equivalent, original,
   audit, amendment, addendum, or correction.
5. Preserve every receipt and container relationship even when one content object backs them.
6. Build connected groups containing:
   - all artifacts and versions from one case or claim;
   - original, audit, amended, and addendum records;
   - exact, visual, and semantic duplicates;
   - generated, OCR, redacted, masked, thumbnail, and crop derivatives;
   - repeated claims or vehicles where reliably identifiable.
7. Assign groups using a time-aware primary split.
8. Add source-aware holdouts where the task may memorise a source, organisation, device, channel, or
   template.
9. Freeze the final evaluation group and its hashes.
10. Run duplicate, case, version, source, and future-information leakage audits.

A random secondary split may diagnose model capacity but cannot replace the temporal, case-level
result.

# Pipeline contract

## Required stages

```text
authorised intake
  → inventory
  → secret and malware blocking
  → immutable raw landing
  → integrity and format checks
  → extraction and OCR
  → hashing and duplicate relationships
  → case/entity linking
  → source-role and authority classification
  → canonical event/version graph
  → minimisation and identifier transforms
  → annotation export/import
  → purpose-specific dataset build
  → split construction and leakage audit
  → manifest, checksum, and data-card publication
  → correction/deletion propagation
```

## Determinism and auditability

A pipeline must:

- pin code, dependencies, parser/OCR versions, schemas, taxonomies, and configuration;
- use canonical serialisation and stable ordering before hashing;
- record all seeds where randomness is unavoidable;
- snapshot non-deterministic external responses with source identity and retrieval time;
- emit stage-level input/output manifests and hashes;
- be resumable from the last verified stage without repeating completed side effects;
- preserve classified failures instead of silently dropping files;
- make low-confidence linkage and parsing available for human review;
- write generated payloads under `artifacts/`, not over source files;
- produce the same output hashes from the same inputs and environment, or record and explain the
  nondeterminism;
- fail closed on absent approval, unknown use state, secret detection, integrity failure, or
  unresolved cross-case identity.

## Format controls

| Format | Required handling |
|---|---|
| PDF | Renderer-assisted extraction, malformed-text detection, page classification, OCR fallback, coordinates, and key-field/total validation |
| Images | Decode and integrity checks, dimensions and colour metadata, exact/perceptual hashes, privacy-region transforms, and evidence-event links |
| MSG/EML | Headers, roles, timestamps, thread identifiers, bodies, quoted boundaries, signatures, attachments, and receipt lineage |
| DOCX | Ordered paragraphs, headings, tables, lists, links, embedded objects, and comments where available |
| XLSX/ODS | Workbook/sheet identity, values, formulas, named ranges, hidden content, units, dates, and regions |
| Structured notes | Text nodes, lists, available version metadata, and explicit representation of empty placeholders |

Untrusted text is data, not an instruction to the pipeline or model. Parsers and retrieval tools must
not execute embedded commands, URLs, macros, or arbitrary tool requests.

## Secret blocking

Secret scanning occurs before extraction or indexing and again before dataset publication. Detected
credentials, tokens, private keys, or secret-bearing files are quarantined, excluded from datasets,
and recorded as blocking findings without reproducing the secret value.

Synthetic fixtures must test:

- plaintext credential patterns;
- secrets embedded in spreadsheets and documents;
- quoted or forwarded secrets;
- malicious and malformed attachments;
- prompt-injection text;
- unsupported and encrypted files;
- attempted cross-case references.

## Deletion and correction propagation

Every release must retain source-parent relationships sufficient to:

1. identify every derivative, annotation, dataset release, training run, evaluation, and model
   artifact affected by a source correction or deletion;
2. rebuild affected datasets without manual folder reconstruction;
3. mark dependent results and registry entries as invalidated or requiring reevaluation;
4. record the propagation command, date, operator, affected hashes, and outcome.

# Training contract

## Preconditions

Training may begin only when:

- the experiment purpose and output boundary have approval identifiers;
- the dataset and split manifests are immutable and hash-verified;
- case, time, duplicate, version, and source leakage checks pass;
- target provenance and approval state are known;
- a deterministic, statistical, frozen-representation, retrieval, prompt-only, template, or human
  baseline exists as applicable;
- primary metrics, challenge slices, calibration measures, abstention behavior, and stop conditions
  were declared before viewing sealed results;
- the base model or representation and its exact licence record were verified for the run;
- private input, secret, provider, region, and export controls are configured;
- a portable artifact and rollback plan exists.

General-purpose language, vision, or multimodal foundation training from random initialisation is
not a current experiment. Scratch training is limited to narrow classifiers, regressors, anomaly
detectors, rerankers, or matched baselines where the task and labels justify it.

Adaptation is conditional rather than presumed necessary. It starts only after a simpler baseline
demonstrates a measurable deficit.

## Training sequence

1. Establish deterministic, statistical, frozen-embedding, retrieval, prompt-only, template, and
   human baselines as appropriate.
2. Validate the end-to-end path on a small, reviewed cohort.
3. Run learning curves over progressively larger fractions selected by the experiment.
4. Diagnose errors by case, time, source, vehicle, evidence quality, and challenge condition.
5. Expand data for observed failures rather than indiscriminately increasing volume.
6. Re-run untouched validation and challenge suites.
7. Evaluate once on the sealed holdout.
8. If gates pass, perform shadow evaluation with no business effect.
9. Record `promoted`, `held`, or `stopped`; do not infer activation from a successful training job.

## Reproducible run record

```yaml
training_run:
  run_id:
  experiment_id:
  started_at:
  completed_at:
  authorised_purpose_id:
  approval_ids: []
  code_revision:
  command:
  environment:
    lockfile_hash:
    container_or_system_identity:
    library_versions:
  data:
    dataset_release:
    dataset_manifest_hash:
    split_manifest_hash:
    evidence_cutoff_rules:
  model:
    base_identity:
    base_revision:
    base_hash:
    licence_record:
    architecture:
    adapter_or_head:
  inputs:
    image_or_context_size:
    feature_and_transform_versions:
  hyperparameters:
  random_seeds: []
  hardware:
    device_types:
    device_count:
    memory:
  provider:
    identity:
    region:
    job_id:
  authorisation:
    account_id:
    spending_cap_approval_id:
  duration:
  actual_incurred_cost:
  outputs:
    checkpoint_hashes: []
    export_hashes: []
    processor_hashes: []
    log_hashes: []
  evaluation_suite:
  outcome:
```

`actual_incurred_cost` is recorded only after a run and is not a forecast. No estimated cost,
duration, throughput, or capacity claim belongs in a promotion record as if it were measured
evidence.

## Training security

- Inputs and jobs are private by default.
- Secrets are supplied only through an approved secret mechanism.
- Raw case data is not published to dataset or model services.
- External outputs are exported promptly to controlled artifact storage.
- Dataset and model exports are hash-verified and logged.
- Service identities for ingestion, training, evaluation, and artifact access remain separate.
- Training jobs cannot access the sealed suite except through the controlled evaluation path.
- Restricted identifiers are excluded or masked unless the approved task explicitly requires them.
- A hosted endpoint cannot be the sole usable copy of a promoted artifact.

# Evaluation contract

## Evaluation layers

| Layer | Required measures |
|---|---|
| Artifact processing | Decode success, page/attachment recall, extraction precision/recall, case-link accuracy, duplicate detection, redaction recall, version identification, calculation reconciliation |
| View and quality | Macro F1, per-flag precision/recall, evidence-sufficiency performance, false-ready rate, calibration, measured latency on named hardware |
| OCR and identity | Exact string, character error, unit accuracy, alternatives, confidence calibration, conflict recall, false-conflict rate, out-of-scope abstention |
| Component and damage | mAP/mAR, per-class precision/recall, undamaged-region false positives, poor-evidence and source/time/vehicle slices |
| Multi-image findings | Supported-finding precision/recall, evidence-citation correctness, unsupported-finding rate, missing-evidence recall, contradiction detection, calibration |
| Estimate and valuation support | Operation/comparable ranking, safety-related omission, range coverage, source/effective-date integrity, temporal residuals, calculation defects prevented |
| Language and correspondence | Question coverage, factual consistency, unsupported assertions, citation correctness, source-role confusion, PII/cross-case leakage, edit/reject rate |
| QA and anomalies | Precision/recall by defect, material-defect escape rate, alerts per case, reviewer burden, incremental detection over rules |
| Workflow forecasting | Calibration/error, baseline improvement, time-valid subgroup results, blocker visibility |
| Human review | Acceptance, edit, rejection, abstention, evidence-inspection behavior, independent post-acceptance error, disagreement, review time |
| Security and reliability | Cross-case leakage, prompt injection, identifier regurgitation, access controls, artifact integrity, restore, deletion propagation, audit completeness, offline reproduction |

Aggregate accuracy is insufficient. Results must include uncertainty or confidence intervals where
appropriate, cohort dates, measured cohort sizes, missingness, and relevant slices.

## Baselines

Each experiment declares applicable comparators:

- deterministic rules and calculations;
- simple statistical models;
- frozen embeddings or unadapted pretrained models;
- keyword, semantic, and hybrid retrieval;
- templates;
- prompt-only structured generation;
- current human workflow;
- prior accepted artifact.

A complex model cannot be justified solely by outperforming a weak or omitted baseline.

## Sealed suite

The sealed suite must:

- be identified by immutable manifest and hashes;
- contain complete case groups rather than isolated files;
- preserve temporal and source holdouts;
- include clean ordinary cases as well as defects and edge cases;
- exclude training and active-learning access;
- record every exposure;
- never be used for prompt, model, threshold, taxonomy, or feature tuning;
- be replaced or supplemented when repeated exposure makes it unsuitable, while retaining the old
  suite for regression history.

## Required challenge conditions

The applicable suite must deliberately include:

- poor, incomplete, obstructed, and contradictory evidence;
- duplicate, recompressed, embedded, forwarded, and semantically related artifacts;
- wrong-vehicle and cross-case contamination;
- unrelated or pre-existing visible damage;
- subtle damage and no-visible-damage cases;
- uncommon vehicles, body structures, and powertrains;
- ambiguous identifiers and odometer units;
- original, audited, amended, and corrected versions;
- later evidence that must be excluded from an earlier cutoff;
- third-party assertions and outcome-linked requests;
- stale, undated, superseded, conflicting, or wrong-scope references;
- source, template, device, channel, and calendar shifts;
- unsupported prompts and missing current lookups;
- PII leakage and identifier-regurgitation probes;
- prompt injection, malicious attachments, and malformed files;
- rare or high-consequence conditions applicable to the task;
- prior documented failures and rollback-triggering regressions.

## Calibration and abstention

Confidence dimensions remain separate. Component identity, visible damage, relatedness, evidence
sufficiency, operation support, and retrieval applicability must not be collapsed into one score.

| Output mode | Requirement |
|---|---|
| Automatic deterministic extraction | Predeclared high-precision gate with exact source traceability and an exception path |
| Show suggestion | Calibrated confidence and acceptable error at the selected coverage |
| Prominent warning | Elevated uncertainty, conflict, source/date concern, or subgroup risk is exposed |
| Abstain or request evidence | Required support is absent, evidence conflicts, the case is outside evaluated scope, or the operating threshold is not met |

Required calibration measures include:

- expected calibration error;
- Brier score for probabilistic outputs;
- risk-coverage curves;
- error at the selected threshold;
- false-ready rate;
- unsupported-finding rate;
- calibration by relevant subgroup and time period.

Thresholds are experiment-specific. They must be approved and recorded before sealed evaluation;
this README does not supply speculative universal numbers.

## Human-review protocol

Reviewers must be able to inspect:

- exact source artifacts, image regions, or text spans;
- source role and timestamps;
- visible, reported, inferred, calculated, or proposed status;
- model/rule/reference version;
- confidence and known limitations;
- contradicting evidence;
- effective-dated references;
- effect on structured fields, calculations, and generated text.

Permitted review records are:

```text
approve
edit
reject
request_more_evidence
mark_not_assessable
wrong_source_or_case
appropriate_abstention
```

Each record includes a structured reason and may retain explanatory free text. Acceptance is not
automatically ground truth, and amendments must be reason-coded rather than presumed to indicate an
earlier defect.

## Stop and hold gates

An experiment is held or stopped when any applicable condition occurs:

- no meaningful improvement over the declared baseline;
- dataset, label, taxonomy, or chronology quality prevents interpretable evaluation;
- duplicate, case, version, source, or future-information leakage;
- incorrect automatic case merge or cross-case disclosure;
- source-role confusion that represents an assertion as an accepted fact;
- invented fact, source, method, value, attachment, action, or citation;
- unsupported-finding or false-ready performance exceeds the declared limit;
- poor calibration or abstention at the intended operating point;
- material collapse on temporal, source, vehicle, device, or evidence-quality holdouts;
- unacceptable rare or high-consequence challenge performance;
- alerts or output volume prevent meaningful review;
- reviewers cannot trace a material proposal to evidence;
- reviewers exhibit unmitigated default acceptance or fail to inspect evidence;
- unresolved critical privacy, security, integrity, or deletion findings;
- larger training is proposed without evidence of a smaller-method ceiling;
- no measurable value over the appropriate human or deterministic baseline;
- offline reproduction, export, checksum verification, or rollback fails.

# Registry contract

## Required artifact bundle

```text
model/
  checkpoint or adapter reference
  portable inference export where feasible
  tokenizer or image processor
  label map and schema
  exact configuration
  dependency and base-model lock
  model card
data/
  dataset manifest and lineage
  split manifest and hashes
  taxonomy and transformation versions
evaluation/
  baseline and full metrics
  challenge-set results
  calibration and abstention thresholds
  security and leakage results
  offline smoke-test inputs and expected outputs
governance/
  approval identifiers
  use-state and licence records
  known limitations
  expiry and review point
  promotion decision
  rollback instructions
checksums.txt
```

Binary artifacts remain in an approved external artifact store. Git contains immutable references and
hashes only. If a base artifact cannot be redistributed, preserve its immutable source identifier,
licence record, adapter, configuration, and reproducible assembly process.

## Model card

```yaml
model_card:
  artifact_id:
  version:
  created_at:
  experiment_id:
  task:
  model_type:
  base_identity:
  base_revision:
  base_licence:
  artifact_licence:
  dataset_release:
  split_manifest:
  intended_uses:
  prohibited_uses:
  required_inputs:
  output_schema:
  evidence_and_citation_contract:
  calibration:
  abstention:
  evaluated_scope:
  unevaluated_scope:
  metrics:
  subgroup_results:
  challenge_results:
  security_results:
  known_limitations:
  human_review_requirement:
  monitoring_signals:
  expiry_at:
  review_owner:
  artifact_hashes:
  offline_smoke_test:
  rollback_version:
```

`intended_uses`, `prohibited_uses`, thresholds, and approval fields are records of decisions made by
`Pegasus.Core` or authorised humans. The registry does not create them.

## Promotion manifest

```yaml
promotion:
  promotion_id:
  artifact_id:
  artifact_hash:
  source_experiment:
  candidate_version:
  previous_version:
  created_at:
  evaluation_record:
  evaluation_hash:
  model_card_hash:
  dataset_manifest_hash:
  split_manifest_hash:
  licence_record_hash:
  inference_contract_hash:
  smoke_test_hash:
  decision:
    outcome: promoted | held | rejected | superseded
    decided_at:
    decision_owner:
    approval_ids: []
  activation_scope:
  expires_at:
  rollback:
    target_version:
    target_hash:
    instructions:
    verification_test:
```

Promotion requires:

- reproducible dataset, split, code, configuration, environment, and artifact;
- performance above the declared baseline on untouched case-level data;
- acceptable calibration, abstention, subgroup, and challenge results;
- passed privacy, security, integrity, deletion, and cross-case tests;
- documented limitations and expiration;
- an offline smoke test;
- a retained prior working version;
- tested rollback instructions;
- explicit decision and approval identifiers.

Promotion is evidence status, not deployment. No registry entry establishes a production choice or
business activation.

## Rollback

Rollback instructions must be executable without retraining and must specify:

1. the prior artifact and immutable hash;
2. compatible schema, processor, retrieval, and deterministic-rule versions;
3. how to disable or withdraw the affected artifact;
4. the offline verification test and expected result;
5. treatment of outputs produced by the withdrawn version;
6. reevaluation or invalidation requirements for dependent artifacts;
7. the incident or regression evidence that triggered rollback.

A rollback is required when an integrity failure, wrong-case disclosure, harmful unsupported output,
material benchmark regression, expired approval, uncontained security issue, or unreproducible
artifact invalidates the promoted evidence.

# Current evidence and technical decisions

Only evidence that still supports a current experiment is retained here. It does not establish
archive-wide prevalence, dataset sufficiency, or model performance.

## Dated evidence retained

| Evidence date | Current experiment support | Checksum status | Current outcome |
|---|---|---|---|
| 2026-07 | A2 tiered extraction: inspected documents included heterogeneous formats, visually readable PDFs with parser-specific malformed text, image-heavy pages, and structured files requiring format-specific handling | No source snapshot or file checksum was supplied in the supporting review | Retain as design evidence and synthetic-fixture motivation only; it cannot identify a dataset release or support a metric |
| 2026-07 | A1/A3 lineage and deduplication: sampled evidence showed artifacts repeated across folders, messages, attachments, embedded copies, and report versions | No immutable source manifest or checksum was supplied | Require content identity, receipt lineage, version edges, and case-level splits before evaluation |
| 2026-07 | B1/B2 evidence metadata: sampled standalone images did not retain EXIF, and filename-derived times were not independently verified | No source image checksums were supplied | Treat filename time as unverified; use receipt/event provenance and include missing-metadata challenge cases |
| 2026-07 | B6 and version-aware evaluation: sampled material included original, audit, and amended states with later evidence | No version-set checksum was supplied | Preserve every version and evidence cutoff; never use later findings as earlier visible truth |
| 2026-07 | A2 secret blocking: an inspected spreadsheet was reported to contain plaintext portal credentials | No source checksum was supplied, and secret values must not be reproduced | Treat credential detection as a blocking pipeline test; quarantine matching content and exclude it from extraction, indexing, training, and evaluation |
| 2026-07 | All model experiments: the supporting technical review contained designs and hypotheses but no completed run manifest, dataset checksum, model hash, sealed metric, calibration result, or portability test | Not available | No model capability, preferred family, or readiness claim is current evidence |

## Current technical decision outcomes

| Decision | Current outcome | Reconsideration evidence |
|---|---|---|
| Learning and evaluation unit | Case at a recorded evidence cutoff | A benchmark showing equivalent leakage-safe validity from another unit |
| Source representation | Immutable artifacts plus a canonical event/version graph | Successful reconstruction, deletion, and leakage tests for an alternative |
| Primary split | Case-level, time-aware, after deduplication; source holdout where relevant | Predeclared task evidence demonstrating a different split does not inflate results |
| General foundation training from random initialisation | Out of scope | A separately approved corpus and experiment demonstrating appropriate scale, diversity, and evaluation |
| Narrow scratch models | Allowed only as matched baselines or narrow task models | Direct sealed comparison |
| Adaptation | Conditional on a measured deficit against simpler baselines | Sealed baseline and learning-curve evidence |
| Continued indiscriminate domain pretraining | Out of scope | A separately approved, contamination-controlled experiment with a defined task benefit |
| Monetary and deterministic values | Kept in dated sources and reproducible calculations, not treated as timeless model targets | No reconsideration without a new technical contract |
| Detection versus segmentation | Boxes first; masks only after measured incremental value | Successful detector and downstream boundary-precision evidence |
| Pipeline versus end-to-end multimodal method | Unresolved experiment question | B5 comparison on grounding, calibration, failure analysis, and reviewability |
| Historical amendments as preference targets | Prohibited without evidence-time reconstruction and reason coding | Reviewed examples proving equal information availability and valid target provenance |
| Current model family | No selection | Reproducible comparative evaluation and authorised decision |
| Runtime activation | None created by this workspace | Separately approved implementation, activation record, and rollback path |

# Open questions

## Corpus and provenance

- What are the dated, deduplicated distributions of cases, artifacts, messages, report versions,
  evidence sources, vehicle families, and assessment periods?
- Which cases have reliable evidence cutoffs, source roles, final approval state, and complete version
  lineage?
- How often can images be linked to exact findings rather than only to a case or report?
- Which amendment reasons can be recovered reliably?
- Can confirmed no-change outcomes be distinguished from absence of a recorded amendment?
- Which recurring vehicles or claims can be grouped without exposing direct identifiers?
- What proportion of extraction, OCR, case linking, and deduplication failures remains after the
  deterministic pipeline is implemented?
- Which source families have current use-state approvals for training, evaluation, retrieval,
  aggregation, or no use?

## Labels

- What are the approved versions of the view, quality, component, damage, operation, message-purpose,
  and workflow-event taxonomies?
- Which labels can trained non-specialists provide, and which require authorised domain review?
- What agreement measures and task-specific acceptance thresholds are required?
- How will unresolved reviewer disagreement be adjudicated?
- Can severity be defined through observable, repeatable criteria?
- How will `unknown_visible_anomaly` be monitored so it does not conceal taxonomy defects?
- Which rare or high-consequence classes have enough support for evaluation rather than only
  abstention?

## External and changing sources

- Which current vehicle, method, valuation, parts, rate, salvage, and reference sources are approved
  for each use state?
- What immutable identifiers, effective dates, supersession links, and retrieval records do they
  provide?
- Which sources may be cached, transformed, indexed, or used as model features?
- What is the required behavior when a current source is unavailable, undated, conflicting, or
  outside scope?
- Which source conflicts require a governed resolution record before use?

## Evaluation

- What are the measured human and deterministic baselines for each experiment?
- What task-specific limits apply to false-ready, unsupported-finding, safety-related omission,
  cross-case, calibration, and abstention errors?
- Which subgroup tolerances are required?
- Which challenge conditions are blocking for each task?
- What evidence is required to move from historical replay to shadow evaluation?
- How will repeated sealed-suite exposure be detected and handled?
- Which human-review measures demonstrate evidence inspection rather than automation bias?
- What observation periods and power calculations are required before interpreting an intervention?

## Training and model methods

- For each narrow task, does transfer learning outperform a matched scratch baseline?
- Does self-supervised adaptation add value beyond strong frozen representations?
- Does a retrieval or prompt-only method already meet the task gate?
- Is adaptation necessary for controlled narrative style?
- Does a pipeline-first multimodal method outperform an end-to-end method on grounding and
  reviewability?
- Which model or representation licences and export paths remain valid at build time?
- What is the smallest method that meets the sealed, calibration, and portability gates?
- Can results be reproduced offline from the retained artifact package?

## Security and infrastructure

- Which providers, regions, storage systems, and execution environments are approved for each
  experiment?
- Which tasks require local-only processing?
- How are case and source permissions enforced inside retrieval rather than only at the interface?
- What export controls apply to datasets, indexes, adapters, checkpoints, and evaluation suites?
- Have prompt injection, malformed attachment, data poisoning, model extraction, membership
  inference, identifier regurgitation, restore, and deletion tests been implemented?
- What measured latency, throughput, capacity, and recovery requirements apply to an approved
  implementation?
- How will source corrections and deletions invalidate dependent runs and registry entries?

## Authority and activation

- Which authorised human owns each experiment, threshold set, approval, expiry review, and rollback
  decision?
- Which business facts must be supplied by `Pegasus.Core` rather than inferred from source text or
  historical outputs?
- What exact reviewed output boundary applies to each experiment?
- What separately approved implementation would consume a promoted artifact?
- What evidence and approval are required for any shadow, controlled, or broader activation?

# Canonical Case Data Model

## Executive conclusion

The correct unit of learning is the **case at a point in time**, not an individual image or final PDF. A canonical data model must preserve evidence provenance, version history and the difference between supplied facts, observed visual evidence, external data and engineer opinion.

Without this model, training will create label leakage, duplicate evidence, false attribution and unreliable evaluation.

## Core entities

### Case

The case is the stable container for all events and artifacts.

Recommended fields:

```yaml
case:
  case_id: pseudonymous_internal_id
  opened_at:
  closed_at:
  status:
  instructing_organisation_id:
  instruction_reference:
  collision_engineers_reference:
  accident_date:
  assessment_mode: remote_image_based
  jurisdiction:
  retention_class:
  rights_status:
```

Names, addresses, contact details and raw external references should remain in a restricted identity vault. The analytic/training case record should use pseudonymous identifiers.

### Vehicle

```yaml
vehicle:
  vehicle_id:
  vrm_token:
  vin_token:
  make:
  model:
  derivative:
  body_style:
  registration_year:
  engine:
  fuel_or_powertrain:
  odometer:
  odometer_source:
  condition:
  previous_total_loss:
  history_source:
```

For general damage training, raw VRM and VIN are usually unnecessary. OCR or vehicle-identity projects should use a separately controlled dataset because identifiers become the task target.

### Instruction

Instructions should be represented as material facts and requested questions, not merely as an email body.

```yaml
instruction:
  instruction_id:
  received_at:
  sender_role:
  requested_outputs:
  stated_accident_circumstances:
  stated_damage:
  vehicle_location_text:
  deadlines:
  source_artifact_id:
```

The phrase “inspect the vehicle” in a solicitor template does not change the actual assessment mode. The source wording must be retained separately from the normalised workflow fact that Collision Engineers performs a remote image-based assessment.

### Artifact

Every file and message becomes an immutable artifact record.

```yaml
artifact:
  artifact_id:
  case_id:
  artifact_type:
  original_filename:
  mime_type:
  sha256:
  perceptual_hash:
  byte_length:
  received_at:
  source_party_role:
  parent_message_id:
  duplicate_of_artifact_id:
  storage_location:
  access_class:
```

Raw artifacts should not be overwritten when extraction improves. Derived text, thumbnails and redacted copies should have their own IDs and parent links.

### Evidence event

An evidence event groups artifacts received together and defines what was knowable at that time.

```yaml
evidence_event:
  event_id:
  case_id:
  occurred_at:
  received_at:
  source_role:
  event_type:
  artifact_ids:
  supersedes_event_id:
  engineer_seen_at:
```

Examples include initial image supply, repairer estimate, later strip-down findings and third-party valuation evidence.

### Image observation

Image labels should be attached to a specific image version.

```yaml
image_observation:
  artifact_id:
  view_class:
  visible_vehicle_regions:
  capture_quality:
  identifiers_visible:
  damage_annotations:
  relatedness_status:
  annotation_source:
  annotation_confidence:
```

Damage annotations should distinguish:

- directly visible;
- suggested but ambiguous;
- reported by another party;
- not visible;
- not assessable from the image.

### Assessment version

```yaml
assessment:
  assessment_id:
  case_id:
  version_number:
  version_type: original | audit | amended | addendum | final
  created_at:
  evidence_cutoff_at:
  parent_assessment_id:
  author_id:
  reviewer_id:
  approval_status:
  outcome:
  roadworthiness_opinion:
  impact_area:
  impact_magnitude:
  uncertainty_notes:
```

`evidence_cutoff_at` is critical. A model must be evaluated against the evidence available when the assessment was created, not evidence obtained later.

### Damage and operation records

```yaml
damage_item:
  damage_id:
  assessment_id:
  vehicle_component:
  side_and_position:
  damage_type:
  severity:
  causal_relatedness:
  proposed_action:
  supporting_artifact_ids:
  unsupported_or_reported_only:
  confidence:
```

```yaml
repair_operation:
  operation_id:
  assessment_id:
  operation_family:
  component:
  action:
  labour_hours:
  part_reference:
  method_reference_ids:
  reason:
  supporting_damage_ids:
```

This structure makes it possible to ask whether every operation is supported by a visible observation, external method, reported fact or explicit precaution.

### Estimate

```yaml
estimate:
  assessment_id:
  currency:
  labour_hours:
  hourly_rate:
  labour_total:
  parts_total:
  paint_materials_total:
  specialist_other_total:
  subtotal:
  tax_rate:
  tax_total:
  grand_total:
  price_effective_at:
  price_source:
```

Raw monetary values should be accompanied by an effective date and source. Models should not be trained to memorise time-sensitive prices as though they were permanent.

### Valuation

```yaml
valuation:
  assessment_id:
  valuation_date:
  retail_value:
  trade_value:
  engineer_value:
  guide_observations:
  market_examples:
  mileage_adjustment:
  condition_adjustment:
  adverse_history_adjustment:
  rationale:
  source_effective_dates:
```

### Message and response

```yaml
message:
  message_id:
  case_id:
  thread_id:
  direction:
  sender_role:
  recipients_roles:
  sent_at:
  subject_class:
  intent_class:
  body_artifact_id:
  attachment_ids:
  adopted_as_fact:
  requires_response:
```

Inbound and outbound messages must never be pooled into one “writing style” dataset.

## Truth and provenance model

Every substantive fact should have one of these provenance classes:

1. **Visual observation** — directly supported by one or more images.
2. **Documented fact** — stated in supplied documentation.
3. **External lookup** — obtained from DVLA, MOT, history or valuation services.
4. **Reported later finding** — supplied by a repairer after further work.
5. **Engineer inference** — professional interpretation of the evidence.
6. **Client or third-party assertion** — supplied opinion not independently adopted.
7. **Calculated value** — deterministic result from recorded inputs.

The model may use all classes as context, but outputs must disclose which class supports each conclusion.

## Dataset views

One governed case store should generate multiple task-specific views:

- de-identified report extraction;
- view and quality classification;
- component/damage detection;
- multi-image assessment;
- report drafting;
- email triage;
- approved response drafting;
- valuation analysis;
- supplement-risk prediction;
- QA and anomaly detection.

Each view should include only fields necessary for its purpose.

## Versioning and leakage prevention

- All artifacts and versions for a claim must remain in one train, validation or test split.
- Near-duplicate vehicles or repeated claims should be grouped where detectable.
- Original and amended reports must not be separated across splits.
- Later evidence must not be exposed to a model predicting the earlier report.
- Generated or redacted derivatives inherit the original artifact's split.

## Conclusion

The canonical data model is the foundation of every proposed use case. It enables defensible training, accurate evaluation, evidence citation and future deletion or correction requests. Building models before this case graph exists would create fast demonstrations but unreliable products.


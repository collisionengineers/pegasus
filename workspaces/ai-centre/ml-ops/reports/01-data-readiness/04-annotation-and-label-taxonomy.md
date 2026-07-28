# Annotation Strategy and Label Taxonomy

## Executive conclusion

The existing reports provide strong case-level supervision but weak image-level localisation. A targeted annotation programme is required for the highest-value vision tasks. The programme should prioritise labels that improve remote evidence quality and engineer efficiency, not attempt to describe every visible vehicle detail from the outset.

## Annotation principles

- Annotate only labels tied to a defined product decision.
- Preserve “unknown”, “not visible” and “insufficient evidence”.
- Separate directly visible damage from supplied or inferred findings.
- Record annotator role and confidence.
- Use engineers for professional labels and trained non-engineers for objective capture labels.
- Double-annotate a representative subset to measure agreement.
- Route disagreements into taxonomy improvement, not majority-vote concealment.

## Label layers

### Layer 1 — Capture and evidence quality

Suitable for trained operations annotators:

- image valid/invalid;
- blur;
- darkness;
- overexposure or glare;
- obstruction;
- excessive distance;
- excessively tight crop;
- orientation;
- duplicate/near duplicate;
- unrelated image;
- readable identifier;
- sufficient context;
- sufficient detail.

These labels support the first recommended product: remote evidence completeness and guided capture.

### Layer 2 — View class

Recommended controlled vocabulary:

- front three-quarter left/right;
- rear three-quarter left/right;
- direct front/rear;
- full left/right side;
- roof;
- underbody;
- engine bay;
- boot/load area;
- interior;
- dashboard/odometer;
- VIN/chassis label;
- registration plate;
- wheel/tyre by corner;
- damage context;
- damage close-up;
- repairer/estimate document;
- other.

An image may have one primary view and several visible-region labels.

### Layer 3 — Vehicle component

Use a hierarchy rather than a flat list:

```text
vehicle
├── front
│   ├── bumper
│   ├── grille
│   ├── bonnet
│   ├── headlamp
│   └── front panel
├── side
│   ├── wing
│   ├── front door
│   ├── rear door
│   ├── sill
│   └── quarter/side panel
├── rear
│   ├── bumper
│   ├── tailgate/boot
│   ├── lamp
│   └── rear panel
├── glazing
├── wheel_and_tyre
├── suspension_adjacent
└── identifiers_and_instruments
```

Side and position should be separate fields so that the taxonomy remains consistent.

### Layer 4 — Damage type

Initial classes:

- scratch/scuff;
- paint transfer;
- dent;
- crease;
- crack/split;
- puncture/tear;
- broken/missing;
- displaced/misaligned;
- crushed/deformed;
- corrosion or pre-existing deterioration;
- glass damage;
- wheel-rim damage;
- tyre damage;
- lamp damage;
- unknown/ambiguous.

Do not create severity classes before engineers define operationally reproducible criteria.

### Layer 5 — Causal relatedness

- consistent with instructed impact;
- possibly consistent;
- unrelated/pre-existing;
- insufficient evidence;
- not assessed.

This is a professional label and should include a short rationale or evidence link.

### Layer 6 — Proposed action

- no action;
- inspect/check;
- repair;
- replace/renew;
- refinish;
- blend;
- remove and refit;
- calibrate/diagnose;
- wheel alignment;
- specialist operation;
- request more evidence;
- cannot determine remotely.

Action labels should be tied to the assessment version and methods available at that time.

## Annotation forms by task

### Image classification

One row per image:

```text
artifact_id, primary_view, quality_flags, sufficient_context,
sufficient_detail, identifier_type, annotator, confidence
```

### Object detection

Use COCO-compatible boxes:

```text
image_id, component_class, damage_class, x, y, width, height,
occluded, truncated, confidence
```

Boxes are appropriate for components and localised damage regions.

### Segmentation

Use masks only where boundary precision materially matters, such as:

- damaged versus undamaged panel area;
- scratch/paint-transfer region;
- shattered glass;
- panel deformation boundary;
- wheel-rim affected area.

Segmentation is substantially more expensive than boxes and should follow a successful detection pilot.

### Multi-image case annotation

One record per finding:

```text
case_id, evidence_cutoff, component, damage, severity, relatedness,
supporting_image_ids, contradicting_image_ids, missing_views,
engineer_confidence, final_action
```

This directly supports evidence-grounded assessment drafting.

## Using existing reports as weak labels

Reports can bootstrap annotation by suggesting:

- impact area;
- components;
- repair versus replace;
- outcome;
- roadworthiness;
- total estimate;
- valuation and category.

These are weak labels because:

- they apply to the case, not necessarily every image;
- some findings came from external facts or later evidence;
- an operation can be precautionary rather than visually demonstrated;
- amended reports include information unavailable originally;
- boilerplate can be mistaken for a case-specific finding.

Weak labels should pre-populate an annotation interface, but an engineer must confirm image support.

## Sampling plan

The first annotated cohort should be deliberately diverse:

- repairable and total-loss cases;
- light through severe visible damage;
- all impact regions;
- common and uncommon vehicle types;
- petrol, diesel, hybrid and EV;
- multiple instructing firms and evidence sources;
- good and poor image sets;
- cases with and without amendments;
- cases with unrelated damage.

Do not sample only “clean” successful reports. Evidence-quality models need failures and incomplete submissions.

## Quality assurance

Track:

- inter-annotator agreement;
- engineer/non-engineer disagreement by label;
- class balance;
- ambiguous-label rate;
- annotation time;
- correction rate after model pre-labelling;
- taxonomy changes;
- proportion of findings with explicit evidence.

Gold-standard cases should be periodically re-reviewed without showing the old labels to detect drift.

## Recommended first annotation targets

1. 5,000–10,000 images for view and quality classification.
2. A smaller engineer-reviewed set of 1,000–2,000 cases for evidence sufficiency.
3. Several thousand component/damage boxes across common damage regions.
4. A versioned cohort of original and amended reports for supplement risk.

These are planning ranges, not guaranteed minimums. A learning-curve experiment should determine whether more data is valuable.

## Conclusion

The annotation programme should begin with objective image quality and view labels, then progress to component, damage and evidence-grounding labels. This produces useful systems early while reserving scarce engineer time for labels that require professional judgement.


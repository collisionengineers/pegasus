# Remote Evidence Quality and Guided Capture

## Executive conclusion

This is the strongest first vision use case for Collision Engineers.

The system should determine whether a remote image set is usable, identify missing views and give one corrective instruction at a time. It should not claim to have assessed damage merely because capture quality checks pass.

The opportunity applies both:

- during live guided capture; and
- after photographs arrive by email, messaging service or repairer upload.

## Business problem

Remote assessment quality depends on the evidence supplied. Common failure modes include:

- no contextual view of the affected side;
- close-ups that do not reveal which panel is shown;
- glare on dark paint;
- blur or poor light;
- cropped panel boundaries;
- no wheel or tyre evidence near an impact;
- no odometer or identity evidence;
- duplicate photographs;
- inconsistent vehicles;
- no image supporting a stated damage item;
- later supplements caused by incomplete initial evidence.

The current process spends expert and administrative time discovering and resolving these gaps.

## Product outcomes

The system should produce:

```yaml
evidence_readiness:
  overall_state: not_started | collecting | needs_correction | ready_for_engineer
  accepted_views:
  rejected_views:
  missing_views:
  quality_issues:
  next_instruction:
  warnings:
  model_version:
```

`ready_for_engineer` means the configured evidence checklist is met. It does not mean the vehicle is fully assessable or that the model has determined the repair.

## Capture state machine

Recommended states:

1. **Choose required view**  
   The workflow requests a specific image such as rear three-quarter right.

2. **Vehicle not framed**  
   No vehicle or insufficient vehicle area is visible.

3. **Move farther away / move closer**  
   The image lacks context or detail.

4. **Improve light / reduce glare**  
   Exposure or reflection prevents useful review.

5. **Hold steady**  
   Blur or motion is detected.

6. **Adjust angle**  
   The requested region or panel boundary is not visible.

7. **Ready**  
   Deterministic and learned checks pass.

8. **Captured**  
   The image is stored and linked to the required-view slot.

9. **Engineer follow-up required**  
   The automatic workflow cannot determine what correction is needed.

The interface should display one primary correction at a time. Multiple simultaneous warnings reduce compliance.

## Detection layers

### Deterministic checks

Use simple calculations for:

- image dimensions;
- file integrity;
- exposure histogram;
- blur score;
- orientation;
- duplicate hash;
- minimum/maximum file size;
- camera permission and storage success.

### Lightweight vision checks

Use compact models for:

- vehicle present;
- coarse view;
- wheel/tyre present;
- dashboard/odometer;
- plate/identifier;
- affected region approximately framed;
- full-context versus close-up;
- heavy obstruction.

### Workflow checks

Use configured rules for:

- required views by incident area;
- required identity evidence;
- number of contextual and close-up views;
- EV/hybrid-specific evidence prompts where approved;
- client-specific evidence requirements;
- whether a new image duplicates an accepted one.

## Suggested evidence protocol

A base set could include:

- front three-quarter left and right;
- rear three-quarter left and right;
- full affected side;
- odometer;
- registration/identity view;
- affected-region context;
- one or more damage close-ups;
- wheel/tyre view when impact is adjacent;
- any engineer-requested additional view.

The exact checklist should be defined by Collision Engineers and varied by vehicle/body style and reported impact. The model should not invent mandatory views.

## Training data

Required labels:

- primary view;
- visible regions;
- blur/exposure/glare/obstruction;
- context sufficient;
- detail sufficient;
- accepted/rejected by engineer;
- rejection reason;
- requested next view;
- eventual need for follow-up.

Historical images can be weakly labelled from report ordering and filenames, but engineer/operations confirmation is necessary.

Poor images are essential training data. A dataset containing only accepted report photographs cannot teach reliable rejection.

## Live versus inbox deployment

### Live capture

Requirements:

- on-device or low-latency inference;
- no dependency on network availability for basic readiness;
- portable model;
- clear accessibility and safety design;
- no capture while the user is driving;
- resumable sessions;
- local draft storage and secure upload.

### Inbox evidence checker

Requirements:

- attachment extraction;
- duplicate detection;
- case linking;
- view-set summary;
- suggested evidence-request email;
- admin or engineer approval;
- record of who supplied each photograph.

Inbox deployment is a lower-risk pilot because it does not require a new capture interface.

## Metrics

Primary:

- false-ready rate;
- missing-view recall;
- accepted-image precision;
- duplicate detection;
- reduction in evidence follow-up;
- reduction in engineer time spent sorting images;
- supplement/amendment reduction attributable to evidence completeness.

Secondary:

- user completion rate;
- recapture count;
- time per required view;
- model latency;
- accessibility outcomes.

## Failure handling

The system should abstain when:

- the vehicle type is outside scope;
- severe obstruction persists;
- requested region cannot be identified;
- multiple vehicles appear;
- the evidence conflicts with instructions;
- the model confidence is low;
- a safety-sensitive view cannot be established.

## Non-goals

The first release should not:

- identify exact repair operations;
- determine roadworthiness;
- calculate an estimate;
- certify damage causation;
- identify hidden damage;
- replace engineer evidence review.

## Recommended pilot

1. Label existing images for view and quality.
2. Build an offline batch checker.
3. Run it on newly received cases in shadow mode.
4. Compare predicted missing views with actual engineer requests.
5. Only then integrate the proven checks into live guided capture.

## Conclusion

Remote evidence quality is the enabling layer for all later vision work. It offers immediate efficiency gains, a tractable annotation problem and a clear safety boundary: the system guides evidence collection while the engineer performs the assessment.


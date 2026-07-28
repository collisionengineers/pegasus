# Vehicle Identification and OCR

## Executive conclusion

Vehicle identity should be established primarily from registration/VIN data and authoritative services, not inferred solely from visual appearance.

Vision is valuable for reading identifiers and checking consistency. It is not the preferred source of exact make, model or derivative when authoritative structured data is available.

## Use cases

- Read a VRM from an image.
- Read VIN/chassis labels.
- Read odometer mileage.
- Classify an image as plate, VIN, odometer or general vehicle image.
- Compare visible identity with instructions.
- Detect possible wrong-vehicle or mixed-case images.
- Estimate broad make/model family when no identifier is available.
- Flag inconsistent body style, badge or generation for human review.

## Authoritative identity flow

```text
Instruction VRM
    + OCR candidate from evidence
    → normalise and compare
    → query approved vehicle data source
    → return structured make/model/year/fuel facts
    → visually check for material inconsistency
    → engineer/admin resolution if conflict
```

The DVLA Vehicle Enquiry Service accepts a registration number and returns vehicle details. [DVLA Vehicle Enquiry API](https://developer-portal.driver-vehicle-licensing.api.gov.uk/apis/vehicle-enquiry-service/v1.2.0-vehicle-enquiry-service.html)

Other commercial vehicle, valuation and history services may provide richer derivative and history information subject to licence.

## OCR tasks

### Registration plate

Pipeline:

1. detect plate region;
2. rectify perspective;
3. enhance contrast conservatively;
4. recognise characters;
5. apply UK registration-format constraints;
6. return alternatives and confidence;
7. compare with instructed VRM.

The system must retain the original image and avoid silently altering evidence.

### VIN

Challenges:

- small characters;
- glare and reflections;
- embossed or low-contrast text;
- restricted viewing angle;
- confusion between `0/O`, `1/I`, `5/S`, `8/B`;
- labels containing several other codes.

Use VIN checksum/format validation where applicable, but never replace an observed character without recording the correction.

### Odometer

The sample includes clear dashboard imagery. OCR should extract:

- displayed odometer;
- unit: miles or kilometres;
- trip versus total mileage;
- warning if display is ambiguous;
- image ID and crop coordinates.

Cross-check against MOT or supplied mileage. A mismatch is a review flag, not proof of wrongdoing.

## Visual make/model recognition

Possible labels:

- manufacturer;
- model family;
- body style;
- approximate generation;
- visible derivative/badge.

Limitations:

- trim variants can be visually indistinguishable;
- accident damage can remove badges or lights;
- rebadging and plate changes exist;
- images may show only a close-up;
- aftermarket parts alter appearance;
- model-year differences may be subtle.

Use visual recognition for:

- retrieval of likely method families;
- consistency checks;
- routing;
- a fallback suggestion.

Do not use it as sole authority for valuation or exact parts.

## Wrong-vehicle and mixed-case detection

Compare:

- OCR registrations across images;
- vehicle embeddings and colour;
- make/model predictions;
- wheel/body/lamp appearance;
- timestamps and source event;
- report identifiers;
- VIN where supplied.

Possible outputs:

- consistent set;
- likely duplicate;
- possible second vehicle;
- identifier conflict;
- insufficient identity evidence.

The system should not automatically delete or reassign evidence.

## Data requirements

Separate authorised datasets:

1. plate detection/OCR;
2. VIN detection/OCR;
3. odometer detection/OCR;
4. make/model classification;
5. case-consistency pairs.

For privacy and minimisation, general damage models should use masked identifiers unless they need them.

## Evaluation

### OCR

- full-string exact match;
- character error rate;
- correct unit;
- top-K alternatives;
- confidence calibration;
- low-quality-image abstention.

### Identity consistency

- conflict detection recall;
- false conflict rate;
- mixed-case detection;
- engineer resolution time;
- wrong-vehicle errors prevented.

### Visual recognition

- top-1/top-5 make and model;
- performance by crop/view;
- performance by vehicle age;
- unknown/out-of-scope rejection.

## Security and privacy

VRM and VIN can contribute to identifying a person when combined with other case information. Controls should include:

- restricted raw identifier store;
- tokenised values in analytics;
- encrypted transport and storage;
- role-based access;
- inference-log minimisation;
- separate training authorisation;
- masking in screenshots and demonstrations.

## Recommended first release

Implement:

- image-type classification;
- plate and odometer region detection;
- OCR with confidence;
- instruction comparison;
- DVLA/approved-data lookup;
- discrepancy queue.

Delay fine-grained visual model recognition until a business case remains after authoritative lookup.

## Conclusion

Vehicle identification is best implemented as OCR plus authoritative data and visual consistency checks. This is more accurate, explainable and maintainable than training a model to infer an exact vehicle solely from damaged-image appearance.


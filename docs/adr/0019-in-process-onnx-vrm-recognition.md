---
id: ADR-0019
status: accepted
date: 2026-08-03
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: [frd-06]
tags: [vrm, onnx, image]
---
# ADR-0019: In-process ONNX VRM recognition engine

- Date: 2026-08-03
- Status: accepted

## Context

`INT-17` allocates suggestion-first automatic vehicle-registration reading
from ordinary vehicle images. Open decision 1 held the mechanism question:
in-process model bytes versus one guarded external adapter. Evidence was
gathered on 2026-08-03 (recorded in the open-decisions register history):

- Every external ANPR cloud candidate sends image bytes to US
  infrastructure (Plate Recognizer Snapshot: Linode/AWS US, 30-day rolling
  image retention; Rekor CarCheck: AWS US, retention unstated). The only
  UK-resident external option is Azure AI Vision Read from a UK South
  resource (in-region, deleted within 24 hours, managed-identity auth),
  which is generic OCR rather than plate-specialised. Any external route
  would also require consciously amending `INT-17`'s recorded "no external
  upload" boundary and the personal-data/vehicle-image retention rules
  before activation.
- A modern plate-specialised open stack exists as plain ONNX: the
  fast-alpr YOLOv9-based plate detector and the fast-plate-ocr global CCT
  recogniser. Release assets are static and SHA-256 pinnable, and run
  directly from `Microsoft.ML.OnnxRuntime` (MIT, RID-native binaries, no
  Python service, no runtime download).
- The operator directed on 2026-08-03 that licence compatibility is not a
  selection constraint: Pegasus is staff-only and external portals are a
  permanent boundary (`BND-06`), so copyleft network-clause obligations
  reduce to offering source to Collision Engineers' own staff. Origin,
  hash, and RID review remain required.
- No credible published UK-plate benchmark exists for any candidate; only
  an evaluation on genuine Collision Engineers case images decides
  fitness. The local immutable corpus holds roughly 7,000 genuine case
  images with case-level VRM attribution available for a labelled cohort
  and untouched holdout.

The operator selected the in-process route on 2026-08-03.

## Decision

Adopt an in-process ONNX recognition engine for `INT-17`:

- Plate detection uses the fast-alpr YOLOv9-based plate-detection model
  and text recognition uses the fast-plate-ocr global CCT model, both as
  vendored ONNX bytes with recorded origin URL and SHA-256 for each file,
  executed via `Microsoft.ML.OnnxRuntime`. No separate service, container,
  Python runtime, or runtime download exists.
- `Pegasus.Core` owns the port; the ONNX execution lives in
  `Pegasus.Infrastructure`. The engine returns per-image VRM candidates
  with confidence and the exact source-image identity, or abstains. It
  never accepts a registration, invents an instruction, mutates a Case or
  Image intake, or uploads an image anywhere.
- Suggestion-first remains the settled product boundary: every suggested
  VRM requires an authorised staff confirmation before any record uses
  it, and the suggestion stays bound to its retained source image.
- Acceptance evidence: a frozen labelled cohort and untouched holdout
  drawn from the genuine local corpus, evaluated locally (the corpus is
  absent in CI). The first evaluation run sets the provisional
  accuracy/abstention bar for operator review; wrong-suggestion rate is
  the primary measure because an accepted wrong VRM is worse than an
  abstention. Declaring the capability accepted still requires the normal
  evidence tiers.

## Consequences

No image leaves the application, no external credential exists, and no new
deployment unit is created. The `Microsoft.ML.OnnxRuntime` native package
and vendored model bytes add tens of megabytes to the build. UK-plate
accuracy is unproven until the cohort evaluation; `INT-17` stays
non-blocking for the alpha and the engine fails toward abstention, never
toward a guessed registration.

A future engine change — an external adapter, a retrained detector, or a
replacement recogniser — is a new decision against the same cohort and
gate, not a silent swap. Automatic image-led/instruction-led matching
(`INT-28`/`INT-32`) remains separately gated: reading a plate is not
associating a record. Open decision 1 retains only the still-open
threshold acceptance from the operator-reviewed cohort.

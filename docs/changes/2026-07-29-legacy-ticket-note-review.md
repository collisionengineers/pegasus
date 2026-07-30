# Change: Resolve legacy ticket-note product decisions

```yaml
id: 2026-07-29-legacy-ticket-note-review
type: decision
status: in_review
risk: high
created: 2026-07-29
updated: 2026-07-29
issue: pending explicit authorisation for a GitHub write
pull_request: none
baseline: local reviewed documentation head
target_release: existing capability allocations
roadmap_horizon: Now, Next, Later
mode: development
supersedes: none
superseded_by: none
```

## Required outcome

Distil the user's direct resolutions from the retained legacy ticket-note review
into Pegasus's canonical product, domain, capability, and visual-design owners.
The supplied notes remain raw historical evidence and are not indexed or treated
as product authority.

## Affected canonical owners

- [Domain glossary](../../CONTEXT.md): Case, Image Case, Image Intake Reference,
  `Not ready`, and image-readiness terminology.
- [Product requirements](../requirements.md): intake, Case identity and
  lifecycle, image evidence, engineering handoff, dashboard, and interaction
  requirements.
- [Capability inventory](../capabilities.md): current label of UI-04.
- [Design requirements](../../design/product/requirements.md) and
  [UI specification](../../design/product/ui-spec.md): visual and interaction
  contracts.

## Accepted direct decisions

### Image-led intake, identity, and evidence

- Image-only intake with a usable normalised VRM creates an **Image Case** in
  `Not ready`, carrying an Image Intake Reference rather than a Case/PO.
  It is not `Needs sorting` solely for lacking a formal instruction or Principal.
- An Image Intake Reference is `{normalised VRM}-{sequence}`, begins at `-01`,
  has a two-digit minimum, expands after `-99`, and is never reused.
- An Image Case may consolidate only into one eligible instructed pre-report
  Case. Automatic consolidation needs an unambiguous normalised VRM and no
  explicit contradictory identity evidence; otherwise an authorised staff
  member makes a reasoned decision. Both identities, evidence histories, and
  source origins remain permanent. Reversal is permitted only before report
  delivery and restores both Cases to `Not ready`.
- A reliable different-VRM or vehicle-colour indication excludes the retained
  image from Case-vehicle and report-image selection. It is third-party vehicle
  evidence when enough detail identifies that other vehicle, otherwise
  unmatched-vehicle evidence.
- A normal Case/PO sequence keeps `001`–`999`, expands to `1000`–`9999`, and
  then fails closed. No reference is truncated, wrapped, or reused.

### Lifecycle, readiness, and cancellation

- Missing required source images keep a Case `Not ready`; image quality and
  coverage assessment remains advisory and never affects allocation, state,
  Review, Engineer eligibility, chasers, or staff discretion.
- Every unmet progression requirement is a named, actionable blocker with its
  source/provenance, reason, and permitted resolution. No aggregate opaque
  field-review blocker is allowed.
- Actions are enabled only by their explicit current prerequisites. An unchanged
  or unrelated save cannot unlock an action or reset lifecycle, readiness, or
  advisory state.
- A cancellation message for a pre-report Case creates `Held pending staff
  decision`, not an automatic terminal state. Staff may confirm `Provider
  cancelled`, or release the Case only after the message is itself reasonedly
  recategorised or unlinked/reassociated with permanent before/after history.

### Engineering image selection

- The evidence surface retains source images, provenance, categories, and
  advisories. It does not host opposing report-image inclusion/exclusion
  controls.
- The Engineer report-generation section owns human report-image selection and
  order. A staff override may reject automated registration visibility advice,
  but the first overview still requires a human-confirmed full readable
  registration. Reflections remain excluded.

### Mailbox interaction

- `provider-chasing-for-update` is a distinct `in-progress-cases` subtype.
- At the allocated mailbox-workspace activation, staff can filter and queue
  each approved mailbox exactly. The accessible email quick preview is
  navigation only and cannot mutate message, classification, association, Case
  state, or custody.
- Evidence image preview makes loading and enlarged-view states explicit while
  preserving the source occurrence and Case context.

### Operations and visual interaction

- The dashboard metric is **New cases today**, counting every instructed Case
  and Image Case created in the current Europe/London day, including a Case
  subsequently terminally merged or closed that day; it excludes Triage,
  `Needs sorting`, and `Blocked intake`.
- `Sent to Engineer` and `Reports sent` retain their settled wording and day/week
  semantics.
- Search results are full-row keyboard-focusable controls with a visible
  affordance. At constrained desktop width, a long Case/PO or Image Intake
  Reference moves to a labelled second line instead of overlapping the received
  timestamp.
- Inbox and intake rows always show received date above time and their exact
  processing outcome rather than generic `New`.
- A semantic action or state has one consistent icon throughout Pegasus; a
  decorative or generated substitute is not permitted.

## Deferred-capability impact

No new capability, project, runtime, store, integration, or deployment unit is
created. The Image Case terms refine existing `INT-27` and related current
intake/case capabilities. The report-generation selection section is a
preserved product seam: it creates no alpha control, caller, model, provider,
or dormant UI. Any activation remains subject to the existing capability
allocation, accepted engineering-work contract, real Core caller, visual review,
and operator acceptance.

## Evidence and review state

This record captures direct user decisions and local canonical-document changes
only. It does not prove implementation, a Core caller, test coverage, deployment,
or operator acceptance. An external issue has not been created because no
explicit authorisation named a GitHub-write target.

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

- [Domain glossary](../../CONTEXT.md): Case, Image intake, Image Intake Reference,
  Principal, Needs sorting, and related terms.
- [Product requirements](../requirements.md): intake, Case identity and
  lifecycle, image evidence, engineering handoff, dashboard, and interaction
  requirements.
- [Capability inventory](../capabilities.md): current label of UI-04.
- [Design requirements](../../design/product/requirements.md) and
  [UI specification](../../design/product/ui-spec.md): visual and interaction
  contracts.

## Accepted direct decisions

### Image-led intake, identity, and evidence

- Image-only intake with a usable normalised VRM creates a pre-Case **Image
  intake** carrying an Image Intake Reference. It is not a Case, receives no
  Case/PO or lifecycle state, and is not `Needs sorting` solely for lacking a
  formal instruction or Principal.
- An Image Intake Reference is `{normalised VRM}-{sequence}`, begins at `-01`,
  has a two-digit minimum, expands after `-99`, and is never reused.
- An Image intake may associate with only one eligible instructed pre-report
  Case. Automatic association requires an unambiguous normalised VRM and no
  explicit contradictory identity evidence; otherwise authorised staff make a
  reasoned decision. Both identities, evidence histories, and source origins
  remain permanent. A reasoned reversal before report delivery restores the
  Image intake to its pre-Case state and leaves the instructed Case unchanged.
- A raw recognizer result remains a suggestion and cannot exclude an image.
  Only staff-confirmed or otherwise accepted different-VRM/vehicle-colour
  evidence excludes the retained image from the Case-vehicle set and automatic
  EVA bundle. It is third-party vehicle evidence when enough accepted detail
  identifies that other vehicle, otherwise unmatched-vehicle evidence.
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
  advisories. It does not host competing report-image inclusion/exclusion or
  ordering controls.
- The focused alpha EVA handoff automatically includes every eligible
  custody-confirmed Case-vehicle image in deterministic order. EVA retains
  downstream report-image selection and ordering until its accepted
  replacement; Pegasus exposes no alpha selection step.

### Mailbox interaction

- `provider-chasing-for-update` is a distinct `in-progress-cases` subtype.
- A multi-Case provider chase is `General — general-chase`: it remains one
  unlinked source occurrence, with no source copy or one-to-many Case links.
- A non-actionable daily case summary is `General — case-summary` and creates
  no intake, Triage, or Case work.
- A `Needs sorting` item explains the missing, ambiguous, or contradictory
  predicate; it never borrows the rationale for a different classification.
- A successful focused manual EVA package is immediately downloadable with its
  JSON, every eligible custody-confirmed Case-vehicle image, and manifest. The download alone proves neither EVA
  receipt nor report delivery and does not change Case state.
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
created. The Image intake terms refine existing `INT-27` and related current
intake capabilities without allocating a Case. The report-generation selection
section remains a future product seam: it creates no alpha control, caller,
model, provider, or dormant UI because the focused alpha exports all eligible
Case-vehicle images. Any later activation remains subject to the existing
capability allocation, accepted engineering-work contract, real Core caller,
visual review, and operator acceptance.

## Evidence and review state

This record captures direct user decisions and local canonical-document changes
only. It does not prove implementation, a Core caller, test coverage, deployment,
or operator acceptance. An external issue has not been created because no
explicit authorisation named a GitHub-write target.

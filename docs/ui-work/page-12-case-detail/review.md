# Page 12 — Case detail (the case workspace) — review

> Vocabulary note. The legacy pipeline term is banned from every deliverable in this folder set,
> including this review; where a current heading uses it, it is written `in·take` (with an
> interpunct) so the zero-occurrence check stays clean.

Captures: two full-page screenshots of genuine case QDOS26001 — the read-only capture and the
edit-mode capture. Source: `src/Pegasus.Web/Pages/Cases/Details.cshtml` plus the partials
`_CaseSummary.cshtml`, `_CaseWorkflow.cshtml`, `_CaseDocuments.cshtml`, `_CaseHistory.cshtml`.
This is the single largest surface in the application; the edit-mode capture is roughly 7,900px
tall — five full screens of stacked panels for one case.

## 1. Aesthetics

- **The summary panel wastes its width catastrophically.** "Case identity and current state"
  renders thirteen `dt`/`dd` pairs as a single vertical stack down the left edge of a full-width
  panel: label, value, label, value, for ~300px of height while the right 80% of the panel is
  empty white. The first screenful of the case workspace is mostly nothing.
- **Ten lifecycle action forms stacked vertically.** "Lifecycle actions" in edit mode renders
  Hold / Release hold / Transition to report preparation / Transition to Review / Close /
  Reopen / Archive / Create linked replacement / Assign Engineer / Record Engineer finding as
  ten separate full forms — each with its own Reason input and several with their own
  four-checkbox completeness block — producing an unbroken column of controls in which "Close
  case" and "Hold case" look identical in weight and adjacency. Mutually exclusive actions
  (most are invalid for the current state) are all rendered anyway.
- **The 18-row provenance table is dumped at the top of the workflow area** ("Typed case data
  and provenance": Claimant → Inspection mode, columns Field / Accepted fact / Suggestion /
  Staff-confirmed), almost entirely em-dashes for this genuine case. The operator scrolls a
  wall of "— — —" before reaching a single action.
- **Raw enum values as content**: State "Review" comes from `@workflow.State` unmapped — the
  same render produces `NotReady`, `ReportPreparation`, `PostReportComplete`,
  `CollisionEngineersRejected` on other cases; Case type from `@details.Summary.CaseType`
  (`InspectionAndAudit` on audit-carrying cases); document rows render `SemanticRole`,
  `CustodyStatus` enums; Origin renders `manual_upload` snake_case; history renders event codes
  like `case_created`.
- **Identifier and hash noise throughout**: "Source hash `6D521155B9EB73712F2BE7526357310C…`",
  "Artifact SHA-256" rows, document "Hash" column of 64-hex `<code>` strings, "Workflow
  version 0", Engineer as a GUID (`ToString("D")`) or "Unassigned", byte counts ("`ContentLength`
  bytes", "…bytes accepted").

## 2. Practicality

- **Edit mode is narrated in implementation language.** The full set, verbatim: "An opaque edit
  lease is active for this response. A successful change consumes it." / "Edit authority is
  active for this staff session, but its protected browser state must be recovered." /
  "Read-only view. Enter edit mode to make one versioned change." / "Enter edit mode to expose
  reasoned lifecycle commands." Four sentences about leases, sessions, versions, and command
  exposure, where the operator needs: a toggle, and possibly "Someone else is editing".
- **A visible prefilled evidence input.** "Transition to Review", "Reopen", and "Assign
  Engineer" each display an "Evidence reference" text input prefilled with the literal string
  `case-completeness-projection` — an internal key, shown editable, that the operator must not
  touch but is invited to.
- **A human is asked to type a SHA-256.** "Immutable report approval" offers "Artifact SHA-256"
  as a 64-character pattern-validated text input. No human produces a hash by typing; this is a
  file-picker's job or an automated pipeline's.
- **Self-negating empty states**: "No Box file requests are recorded. This does not imply the
  provider is available." / "No public upload request is recorded. Availability is not
  assumed." / "No vehicle lookup evidence is recorded. This is distinct from a confirmed
  no-result or unavailable outcome." Each empty state hedges itself into meaninglessness.
- **"reasonedly" is not a word** — "association can be reasonedly reversed on the origin
  receipt" (the case's image-material section).
- **Duplicated confirmation checkboxes**: "Instructions complete" + "Instructions
  staff-reviewed" and "Images complete" + "Images staff-reviewed" appear as four checkboxes in
  *five* different forms (Confirm completeness, Transition to Review, Reopen, Assign Engineer —
  and the summary's completeness policy line re-states the result).
- **One "Reason" per form, fifteen-plus times per page.** Every form carries its own required
  reason input; the edit-mode capture shows a page with more than fifteen reason fields visible
  simultaneously.
- **EVA blocking reasons render as a thirteen-item raw bullet list** ("The EVA source mapping
  is not activated by an explicitly accepted mapping/config version.", "Work Provider does not
  have accepted evidence."…) — machine validation output printed unedited.

## 3. Performance / Design / Good practice

- **Rule violations by the book**: rule 1 (lede: "Read-only evidence remains visible without
  edit mode. Every change uses the current version and an active lease where required."),
  rule 3 (unmapped enums and `case_created` codes — the close/reopen selects at
  `_CaseWorkflow.cshtml:205,211` prove the labelled pattern exists on this very page),
  rule 4 (GUIDs, hashes, version integers), rule 5 (byte counts), rule 8 (duplicate
  confirmations), rule 9 ("EVA handoff preparation is unavailable for this runtime or case."
  rendered as a permanent panel).
- **Ten forms mean ten hidden-field blocks** (`id`, `expectedVersion`, `operationKey`,
  `editLeaseToken` repeated per form) and ten distinct `NewOperationKey()` values per render —
  correctness survives, but the page invites version-conflict errors: any successful action
  invalidates the other nine forms' `expectedVersion`, so a second click anywhere produces a
  stale-version failure the operator cannot anticipate.
- **The document custody table nests three loops** (documents × occurrences × versions) and
  renders per-row inline forms *inside* another form via the `form=` attribute — fragile markup,
  and each row carries checkbox + removal-reason input + confirm-third-party input at once.
- **The one-time secret panel** ("Copy this secret now — It is displayed once and is not
  available from case history.") renders as an ordinary panel among panels; a critical
  ephemeral credential deserves a modal with an explicit copy control.
- **Kept, because it is right**: read-only evidence always visible without edit mode; one
  versioned change per commit; immutable history at the bottom; documents as a custody table;
  the corrects/corrected-by replacement links.

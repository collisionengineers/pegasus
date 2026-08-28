# Research — INTK-046 (lane C2)

## Verified by read-only check (worktree at origin/dev 4d696225)

- Wave-1 shell and vocabulary are on `dev`: `_Layout` renders the one
  `main.app-main`; `site.css` declares the record frame (`record`,
  `record-head`, `record-identity`, `record-accent`, `record-bar`,
  `record-body`), `definition-list`, `fact-grid`, `timeline`,
  `notes-list`/`note-entry`/`note-meta`, `notice`, `btn` family. Old
  classes (`detail-list`, `review-grid`, `field-card`, `page-heading`,
  `status-card`, `primary-action`) live only in the LEGACY block
  (site.css:851+) that wave 5 deletes — the port must move off them.
- CASE-012's `Cases/Details.cshtml` is the merged record-page reference:
  `page-header` (eyebrow + h1 + Back to Cases + Refresh `data-refresh-form`),
  `article.record`, `record-bar` with per-state buttons,
  `_ReasonDialog` with `DialogHiddenFields`, `data-dialog-open` triggers.
  No page sets `WorkspaceRecord` yet — matching that.
- Prototype final layer (the contract): `triageCaseView` (line 1860),
  `renderUnidentified` + resolve dialog (1028/1182, route-renamed 1673),
  `renderImageCase` (1127). Earlier layers (`renderTriage` 1027, the
  old Triage action bar, `assessment-v2`) are dead — not ported.
- Core seams (all existing, no new ones needed):
  - Triage: `ITriageStore` transitions behind `DetailsModel.OnPostActionAsync`
    (assign, unassign, await_information, record_finding,
    supersede_finding, link_response, unlink_response, complete, cancel,
    reopen, link_case, unlink_case). `CompleteTriage` requires
    `FindingRecorded`; findings supersede, never overwrite.
  - Unidentified: `IResolveUnidentified` validates the destination exists
    (case/image-intake/triage via their queries, blocked receipt by
    decision, `ExternalReference` free-form, no port).
  - `UnidentifiedMediaKindPolicy`/`OperatorLabels.EmailHandle` already
    own kind/handle wording shared with the queue rows.
- `OperatorLabels` has state/reason/media-kind/channel maps but **no**
  map for `UnidentifiedResolutionTargetKind`; the live page prints enum
  names via `GetEnumSelectList`. One label map is added there (one list
  per concept).
- Inbound links to keep working (6+): Cases queues rows (triage,
  unidentified, image), Case Files vehicle-images links, upload outcome
  (`/Unidentified/{id}`, `/Received/{id}`), Work Centre work items,
  Received page cross-links, Search results. Routes are unchanged.
- Owned tests' load-bearing assertions (not run here; build only):
  - `TriageEvidenceImagesWebTests`: "Vehicle images" section + asset
    hrefs `/Received/{id}/Asset/` on `/Triage/{id}`.
  - `ImageIntakeWebTests`: "Register Image intake" (+ its `</h2>` absent
    after registering), "No readable registration",
    "Vehicle images registered", "AB12CDE-01",
    "Associated with Case" on `/Received/{id}`; on `/VehicleImages/{id}`:
    "AB12CDE-01", "awaiting definitive instruction".
  - `ImageViewingWebTests`: gallery emits `data-evidence-set/item`,
    `data-file-name="vehicle.png"`, `alt`, `loading="lazy"`, and the
    `_EvidenceViewer` Previous/Next/Save as/Close buttons — all come from
    the retained `_ImageGallery`/`_EvidenceViewer` partials.
  - `QdosIntakeWebTests`/`GroupedIntakeWebTests`: exercise
    `/Upload/Status` and stores, not this lane's markup; only the
    `/Unidentified/` and `/Cases/` link targets must survive.

## Assumed (flagged for review)

- The contract's Resolve-dialog wording "Create Case from accepted
  instruction" has no `UnidentifiedResolutionTargetKind` behind it: Core
  resolves to an existing destination only, and creating the case is the
  origin receipt's action (auto-resolved by reconciliation, INTK-048).
  Rendering it would be an inert control. The select therefore carries
  the five real kinds with operator labels (see plan P3).
- Triage identity: Core has no operator reference or provider field; the
  registration is the record's identity and the source channel is the
  closest "provider" fact. The prototype's "TR-…" refs and provider names
  are fixture data — not copied.

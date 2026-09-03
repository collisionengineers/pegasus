# Open questions — ENG-031 (2026-09-02)

The plan (2026-09-02) adopts an ASSUMED default for each question so it can
be executed once the operator confirms or overturns it; the conditional
approval-linkage steps stay unbuilt until Q1 is answered or parked.

- [ ] Which durable event defines the report version that snapshots curation:
  report-draft generation, report approval, detected sent evidence, or a
  specified combination? The current `CaseReportApprovals` record and the
  sent-evidence record are separate. Plan default (ASSUMED): report approval
  — FRD-11 says draft generation saves nothing, so the approval record gains
  an immutable curation-snapshot reference beside the artifact identity and
  SHA-256.
- [ ] What operator-controlled, durable disposition identifies an image with a
  person's reflection? No such classification exists in code, yet
  `docs/frd/frd-06-vehicle-and-engineering-evidence.md:129` says report-image
  selection continues to exclude it. Is the "Not used" role sufficient, or must
  ENG-031 add a persisted reflection marker? Plan default (ASSUMED): "Not
  used" is sufficient; no reflection marker is added, and Core eligibility
  excludes confirmed third-party evidence independently.

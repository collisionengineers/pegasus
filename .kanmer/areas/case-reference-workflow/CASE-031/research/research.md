# Research — CASE-031: claimant address extraction and EVA API

## Question

How should Pegasus extract a claimant address when source evidence provides
one, retain its provenance, expose it for staff review, and send the same
canonical value as EVA ClmAdd without changing the operator ZIP?

## Findings

- QDOS extraction defines Claimant name but no claimant address in
  QdosInstructionExtractionPolicy.cs. InstructionFieldEngine already owns
  candidates, conflicts, source labels, normalization and provenance.
  Extraction should add an explicit provider field definition and must not
  infer from inspection, repairer, sender, principal or third-party addresses.
- InstructionDraft and its SQL entity carry claimant name but no address.
  A nullable bounded draft column and EF migration are required. Nullable is
  correct because this detail is captured where available and does not gate
  otherwise safe Case allocation.
- CaseDataSnapshotFactory.AddExtractedValue is the existing provenance guard
  for promoting unambiguous intake values into Case data. Claimant address
  should follow claimant name through this helper.
- Case data is an existing versioned field-row model. CaseClaimantData is the
  correct aggregate to extend from Name to Name and Address. The existing Save
  Case command already supplies lease, version, history and provenance rules.
  No new table, service or mutation path is needed.
- Intake review already has one shared instruction-draft partial. The Case
  overview and editor each render the canonical projection once. These are the
  existing display/edit surfaces to extend.
- EVA API submission currently derives its payload from the operator-export
  mapping, but the user has explicitly excluded changes to the ZIP. The
  smallest safe seam is:
  - leave EvaReplayFields, CaseEvaMapping, EvaBundleSchema and the 13-key
    archive untouched;
  - read CaseClaimantData.Address from the already-loaded Case projection in
    EvaSubmissionStore;
  - pass it separately into CaseEvaApiMapping and EvaInstructionPayload;
  - serialize exact key ClmAdd in EvaApiTransport.
  This preserves one canonical Case value without changing archive bytes.
- EVASubmissionStore must validate claimant address before image loading or
  transport. Missing, whitespace-only and over-40-character values should
  return a named blocking reason and make no network call. Truncation would
  create a different address and is not acceptable.
- The normalized vendor guide linked by [[DOCS-015]] defines ClmAdd as required
  with maximum length 40.
- Controlled test-environment submissions on 2026-08-28 proved null, empty,
  ordinary/line-break whitespace and U+00A0 receive HTTP 400. Period, hyphen,
  apostrophe, U+0000 and U+200B produce opaque HTTP 500. No placeholder is
  safe or allowed.
- Existing tests provide focused seams for extraction, draft/Case persistence,
  browser display/edit, API mapping, exact transport JSON and no-network
  blocking. EVA bundle tests should run unchanged as a non-regression gate.
- No external research sources are declared for this ticket. Inputs were the
  linked vendor guide, governing docs, current source and controlled EVA test
  responses.

## Implications

- Extend existing intake and claimant models only.
- Keep claimant address optional for Case allocation and required only at EVA
  API submission.
- Use conservative explicit labels and existing conflict handling.
- Preserve a bounded canonical Case value; enforce EVA's 40-character boundary
  locally without truncation.
- Do not modify the ZIP schema, mapping identity, fixtures or deterministic
  output.
- No further live EVA requests are required for implementation proof.

## Open questions

None. The operator explicitly ruled the ZIP out of scope.

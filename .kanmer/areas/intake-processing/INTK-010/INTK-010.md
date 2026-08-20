---
id: INTK-010
type: ticket
title: >-
  Upload flow v2: clean per-file rows with progress, then a confirmation step
  offering attach-to-case or create-case
status: done
area: intake-processing
assignee: claude-code
profile: feature
stageEntered:
  implementing: '2026-08-19T23:47:55.366Z'
  review: '2026-08-20T00:37:10.289Z'
  verifying: '2026-08-20T00:57:15.252Z'
  done: '2026-08-20T01:29:45.309Z'
taken_at: '2026-08-19T23:18:29.012Z'
branch: task/intk-010-upload-flow-v2
worktree: ../pegasus-worktrees/intk-010-upload-flow-v2
labels:
  - upload
  - ui
  - design
  - operator-reported
links:
  - DELIV-012
  - INTK-005
  - INTK-006
  - INTK-008
  - CASE-003
  - INTK-009
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-12-operator-experience.md
  - docs/design/README.md
prs:
  - '433'
deployment: production
archived: false
created: '2026-08-19T23:17:07.323Z'
updated: '2026-08-20T01:29:45.309Z'
---

## What

Rebuild the staff upload experience per the operator's direction of 2026-08-20, given verbatim against the deployed release-12 page:

> "the upload page has the files leaking. It should be a clean design that has rows of files with a spinning icon that turns into a tick when the upload is done. It says 'Each file has its own receipt and remains associated with this submission group.' for some reason which makes no sense. after upload it should give the user a confirmation window: Either it located a case - and offers to attach whatever it was to that case (they can override), or it offers to make a new case (either images or instructions, depending on what they had)"

Three parts:

1. **Selected-file presentation.** The current readout crams filenames into one run-on line (screenshot on file: `{4D7C134E-…}.png (1.1 MB) WhatsApp Image 2026-05-20 at 9.47.24 AM…`). Replace with clean rows — one file per row: name, size, and a per-file state icon that is a spinner while uploading and becomes a tick when that file is stored. Design-system icons and motion conventions; state never by colour alone; honest states for a failed file.
2. **Kill the mechanics narration.** "Each file has its own receipt and remains associated with this submission group." and any similar copy goes — it narrates internals ("receipt", "submission group") that mean nothing to the operator and violate the banned-terms rule.
3. **Post-upload confirmation step.** When processing resolves, the operator sees a confirmation surface:
   - **A case was located** → offer to attach the uploaded material to that case, with the operator able to **override** (choose a different destination);
   - **No case located** → offer to **create a new case** — Image-initiated or Instruction-initiated depending on what was uploaded (images vs instruction documents).

## Why

Operator review of the deployed release-12 upload flow. The current flow ends at a passive status page; the operator wants the upload to finish in a decision, not a report.

## Constraints and things the plan must settle honestly

- **Fail-closed invariants hold.** Images alone must not create a *definitive automatic* association; the confirmation step is a **staff decision**, which the product model explicitly permits ("linked automatically only on a definitive match, or linked manually by staff"). The plan must state exactly how the offer interacts with INT-28's accepted automatic association: when automation already attached at the accepted bar, the confirmation reports it (with reversal being the existing reasoned path); when automation abstained, the offer is the staff decision.
- Processing is asynchronous (Worker). The confirmation appears when processing resolves; the page already knows how to wait (`data-auto-refresh`). Per-file spinner→tick during the upload POST itself is client enhancement in `site.js` on the existing single-group submission — do not invent a second upload endpoint or break the replay-token semantics from [[INTK-005]].
- "Create a new case" must reuse the existing creation flows. **[[CASE-003]] is in scope to fix if it blocks this**: `/Cases/Create` without a receipt currently 500s; the offer will link into creation seeded from the uploaded material.
- Owns `Pages/Upload.cshtml`, `Pages/UploadGroupStatus.cshtml`, `Pages/UploadStatus.cshtml`, `wwwroot/js/site.js` (carved out of [[PLAT-010]]'s sweep). Vocabulary rules apply throughout (no "intake"/"receipt"/"custody"/GUIDs operator-facing).
- FRD-02 owns the upload/identity behaviour and FRD-12 the operator surface — both updated in the same PR.

## Verification

- [ ] Multi-file selection renders as rows; each row spins during upload and ticks when stored; a failed file states its failure.
- [ ] No mechanics copy on the upload or status surfaces.
- [ ] Located case → attach offer with override; no case → create offer with the correct case type for the content; both paths land the material where the operator chose.
- [ ] Automatic association at the accepted bar still behaves per INT-28 and is reported, not silently duplicated.
- [ ] Browser + AccessibilityTests green; visual pass at 1920.

## Outcome

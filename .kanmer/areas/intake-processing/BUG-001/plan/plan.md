# Plan — BUG-001

## Chosen approach

Treat BUG-001 as an evidence-and-disposition ticket. Current `dev` already contains the Case/PO allocation and Box custody implementation, so changing code pre-emptively would duplicate or destabilise settled owners. Verification will proceed in increasing evidence tiers and will stop at the first concrete failure.

## Governing docs

- `docs/frd/frd-02-intake-and-source-identity.md`: verify that one definitive authorised QDOS receipt reaches replay-safe automatic Case/PO allocation; do not equate receipt or staging with case creation.
- `docs/frd/frd-05-documents-extraction-and-custody.md`: verify that the allocated immutable reference owns the Box case folder, the accepted source is retained, and custody failure remains visible and recoverable.
- No governing document change is planned. The ticket tests existing accepted behaviour.

## Ordered steps

1. **Create the task worktree only when implementation/verification is authorised.** Use a dedicated BUG-001 branch/worktree from fresh `origin/dev`; record the exact head. Do not alter source merely to make the ticket produce a diff.
2. **Complete conclusive local evidence.** Run the focused allocation, mailbox-intake, custody-outbox, and Worker-composition tests with a sufficient timeout. Capture exact pass/fail counts and logs.
3. **Classify any failure before changing code.** If current-head tests fail, isolate the smallest failing boundary. File/link a narrow defect or revise this ticket only when the failure is reproducible; do not fold unrelated repair into the broad historical symptom.
4. **Confirm deployment ancestry read-only.** Identify the currently deployed Web and Worker source/package revisions and prove whether they contain `9393c983`, `379d7ddd`, `864f46fc`, `0743ac32`, `73a3380d`, and `f08e2df6`. If later fixes are absent, deployment is a prerequisite; deployment itself requires exact-target approval.
5. **Prepare an exact live-proof manifest.** Name the approved QDOS mailbox/message, production resources, expected Principal, expected single Case/PO result, approved Box root/subfolder, readback queries, timestamps, and rollback/containment boundary. Obtain explicit approval before any external write.
6. **Exercise one genuine production journey only after approval.** Capture the mailbox caller, retained receipt, processing/allocation outcome, exactly one Case/PO/reference, custody work, correctly named Box folder, retained source versions, and absence of duplicate effects on replay.
7. **Disposition.**
   - If local, deployment, and controlled live evidence pass, write `proof.md`, record the already-merged resolving commits and deployment evidence, and close BUG-001 without product-code changes.
   - If live execution fails, preserve the evidence, stop further mutation, and create a narrowly scoped fix ticket for the exact failed boundary.
   - If live approval is withheld, leave BUG-001 open and label/report it as locally resolved but production-unverified; do not call it fully resolved.

## Proof production

`proof.md` must separate:

- current-source inspection and commit ancestry;
- focused local test output;
- deployed Web/Worker source identity;
- actual Worker/mailbox caller evidence;
- production Case/PO and allocation readback;
- production Box folder/source custody readback;
- replay/duplicate-effect evidence;
- approval record for every external write.

## Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Closing from source inspection while production remains unproved | Require deployed identity plus a genuine controlled journey |
| Treating a timeout as a failure or pass | Re-run with adequate timeout and retain final test output |
| Creating a duplicate production case | Use one approved definitive message and verify replay-safe single allocation before any retry |
| Writing to the wrong mailbox, SQL estate, or Box root | Exact-target manifest and fresh explicit approval immediately before writes |
| Broadening into speculative refactoring | No source change unless a specific current failure is reproduced |
| Overstating deployment as runtime evidence | Record registration, deployment, caller execution, and business outcome as separate tiers |

## Stop point for this planning task

This document and the checklist make BUG-001 ready for a later execution decision. Do not create a worktree, take the ticket into Implementing, run a production journey, deploy, edit source, or close the ticket as part of this planning phase.

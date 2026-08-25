# Plan — INTK-040: Route unidentified mailbox image attachments through Image Intake

## Approach

Add one narrow mailbox-to-group adapter at the queued-intake boundary: when a newly processed mailbox receipt would otherwise become Unidentified, has no instruction or prior Case/Triage route, and contains direct image attachments, read only those attachment artifacts and submit them through the existing grouped intake service. Preserve the mailbox source channel and a parent receipt id on the group so retries are idempotent and provenance is explicit. This beats treating the EML as image-only (which custodies the wrong bytes), scanning attachments inside mailbox policy (which duplicates Image Intake), or retaining both outcomes (which contradicts the operator ruling).

## Governing docs

- **Modifies docs/frd/frd-02-intake-and-source-identity.md with explicit user authorization** — specify that an otherwise-Unidentified mailbox message with direct image attachments becomes one grouped Image Intake submission, with existing matched, Image-initiated Case, unreadable/conflicting, and technical-failure outcomes.
- **Meets docs/frd/frd-05-documents-extraction-and-custody.md** — each selected photograph becomes a normal retained child source, while EML and inline assets are not registered for image custody.
- **Meets docs/frd/frd-12-operator-experience.md** — suppress the redundant parent queue item so one submission settles to one operator-facing group outcome.
- **Meets docs/adr/0029-image-initiated-case-projection.md** — invoke the existing grouped image lifecycle and keep Image-initiated Case creation/projection in its existing owner.
- **Modifies docs/operator-notes.md with explicit user authorization** — record the binding business ruling that the image group replaces the parent Unidentified outcome and does not retroactively alter U35.

## Steps

1. Extend grouped intake's internal request and persisted group with source-channel and nullable parent-receipt provenance, enforce replay consistency, update fakes/callers, and add the schema migration plus exact Worker grants.
2. Add the mailbox image submission adapter and wire it into fresh queued processing before completion; defer the parent Unidentified item only for eligible candidates, use a stable receipt-derived group token, and turn terminal child-submission failure into one technical-failure Unidentified result.
3. Compose the existing grouped intake service in the Worker and update the authoritative operator note and FRD behavior.
4. Add focused unit/integration tests for a three-JPEG U35 shape, match/no-match/no-readable/conflicting outcomes through existing group automation, inline/no-image/instruction/Case/Triage exclusions, replay idempotency, terminal failure visibility, and custody selecting child JPEG sources rather than EML/inline assets.
5. Run locked restore, Release build, focused tests, relevant integration/architecture tests, and full tests; inspect the branch diff for reuse, simplification, efficiency and abstraction altitude, applying behavior-preserving findings.

## Verification

Run the repository's locked restore/build commands from the runbook, then filtered Core tests for grouped intake, processing deferral and mailbox submission; filtered Integration tests for mailbox/group persistence and custody; Architecture tests for DI/migration/schema invariants; and the full solution test command. Record exact successful commands and named behavioral tests in the post-implementation report. The later merged verification writes proof on merged dev/release state; this execution does not deploy.

## Risks / open questions

- A crash after staging some child receipts must not duplicate them; stable group/member tokens and parent consistency make retry deterministic.
- Parent suppression must not strand work; submission occurs before work completion, and a terminal submission failure registers a technical-failure Unidentified item.
- Existing U35 must remain unchanged; the hook runs only in the fresh-processing branch and no backfill/replay job is added.
- INTK-039 remains taken in Verifying, but PR #545 is already merged. This task is based on current origin/dev and will not modify that ticket's branch/worktree.

## Simplification pass — 2026-08-25

Independent review covered reuse, simplification, efficiency and abstraction altitude over the branch diff.

- **Reuse:** replaced the new parent-specific group query with the existing mailbox channel + stable submission-token lookup; retained `ParentReceiptId` only as persisted provenance.
- **Clarity:** made grouped source channel explicit at every caller and removed the unsupported speculative Automation operation branch.
- **Consistency:** reused `DownloadIntakeSource.FixedTimeHashEquals` and checked retained content length as well as hash.
- **Correctness found during the pass:** a final failure after one child was staged could have opened a parent U-item and later a child U-item. The terminal outcome is now keyed to the partial submission group, and grouped reconciliation replays that same technical-failure origin. Added a focused assertion proving group-scoped identity.
- **Disposition:** all findings applied. No abstraction, cache, queue, flag, compatibility path, or unrelated cleanup was added. `git diff --check origin/dev` passes (line-ending notices only).

## Review fix — 2026-08-25

Independent PR review found that a recoverable final group read could fail after every child member was already durable. Registering a technical U in that state would compete with the complete group's normal Image Intake outcome. The catch now suppresses a technical U only for a transient failure when durable membership is complete; incomplete groups retain the group-scoped technical failure, and non-transient identity conflicts still fail closed. A focused regression covers the completed-group case.

# Independent review — 2026-08-25

## Changes

- `docs/operations.md` advances the canonical deployed-state summary from release 28 to release 29.
- It adds the release-29 source, image, manifest, revision and migration record.
- It records ENG-018's deployed removal of the obsolete EVA activation gate and environment settings while retaining mapping version 2 as export metadata.
- It updates the release-28 defect note to point to the deployed correction and explicitly excludes any claim of an authenticated operator Export or EVA import.

## Comments

- Non-blocking: the release-manifest artifact is retained outside the repository, so the document records its hash rather than linking a repository artifact. This matches the existing release-evidence convention.
- No blocking findings.

## Disposition

- The non-blocking retention observation is won't-do-because the immutable artifact bundle is intentionally local deployment evidence; `docs/operations.md` is the canonical checked-in current-state record.
- No follow-up ticket is required.

## Verdict

**Pass.** Independently checked PR #543's one-file diff against DELIV-019's plan and post-implementation report. Verified retained manifest SHA-256 `35C73A4D09A3BD0108CBBAE15EB5DA82295D93B2FBBDC3FA0634E848DAC17D55`, source `b1aa68c86063fbcf70658f10271e6b622e792d32`, migration identity `20260825001401_RemoveWorkflowCompletenessWaivers`, image digest `sha256:cb4803190a9db02361eb03228acf3905149ccbbbb14f008cc7b70834ddc6b31e`, ACR tag, ready Web revision, 100% traffic, absence of all `Eva__AcceptedMapping__*` Web settings, and nine enabled Worker functions. Re-ran production smoke successfully against the exact source/version. Source inspection confirms mapping version 2 remains metadata and the obsolete activation type/error/settings are absent. GitHub's applicable documentation and supporting checks are green; code lanes correctly skipped for the docs-only diff. The text explicitly avoids claiming an authenticated operator Export or EVA import.

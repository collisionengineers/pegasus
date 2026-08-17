# Proof — BUG-001

## Verified revision

- Main revision: `2b0df78cd599cef9f273a8ae04ce3b7889c97f78`
- BUG-001 merge commit present in main history: `03ce5a715fc633e80703f6711d8edfeb40a69b13`
- Feature PR: https://github.com/collisionengineers/pegasus/pull/386
- Authorized dev-to-main release PR: https://github.com/collisionengineers/pegasus/pull/394
- Release merged at: 2026-08-17T14:11:39Z
- Verification checkout: clean `main`, equal to `origin/main`

## Behaviour proved

- QDOS identity is established only from an exact effective-sender domain:
  - `qdosassist.co.uk`
  - `qdosassists.co.uk`
  - `qdoslaw.co.uk`
- A Collision Engineers staff forward uses the one proved prior/original sender.
- Once QDOS is identified, extraction does not require QDOS wording or a second instruction/scan identity gate.
- Body, subject, filename, metadata, attachment, OCR, or AI text alone cannot establish QDOS.
- Senderless scanned documents retain provider-neutral `OcrRequired`.
- Automatic allocation uses the accepted persisted route principal and fails closed when route/draft identity is absent or inconsistent.
- The change is QDOS-specific; it does not activate equivalent identity rules for every provider.

## Release CI evidence

Release PR #394 passed:

- changes
- documentation
- reference-data
- infrastructure
- unit
- browser
- SQL integration shards 1, 2, and 3
- SQL integration coverage
- source workspaces

Primary release CI run: https://github.com/collisionengineers/pegasus/actions/runs/32037447986

The source-workspaces lane initially reproduced one timing-sensitive document-extraction cancellation test: it reached `ResourceLimitExceeded` before cancellation. The other 971 tests passed and one corpus-only test skipped. The failed job was rerun unchanged and passed: https://github.com/collisionengineers/pegasus/actions/runs/32037448019

## Merged-main local evidence

Commands were run from clean main at `2b0df78cd599cef9f273a8ae04ce3b7889c97f78`.

- `dotnet restore ./Pegasus.slnx --locked-mode`
  - Passed; all projects up to date.
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
  - Passed; 0 warnings, 0 errors.
  - An earlier attempt was invalidated by an orphaned compiler from a timed-out command holding the integration-test output. After that process exited, the unchanged build passed.
- Focused Core QDOS, `ProcessIntakeTests`, and `AllocateDefinitiveIntakeTests`
  - 151 passed, 0 failed.
- Complete architecture project
  - 94 passed, 0 failed.
- Focused QDOS intake/allocation/custody integration batch
  - 36 passed, 3 failed under combined shared-fixture execution.
  - Each of those three cases then passed individually, unchanged:
    - `CompletedAllocatedUploadStatusLinksOnlyToItsCase`
    - `CancellationAfterAtomicSuccessRethrowsWithoutDeletingOrFailingTheOutcome`
    - `PrincipalCorrectionAndCompletedSourceRedeliveryCannotAllocateBeforeStaffRetry`
  - Release CI independently passed all three SQL shards and browser coverage.

## Runtime boundary

Pegasus is pre-release. No production deployment, mailbox mutation, retained-receipt reevaluation, Box write, or live-app assertion was performed or required for this code-verification proof. The proof establishes merged code and automated behaviour, not deployment.

## Result

BUG-001 is verified on merged main. The implementation and tests conform to the settled QDOS sender-identification rule, and the Done gate may be crossed.

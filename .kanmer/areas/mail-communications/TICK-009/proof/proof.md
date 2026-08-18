# Proof — TICK-009 MAIL-21 (verified on merged `main` `f1e116c6`, 2026-08-18)

Hand-off commands from the post-implementation report, run in the release worktree at `f1e116c6` (Release build, LocalDB):

```
dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~QdosMailClassificationPolicyTests|FullyQualifiedName~ProcessIntakeTests.Classification|FullyQualifiedName~ProcessIntakeTests.AmbiguousClassification"
Passed!  - Failed: 0, Passed: 29, Skipped: 0, Total: 29

dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~QdosEmailCohortTests" (run together with the TICK-010/TICK-026 filters)
  Skipped QdosEmailCohortTests.LabelledClaimTokensNeverCollideAcrossCaseFolders
  Skipped QdosEmailCohortTests.LabelledWorkTypeEmailsNeverMisclassifyAcrossFamilies
  → labelled facts skip (no labelled corpus tree on this machine, as the hand-off expects); volume fact passed with the local flat corpus discovered.
Combined run: Passed! - Failed: 0, Passed: 19, Skipped: 2, Total: 21
```

- `docs/operations.md` volume-cohort observation contains counts only (checked during the release-9 docs refresh; no filenames or PII).
- Deployment: shipped to production in release 9 (revision `pegasus-prod-web-252ow37gij--f1e116c6eb93`, Worker package `f1e116c6`); smoke passed. Live classification of a real mailbox message is not claimed here (MAIL-21 live acceptance remains a separate evidence state per the capability register).

PR #391 merged 2026-08-17T13:59:38Z.

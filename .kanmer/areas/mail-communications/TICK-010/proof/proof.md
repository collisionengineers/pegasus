# Proof — TICK-010 MAIL-22 (verified on merged `main` `f1e116c6`, 2026-08-18)

```
dotnet test tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~MailTaxonomyTests"
Passed!  - Failed: 0, Passed: 15, Skipped: 0, Total: 15

dotnet test tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj -c Release --no-build --filter "FullyQualifiedName~MailboxIntakeIntegrationTests.OtherMailClassificationDecisionReloadsNameAndReasoning|FullyQualifiedName~MailboxIntakeIntegrationTests.SentOtherMailClassificationDecisionReloads|FullyQualifiedName~MailboxIntakeIntegrationTests.SentFamilyClassificationReloadsWithAndWithoutReplyContext"
  (run together with the TICK-009/TICK-026 filters) → all three passed; combined Passed! - Failed: 0, Passed: 19, Skipped: 2, Total: 21 (LocalDB, Release)
```

Expected 15 taxonomy + 3 persist/reload — met. Deployment: shipped in release 9 (web revision `--f1e116c6eb93`, Worker `f1e116c6`); smoke passed. Live user-confirmed classification against the deployed estate is not claimed.

PR #392 merged 2026-08-17T13:51:11Z.

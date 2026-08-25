2026-08-25 implementation hand-off: commit 1cabc66e pushed; PR #548 targets dev. Release build, Core 989/989, Architecture 99/99, SQL-backed U35-shaped scenario all pass after final simplification. Full non-corpus/non-browser Integration run earlier in the branch passed 910 with 2 expected skips. No deployment and no mutation/replay of U35.

2026-08-25 review fix: commit 2440f1a6 suppresses a competing technical U only when a transient post-staging failure sees every group member already durable. Release build passes with 0 warnings/errors; focused mailbox submission tests 7/7 and full Core 990/990 pass.

2026-08-25 CI correction: head af50a650 updates the Azure bootstrap permission census for the migration's Worker INSERT grants. Test-AzureDeploymentPlan -Mode Local passes; Test-MigrationGrants accounts for all 72 migrations. Prior sql-integration-coverage failure was downstream of the failed changes gate and missing skipped-shard artifacts.

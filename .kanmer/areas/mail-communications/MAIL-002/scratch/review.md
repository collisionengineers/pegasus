## Independent review — PR #451 (orchestrator, 2026-08-20)

Verdict: **pass**, merge on green CI.

- Identifier removal is complete: `MailboxIdentity`/`InboxFolderIdentity`/`SentFolderIdentity` no longer appear in the view or bind from the form; edits resend the row's own stored identities server-side (with the regression test that caught the missing-identity precheck going red first); add-by-address resolves via the new Core port `IResolveApprovedMailboxIdentity` — Graph-backed in production, deterministic local fake offline — failing closed with one honest, GUID-free sentence.
- Copy fixes ride along (dropped the "Version" column and narration); error messages rewritten without identifier vocabulary.
- Infra: only `Graph__BaseUri` added to the Web container app (commented rationale: Web never polls). EXPECTED release-14 provision-preview diff — record at deploy.
- Deploy-time approval item correctly surfaced, not executed: the Web managed identity needs `User.Read.All` + `Mail.Read` Graph app roles for live resolution (documented in the runbook). Until granted, add-by-address fails closed with the honest message — acceptable interim, to be put to the operator at release.
- Tests: rewritten web tests 4/4, resolver-port tests 6/6, admin-policy 61/61 unchanged, a11y 24/24 incl. the page, `Test-AzureDeploymentPlan -Mode Local` green.

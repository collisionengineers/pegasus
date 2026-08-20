# Post-implementation report

Azure Identity `AuthenticationFailedException` is now mapped at the existing Deleted external boundary to `DeletedMailSearchState.Unavailable`. The real Graph source with a failing credential is driven through authenticated `/Inbox`; it renders the existing unavailable state and never reaches HTTP. Existing caller-cancellation evidence remains green.

Commit: `7932d683782669e112f3d996c6914323e8ba72d4`; PR: https://github.com/collisionengineers/pegasus/pull/469. Files: `GraphApprovedSources.cs`, `MailWorkspaceWebTests.cs`; Graph cancellation coverage remains in `ProductionGraphSourceTests.cs`. No retry, exception wrapper, credential change, or external write.

## Verification

- Release solution build passed with 0 warnings/errors.
- Core retained-mail class: 27/27 passed.
- Focused Graph/Web/SQL blocker slice: 27/27 passed.
- Complete `MailWorkspaceWebTests` plus `RetainedMailPersistenceTests`: 38/38 passed.
- Exact normalized-body SQL rerun: 1/1 passed.
- `git diff --check`: passed.

No external/cloud/mailbox write, deployment, backfill, merge, or self-review occurred.

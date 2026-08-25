# Research — PR-061

FRD-07 says Export is available only while the Case is in Review. `ExecuteAsync` reads the case projection and checks Review before package construction. `RecordExportAsync` later starts a serializable transaction and locks `CaseWorkflows`, but its lock helper returns only the Case id and never validates the locked state. A concurrent case-data save can therefore demote Review to NotReady after the first read but before the export record commits.

The existing `AcquireExportRecordLockAsync` and transaction are the correct owner. Return the locked workflow state from that helper and throw the existing `CaseNotInReviewException` before replay/proxy/history work when it is not Review. No schema, service, flag, retry system or compatibility path is needed.

The existing SQL export test already creates a real Review case and inspects history/proxy. A deterministic regression can acquire the workflow row lock in a separate transaction, start Export so it waits, demote the row to NotReady in the holding transaction, commit, and then assert Export fails with no new history/proxy. No open questions.

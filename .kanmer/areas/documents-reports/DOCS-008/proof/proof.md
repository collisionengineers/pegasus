# Proof

**Shipped:** PR #510, commits `7af59b2d` / `e65bf2ce` / `6e0df6f3` ·
**Deployed:** Release 20, `05fe7a7f`, smoke-asserted source SHA, revision
`pegasus-prod-web-252ow37gij--05fe7a7f2d86` at 100% traffic ·
**Migration applied:** `20260822044425_GrantWorkerCaseDocuments`.

## The denied operation now succeeds, checked as the Worker itself

This is not a role-grant listing — it impersonates the actual database principal
the Worker connects as and asks the engine whether the exact operation that was
failing is now permitted:

```sql
EXECUTE AS USER = 'pegasus_worker_runtime';
SELECT HAS_PERMS_BY_NAME('dbo.CaseDocuments','OBJECT','INSERT'), …
REVERT;
```

```
TableName            CanInsert  CanSelect/Update
CaseDocuments            1            1
DocumentVersions         1            1  (UPDATE)
DocumentOccurrences      1            1
```

Before the release the same three tables carried `DELETE DENY` and no grant at
all, which is why `EfQueuedCustodyProcessor.cs:375-420` — the code that selects
from `DocumentOccurrences` and inserts into all three — threw `SqlException` 229
inside a `DbUpdateException` after the Box uploads had already succeeded.

The grant matrix read back from `sys.database_permissions` after the bundle
applied:

```
pegasus_worker_runtime_role  CaseDocuments        SELECT GRANT  INSERT GRANT               DELETE DENY
pegasus_worker_runtime_role  DocumentOccurrences  SELECT GRANT  INSERT GRANT               DELETE DENY
pegasus_worker_runtime_role  DocumentVersions     SELECT GRANT  INSERT GRANT  UPDATE GRANT DELETE DENY
```

DELETE remains denied, as the least-privilege baseline requires.

## Tests

```
dotnet build --configuration Release                     0 warnings, 0 errors
AzureSqlRuntimeRoleMigrationTests                        12 passed
~Custody                                                 77 passed
CommittedMigrationCreatesTheSqlServerSchema              passed
CI on PR #510                                            11 checks green
```

`LatestMigrationGrantsWorkerTheCaseDocumentTables` now forbids the exact state
production was in.

## Evidence tier, stated honestly

The **cause is fixed and verified live**: the operation that failed is permitted,
checked against the real principal on the real database. What has **not** been
observed is a case completing custody end to end, because no case has been
created since the migration applied and a terminal failure is never retried
automatically — QDOS26009 and QDOS26010 still carry their `failed` records.

Two routes close that gap, both the operator's to take because both write to Box:
one instruction through the pipeline, or the **Retry custody** control already on
the case page (`_CaseWorkflow.cshtml:486` → `Custody.cshtml.cs
OnPostRetryCustodyAsync`), which re-runs custody on an existing failed case with
a reason. Either one also closes [[DOCS-006]], [[DOCS-007]], [[CASE-013]],
[[CASE-014]], [[CASE-017]], [[INTK-029]] and [[INTK-030]].

## Not fixed here

Nothing verifies a runtime role's grants against what the code actually writes,
which is why this defect class has shipped three times. Filed as [[PLAT-035]]
with the disposition recorded in the post-implementation report rather than
dropped.

# Plan — grant the Worker the document tables

## The cause, proven

Read live from `pegasus-prod-sql-252ow37gij/pegasus` on 2026-08-22 (read-only,
`sys.database_permissions` joined to `sys.objects`). Of the three tables the
custody path writes, the Worker role holds **only the DELETE deny**:

```
pegasus_worker_runtime_role  CaseDocuments        DELETE  DENY
pegasus_worker_runtime_role  DocumentOccurrences  DELETE  DENY
pegasus_worker_runtime_role  DocumentVersions     DELETE  DENY
```

No SELECT, no INSERT, no UPDATE. Every other table the Worker writes carries an
explicit grant beside its deny. The Web role does hold them
(`20260729199000_RuntimeRoleReconciliation.WebGrants:110,125,126`); the Worker
block at `:166` never listed them, because when that baseline was written only
the Web created case documents.

`fef817b8` (DOCS-007, shipped in Release 17, merged 2026-08-21T22:28Z) moved
document registration into the Worker — `EfQueuedCustodyProcessor.cs:375-420`
selects from `DocumentOccurrences` and inserts into all three. The Worker has
been denied ever since.

Every observed fact follows from this and nothing else:

| Observation | Explained |
| --- | --- |
| The files really are in Box | The uploads run before the record write and succeed |
| `custody_unexpected_failure` | `SqlException` 229 inside `DbUpdateException` — not in the classifier's list |
| `AttemptCount = 1`, never retried | Unclassified ⇒ terminal, not transient |
| `CaseDocuments` = 0 rows for both cases | The INSERT is the thing being denied |
| `create_image_case_custody` **completed** at 23:31Z | Image custody writes `ImageIntakes`, which the Worker *is* granted — so Box access was never the problem |
| Only QDOS26009 (23:00Z, 21 Aug) and QDOS26010 (02:01Z, 22 Aug) affected | Both are the only cases created after Release 17 deployed |

This is CASE-010 repeating exactly: `20260821095500_GrantWorkerVehicleLookupRequests`
records the same class of defect — "the request row INSERT the sweep performs
was denied on the deployed estate … Local/LocalDB tests run full-privilege and
never exercise the least-privilege role, so this only ever failed against the
deployed estate."

## Steps

1. **Migration.** Copy `20260821095500_GrantWorkerVehicleLookupRequests` and
   change the grants to the three tables, using the Web role's own strings:
   `CaseDocuments` → `SELECT, INSERT`; `DocumentOccurrences` → `SELECT, INSERT`;
   `DocumentVersions` → `SELECT, INSERT, UPDATE`. UPDATE on versions is required
   — `EfDocumentCustodyStore.cs:75-81` clears `IsCurrent` on prior versions.
   `Down` revokes exactly those and nothing else. DELETE stays denied.
2. **Test.** One `[Fact]` in `AzureSqlRuntimeRoleMigrationTests`, shaped like
   `LatestMigrationGrantsWorkerAutomaticVehicleLookupInsert`, asserting the
   Worker's *exact* permission set per table and that DELETE remains denied.
3. Build Release, run the runtime-role test class, then the custody tests.
4. PR to `dev`, green CI, review, merge, promote, deploy, **apply the migration**
   (`efbundle` — this release has a schema change, unlike the last three).
5. Verify on the estate by re-reading `sys.database_permissions`, then close the
   loop on a real case.

## Why not a broader repair

Two smaller ideas were considered and rejected:

- **Classify `DbUpdateException` as transient so custody retries.** It would
  have retried this failure forever without fixing it, and it is wrong in
  general — a constraint violation is not transient. The Release 19 diagnostic
  already solves the visibility half honestly.
- **Add a test that walks every `context.Set<T>()` in Worker code and asserts a
  matching grant.** That is the test that would have caught this, and both
  earlier instances too. It is real work with real value, but it is a different
  change from fixing the outage, and it needs a reliable way to attribute a
  store to a runtime role. Filed as its own ticket rather than smuggled in here.

## Acceptance

- The Worker role reports exactly `SELECT, INSERT` on `CaseDocuments` and
  `DocumentOccurrences` and `SELECT, INSERT, UPDATE` on `DocumentVersions`,
  read back from the production database after the migration applies.
- A case created after the deploy reaches custody `completed`, and its
  `CaseDocuments` row count is non-zero.
- That single case also closes [[DOCS-006]], [[DOCS-007]], [[CASE-013]],
  [[CASE-014]], [[CASE-017]], [[INTK-029]] and [[INTK-030]], all of which have
  been waiting on custody succeeding.

## Simplification pass

To be recorded here before the PR opens.

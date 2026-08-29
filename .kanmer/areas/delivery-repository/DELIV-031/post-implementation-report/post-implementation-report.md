# Post-implementation report

Rewritten 2026-08-29 after adversarial verification returned `needs-work`.
The first version overclaimed its evidence; the corrections are listed under
"What the first report got wrong" and the full dispositions are in the plan.

## What changed

`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`, in
`LocalDbTestDatabase`. One file, +27/-2.

- `BuildConnectionString` (~line 534): `ConnectTimeout` raised from 15 to 60
  on the single shared `SqlConnectionStringBuilder`.
  `MultipleActiveResultSets` and `InitialCatalog` unchanged.
  **This is the fix for the signature the ticket names.**
- `DisposeAsync()`'s `DROP DATABASE` path (~line 768): a bounded retry around
  `ExecuteNonQueryAsync()` — 5 attempts, `TimeSpan.FromSeconds(attempt)`
  backoff (10s total), filtered to `SqlException.Number == 5061`
  (`lockNotPlacedErrorNumber`). The connection and command are reused across
  attempts; the fifth attempt rethrows. **This is the fix for a second,
  distinct signature found in CI job 98903659122.**

No other file changed. No `src/` file, workflow or script touched.

## Evidence

| Claim | Command | Result |
| --- | --- | --- |
| The connect-timeout signature | ticket body + four job logs | `[Post-Login] complete=13999/14000/14014` ms against a 15s budget |
| The 5061 signature is real and originates here | `gh run view --log-failed --job 98903659122` | `ALTER DATABASE failed because a lock could not be placed`, stack at `LocalDbTestDatabase.DisposeAsync() ... :line 767` and `:773` |
| 5061 is the right number | `SELECT message_id FROM sys.messages WHERE text LIKE 'ALTER DATABASE failed because a lock%'` | `5061`, severity `16` (connection survives) |
| The transient family `ConnectRetryCount` covers occurs here | grep of all five logs for 4060/40197/40501/40613/233/10928/10929 | **zero hits** — hence removed |
| Failures are cold-start | first-failure offsets in the five logs | **no** — 3m27s to 6m29s in |
| Contention source | `xunit.runner.json`, `ci.yml:149-183` | 4 parallel collections per shard; shards are on separate runners |

## What the first report got wrong

1. **The 5061 citation.** It said 5061 was "the second failure signature in
   the ticket's evidence". It is not — the ticket names one signature. 5061 is
   real, but it lives in a fifth CI job the ticket never listed. The verifier
   was right to call this a fabricated citation even though the error number
   itself turned out to be correct and well-founded.
2. **`ConnectRetryCount` / `ConnectRetryInterval`.** Described as "ADO.NET's
   built-in idle-connection retry". Wrong: Microsoft documents that open
   connection resiliency does cover `SqlConnection.Open`. But the properties
   retry only the built-in transient *server* error list, which excludes the
   client-side -2 this ticket targets — so they could never have fixed it, and
   nothing they do cover appears in any log. **Removed**, not merely
   re-explained.
3. **"Per the ticket's explicit Do NOT list."** No such list exists on the
   ticket; the constraint came from the orchestrator's lane brief.
4. **The contention was described as cross-shard.** Each shard has its own
   runner. The contention is intra-shard, between four xUnit collections.
   Found while re-checking, not raised by the verifier.
5. **No simplification pass.** The plan promised one and none was run before
   the PR opened. It has now been run and recorded.
6. **The retry backoff (2.5s) was too small** for the window the same commit
   cited. Now 10s, with the two failure modes no longer sharing one
   justification.

## Build

`dotnet build ./Pegasus.slnx --configuration Release`

Exit code 0 — 0 Warning(s), 0 Error(s).

## Tests

Both filters re-run against the final committed bytes:

- `--filter "FullyQualifiedName~IntakePersistenceIntegrationTests"` —
  Failed: 0, Passed: 10, Skipped: 0, Total: 10 (1m12s).
- `--filter "FullyQualifiedName~CaseTaskArchivePersistenceTests|...
  OrganizationAdministrationWebTests|...AutomationConnectorAuthorizationTests|
  ...LocalDbTemplateDatabaseTests"` — Failed: 0, Passed: 48, Skipped: 0,
  Total: 48 (2m18s).

The second filter is new this round: the first report only exercised the file
it edited, never the classes that actually failed in CI. It includes
`CaseTaskArchivePersistenceTests`, the class that raised the 5061.

No assertion was weakened, skipped, deleted or inverted.

The ticket's own acceptance — ten consecutive `sql-integration` runs without a
connection-timeout failure — is post-merge CI evidence and is carried to
`verifying`. It cannot be gathered locally: one local process does not
reproduce four-way collection contention on a four-vCPU runner.

## Follow-up raised

- [[DELIV-033]] — evaluate an execution-strategy connection retry, the half of
  the ticket's preferred approach this lane did not deliver. Trigger-gated on
  DELIV-031's acceptance failing, so it is not speculative work.

## Out-of-scope defects found

None.

## Risks / open questions

- Four-way collection contention was not reproduced locally; effectiveness is
  confirmed by watching post-merge shard runs, per the ticket's own
  verification.
- The 5061 lock-wait duration was never measured. The 10s backoff is an
  order-of-magnitude choice against the contention window observed on the same
  instance, not a measurement. If CI still shows 5061 after this merges, the
  budget is the first thing to revisit.
- The `ConnectTimeout` raise is a mitigation for contention, not a removal of
  it. If the flake persists, the structural fix is to reduce concurrent DDL
  per instance (fewer parallel collections, or a per-collection instance),
  which is a larger change than this ticket's scope.

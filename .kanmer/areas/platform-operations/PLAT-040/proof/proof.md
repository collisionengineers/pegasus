# Proof — executed 2026-08-23, after release 26 deployed and smoked

Tier: **production**. Every figure below is a live read of the estate, taken by
the wipe script in the same batch as the deletes, or by `az` immediately after.

## SQL — `pegasus` on `pegasus-prod-sql-252ow37gij`

Before (read-only inventory, same identity, same session):

| | |
| --- | ---: |
| Tables total | 99 |
| Preserve list, listed / found in schema | 31 / 31 |
| Tables that would be wiped | 68 |
| Rows that would be deleted | **559** |

After, from the wipe batch's own verification:

```
Disposition | Tables | Rows
wiped       |     68 |    0
preserved   |     31 |  287
```

`StillHasRows` returned **no rows** — every one of the 68 is empty.

**`CaseSequences.LastAllocatedSequence` = 12**, unchanged. The next case is
QDOS26013. References are not reused; that is a product invariant and the wipe
did not touch it.

## A false start worth recording

The first attempt **failed to run at all**: my verification `SELECT` used a
subquery inside a `GROUP BY`, which SQL Server rejects at compile time. Because
it compiles the whole batch before executing any of it, nothing was deleted —
confirmed immediately afterwards by re-counting: Cases 4, IntakeReceipts 17,
IntakeAssets 106, RetainedMailboxMessages 5, UnidentifiedItems 1, all intact.
The batch was fixed and re-run. Recorded because "the destructive script threw"
is exactly the moment to prove nothing partial happened, rather than assume it.

## Blob storage — `pegcustody252ow37gij`

| Container | Before | After |
| --- | ---: | ---: |
| `transient-intake` | 77 | **0** |

`authentication-ring` and `box-links` were **not** merely left alone — this
identity has no data-plane access to either (`az storage blob list` returns
"You do not have the required permissions"). Access is closed by design
([[PLAT-017]] recorded the same), so they could not have been touched.

`pegtrans252ow37gij` containers `app-package`, `azure-webjobs-hosts`,
`azure-webjobs-secrets` untouched — Functions runtime and the deployed worker
package.

## Queues — `pegtrans252ow37gij`

`intake-work`, `intake-work-poison`, `external-work`, `external-work-poison`:
**0 messages** before and after.

## The estate still works

`Invoke-ProductionSmoke.ps1` re-run **after** the wipe:

```
Production Worker activation smoke passed (approved-live-worker).
Production smoke passed.
```

Health, exact version and SHA (`0.1.0-alpha.1` / `7d6a948a…`), anonymous
denial, https redirect and the nine Worker activation settings all pass against
the emptied database.

## Out of scope, as planned

- **Outlook** untouched. The five messages are still in the mailbox; poll
  cursors were preserved, so they will **not** be re-ingested.
- **Box** untouched. The case folders under root `405543781910` still hold the
  QDOS26009–26012 files and the four `a.QDOS26001–4` orphans from [[PLAT-017]].
  Not Azure storage, so not covered by this authorisation — raised with the
  operator as a separate decision.

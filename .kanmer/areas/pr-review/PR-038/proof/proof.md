# Proof

**Shipped:** PR #477 (`task/tick-049-mail-07-confirmed-folder-move`), merge `e4d56d9e`
**Deployed:** `git merge-base --is-ancestor e4d56d9e 4111ad29` → **true**. Release 16 runs
revision `pegasus-prod-web-252ow37gij--4111ad291779`, built from `4111ad29`, confirmed
active by `az containerapp revision list` on 2026-08-21.

## The finding

> Two confirmations with different keys can both pass the check, persist, and call Graph
> … enforce the single eligible provider operation **at the database boundary**, not only
> with a pre-insert query.

## Verified in the shipped code

`src/Pegasus.Infrastructure/Persistence/MailboxModelConfiguration.cs:118-120`:

```csharp
entity.HasIndex(item => item.RetainedMailboxMessageId)
    .IsUnique()
    .HasFilter("[Outcome] IN ('pending', 'uncertain')");
```

A **filtered unique index**, so at most one operation per retained message can be in a
non-terminal outcome at a time. Two different-key confirmations cannot both persist: the
second violates the index inside the transaction, before any provider call. That is the
database boundary the finding asked for, not a pre-insert query.

Matching-key replay is preserved by the separate `HasIndex(item => item.OperationKey).IsUnique()`
at line 117, and a deliberate new-key retry after a terminal failure is admitted because a
terminal outcome falls outside the filter.

## Not claimed

The concurrency behaviour is proved by the index and by the tests that shipped with it
(`RetainedMailPersistenceTests`, 29 facts). It has **not** been exercised against two
genuinely concurrent live confirmations in production, and this proof does not claim it
has.

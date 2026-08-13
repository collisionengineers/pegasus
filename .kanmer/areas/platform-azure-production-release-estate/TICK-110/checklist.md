# Estate reconciliation checklist

## Migrated validation — [[TICK-115]]

- [ ] With fresh exact-target approval, inventory the scheduled predecessor Key Vault purge state.
- [ ] Record the observed state without relying on expired purge dates or stale local `azd` data.
- [ ] Preserve the rule that live Azure, credentials, and destructive operations require separate approval.

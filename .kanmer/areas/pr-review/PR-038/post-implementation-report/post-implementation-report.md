# Post-implementation report — PR-038

## Outcome

Added a database-enforced single active folder-move claim per retained message. The existing operation-key uniqueness remains, while a filtered unique per-message index covers only pending/uncertain rows so terminal failure permits a deliberate new-key retry. Exact current-location probing occurs after the durable reservation, preventing a second provider move even across the SQL/provider split.

## Verification

- `ConcurrentDifferentKeysHaveOneActiveClaimAndOneProviderMove` passed and asserts the filtered unique schema, overlapping different keys, one provider move and one durable row.
- `ProviderFailurePreservesClassificationAndAllowsANewKeyRetry` passed and proves a new key after terminal failure.
- Release solution build passed with 0 warnings/errors.
- No live mailbox, Graph, cloud, permission or deployment write occurred.

## Simplicity

The dated plan records all four lenses. This is one filtered index on the existing dedicated table, not a lock service or generic framework.

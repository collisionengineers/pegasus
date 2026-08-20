# Research — PR-042

## Question

Which evidence claims in TICK-049 are not backed by executable tests?

## Verified findings

The initial branch has input validation, one persistence happy path, one Web happy path and Graph request-shape tests, but lacks exact stale classification/policy/binding, current-location mismatch, operation-key conflict, provider failure, uncertain Web recovery and classification/history preservation tests. The PIR also groups files instead of giving the final exact inventory.

## Implication

Add named tests for each behavior not supplied by PR-038–041, then replace counts/claims and inventory with actual commands/results.

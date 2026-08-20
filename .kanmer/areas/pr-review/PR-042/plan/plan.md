# Plan — PR-042

## Approach

1. Add exact named persistence/Web tests for every remaining claimed failure path.
2. Assert the mover is not called before freshness/current-location refusal and classification/history remain unchanged after provider failure.
3. Run focused/proportional commands and record exact test names/counts.
4. Replace TICK-049 overclaims with the final file inventory and observed evidence.

## Governing docs

FRD-08’s failure and preservation behavior requires executable evidence. This is evidence completion, not new scope or architecture.

## Risks

Avoid test proliferation by covering one behavior per material branch and reusing existing LocalDB/Web fixtures.

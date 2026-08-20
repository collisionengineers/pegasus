# Plan — PR-039

## Approach

1. Extend the existing result with operation key and separate failure detail.
2. For an uncertain latest result, render a status-check form using the original key and confirmation reason; do not create a new move confirmation.
3. Exercise destination, source and unresolved probe results through authenticated POSTs and assert no blind second move.
4. Run focused Web/persistence tests and update evidence.

## Governing docs

FRD-08 requires visible failure and staff-initiated retry. Reusing the exact confirmation for a probe satisfies it without repeating the provider mutation. No ADR is needed.

## Risks

A changed classification/binding must still fail freshness checks before recovery; tests will make that explicit.

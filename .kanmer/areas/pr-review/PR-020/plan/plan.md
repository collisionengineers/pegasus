# Plan — PR-020

## Approach
Extend the existing adapter catch filter for `TaskCanceledException` only when the supplied caller token is not cancelled. Estimate: 2 files, under 50 lines.

## Governing docs
FRD-08's unavailable state covers provider failure; caller cancellation remains cancellation.

## Steps
1. Add the narrow catch filter.
2. Prove timeout versus caller cancellation and simplify.

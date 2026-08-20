# Plan — PR-020

## Approach
Extend the existing adapter catch filter for `TaskCanceledException` only when the supplied caller token is not cancelled. Estimate: 2 files, under 50 lines.

## Governing docs
FRD-08's unavailable state covers provider failure; caller cancellation remains cancellation.

## Steps
1. Add the narrow catch filter.
2. Prove timeout versus caller cancellation and simplify.

## Simplification pass — 2026-08-20

- Reuse: applied — existing unavailable state and HttpClient cancellation semantics are used.
- Simplification: one filtered catch; no retry wrapper or new exception taxonomy.
- Efficiency: timeout returns immediately as unavailable.
- Altitude: transport timeout mapping stays in the Graph adapter; caller cancellation still propagates.

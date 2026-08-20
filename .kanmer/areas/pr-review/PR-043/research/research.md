# Research — PR-043

## Question

How can same-key replay remain idempotent while the original Graph move is still in flight, without weakening genuine uncertain recovery?

## Findings

- At PR #477 head `fc3b651e`, `EfRetainedMailFolderMoveStore.MoveAsync` sends both `pending` and `uncertain` replays to `RecoverAsync`. Source location is a normal observation before the original call finishes, so a concurrent replay can incorrectly persist `failed`.
- The filtered unique per-message index covers both `pending` and `uncertain`. Leaving the row pending keeps different keys excluded; changing it to failed frees the slot.
- The original provider path already catches a completed/failed provider call before probing. The smallest safe recovery boundary is to persist that operation as `uncertain` first, then run the existing probe. A crash after that save remains recoverable by the authenticated same-key action.
- Existing `BlockingFolderMover`, LocalDB scopes and `ConcurrentDifferentKeysHaveOneActiveClaimAndOneProviderMove` provide the exact overlapping-call fixture. No new framework, worker or timer is needed.
- FRD-08 requires deliberate, duplicate-safe retry and visible recoverable provider failure. Refusing a matching pending replay as “still processing” preserves both.

## Implication

Handle replay states separately: Pending throws a focused still-processing exception with no probe or mutation; Uncertain alone invokes `RecoverAsync`. When the original provider call throws, durably set the existing row to Uncertain before invoking the existing probe.

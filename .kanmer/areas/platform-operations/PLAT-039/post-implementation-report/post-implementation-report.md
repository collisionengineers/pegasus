# Post-implementation report

**PR #523**, merged to `dev` at `7d6a948a`, promoted to `main` by exact-SHA
atomic fast-forward, deployed as **release 26**.

## What shipped

`BoxJwtAuthorizationHeaderProvider` mints with `RefreshTokenAsync` and holds
the token against the lifetime Box states, renewing 120 seconds ahead of it.
Header and expiry live in one immutable `Lease`, published and read through
`Volatile`, so the lock-free fast path can never pair one token's header with
another's expiry. The mint is single-flight behind a `SemaphoreSlim`, so an
export reading eight photographs across a renewal takes one token, not eight.

`RequestTimeout` is declared beside the margin and read by the registration
that builds the Box `HttpClient`, so the two numbers are joined by the compiler
rather than by a comment. The mint guard rejects a lifetime inside the margin,
which would otherwise never be live and would mint on every call.

`HttpRequestException` was added to both export handlers' catch lists.

## Evidence

- `BoxAuthorizationHeaderTests` — 7 tests, none of which existed before; this
  class had no coverage at all, which is how the defect shipped.
- CI green on `ce4d646c`: unit, browser, all three sql-integration shards.
- Local: Core 937, architecture 99.
- Production smoke passed before and after the data wipe.

## Deviations from plan

Two, both from the independent review and both recorded in `plan`: the margin
moved 60 → 120 seconds (it must exceed the 100-second client timeout), and
`AddProductionBoxCustody` now registers `TimeProvider` itself instead of
relying on call order.

## Not done, deliberately

`Dispose()` stays unguarded — a Box call awaiting the gate at shutdown sees
`ObjectDisposedException` rather than clean cancellation. Shutdown-only;
disposal itself is safe because the container owns the instance. Considered,
declined, recorded.

# Plan

## 1. Renew the header on Box's own stated lifetime

`BoxJwtAuthorizationHeaderProvider` currently calls
`RetrieveAuthorizationHeaderAsync`, which the SDK answers from a cache it never
expires. Replace that with an explicit mint:

- `RefreshTokenAsync(session)` returns `AccessToken` carrying `AccessTokenField`
  and `ExpiresIn` (seconds). Cache `"Bearer " + AccessTokenField` together with
  `now + ExpiresIn`, and re-mint once the remaining life drops below a margin.
  The lifetime is **read from Box**, not assumed — no hard-coded 60 minutes.
- Guard the mint with a `SemaphoreSlim(1,1)` and re-check inside it, so a burst
  of concurrent Box calls mints one token, not one each. The existing `Lazy`
  over `BoxJwtAuth` stays; it is the client, not the token.
- Take `TimeProvider` so the expiry is testable. Reuse: the container already
  registers `TimeProvider.System` in `AddInfrastructure`, and
  `DependencyInjection.cs:594` already resolves it exactly this way.
- Fail closed unchanged: a mint that yields no token still throws
  `InvalidOperationException`, the same sentence as today.

Margin: 60 seconds. Long enough to cover a slow request that started just
before the boundary; short enough that it is not a second lifetime policy.

## 2. Let the export report a transport failure like every other failure

`ExportModel.OnGetAsync` catches five exception types and misses
`HttpRequestException`, so a Box outage renders the generic error page. Add it
to the existing `when` clause — one type, no new handling.

## 3. Tests

`BoxAuthorizationHeaderTests`, against a fake `BoxJwtAuth` seam:

1. the first call mints;
2. a second call inside the lifetime reuses the same header without minting;
3. a call past `ExpiresIn` minus the margin mints again;
4. concurrent callers across an expiry mint once;
5. a mint returning no token throws.

The provider is `internal`; `Pegasus.IntegrationTests` already sees
Infrastructure internals.

**Seam.** `BoxJwtAuth` is a concrete SDK class with no interface, so (1)–(5)
need one. Keep it minimal and internal — a delegate the production constructor
defaults to the real `RefreshTokenAsync`. That is not a new abstraction for its
own sake; without it this class stays as untested as it is today, which is how
the defect shipped.

## 4. Verification

Local: `dotnet test tests/Pegasus.IntegrationTests --filter "FullyQualifiedName~BoxAuthorizationHeader"`.

Production, after deploy: export `ap.QDOS26012` more than an hour after the
revision starts and confirm the archive downloads. That is the only test that
distinguishes this fix from the current behaviour, because a fresh container
passes either way — the proof must be taken **late**, not at deploy time.

## Simplification pass

To be recorded here, dated, before the PR.

## Simplification pass — 2026-08-23

Four lenses over the branch diff, run by hand rather than through the
`code-simplifier` agent (the operator's standing instruction this session
forbids delegating to it). Findings and dispositions:

| Lens | Finding | Disposition |
| --- | --- | --- |
| **Efficiency / correctness** | The lock-free fast path read `header` (a reference) and `expiresAtUtc` (a 16-byte `DateTimeOffset`) as two separate non-volatile fields while the renewing thread wrote both. A torn or reordered read could pair one token's header with another's expiry — and the failure mode that matters is serving a **stale** header, i.e. re-creating the 401 this ticket exists to remove. | **Fixed here.** Header and expiry are now one immutable `Lease` record, published with `Volatile.Write` and taken with `Volatile.Read`. A single reference read is atomic, so the two values can never disagree. A correctness finding, so it is fixed on the branch rather than filed. |
| **Reuse** | `MutableTimeProvider` already exists in `CaseDataCompletenessPersistenceTests` and is already borrowed by `AssessmentPersistenceIntegrationTests`; `TimeProvider` is already resolved from the container at `DependencyInjection.cs:594`. | Both reused. No new time abstraction. |
| **Simplification** | The `Lazy<(BoxJwtAuth, NetworkSession)>` could collapse into the constructor. | **Kept.** It defers parsing the JWT config and decrypting the private key until Box is actually used; eager construction would move that cost into container build for every process, including ones that never touch Box. Behaviour-preserving from the original. |
| **Altitude** | `BoxAccessToken` could have been an anonymous tuple on the delegate. | **Kept as a named record struct** — it is read at two call sites and `(string?, long?)` says nothing at either. |
| **Altitude** | `RenewalMargin` is a second number beside Box's lifetime. | **Kept.** One constant, stated once, with the reason on it. The alternative — renewing exactly on expiry — puts an in-flight request the wrong side of the boundary. |

Nothing was left unapplied.

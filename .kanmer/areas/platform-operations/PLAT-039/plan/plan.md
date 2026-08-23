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
- Take `TimeProvider` so the expiry is testable.
- Fail closed: a mint that yields no usable token throws
  `InvalidOperationException`.

**Margin: 120 seconds**, because it must exceed the Box `HttpClient` timeout of
100 seconds (`DependencyInjection.cs`). A shorter margin lets a long
photograph transfer begin inside the margin and still be running after the
token dies — the same intermittent 401 this ticket removes. *(Set to 60s
first; corrected after review — see below.)*

## 2. Let the export report a transport failure like every other failure

`ExportModel.OnGetAsync` catches five exception types and misses
`HttpRequestException`, so a Box outage renders the generic error page. Add it
to the existing `when` clause — one type, no new handling. The POST handler's
selective export reads Box the same way and gets the same clause.

## 3. Tests

`BoxAuthorizationHeaderTests`, against a fake `BoxJwtAuth` seam: the first call
mints; a second inside the lifetime reuses without minting; a call past the
lifetime mints again; a call inside the renewal margin mints again; concurrent
callers across an expiry mint once; a mint returning no token throws; a mint
stating no lifetime throws.

**Seam.** `BoxJwtAuth` is a concrete SDK class with no interface. Keep it
minimal and internal — a delegate the production constructor defaults to the
real `RefreshTokenAsync`. That is not a new abstraction for its own sake;
without it this class stays as untested as it was, which is how the defect
shipped.

## 4. Verification

Local: `dotnet test tests/Pegasus.IntegrationTests --filter "FullyQualifiedName~BoxAuthorizationHeader"`.

Production, after deploy: export `ap.QDOS26012` **more than an hour after the
revision starts** and confirm the archive downloads. A fresh container passes
either way, so the proof must be taken late, not at deploy time.

## Simplification pass — 2026-08-23

Four lenses over the branch diff, run by hand rather than through the
`code-simplifier` agent (operator instruction this session). Findings:

| Lens | Finding | Disposition |
| --- | --- | --- |
| **Efficiency / correctness** | The lock-free fast path read `header` (a reference) and `expiresAtUtc` (a 16-byte `DateTimeOffset`) as two non-volatile fields while the renewing thread wrote both. A torn read could pair one token's header with another's expiry — and the failure that matters is serving a **stale** header, re-creating the 401 this ticket removes. | **Fixed here** (`282ba44a`). One immutable `Lease` record, published with `Volatile.Write`, taken with `Volatile.Read`. A correctness finding, so fixed on the branch rather than filed. |
| **Reuse** | `MutableTimeProvider` already exists and is already borrowed by `AssessmentPersistenceIntegrationTests`; `TimeProvider` is already container-resolved elsewhere. | Both reused. |
| **Simplification** | The `Lazy<(BoxJwtAuth, NetworkSession)>` could collapse into the constructor. | **Kept.** It defers JWT config parsing and private-key decryption until Box is used; eager construction moves that into container build for every process. |
| **Altitude** | `BoxAccessToken` could be an anonymous tuple. | **Kept** as a named record struct — `(string?, long?)` says nothing at either call site. |

## Independent review — 2026-08-23, PR #523

A reviewer that did not implement the work read the diff against the plans. It
raised two items here, both applied:

| Finding | Disposition |
| --- | --- |
| **Renewal margin shorter than the Box client timeout.** 60 s margin against a 100 s `HttpClient` timeout: a 90-second photograph download can start with 61 s of token life left and still be running when it dies. | **Fixed.** Margin is 120 s, with the dependency on the client timeout written on the constant so the next person changing either sees it. |
| **`AddProductionBoxCustody` resolves `TimeProvider` without registering one.** It works only because every caller also calls `AddPegasusInfrastructure` first. | **Fixed.** `TryAddSingleton(TimeProvider.System)` beside the existing `TryAddSingleton(HttpClient)`, so the storage profile stands up on its own. |
| **The plan promised the old exception sentence and the code changed it** ("no authorization header" → "no usable access token"). | **Plan corrected above** rather than the code reverted: the new sentence also covers a token with no stated lifetime, which is a case the old wording did not describe. Grep confirms nothing depends on the old text. |
| **`Dispose()` is unguarded** — a Box call awaiting the gate at shutdown sees `ObjectDisposedException` rather than clean cancellation. | **Not fixed.** Shutdown-only and cosmetic; the reviewer confirmed disposal itself is safe because the container owns the instance. Recorded as considered, not missed. |

The pass above missed nothing on this ticket, but see MAIL-012 — the same
by-hand pass missed a real defect there, and this review is what caught it.

# Plan

Estimated diff: one test file, about 90 lines.

1. Reuse `IntakeWebApplicationFactory(useIntegrationTestAuthentication: true)` and the repository's `RemoveAll<T>` override convention to install one controlled Deleted-source fake.
2. Through authenticated `/Inbox` GETs, prove a zero-retained-row approved mailbox tab, bounded maximum, matched visible location, truncation, page-one/page-two results, and unavailable state.
3. Run focused Web verification and the four lenses; update PIR/traceability.

## Governing docs

FRD-08 caller evidence is produced at the authenticated Web tier without changing its behavior or external scope.

## Simplification pass — 2026-08-20

- Reuse: the authenticated integration factory, RemoveAll<T> override, Core source port, existing page, and pager are exercised directly.
- Simplification: one controlled fake covers mailbox selection, match location, bound, truncation, paging, and unavailable state.
- Efficiency: no production code or extra application host was added.
- Altitude: this is caller evidence only; Graph and Core behavior remain owned by their existing tests.

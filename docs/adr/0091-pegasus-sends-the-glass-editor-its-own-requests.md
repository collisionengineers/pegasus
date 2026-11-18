---
id: ADR-0091
status: accepted
date: 2026-11-18
supersedes: []
superseded_by: []
related_capabilities: [EXT-06, ENG-01]
related_frd: [FRD-25]
tags: [glass, repair-spec, session, editor]
---

# ADR-0091: Pegasus's server sends the Glass's editor its own requests

## Status

Accepted on 18 November 2026 (R1167). This record owns how Pegasus adds to,
saves and closes a Glass's estimate with no person at the editor.
[FRD-25](../frd/frd-25-repair-estimates-imports-and-glasss-sessions.md#glasss-launch-and-return)
owns what staff see.

Amended 18 November 2026 (R1168 to R1173): the client's first user is the
rebuild of a repair spec made outside Pegasus, **Import and open** on the
Case. The gateway runs it as one piece of background work with its own time
cap ([ADR-0058](0058-glass-provider-work-in-the-web-host.md)): it starts the
estimate, opens the editor from the address the start produced, adds the
spec's lines, saves and exits, and hands the editor's way home to the
existing return path only once it is proved to be the session's own
callback.

## Context

Pegasus starts a Glass's estimate from its server by driving the portal as a
browser would ([ADR-0062](0062-glass-placeholder-vehicle-for-unknown-plates.md),
[ADR-0063](0063-glass-estimate-belongs-to-its-repair-spec.md)). It then hands
the browser the editor's address and waits for the return. Everything inside
the editor has needed a person: Pegasus sent it no part, no operation and no
save.

Three things the operator asked for on 18 November 2026 need Pegasus to put
positions on an estimate itself: rebuilding an estimate made outside Pegasus,
estimates the Automation actor makes, and help inside a session a member of
staff has open. Glass's offers Collision Engineers no interface for that. The
editor's own pages do it with ordinary requests that carry a session key, and
the key is made when the editor's address is first loaded. Pegasus already
holds that address.

Live proofs on the hosted test instance on 18 November 2026 showed the route
works with no browser at all: a program followed the editor's address to a
key, read the estimate and the part list, added a special operation and a
part, saved and exited, and Pegasus's own return path imported the result. A
second client holding only the key was also let into a session a browser had
open.

## Decision

1. **An editor client beside the portal client.** `GlassEditorClient` in
   `src/Pegasus.Infrastructure/Glass/` opens an editor session from the launch
   address, reads the estimate and the editor's lists, adds and removes
   positions, and saves and exits. It asks only the editor's host
   (`GlassRepairEstimateOptions.EstimatorBaseUri`), one request at a time
   with a pause between them, and follows no redirect.
2. **It posts what the page serves.** A position is added by asking for its
   dialog page and posting that page's own form, changed only in the ways the
   dialog's own script changes it. Pegasus makes up no price, time, part
   number or code. Where a page is not the one the client knows, it refuses
   and posts nothing.
3. **Pinned to what a browser sent.** Every address the client builds and
   every post it makes is compared in tests with the address or body a browser
   sent for the same page. The test pages are cut from browser sessions, with
   every key and token replaced by a made-up one.
4. **Saving is two requests.** The save request, then the close request,
   then the launch page on the way out, which names the address to return to.
   The return then runs through the existing callback, relay, export and
   import. Close alone reports a save and records nothing.
5. **Nothing of the provider's is patched, and nothing is read from a staff
   member's window.** The client is a second caller of the provider's pages,
   as the portal client is. The rule that the Case page may read only the
   estimator window's frame depth is unchanged.
6. **The key is a secret.** The session key, the cookies, the launch address
   and the return address are never logged and never put in an error. A
   session's state is carried by its caller as the portal session's is.

## Consequences

- The client copies the provider's page behaviour. When Glass's changes a
  dialog, the client refuses that dialog, with a `glass.editor.*` code, until
  the test pages are cut again and the builder follows. It does not guess.
- Glass's has not agreed to automated use of its editor. The operator
  accepted that on 18 November 2026 (R1167).
- The client does not choose rows; the build does. Its first version
  chooses only special operations: the one row of the special list's "Free
  positions parts" group whose name is exactly the line's (R1169). Which row
  of the part list a part uses depends on the vehicle's equipment, and that
  choice belongs to the change that first builds parts.
- Each estimate Pegasus builds makes a stock vehicle and an estimate in the
  Glass's account, as any first press of Glass's does.

## Options considered

- **Glass's own interface for a prepared calculation.** Not open to this
  account: a call to it was refused on 4 September 2026 for want of client
  credentials.
- **Driving a browser on the server.** It would run the provider's pages
  unchanged, and it would also bring the start-up stall of those pages and a
  browser to host. Rejected.

## Links

- [FRD-25](../frd/frd-25-repair-estimates-imports-and-glasss-sessions.md)
- [ADR-0058](0058-glass-provider-work-in-the-web-host.md)
- [ADR-0063](0063-glass-estimate-belongs-to-its-repair-spec.md)

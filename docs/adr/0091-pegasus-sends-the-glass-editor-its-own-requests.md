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

Amended 18 November 2026 (R1170): the Automation is to help in an
Engineer's open Glass's session live, through the Pegasus helper extension,
with no hand-over. Its first part is the extension: it makes the framing
page's `dialog` exist before Glass's editor frame asks for it, and hands the
editor frame's own address to `/Integrations/Glass/Help`, which holds the key
in the Web host's memory only (decisions 5 and 6). The key's one user is the
Automation's help in that session while the Engineer has turned it on
(R1182 to R1185;
[FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md#help-in-an-engineers-open-session)):
read, search, add and remove, never save, exit or close.

Amended 18 November 2026 (R1177 to R1180): the client's second user is the
Automation's own Glass's sessions
([FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md#glasss-sessions)).
Each of the Automation's acts is its own piece of background work keyed on
the session, so the session's acts run one at a time. The editor stays open
between the Automation's calls: its session and the time of its last request
are kept in the session's protected state, so the pause between requests
holds across calls. The Automation's own sessions read nothing from a staff
member's window; decision 5 is as R1170 amended it.

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
open. That was one trial: the first live session, on 19 November 2026, showed
the key alone reaches the server that holds the session only by chance
(decision 5).

## Decision

1. **An editor client beside the portal client.** `GlassEditorClient` in
   `src/Pegasus.Infrastructure/Glass/` opens an editor session from the launch
   address, reads the estimate and the editor's lists, adds and removes
   positions, and saves and exits. It also stores a part's equipment answer
   for the vehicle as the editor's list stores it, just before the part's
   dialog, when the Automation gives one (19 November 2026; R1206). It asks only the editor's host
   (`GlassRepairEstimateOptions.EstimatorBaseUri`), one request at a time
   with a pause between them, and follows no redirect.
2. **It posts what the page serves.** A position is added by asking for its
   dialog page and posting that page's own form, changed only in the ways the
   dialog's own script changes it. Pegasus makes up no price, time, part
   number or code. A price or time the Automation types is the Automation's
   input, as a person's would be, and never Pegasus's own (19 November 2026;
   R1206); so is the reason Glass's asks for above its own hours. Where a
   page is not the one the client knows, it refuses and posts nothing.
3. **Pinned to what a browser sent.** Every address the client builds and
   every post it makes is compared in tests with the address or body a browser
   sent for the same page. The test pages are cut from browser sessions, with
   every key and token replaced by a made-up one.
4. **Saving is two requests.** The save request, then the close request,
   then the launch page on the way out, which names the address to return to.
   The return then runs through the existing callback, relay, export and
   import. Close alone reports a save and records nothing.
5. **Pegasus changes no Glass's file or request.** The client is a second
   caller of the provider's pages, as the portal client is. In a staff
   member's browser the Pegasus helper extension (`src/pegasus-glass-helper/`)
   reads the editor frame's own address and hands it to Pegasus when that
   member of staff's session is theirs (the cookie) and is `Active`. With it
   go the two cookies of Glass's load balancer the frame holds (`AWSALB`,
   `AWSALBCORS`; 19 November 2026): one of Glass's servers holds an editor
   session, those cookies name it, and a request on the key alone reaches
   whichever server the balancer picks. In the first live session 7 of 8
   requests on the key alone were answered 200 with a page that had none of
   the editor's lists. Neither cookie is a login. The helper
   defines the framing page's `dialog` early. It reloads the selected list
   when asked. It does nothing else. The rule that the Case page may read only
   the estimator window's frame depth is unchanged.
6. **The key is a secret.** The session key, the cookies, the launch address
   and the return address are never logged and never put in an error. A
   session's state is carried by its caller as the portal session's is.

## Consequences

- The client copies the provider's page behaviour. When Glass's changes a
  dialog, the client refuses that dialog, with a `glass.editor.*` code, until
  the test pages are cut again and the builder follows. It does not guess.
- Glass's has not agreed to automated use of its editor. The operator
  accepted that on 18 November 2026 (R1167).
- The client does not choose rows; the build does. A special operation is
  the one row of the special list's "Free positions parts" group whose name
  is exactly the line's (R1169). A part's row is chosen by
  `GlassEditorRows` from the part list (`list.php`) and the row's equipment
  map, and only where the choice is certain (R1172): the part's block needs
  no equipment, its map lists no entry that does in its part group or its
  Replace group, and the pair is the row and its Replace time row. Anything
  else is left off; no equipment answer is guessed. The rule was judged
  offline against the editor's own list code, run with the row's side as a
  browser sets it, on the captured list of 7 September 2026: it agreed on
  every row it calls certain. The dialog must then serve the line's part
  code and side before anything is posted. A repair or remove-and-refit
  line (R1172) takes the pair Replace would, because the editor asks for a
  part's dialog by one pair whatever the operation. The head must also offer
  the operation, its map's remove-and-refit group must need no equipment, and
  the block must have at most one time row of the operation. That rule was
  judged the same way and agreed on the editor's own choice of the
  operation's time rows. The client then changes the dialog's operation as
  its own script does, and answers OK to the dialog's question before a part
  with no figures only when the build asks it to.
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

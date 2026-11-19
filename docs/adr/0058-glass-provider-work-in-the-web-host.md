---
id: ADR-0058
status: accepted
date: 2026-09-28
supersedes: []
superseded_by: []
related_capabilities: [EXT-06]
related_frd: [FRD-25]
tags: [glass, background-work, data-protection, performance]
---

# ADR-0058: Glass's provider work runs in the background of the Web host

## Status

Accepted on 28 September 2026. The operator chose to run Glass's launch and
return in the background. FRD-25 owns what staff see. This record owns where
the work runs and why.

## Context

A Glass's launch makes 15 to 19 provider calls in a row. Production showed a
launch taking 8.5 to 11 seconds inside the staff member's request, and a
return taking up to 12.6 seconds. The browser waited the whole time.

The work reads two protected things: the session's provider state and the
staff member's own Glass's password. Both are protected by the Web host's
Data Protection key ring ([ADR-0043](0043-per-engineer-vendor-credential-protection.md)).
That same ring protects the staff sign-in cookies.

## Decision

The staff member's request does only the checks and the durable claim. It
proves authority, ownership and the one-use token, records the session, and
answers at once. The provider work runs afterwards in the Web host itself.

The Web host keeps a bounded in-memory queue and a list of the sessions it is
working on. A request holds its session busy from before it writes the claim
until the work it queued has run. So no window finds the session idle between
its claim and its work, and a racing second request waits on the first one's
work. A background service runs a few items at a time, but one session's work
runs in turn. Each item gets its own dependency scope and an overall time cap.
The Glass's window polls a small owner-only state answer and moves on when the
work is done.

The work does not move to the Worker. The Worker would need the Web key ring to
read the session state and the password. Sharing that ring would also let the
Worker read and mint staff cookies.

## Consequences

- The list of running work is this process's memory. It is correct only for a
  host with one instance, which is what runs today. A second instance would
  settle another instance's running work as interrupted.
- A restart or the time cap stops work part-way. The session is left as an
  interrupted request always left it: Prepared stays resumable, and anything
  past it becomes Unknown and holds the account. The Glass's window settles a
  session it finds waiting with nothing running.
- A full queue never drops work. The request runs the work itself, as every
  request did before this decision.
- The claim keeps the return's own message in the session's protected state,
  and the claim before the relay records that the relay began. Work lost with
  the process before the relay began is relayed once by Resume. A relay that
  began is never repeated; its export is looked up instead.
- A session cannot be closed while its work runs, so a launch cannot leave a
  vehicle or estimate at the provider without a session.
- EVA export and staff e-mail stay inside their requests.

## Links

- [FRD-25 — Glass's launch and return](../frd/frd-25-repair-estimates-imports-and-glasss-sessions.md#glasss-launch-and-return)
- [FRD-25 — Glass's interrupted sessions](../frd/frd-25-repair-estimates-imports-and-glasss-sessions.md#glasss-interrupted-sessions)
- [ADR-0043 — Per-Engineer vendor credential protection](0043-per-engineer-vendor-credential-protection.md)

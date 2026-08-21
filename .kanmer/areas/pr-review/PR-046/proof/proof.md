# Proof

**Shipped:** PR #486, merge `708706b8` · **Deployed:** ancestor of `4111ad29` → true (Release 16, active revision `…--4111ad291779`).

## The finding

PR #486 replaced the MAIL-09 capability note and **dropped** the statement that the
QDOS-direct subset was pulled forward to `Now / 0.1.0-alpha.1` under ADR-0020. The
capability registry owns scheduling and allocation; documenting a new general implementation
must not erase a still-valid allocation fact.

## Verified in the shipped document

`docs/capabilities.md:218`, on the deployed revision, carries **both** facts in one row:

> …Locally implemented in the durable queued-intake caller… **The QDOS-direct subset remains
> pulled forward to `Now / 0.1.0-alpha.1` under the operator-accepted predicates in
> [ADR-0020](adr/0020-accepted-qdos-case-association-predicates.md); the general capability
> keeps this row's `Next / 0.3.0` allocation.**

The ADR-0020 link is present and resolves. The allocation columns still read
`Next | 0.3.0`, so the general capability's schedule is unchanged while the pulled-forward
subset is stated rather than implied.

## Operator truth unchanged, deployment not implied

The row ends: *"No live mailbox, provider, deployment, or cloud write was performed."*
That sentence is the second half of the finding — the note must not imply live evidence —
and it survived the edit. It is still accurate: the code is on the deployed revision, but
no live association has been performed, and a capability row saying otherwise would be the
false assurance this ticket existed to prevent.

Nothing in `docs/operator-notes.md` was touched by PR #486.

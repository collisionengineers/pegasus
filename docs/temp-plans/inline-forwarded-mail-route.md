# Recognise normal staff-forwarded instruction senders

## Decision and current behaviour

Collision Engineers staff forward work to
`instructions@collisionengineers.co.uk` using normal Outlook forwarding. That
is the accepted operational form: staff do not attach the original email. The
outer Collision Engineers sender remains transport provenance; the sender in
the standard forwarded-message header block identifies the route.

Today the MIME reader emits an original-sender identity only for a nested
`message/rfc822` attachment. `QdosMailRoutePolicy` therefore puts an inline
staff forward in `Needs sorting` with "A staff-forwarded message requires
exactly one consistent attached original sender." This is the confirmed cause.

The 2026-08-11 read-only production check confirmed four retained messages
from `desk@collisionengineers.co.uk`. Every recorded a `Needs sorting` route
at policy version 3 with no original identity and the same refusal reason.
None had an attached `.eml`; each body instead contains exactly one ordered
Outlook-style `From:`, `Sent:`, `To:`, `Subject:` header quartet and an address
at one of the accepted QDOS domains. These messages do **not** carry either
the `-----Original Message-----` or `Forwarded message` separator, so separator
recognition would reject the real intake format and is deliberately not part of
the grammar.

## Atomic breakdown

1. **Read-only estate confirmation.** For the affected retained message, read
   its stored MIME and receipt route decision. Record whether it has a normal
   Outlook forwarded-message header block, the outer sender, all extracted
   original-sender evidence, and the persisted refusal reason. No mailbox,
   database, storage, queue, Worker, or configuration mutation is permitted.

2. **Define the narrow forward grammar.** Add a Core contract value for an
   inline original sender. The reader recognises one standard forwarded-message
   header quartet in the outer email's text or decoded HTML: line-start `From:`,
   `Sent:`, `To:`, and `Subject:` headers in that order, containing exactly one
   valid `From:` mailbox. It does not treat an arbitrary `From:` line in prose,
   a separator alone, or a partial/misordered header group as identity.

3. **Extract and retain provenance.** In the MIME reader, extract that sender
   only when the outer transport sender is an exact
   `@collisionengineers.co.uk` address. Keep the outer sender as `Transport`;
   emit the parsed sender as the new inline-original identity with a source
   label that says it came from the forwarded header. Continue extracting
   attached `.eml` originals as before. Outlook `.msg` remains retained manual
   review material, not a route-identity parser.

4. **Make route selection deterministic.** Update the QDOS route policy to
   accept one original sender from either an attached email or a recognised
   inline forward. If the sources disagree, more than one distinct original is
   present, the header is malformed, or no recognised header quartet exists,
   preserve `Needs sorting`. Keep the exact existing three QDOS domains and
   every direct/intermediary boundary unchanged. Increment the policy version
   so existing receipts retain their recorded route decision.

5. **Prove actual callers.** Exercise the MIME reader, `ProcessIntake`,
   mailbox-poll pipeline, persistence/reload, and Inbox route display. A
   standard plain-text or HTML Outlook forward from a Collision Engineers
   mailbox with one accepted QDOS sender must result in that sender as the
   persisted effective sender and QDOS direct route.

6. **Prove refusal boundaries.** Test an arbitrary body `From:` line, a
   separator without the ordered quartet, a partial or misordered quartet, a
   malformed address, multiple conflicting inline blocks, conflicting inline
   and attached originals, and a non-Collision Engineers outer sender. Each
   must remain unaccepted or `Needs sorting`; direct mail and attached `.eml`
   forwarding must remain unchanged.

7. **Document the operational contract.** Update the authoritative operator
   and architecture wording to say normal Outlook forwarding is supported, the
   outer staff sender is retained, and ambiguous evidence still requires staff
   sorting. Do not claim deployment, mailbox configuration, or live
   verification.

8. **Verify and review.** Run the focused Core and mailbox integration tests,
   locked restore/Release build, and the applicable full test profile. Before
   merge, obtain the independent two-question plan review and green CI.

## Estate read-only targets

The planned read-only confirmation is limited to the Pegasus production
subscription `e6076573-23a5-46a8-acef-7e22d264e5db`, resource group
`rg-pegasus-prod`, `pegasus` SQL database, the retained MIME for the named
`instructions@collisionengineers.co.uk` item, and its associated
`IntakeReceipts`/`IntakeMailRouteDecisions` records. It intentionally excludes
all mutation, including re-drive, delete, replay, mailbox change, and setting
change.

# Research — TICK-043: MAIL-01 inbound mailbox identity

## Provenance note

The additional evidence below comes from a **previous implementation** reviewed as a reference design. Project-specific naming has intentionally been omitted. It is research evidence only and does not constitute Pegasus activation, deployment, or operator acceptance.

## Previous-implementation findings

### Message identity is multi-dimensional

The previous implementation did not collapse every mail identity into one generic `emailId`. Its inbound envelope/persistence model kept separate facts for:

- source mailbox identity;
- RFC `Internet-Message-ID`;
- provider-native / Microsoft Graph message identity;
- optional immutable Graph identity;
- `conversationId` plus `In-Reply-To` / `References` thread evidence;
- notification/delivery context such as subscription, tenant, resource and receipt time where applicable;
- payload/content hash;
- sender, subject and received timestamp;
- candidate business identity signals such as provider, case/PO reference, job reference and VRM.

The durable inbound persistence key was effectively **`(source mailbox, Internet-Message-ID)`**, with the mailbox normalised before storage. This is a strong precedent for idempotency because the ordinary Graph item ID can change after an Outlook move, while RFC message identity remains the stable message-level signal.

The Graph edge and poll-based intake could both feed the same conceptual message into intake. Overlapping reads were tolerated because persistence uniqueness/idempotency, rather than polling exclusivity, was the safety boundary.

### Graph item identity must remain separate from RFC identity

Folder movement in the previous implementation re-resolved the current Graph message ID before performing the move because a normal Graph message ID may change when a message moves. This is direct evidence against using the transient provider item ID as the sole durable identity.

Pegasus should therefore use unambiguous field names such as `internetMessageId`, `graphMessageId`, `immutableGraphMessageId`, and `sourceMailbox`, rather than a generic `sourceMessageId` whose meaning can drift.

### Provider identification is independent evidence

Provider resolution from sender identity followed a strict, fail-closed rule:

1. exact full e-mail address match first, case-insensitive;
2. exact sender-domain match second;
3. only active providers qualify;
4. multiple matches produce `Ambiguous` rather than an arbitrary provider;
5. no match produces `Unmatched` rather than a guessed provider;
6. no substring/fuzzy/alias matching is used for provider assertion.

Exact-address precedence is important for generic domains such as Gmail, where the domain alone cannot identify a provider. The previous implementation also contained examples where one provider legitimately owned multiple exact domains, demonstrating that the model should support one-to-many known identities without weakening exact matching.

Attachment/document identity was kept as a separate evidence source using provider-specific phrases and field-extraction rules. This matters for forwarded instructions: the SMTP sender may be staff while an attached instruction provides stronger evidence of the originating provider. Sender identity and document identity should therefore be persisted as separate evidence, not silently overwrite one another.

### Case correlation is a separate decision from message/provider identity

The previous implementation correlated open cases approximately by strong Case/PO/reference evidence first, falling back to VRM only when necessary. A VRM-only candidate could be vetoed when the message carried a conflicting reference. One candidate could be linked; multiple candidates remained ambiguous; zero candidates stayed unlinked.

This supports a strict separation in Pegasus between:

- message identity;
- provider identity;
- thread identity;
- business/case correlation.

`conversationId` or a conversation sibling case should be correlation evidence only; it should not by itself prove that a message belongs to a case.

### Duplicate delivery is not duplicate business work

The previous implementation distinguished at least three concepts:

- the exact same Internet message delivered/observed more than once;
- multiple different messages concerning the same case;
- a genuinely duplicated business instruction.

Pegasus should preserve those distinctions in identity and audit data. A database uniqueness constraint should prevent duplicate ingestion without erasing the fact that separate e-mails may concern the same matter.

## Recommended MAIL-01 reference model

A useful Pegasus identity contract is:

```text
Mailbox identity        = source mailbox
Provider item identity  = Graph/provider message ID (+ immutable ID when available)
RFC message identity    = Internet-Message-ID
Thread identity         = conversationId + In-Reply-To/References
Delivery identity       = subscription/tenant/resource where applicable
Content identity        = payload hash
Business identity       = provider + correlated case candidates/result
```

For persistence/idempotency, **mailbox + RFC Internet-Message-ID** is the strongest direct precedent from the previous implementation. Content hash is useful secondary evidence but should not replace transport identity because legitimate distinct messages can have similar content.

## Implications for Pegasus planning

- Define exact identity field names and ownership before implementation; do not overload one identifier with Graph, RFC and persistence meanings.
- Ensure both webhook and polling paths converge on the same Core identity/idempotency contract.
- Treat provider recognition, attachment recognition and case correlation as independent evidence producers layered after exact message identity.
- Model `Ambiguous` and `Unmatched` explicitly rather than guessing.
- Add acceptance tests for repeated delivery, post-move Graph-ID changes, forwarded-provider instructions, conflicting reference-vs-VRM evidence, and multiple candidate cases.
- This reference design does not resolve the ticket's activation boundary or replace the owning FRD; it supplies implementation research for the eventual task-level plan.

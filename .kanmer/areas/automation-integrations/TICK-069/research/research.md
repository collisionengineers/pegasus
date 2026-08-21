# Research — TICK-069: automated WhatsApp intake

## Question

How could Pegasus receive WhatsApp messages and images automatically without disrupting the way Collision Engineers uses WhatsApp today?

## Findings

Collision Engineers currently uses WhatsApp mainly to obtain vehicle images, with some instructions arriving as text or documents. Staff must still be able to use WhatsApp normally while Pegasus records incoming material. That makes continuity of the existing number and app more important than choosing the cheapest API.

### Provider comparison

| Option | Offer | Cost published by provider | Suitability |
|---|---|---|---|
| **360dialog** | WhatsApp Cloud API with **Coexistence**: the existing WhatsApp Business app and API can use the same number. App and API messages are mirrored; existing history can be retained. | Regular plan: 49 EUR/USD per number per month, plus Meta message charges. | **Best match for a pilot.** It directly addresses coexistence and provides a sandbox. The fixed monthly fee is acceptable if it avoids changing the live staff workflow. |
| **Azure Communication Services** | Managed WhatsApp messaging with a .NET SDK, Event Grid inbound events, and media download. Billing appears on the Azure account. | $0.005 per inbound or outbound message, plus Meta charges. | **Best technical fit if a separate API number is acceptable.** It matches Pegasus’s Azure/.NET estate, but Microsoft’s public material reviewed here does not confirm coexistence with the existing Business app. |
| **Meta Cloud API direct** | Meta-hosted official API with webhooks and media retrieval. No intermediary API is required. | Meta’s category/country message charges; no separate provider offer was assessed. | **Lowest-dependency long-term option**, but Pegasus would own onboarding, webhook security, support and operations. The material reviewed did not establish a direct self-service coexistence route for this business. |
| **Twilio** | Mature API, inbound webhooks, sandbox and broad tooling. | $0.005 per inbound or outbound message, plus Meta template charges. | **Not suitable for the current number.** Twilio says migration from the Business app requires deleting the app account and the same number can no longer continue in the app. |
| **Vonage** | General WhatsApp/omnichannel API. | Platform fee advertised from €0.0001 per message, plus Meta charges; exact price requires sales contact. | Potentially inexpensive, but current public material does not prove the required coexistence route. Not worth preferring over a documented option. |
| **Bird** | Usage-priced WhatsApp API, immediate test key, no seat fee or annual commitment advertised. | Destination/category pricing with Meta fee included; service messages shown at $0.005 in published examples. | Easy to trial, but no clear public evidence found for the required same-number coexistence. |

Sources: [360dialog Coexistence](https://docs.360dialog.com/docs/waba-management/embedded-signup/whatsapp-coexistence), [360dialog pricing](https://docs.360dialog.com/docs/prices-plans-and-payments), [Azure WhatsApp messaging](https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/advanced-messaging/whatsapp/get-started), [Azure pricing](https://learn.microsoft.com/en-us/azure/communication-services/concepts/advanced-messaging/whatsapp/pricing), [Meta Cloud API](https://www.postman.com/meta/whatsapp-business-platform/documentation/wlk6lh4/whatsapp-cloud-api), [Twilio migration](https://www.twilio.com/docs/whatsapp/migrate-numbers-and-senders), [Twilio pricing](https://www.twilio.com/en-us/whatsapp/pricing), [Vonage pricing](https://www.vonage.com/communications-apis/messages/features/whatsapp/pricing/), [Bird pricing](https://bird.com/en-sg/products/whatsapp/pricing/eg).

### What Pegasus needs from the service

- Receive text, JPEG/PNG images, PDF and Word documents with sender, message ID, time, filename and media type.
- Download and retain the original material before acknowledging successful intake.
- Ignore harmless redelivery while showing conflicting or failed receipts.
- Link only when the case match is definitive; otherwise leave the material for staff to resolve.
- Keep manual WhatsApp handling available throughout the pilot and rollback.

The existing Pegasus intake pipeline already provides retention, duplicate handling, processing, case matching and manual resolution. The provider only needs to deliver trustworthy message events and media into that pipeline.

## Implications

**Recommended next step:** obtain a 360dialog sandbox/demo and confirm that the current Collision Engineers number is eligible for Coexistence. Test with a disposable number first. The pilot should prove that messages sent and received in the Business app are mirrored to the API, images and documents can be downloaded, duplicate events are stable, and disconnecting the API leaves normal app use intact.

If Coexistence is unavailable or unacceptable, use a separate number and prefer Azure Communication Services for its .NET SDK, Event Grid integration and consolidated Azure operations.

Do not migrate the live number, register a webhook, or create provider credentials until the exact sandbox and account are approved.

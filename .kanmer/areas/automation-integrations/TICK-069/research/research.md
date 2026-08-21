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

## Azure follow-up — is it Coexistence?

**No. Azure Communication Services Advanced Messaging is not the same-number WhatsApp Business App/API Coexistence required by EXT-15.**

Microsoft’s registration instructions distinguish an existing **WhatsApp Business Account** from an existing **phone-number registration**:

- ACS can connect an existing Meta/WhatsApp Business Account.
- The phone number supplied to ACS must not already be associated with a WhatsApp Business Account.
- Microsoft separately states that another WhatsApp account cannot use that number.
- Once registered, the number is shared with Microsoft and described as locked.

This means “connect an existing account” allows the business to reuse its Meta business structure while adding an eligible number. It does not mean staff can keep using the same number in the WhatsApp Business mobile app.

The event model confirms the distinction. ACS publishes two Advanced Messaging Event Grid events: a received-message event and a delivery-status event. Microsoft Learn does not document the history-sync or Business App message-echo events required to mirror staff activity in a Coexistence setup.

ACS remains a good implementation option for a new or dedicated API number: it offers a .NET SDK, Event Grid delivery, media download, Entra ID authentication and Azure billing. It is not suitable if keeping the current Business App and number is mandatory.

Sources: [Microsoft registration requirements](https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/advanced-messaging/whatsapp/connect-whatsapp-business-account), [ACS WhatsApp overview](https://learn.microsoft.com/en-us/azure/communication-services/concepts/advanced-messaging/whatsapp/whatsapp-overview), [Advanced Messaging events](https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/advanced-messaging/whatsapp/handle-advanced-messaging-events), [media download](https://learn.microsoft.com/en-us/azure/communication-services/quickstarts/advanced-messaging/whatsapp/download-media).

### Revised recommendation

Use 360dialog’s documented Coexistence route for the existing number. Consider ACS only if Collision Engineers accepts a separate WhatsApp number dedicated to Pegasus. Do not test the live number through ACS: its documented onboarding requirements conflict with the current registration and could require destructive account/number changes.

## Verified same-number Coexistence providers — 2026-08-21

For EXT-15, **Coexistence means the existing WhatsApp Business app remains usable on the existing phone number while Cloud API events for that same number reach Pegasus.** Migration to an API number is not Coexistence. This requirement excludes Azure Communication Services and Twilio's documented migration route.

| Provider | What its current documentation confirms | Pegasus suitability |
|---|---|---|
| **360dialog** | Existing Business App number remains in the app and is connected to Cloud API; app-originated messages are delivered as message echoes; recent chat history can be synchronized. | **Recommended first choice.** API/BSP infrastructure rather than a replacement CRM, so Pegasus remains the workflow owner. Published regular plan is 49 EUR/USD per number/month plus Meta charges. |
| **seven.io** | Explicit choice between Full Migration and Coexistence; in Coexistence the app and Cloud API use the same number simultaneously. App messages are exposed as echo messages and can be captured by webhooks. It provides inbound WhatsApp webhooks and a REST API. | **Recommended commercial alternative.** Also API-led and a closer fit than an inbox product. Pricing is Meta fees plus seven platform fees; the exact WhatsApp rate needs a quote/current account price check. |
| **Wati** | Explicit same-number Business App/API Coexistence, contact sync and optional recent chat-history sync; received, sent and status callbacks are available. | **Technically suitable, operationally heavier.** It adds a team inbox, automation and its own billing/credit-line relationship. Consider only if staff also want Wati as their messaging workspace. |
| **respond.io** | Explicit same-number Coexistence with app/API send and receive and app-message echoes. Developer API is available on Growth plans and above. | **Technically suitable, poor fit for simple intake.** It is an omnichannel CRM/automation platform; echo messages can count towards its monthly-active-contact billing and it adds switching friction. |
| **SleekFlow / similar inbox products** | Some advertise Meta Coexistence, but the reviewed material did not establish a better API, commercial or operational fit than the four providers above. | Do not shortlist without a specific commercial reason. |

### Provider-independent limitations

The documented restrictions are largely Meta Coexistence restrictions, not defects unique to one provider:

- the primary Business App must be opened at least once every 13–14 days;
- app features including broadcast lists, message editing/revocation, disappearing messages, view-once media and live location are disabled or restricted;
- WhatsApp for Windows and WearOS activity may not generate webhook events;
- Coexistence throughput is typically limited to 20 messages per second;
- eligibility depends on Meta's assessment of the existing Business App account, region, account age and messaging quality;
- changing BSP later can require disconnect/reconnect work because the provider credit line may not be transferable.

### Recommendation

Request a written eligibility and pricing confirmation from **360dialog and seven.io** for the existing UK number, explicitly asking for *WhatsApp Business App Coexistence*, message-echo webhooks, inbound media retrieval, data residency/subprocessors, support terms and offboarding. Run a disposable-number proof with the better commercial offer before touching the live number.

Do not accept sales wording such as “keep your number” or “migrate your existing number.” The acceptance test is that the same number still sends and receives in the mobile Business App and that those conversations, including app-originated echoes and inbound media, reach the Pegasus webhook.

Sources: [360dialog Coexistence](https://docs.360dialog.com/docs/resources/phone-numbers/coexistence), [360dialog pricing](https://docs.360dialog.com/docs/prices-plans-and-payments), [seven.io Coexistence](https://help.seven.io/en/whatsapp/whatsapp-coexistence), [seven.io WhatsApp API](https://docs.seven.io/en/rest-api/endpoints/whatsapp), [seven.io WhatsApp FAQ](https://help.seven.io/en/whatsapp/whatsapp-faq), [Wati Coexistence](https://support.wati.io/en/articles/11822402-introducing-whatsapp-coexistence), [Wati webhooks](https://support.wati.io/en/articles/14111740-how-to-set-up-and-use-webhooks-in-wati), [respond.io Coexistence](https://respond.io/help/whatsapp/whatsapp-coexistence), [respond.io Developer API](https://respond.io/help/integrations/developer-api).

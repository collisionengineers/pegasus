# Intake and work instructions

## Ways a case starts

There are two ways a case can start at Collision Engineers:

1. Collision Engineers receives a document referred to as Work Instructions.
   These instructions are sent by, or on behalf of, a work provider.
2. Collision Engineers receives a set of vehicle images. These might be
   provided by a repairer (garage, bodyshop, or similar business), and it may be
   unclear or unknown which work provider they relate to.

## What a work instruction contains

A work instruction contains details of a claimant involved in a road traffic
accident. The following details must be extracted and captured:

- Work Provider, also referred to as a principal
- Claimant Name
- Claim Number, an external reference number
- Vehicle Registration (VRM)
- Vehicle Make
- Vehicle Model
- Vehicle Mileage
- Accident Circumstances
- Date of Incident
- Instruction Date; if absent from the document, this defaults to the current date
- Inspection Address; see [inspection address](inspection-address.md)

A set of vehicle images is also required. These should ideally show the damage
the vehicle has sustained and a clear view of the registration.

## Instruction channels and formats

The vast majority of work instructions arrive by email through
[Outlook](../systems-and-integrations/outlook.md), making email the primary
intake-automation target.

The authoritative channel and format list is:

- Email: PDF attachment; DOC/DOCX attachment; or freehand text in the email.
- WhatsApp: PDF attachment; DOC/DOCX attachment; or text typed in WhatsApp.
- A future API into the Collision Engineers system.

## Provider and intermediary email routes

The sender route and the work provider are related but different facts.

- When a provider sends work directly, identify the provider from that
  provider's accepted sender-address traits. Extract the attachments, email
  body, and subject before the provider's direct-email rules determine the
  instruction type and any related case.
- An intermediary has its own rules. Identify the intermediary from its sender
  traits, extract the attachments, body, and subject, and use the intermediary's
  rules to determine the underlying provider, instruction type, and any related
  case.
- A provider may send some work directly and have an intermediary send other
  work. These are separate routes to the same provider; an intermediary email
  must not be interpreted as though it were a direct provider email.
- When Collision Engineers staff forward an email from an
  `@collisionengineers.co.uk` address, use the original forwarded sender for
  route identification and retain the staff forward as transport provenance.

Case association depends on the rules for the identified direct-provider or
intermediary route. Providers do not generally quote a Collision Engineers
Case/PO, so it is never the universal first match and may only be used as a
lowest-priority fallback where that route's evidence supports it. Ambiguous
provider, type, or case evidence remains pre-case for staff sorting.

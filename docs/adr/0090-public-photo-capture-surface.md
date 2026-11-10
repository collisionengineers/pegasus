---
id: ADR-0090
status: accepted
date: 2026-11-09
supersedes: []
superseded_by: []
related_capabilities: [EXT-21, INT-34]
related_frd: [frd-28]
tags: [photo-capture, public-surface, security, tokens]
---

# ADR-0090: Public photo capture surface

## Status

Accepted on the operator's rulings of 9 November 2026 (R773 to R785). It
is the first anonymous surface Pegasus serves to people outside Collision
Engineers since the request-scoped upload links (INT-31) were retired on
17 September 2026.

## Context

Guided photo capture
([FRD-28](../frd/frd-28-guided-photo-capture.md)) asks a customer, or a
firm's staff, for photos of one vehicle from a phone, with nothing to
install and no account: ACC-12 excludes customer accounts and BND-06 a
persistent customer portal. The surface is therefore anonymous, reached
from a link in an e-mail, a text the staff member sends from their own
phone, a WhatsApp message or a QR code, and every request on it must carry
its own authority. Its hostname is printed in links that live for days, so
it is expensive to change once links are out. Production is one App Service
Web App on its `azurewebsites.net` hostname
([ADR-0049](0049-host-web-on-app-service-code-deploy.md)), and a custom
domain is a separate operator decision (EXT-19 stays retired).

## Decision

- **Route-bound anonymous tokens.** A photo link is `/c/{token}`; the app's
  API is under `/capture/api/v1`. The token is the whole authority for one
  capture: it admits that capture's pages and API calls and nothing else,
  and gives no staff access. No cookie is set on the surface.
- **Hash-only tokens.** A token is 256 bits from the platform's random
  source, written as 43 base64url characters, and stored only as its
  SHA-256, INT-31's rule kept. A capture may hold several tokens, so Resend
  mints another and Cancel revokes them all. A presented value that is not a
  token's shape is refused before any store is read.
- **The credential travels in a header.** The app sends `X-Capture-Link: c.`
  and a token, or `s.` and a slug, on every API call, so the token appears in
  a URL only on the page the link opens.
- **Standing-link slugs are not secrets.** `/s/{slug}` only starts a
  capture, which gets a token of its own; it never takes a photo. A
  contact's slug is 12 Crockford base32 characters and the website's is
  the readable one an Administrator types (R797). A slug is stored as it
  is, can be paused, replaced or retired (R796), and is bounded by its daily
  cap of captures started.
- **One answer for every unavailable link.** Unknown, expired, revoked,
  paused and retired credentials, a link whose Case is archived (R793), and
  a standing link whose contact is inactive (R796), get the same page and
  the same 404, so a guesser learns nothing. Opened is recorded on the first
  API call, never on a page fetch, because link previews fetch pages, and
  never on a Session token: the phone that made a capture (a hand-off's
  staff phone, or one taking the photos now, which is Opened as the capture
  is made) does not open it.
- **A token's purpose is recorded with it.** A token is issued for an
  e-mail, a copy, a hand-off or a session, and the purpose travels with
  the token. Only a Session token never records Opened. On a quick link
  sent from a Case, the Hand-off token is the customer's: it opens the
  guided app, while the link's other tokens open the firm's staff start
  (9 November 2026; R798, R808).
- **Same origin now.** The surface is served by the Web host on its own
  origin, `https://pegasus-prod-web-252ow37gij.azurewebsites.net/`, read from
  `PhotoCapture:PublicOrigin` (R779). A custom capture domain later changes
  that setting and the hostname links print, not the routes or the token
  rule; the hosted test instance uses its Tailscale Funnel origin on 8443
  ([ADR-0088](0088-one-hosted-test-instance.md), as amended by R785).
- **Headers and limits.** Every page and API response carries a strict
  Content Security Policy (`'self'` only, `blob:` for images and media, no
  frames, no forms, no workers, no objects), `Permissions-Policy:
  camera=(self), microphone=(), geolocation=()`, `Referrer-Policy:
  no-referrer`, `X-Robots-Tag: noindex, nofollow` and `Cache-Control:
  no-store`. Per-IP rate limits, per-link counts and sizes, per-standing-link
  daily caps and a 4 MiB request body bound every caller; a larger file is
  sent in 4 MiB parts. The figures are constants in Core, listed in
  [FRD-28](../frd/frd-28-guided-photo-capture.md#limits).
- **Telemetry never carries a credential.** A raw token exists only in the
  issuing response, the message that carries it and the phone; its value is
  redacted wherever it would be written to a log or a trace, and the
  credential header and the token and slug path segments are not recorded.
- **The token in the mail body is staff-initiated delivery.** A photo link
  e-mail is a staff send from an approved mailbox (R773), and a text is a
  staff send through Azure Communication Services from Pegasus's own UK
  mobile number (R918); Pegasus sends no link by itself, and WhatsApp is
  Copy link and the staff member's own phone. The e-mail's Sent evidence holds the link as any staff message
  holds what staff wrote.
- **A composition gate.** `Features:PhotoCapture` off registers nothing:
  `/c`, `/s`, `/capture` and `/PhotoCapture` answer 404 before routing.

## Consequences

- The capture app is static content under `wwwroot/capture/`, with its own
  Collision Engineers branding, outside the internal application's imagery
  rule ([design](../design/README.md#public-capture-surface)).
- The surface adds its tables and columns by two additive migrations:
  `PhotoCapture` (PhotoCaptures, PhotoCaptureTokens, PhotoCaptureItems,
  StandingPhotoLinks; `IntakeSubmissionGroups.DeclaredRegistration`) and
  `PhotoCaptureSettleFacts` (StandingPhotoLinkAddresses, PhotoCaptureBatches;
  `PhotoCaptures.ImagesCompletedAtUtc` and `QuickLinkFor`,
  `PhotoCaptureItems.DeclaredCaseId`,
  `IntakeSubmissionGroups.DeclaredIncidentDate`, and Send a photo link's
  settings on `WorkflowConfigurations`). No Key Vault secret is added.
- Moving to a custom domain needs the operator's decision, a hostname on the
  Web App, and `PhotoCapture:PublicOrigin`; links already sent keep the old
  hostname until they expire.

## Options considered

- **Token in the query string or a cookie on every call.** Rejected: the
  query string leaks into logs and referrers, and a cookie would make the
  surface stateful for a person who never signs in.
- **A separate host or domain now.** Deferred: a custom domain is the
  operator's separate decision, and a second host would add a deployment
  for no gain today.

## Links

- Behaviour: [FRD-28](../frd/frd-28-guided-photo-capture.md).
- Rulings: R773 to R785, and R793, R796 to R798 and R808, which amend it,
  in [rulings](../rulings.md).
- Hosting: [ADR-0049](0049-host-web-on-app-service-code-deploy.md).
- Test instance: [ADR-0088](0088-one-hosted-test-instance.md).

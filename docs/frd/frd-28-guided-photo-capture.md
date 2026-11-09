# FRD-28: Guided photo capture

> Owner capabilities: EXT-21, INT-34, MAIL-27, UI-20 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- A **photo link** asks one person for photos of one vehicle. HQ sends it
  from a Case, by e-mail from an approved mailbox or by Copy link; the
  person opens it on their phone, and a guided app takes the photos and
  sends them. Nothing is installed and nobody signs in.
- A **standing photo link** belongs to a Principal, Claim Source or Repairer
  contact, or is the one website link for private customers. It only starts
  captures; each capture it starts is a photo link of its own.
- Each photo is its own received file on the Photo capture channel. A
  capture for a known Case files each photo on the Case as it arrives; one
  with no Case waits and, when it settles, becomes a Vehicle images record
  under the registration the sender typed. A capture's photos never become
  Unidentified.
- Inspection images arrive In report; additional information (the scene,
  the other vehicle, paperwork and video) does not. Files go flat into the
  Case's Box folder under story-order names.
- Pegasus never sends a reminder by itself. **Links we sent** shows what is
  due and a member of staff resends with one click.

## Purpose

This document owns Pegasus's own guided capture, the product idea of issue
#1683, built under new capability identities (operator, 9 November 2026;
R770 to R782). It serves the PRD outcomes for image-led work and complete
images before Review, while keeping its exclusions: no SMS integration
(BND-04), no persistent customer portal (BND-06), no automatic outbound
message (MAIL-19) and no customer account (ACC-12)
([PRD](../prd/pegasus-product.md#operating-and-commercial-constraints)).
Tractable capture still arrives as an e-mailed PDF
([FRD-02](frd-02-intake-and-source-identity.md#ways-intake-starts) `EXT-17`).
Neighbouring rules stay with their owners: receipts in
[FRD-02](frd-02-intake-and-source-identity.md), Box custody and image tags in
[FRD-05](frd-05-documents-extraction-and-custody.md), Vehicle images records
and pairing in [FRD-19](frd-19-image-led-intake-and-pairing.md), matching in
[FRD-22](frd-22-pre-case-gates-matching-and-association.md), field
provenance in
[FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md), staff
send in [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md) and
image completeness in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review). The
public boundary is [ADR-0090](../adr/0090-public-photo-capture-surface.md).

## Behaviour

### Photo links

Staff send a photo link from a Case: the ribbon's **Send photo link** opens
`/PhotoCapture/Send/{caseId}`. The Case and its registration are read-only.
The customer's name, accident date, mobile and e-mail start from the Case
and may be changed. Staff choose the mode, **guided** (a page per step, for
a customer) or **quick** (guidance inside the camera, for a firm's staff),
**Live camera only**, the lifetime, 3, 7 (the default) or 14 days, and
whether full-size originals follow in the background. The link goes by
e-mail, by Copy link, or both (R770): text and WhatsApp are Copy link and
the staff member's own phone. Pegasus has no SMS or WhatsApp provider.

A link is `https://pegasus-prod-web-252ow37gij.azurewebsites.net/c/{token}`
(R776): Pegasus's own hostname, from `PhotoCapture:PublicOrigin`. The token
is 256 random bits written as 43 base64url characters, and Pegasus keeps
only its SHA-256 (INT-31's rule, kept). A capture may hold several tokens:
**Resend** mints another, and **Cancel** revokes them all. The app shows the
registration and never a Case/PO or any other reference (R779).

A capture is add-only until it expires. The sender may reopen the link and
add more; nothing they sent is taken back. **Opened** is recorded on the
link's first call to the app's API, not when the page is fetched, because
link previews fetch pages.

### Standing photo links

A Principal, Claim Source or Repairer contact may have a standing photo link,
managed on the **Photo links** tab of its edit page (R771): the standing
URL, a QR code, Copy, **Pause** and **Resume**, **Replace link**, and its
defaults, which are the mode a link sent on to a customer takes, Live camera
only, the lifetime and the originals choice. The one website link, for
private customers, is managed by Administrators from the Photo capture page
(R777).

A standing link is `/s/{slug}`. A contact's slug is 12 Crockford base32
characters and the website's a readable one. A slug is not a secret: it
lives on WhatsApp and in QR codes, and it can only start a capture, never
take a photo. Starting one asks for the registration and the details; a
capture without a registration is not started. A contact's staff either take
the photos themselves, in quick mode, or send their customer a link, which
starts a capture in the contact's forwarded mode and is carried by their own
phone's messages, not by Pegasus. The website link starts a guided capture.
Each capture started is a photo link with its own token. **Replace link**
gives the contact a new slug.

On **Coming in**, a capture's From names the contact and how the photos
came: **Their staff took them**, **Sent on to their customer**, or
**Customer, from our website**.

### Public surface

The app is the handover's capture app with Collision Engineers branding,
served from `wwwroot/capture/` (R775). Its pages are `/c/{token}` and
`/s/{slug}`; its API is under `/capture/api/v1` and takes the credential in
the header `X-Capture-Link`, as `c.` and a token or `s.` and a slug. The
pages and the API are anonymous and set no cookie.

Every page and API response carries:

- `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' blob:; media-src 'self' blob:; connect-src 'self'; worker-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'`;
- `Permissions-Policy: camera=(self), microphone=(), geolocation=()`;
- `Referrer-Policy: no-referrer`;
- `X-Robots-Tag: noindex, nofollow`;
- `Cache-Control: no-store`.

An unknown, expired, revoked or paused link gets one identical answer: the
page says the link is no longer active, and the API answers 404. The four
are never told apart. The page's further wording is an
[open decision](../open-decisions.md#photo-capture-the-unavailable-pages-wording).

### The app's steps and required items

The app takes, in order:

1. the overview: eight angles clockwise from the front (Front, Front driver
   corner, Driver side, Rear driver corner, Rear, Rear passenger corner,
   Passenger side, Front passenger corner);
2. for each damaged area the sender marks, four stages: an overview "3 big
   steps back", a closer shot "1 big step away", close-ups, then extras;
3. the number plate, the VIN, and the dashboard with the mileage;
4. additional information: the accident scene, the other vehicle,
   paperwork, a walk-around video, and a note.

The damage areas are twelve zones: Front, Front driver corner, Driver side
front, Driver side middle, Driver side rear, Rear driver corner, Rear, Rear
passenger corner, Passenger side rear, Passenger side middle, Passenger side
front and Front passenger corner.

Before **Complete** a capture needs the eight overviews; for each marked
area an overview, a closer shot and one close-up; the number plate; and the
dashboard. The VIN is required only when a bodyshop's own staff took the
photos. Extras, additional information and the note are never required.
**Something missing** is derived from this list.

### Routing and filing

Each item is its own intake receipt on the Photo capture channel. A
submission group's membership is fixed when it is created
([FRD-19](frd-19-image-led-intake-and-pairing.md#grouped-image-intake-routing)),
and a capture keeps receiving, so a capture is not one group from the start.

- **A Case link**, and **a standing capture whose typed registration
  matched exactly one eligible formal Case** (within a known Principal's own
  Cases, its incident date not contradicting the typed one;
  [FRD-22](frd-22-pre-case-gates-matching-and-association.md#matching-conflicts-and-reversible-association)),
  file each item on the Case as it arrives: a receipt for a declared
  destination, recorded as the routing's system work (R773).
- **A standing capture with no such Case** waits. Its receipts are
  processed, with no identification, never Unidentified and never held in
  Box holding. When the capture settles they are formed into one submission
  group carrying the typed registration, and the existing group routing
  registers it as a Vehicle images record (Awaiting instruction, its own Box
  folder, pairing when the instruction arrives, and staff choosing on the
  record's page when several Cases match), or files it on a Case that has
  appeared since ([FRD-19](frd-19-image-led-intake-and-pairing.md)). There
  is no check-the-match state (R773).

A capture **settles** when the sender presses **Send**, or the link expires
with photos on it, and every photo has been processed. On settling, a
capture on a Case:

- completes the Case's images (R778;
  [FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review)),
  never on the first photo;
- writes the sender's note into the Case's Notes as
  "Customer's note (photo link): …";
- fills empty Case fields from the typed details
  ([Case field filling](#case-field-filling));
- raises the Case Engineer's bell, **Photos received**.

A capture that settled with no Case does this when its Vehicle images record
reaches a Case. Photos added after a settle form the next batch; with no
Case, that batch becomes a further Vehicle images record for the same
registration. A full-size original files as the next version of the same
Case image.

### Baskets and names

The Case's Box folder stays flat: no subfolders (R772). An item is named by
the Case's document number, its story position and a plain label, as in
`014 02-03 Front driver corner closer.jpg`. The label uses only the app's
own words: the angle, the zone and the stage, or Number plate, VIN,
Dashboard, Accident scene, Other vehicle, Paperwork or Walk-around video.

The **inspection images** (overview, damage, number plate, VIN and
dashboard) arrive In report. The **additional information** (the scene, the
other vehicle, paperwork and video) arrives not In report, and the other
vehicle arrives tagged Third party
([FRD-05](frd-05-documents-extraction-and-custody.md#image-tags)). Moving an
item between the baskets is the In report toggle. Paperwork and video file
as documents of type Other.

On the Case, Files → Images shows a **What they told us** card and the note,
then the photos in story groups
([FRD-16](frd-16-case-record-workspace.md#files)).

### Statuses and reminders

A capture's status is derived each time it is read, never stored:

| Status | When |
| --- | --- |
| Sent not opened | The link has not been opened |
| Opened | Opened, no photo yet |
| Photos coming in | Photos received, not yet sent |
| Something missing | Sent, or expired with photos, and a required item is missing |
| Needs a phone call | Two reminders and nothing back since |
| Complete | Sent, or expired with photos, and nothing required is missing |
| Expired | Expired with no photos |
| Cancelled | Cancelled by staff |

A reminder is due, and shown, when a link is not opened after 24 hours and
again after 48; when it was started but not finished after 4 quiet hours;
when something is missing once it was sent; and when it is about to expire,
within one day. Pegasus never sends a reminder (R770; MAIL-19 stands):
**Links we sent** shows what is due, and **Remind** or **Resend** is one
click by a member of staff. **Log call** records a phone call.

### The photo link e-mail

The photo link e-mail is a staff send from an Outgoing approved mailbox
through the Graph staff send
([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)),
with the template purpose **Photo link**
([FRD-17](frd-17-administration-workspace.md#e-mail-templates)). Its
placeholders are `{registration}`, `{claimant}`, `{photo link}`,
`{link expiry}` and `{principal name}`. Its built-in body:

```text
Hi {claimant},

Collision Engineers need a few photos of {registration} to assess the damage. Please open this link on your phone – it takes about 3 minutes and there's nothing to download:

{photo link}

The link works until {link expiry}.

Kind regards
Collision Engineers
```

Its subject is an
[open decision](../open-decisions.md#photo-capture-the-photo-link-e-mails-subject).
There is no automatic or scheduled send.

### Case field filling

When a capture settles on a Case, the claimant name, contact number,
accident date, inspection location and mileage the sender typed fill the
Case's empty fields as Facts with source kind Photo link, shown with the
source tag **Photo link**
([FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md#field-provenance-and-value-kinds)).
A value already on the Case is never overwritten (R780). The registration is
never written: it routed the capture. The sender's views that have no Case
field (roadworthy, preferred outcome, in hire, airbags and steering) stay on
the capture and show with the photos. Where a Photo link mileage ranks
among FRD-23's mileage evidence, and whether a Photo link inspection
location needs an operator's confirmation, are
[open decisions](../open-decisions.md#photo-capture-a-typed-mileage-and-inspection-location-against-existing-evidence-rules).

### Staff pages

**Photo capture** (`/PhotoCapture`) is a rail item under Work, after Cases,
with no count (R771;
[FRD-12](frd-12-operator-experience.md#shell-and-routes)). It has two tabs:

- **Coming in**: standing and website captures, with Vehicle, From,
  Customer, Case or Awaiting instruction, Status, Photos and Received;
- **Links we sent**: Case links, with Vehicle, Customer and mobile, Case,
  From, Sent by, Status, Photos and Next reminder, and the actions Resend,
  Remind, Log call and View photos.

`/PhotoCapture/Send/{caseId}` sends a link ([Photo links](#photo-links)).
`/PhotoCapture/{id}` is a link's detail: its facts, the typed details, the
note, the status, the missing items, the items by story group and the
history, with Resend, Remind, Log call, Extend and Cancel. The Vehicle images
record page shows the source **Photo link** with the details and the note.

### Limits

| Limit | Value |
| --- | --- |
| Page requests, per IP | 30 a minute |
| The app's context reads, per IP | 60 a minute |
| Uploads, per IP | 300 a minute |
| Captures started, per IP | 10 an hour |
| Items, per link | 120 a minute |
| Per link | 250 photos, 250 originals, 20 documents, 5 videos, 1.5 GB |
| Captures started, per standing link | 100 a day (website), 50 a day (contact) |
| One request body | 4 MiB; a larger file goes in 4 MiB parts |
| One photo | 10 MB |
| One original | 25 MB |
| One document | 25 MB |
| One video | 100 MB, the intake per-file cap |
| Lifetime | 3, 7 (default) or 14 days |

Photos are JPEG or PNG; paperwork is PDF, JPEG or PNG; video is MP4 or MOV.
The limits are constants, not configuration.

## States and transitions

A photo link is live from issue until it expires or is cancelled. **Extend**
adds 3, 7 or 14 days to its expiry, from the later of now and the expiry;
**Cancel** revokes every token. A standing link is active,
paused or replaced; **Resume** makes a paused link active again. A capture
moves through its derived statuses
([Statuses and reminders](#statuses-and-reminders)) and settles once per
batch. Its destination is undecided, a Case, or a Vehicle images record.

## Edge cases and fail-closed behaviour

- **Unavailable.** An unknown, malformed, expired, revoked or paused
  credential gets the same page or 404, and no store is read for a value
  that is not a token's shape.
- **Caps.** A request past a rate limit, a size, a per-link count or a
  standing link's daily cap is refused; nothing past the cap is kept.
- **Replays.** An item carries the id the phone gave it, so a retried upload
  is the same item. A retried settle creates no second group and files
  nothing twice.
- **Never Unidentified.** A capture's receipt never becomes Unidentified
  and is never held in Box holding: a file the surface could not take was
  refused at the door, and a receipt that waits does so for its capture.
- **Batches after settle.** Photos added after a settle are the next
  batch; with no Case they make a further Vehicle images record for the same
  registration.
- **Registration.** Once a photo has filed against a capture's
  registration, the registration is locked: a different one would be a
  different vehicle.
- **Feature off.** With `Features:PhotoCapture` off, `/c`, `/s`, `/capture`
  and `/PhotoCapture` answer 404, and the rail item, Send photo link and the
  Photo links tab are absent
  ([configuration](../engineering/configuration.md#photo-capture)).

## Acceptance evidence

Core tests cover the token and slug shapes, the required items, the derived
statuses and due reminders, the story order and names, routing and the
settle. Integration tests cover the public routes' one unavailable answer,
the security headers and limits, filing on a Case and the Vehicle images
record a settled capture becomes. The staff pages are signed off on the
pull request's screenshots (R775). Deployment and live acceptance are
separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `EXT-21`, `INT-34`, `MAIL-27`, `UI-20` in
  [capabilities](../capabilities.md).
- Rulings: R770 to R782 in [rulings](../rulings.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-04](frd-04-parties-accounts-and-access.md#contacts-administration),
  [FRD-05](frd-05-documents-extraction-and-custody.md),
  [FRD-12](frd-12-operator-experience.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-16](frd-16-case-record-workspace.md),
  [FRD-17](frd-17-administration-workspace.md),
  [FRD-19](frd-19-image-led-intake-and-pairing.md),
  [FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md),
  [FRD-22](frd-22-pre-case-gates-matching-and-association.md),
  [FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md).
- Technical constraints:
  [ADR-0090](../adr/0090-public-photo-capture-surface.md),
  [ADR-0088](../adr/0088-one-hosted-test-instance.md) (the test instance's
  Funnel).

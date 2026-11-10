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
R773 to R817). It serves the PRD outcomes for image-led work and complete
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
The customer's name, accident date and mobile start from the Case and may
be changed; the e-mail is typed, because a Case holds no customer e-mail.
Staff choose the mode, **guided** (a page per step, for a customer) or
**quick** (guidance inside the camera, for a firm's staff), **Live camera
only**, the lifetime, 3, 7 or 14 days, and whether full-size originals
follow in the background. Send starts on the mode, the Live camera only and
originals choices and the lifetime an Administrator sets in Administration ›
Configuration, and offers only the lifetimes set there: guided, both on and
7 days of all three until they are changed (operator, 9 November 2026; R813,
R817). Live camera only covers the vehicle
photos: the scene and other-vehicle photos, paperwork and the walk-around
video can always come from the camera roll, as in the handover's app (R778;
9 November 2026). The link goes by e-mail, by Copy link, or both (R773):
text and WhatsApp are Copy link and the staff member's own phone. Pegasus
has no SMS or WhatsApp provider.

When Quick is chosen, Send asks who the link is for, **Type**: **Accident
management** or **Bodyshop**, and requires it; a guided link has none
(operator, 9 November 2026; R808). A quick link sent from a Case opens the
handover app's staff start and form for that kind of firm, with the
registration read-only and no reference (R782). Accident management gets
"Photos needed for {registration}", "Thanks for the instruction – we’re
waiting on the photos for this one." and the form **About this claim**, with
the Case's Principal as the instructing firm and the saved addresses of the
Principal's own standing link. Bodyshop gets "Photos needed for
{registration}", "Collision Engineers have been instructed on this vehicle
and are waiting for the photos.", "Confirm a few details, then take the
guided photos – or send the customer a link if the car isn’t with you." and
the form **About this job**, with no saved address; its staff are asked for
the VIN, as a bodyshop's own staff are. **Send customer a link** there hands
the same capture on: what the firm typed is kept, and a guided link to the
same Case, carried by the staff member's own phone, opens the customer's
app, so the Case keeps one link. The firm's Opened stands, and **Links we
sent** shows the link as **Sent on to their customer** (operator, 9 November
2026; R798, R808).

A link is `https://pegasus-prod-web-252ow37gij.azurewebsites.net/c/{token}`
(R779): Pegasus's own hostname, from `PhotoCapture:PublicOrigin`. The token
is 256 random bits written as 43 base64url characters, and Pegasus keeps
only its SHA-256 (INT-31's rule, kept). A capture may hold several tokens:
**Resend** mints another, and **Cancel** revokes them all. The app shows the
registration and never a Case/PO or any other reference (R782).

A capture is add-only until it expires. The sender may reopen the link and
add more; nothing they sent is taken back. After Send the app says "Forgot
something? Open the same link again to send more photos for this vehicle."
A capture started from a standing link, its QR code or the website link by
whoever holds the phone, where opening that link again starts a new capture,
says instead "Forgot something? Open the link again and enter the same
registration to send more photos for this vehicle." (operator, 9 November
2026; R809). **Opened** is recorded on the link's first call to the app's
API, not when the page is fetched, because link previews fetch pages. A
capture a standing link starts to take the photos now is Opened as it is
made. A hand-off's staff phone carries on under a session credential of its
own, which never records Opened, so the paperwork it sends from its form
leaves the customer's link not opened until the customer opens it
(9 November 2026).

### Standing photo links

A Principal, Claim Source or Repairer contact may have a standing photo link,
managed on the **Photo links** tab of its edit page (R774): the standing
URL, a QR code, Copy, **Pause** and **Resume**, **Replace link**, **Retire
link**, and its defaults, which are the mode a link sent on to a customer
takes, Live camera only, the lifetime and the originals choice. The one
website link, for private customers, is managed by Administrators from the
Photo capture page with the same controls and defaults, less the mode,
because its captures are guided (operator, 9 November 2026; R797).

The tab's **Saved addresses** are offered on the firm's form as its
inspection locations: first those taken from the contact, its own address or
a Principal's inspection locations, which stay read-only, then any added on
the tab, the only ones that can be edited or removed (operator, 9 November
2026; R806, R815). The website link has no contact and keeps none.

A standing link is `/s/{slug}`. A contact's slug is 12 Crockford base32
characters. The website's is the readable one the Administrator types: 6 to
60 lowercase letters, digits and hyphens, not starting or ending with a
hyphen, and held by no other link, retired ones included; Pegasus never
makes one up (operator, 9 November 2026; R797). A slug is not a secret: it
lives on WhatsApp and in QR codes, and it can only start a capture, never
take a photo. Starting one asks for the registration and the details; a
capture without a registration is not started. A contact's staff either take
the photos themselves, in quick mode, or send their customer a link, which
starts a capture in the contact's forwarded mode and is carried by their own
phone's messages, not by Pegasus. The website link starts a guided capture.
Each capture started is a photo link with its own token. **Replace link**
gives a contact's link a new slug, and the website link the new address the
Administrator types (R797). **Retire link** asks "Retire this link? Its
address stops working for good and cannot be resumed." and then ends the
link for good: its address answers like an unknown one, it cannot be
resumed, and the contact, or the website, may be given a new link
(operator, 9 November 2026; R796, R805).

On **Coming in**, a capture's From names the contact and how the photos
came: **Their staff took them**, **Sent on to their customer**, or
**Customer, from our website**.

### Public surface

The app is the handover's capture app with Collision Engineers branding,
served from `wwwroot/capture/` (R778). Its pages are `/c/{token}` and
`/s/{slug}`; its API is under `/capture/api/v1` and takes the credential in
the header `X-Capture-Link`, as `c.` and a token or `s.` and a slug. The
pages and the API are anonymous and set no cookie.

Every page and API response carries:

- `Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' blob:; media-src 'self' blob:; connect-src 'self'; worker-src 'none'; object-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'`;
- `Permissions-Policy: camera=(self), microphone=(), geolocation=()`;
- `Referrer-Policy: no-referrer`;
- `X-Robots-Tag: noindex, nofollow`;
- `Cache-Control: no-store`.

An unknown, expired, revoked, paused or retired link, one whose Case is
archived, and a standing link whose contact is inactive, gets one identical
answer: the page says "This link is no longer active." and "Ask whoever sent
it to you for a new one.", and the API answers 404. They are never told apart
(operator, 9 November 2026; R789). A standing link whose contact is inactive
starts no capture until the contact is active again (operator, 9 November
2026; R796). Once a capture's Case is archived its link takes nothing more;
photos already filed stay on the Case, and the capture reads Expired, is due
no reminder, and is neither resent nor extended (operator, 9 November 2026;
R793).

### The app's steps and required items

The app takes, in order:

1. the overview: eight angles clockwise from the front (Front, Front driver
   corner, Driver side, Rear driver corner, Rear, Rear passenger corner,
   Passenger side, Front passenger corner);
2. for each damaged area the sender marks, four stages: an overview "3 big
   steps back", a closer shot "1 big step away", close-ups, then extras;
3. the number plate, the VIN, and the dashboard with the mileage;
4. additional information: the accident scene, the other vehicle,
   paperwork, a walk-around video, and a note. The other vehicle has a row
   of its own, **Other vehicle photos** ("The other vehicle involved"),
   after **Accident scene photos** ("Where it happened") (operator,
   9 November 2026; R800, R805).

The damage areas are twelve zones: Front, Front driver corner, Driver side
front, Driver side middle, Driver side rear, Rear driver corner, Rear, Rear
passenger corner, Passenger side rear, Passenger side middle, Passenger side
front and Front passenger corner.

Before **Complete** a capture needs the eight overviews; for each marked
area an overview, a closer shot and one close-up; the number plate; and the
dashboard, unless the sender said they have no keys: the skip is recorded on
the capture, the dashboard is not listed as missing, and staff see
**Dashboard** "Skipped – no keys available" (operator, 9 November 2026;
R799). The VIN is required only when a bodyshop's own staff took the
photos, on their standing link or a quick Case link for a Bodyshop
(9 November 2026; R808). Extras, additional information and the note are never required.
**Something missing** is derived from this list. After Send, with items
still missing, the app asks **Could you add a few more?**, lists them, and
offers **Add more** and **That’s everything** (operator, 9 November 2026;
R787).

A damage photo taken outside the marked areas, from **Add more** or the
phone's own camera, is accepted. It files in a **Damage** group after the
areas, named as in `015 04-03 Damage.jpg`, and counts towards no area's
required stages; the app keeps the handover's **Other photos** heading for
it (operator, 9 November 2026; R802).

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
  destination. A Case link is the decision of the member of staff who sent
  it, so its photos file on any Case that is not archived, including a
  report-sent, closed or Triage Case; only the standing capture Pegasus
  routed is recorded as the routing's system work (operator, 9 November
  2026; R790, amending R776 for a Case link).
- **A standing capture with no such Case** waits. Its receipts are
  processed, with no identification, never Unidentified and never held in
  Box holding. When the capture settles they are formed into one submission
  group carrying the typed registration, and the existing group routing
  registers it as a Vehicle images record (Awaiting instruction, its own Box
  folder, pairing when the instruction arrives, and staff choosing on the
  record's page when several Cases match), or files it on a Case that has
  appeared since ([FRD-19](frd-19-image-led-intake-and-pairing.md)). There
  is no check-the-match state (R776).

A capture **settles** when the sender presses **Send**, or the link expires
with photos on it, and every photo has been processed. On settling, a
capture on a Case:

- completes the Case's images (R781;
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
Case image: the image keeps its tile, tags, report place and crop, Box shows
the compressed picture as the earlier version of the one file (R664), and
the report prints the original once it is filed. Until then, and should the
original's upload fail, the compressed picture stands. Originals follow only
while a capture files on its Case as each photo arrives: until there is a
Case the app is told originals are off and sends none, and when a Vehicle
images record later reaches a Case the compressed pictures stand (operator,
9 November 2026; R801). An original counts towards the link's originals and
bytes ([Limits](#limits)) before it is filed.
A photo taken in the app's own camera has as its original the camera's
full-resolution frame, sent after the photo itself; an original over 25 MB
is not sent, and the compressed picture stands (operator, 9 November 2026;
R780, R801).

### Baskets and names

The Case's Box folder stays flat: no subfolders (R775). An item is named by
the Case's document number, its story position and a plain label, as in
`014 02-03 Front driver corner closer.jpg`. The label uses only the app's
own words: the angle, the zone and the stage, or Damage (operator,
9 November 2026; R802), Number plate, VIN, Dashboard, Accident scene,
Other vehicle, Paperwork or Walk-around video.

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

A capture's status is derived each time it is read, never stored. Its
Photos count, on both tabs and the link's page, counts photographs, not
paperwork or video:

| Status | When |
| --- | --- |
| Sent not opened | The link has not been opened |
| Opened | Opened, no photo yet |
| Photos coming in | Photos received, not yet sent; paperwork or video alone is not photos (9 November 2026) |
| Something missing | Sent, or expired with photos, and a required item is missing |
| Needs a phone call | Two reminders and nothing back since |
| Complete | Sent, or expired with photos, and nothing required is missing |
| Expired | Expired with no photos, or its Case is archived (operator, 9 November 2026; R793) |
| Cancelled | Cancelled by staff |

A reminder is due, and shown, when a link is not opened after 24 hours and
again after 48; when it was started but not finished after 4 quiet hours;
when something is missing once it was sent; and when it is about to expire,
within one day. Pegasus never sends a reminder (R773; MAIL-19 stands):
**Links we sent** shows what is due, and **Remind** or **Resend** is one
click by a member of staff. **Log call** records a phone call. On **Coming
in**, **Ask for it** is the same click for a capture not sent from a Case: it
makes a fresh link and counts as a reminder (operator, 9 November 2026;
R806, R816).

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

Its subject is "Photos of {registration} for Collision Engineers", and
"Photos for Collision Engineers" when the Case has no registration
(operator, 9 November 2026; R788); staff may edit it before sending. There
is no automatic or scheduled send.

On the Send page the message shows the wording with the customer's name,
the registration, the Principal and the link's expiry filled in;
`{photo link}` stays until the link exists. Once staff edit it, it says
"Edited for this one only." with **Put the standard wording back**. An
edited message is sent as staff wrote it; one they did not edit follows the
name and lifetime the form holds when the link is sent (operator,
9 November 2026; R806).

### Case field filling

When a capture settles on a Case, the claimant name, contact number,
accident date, inspection location and mileage the sender typed fill the
Case's empty fields as Facts with source kind Photo link, shown with the
source tag **Photo link**
([FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md#field-provenance-and-value-kinds)).
A value already on the Case is never overwritten (R783). The registration is
never written: it routed the capture. The sender's views that have no Case
field (roadworthy, preferred outcome, in hire, airbags and steering) stay on
the capture and show with the photos. Where a Photo link mileage ranks
among FRD-23's mileage evidence, and whether a Photo link inspection
location needs an operator's confirmation, are
[open decisions](../open-decisions.md#photo-capture-a-typed-mileage-and-inspection-location-against-existing-evidence-rules).

### Staff pages

**Photo capture** (`/PhotoCapture`) is a rail item under Work, after Cases,
with no count (R774;
[FRD-12](frd-12-operator-experience.md#shell-and-routes)). It has two tabs:

- **Coming in**: standing and website captures, with Vehicle, From,
  Customer, Case, Status, Photos and Received. The Case column reads
  **Awaiting instruction** for a capture with no Case (operator, 9 November
  2026; R805). The From filter also offers **No case yet**, the captures
  with no Case. **Ask for it**, on a live row whose status is Something
  missing, makes a fresh link for that capture: when the capture is on a
  Case and holds an e-mail address it e-mails the link, as Resend does;
  otherwise it shows the link for staff to copy and send themselves
  (operator, 9 November 2026; R806, R816);
- **Links we sent**: Case links, with Vehicle, Customer and mobile, Case,
  From, Sent by, Status, Photos and Next reminder, and the actions Resend,
  Remind, Log call and View photos, with **Cancel link** on each live row.
  From is the Case's Principal (operator, 9 November 2026; R805), with
  **Sent on to their customer** under it once a quick link's firm has
  handed it on (operator, 9 November 2026; R798). Remind is offered only
  while the link is live.

Each tab shows how many captures it holds (operator, 9 November 2026;
R806).

`/PhotoCapture/Send/{caseId}` sends a link ([Photo links](#photo-links)).
`/PhotoCapture/{id}` is a link's detail: its facts, the typed details, the
note, the status, the missing items, the items by story group and the
history, with Resend, Remind, Log call, Extend and Cancel. The history names
who acted: "Cancelled · name", "Link extended · name" and "Call logged ·
name" (operator, 9 November 2026; R805). A live link on a Case also offers
**Cancel link** beside it in the Case's Files → Images. Cancel is the same
act wherever it is offered, with the same refusal, and a repeated click
cancels once (operator, 9 November 2026; R795). Beside a photo link in the
Case's Files → Images stand its status, in the words above, and its photo
count; while a live link has nothing yet, **No photos yet** and, on a link
sent from the Case, **Resend the link**, which is the link page's Resend
(operator, 9 November 2026; R806). Paperwork and video a capture with no
Case received stay kept and unfiled, and each opens from the link's page as
any received file opens; nothing new is filed, chased or deleted, and a
capture that sent only paperwork stays on Coming in (operator, 9 November
2026; R807). The Vehicle images record page shows the source **Photo link**
with the details and the note, on the record of every batch the capture
made, not only its latest; and the Case shows the capture's story once any
of those records has reached it.

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
The limits are constants, not configuration, except which of the three
lifetimes Send offers and the one it starts on, which an Administrator sets
in Administration › Configuration; Extend and a contact's link defaults keep
all three (operator, 9 November 2026; R813, R817).

## States and transitions

A photo link is live from issue until it expires, is cancelled, or its Case
is archived (operator, 9 November 2026; R793). **Extend**
adds 3, 7 or 14 days to its expiry, from the later of now and the expiry;
**Cancel** revokes every token. A standing link is active, paused,
replaced or retired; **Resume** makes a paused link active again, and a
retired link is never active again (R796). A capture
moves through its derived statuses
([Statuses and reminders](#statuses-and-reminders)) and settles once per
batch. Its destination is undecided, a Case, or a Vehicle images record.

## Edge cases and fail-closed behaviour

- **Unavailable.** An unknown, malformed, expired, revoked, paused or
  retired credential, one whose Case is archived (operator, 9 November 2026;
  R793), and a standing link whose contact is inactive (operator, 9 November
  2026; R796), gets the same page or 404, and no store is read for a value
  that is not a token's shape.
- **No connection.** When the phone cannot reach Pegasus, or a refusal
  carries no words of its own, the app says "We couldn’t reach Collision
  Engineers. Check your signal and try again." (operator, 9 November 2026;
  R786). It says the same, as a retry, while photos wait to go because the
  phone is offline or one has failed three times running; a request that
  hangs is given up and retried. Send stays unavailable while any photo is
  still to go, and the browser asks before the page is left then. A photo
  Pegasus refuses is outlined, does not hold Send back, and the review counts
  "{n} of {m} uploaded" (9 November 2026; R786).
- **Caps.** A request past a rate limit, a size, a per-link count or a
  standing link's daily cap is refused; nothing past the cap is kept.
- **Replays.** An item carries the id the phone gave it, so a retried upload
  is the same item, and it declares the Case its first attempt declared. An
  item joins the batch open when Pegasus takes it, never one already
  settled; a retried upload that never reached intake before its batch
  settled joins the open batch. A retried settle creates no second group and
  files nothing twice.
- **Never Unidentified.** A capture's receipt never becomes Unidentified
  and is never held in Box holding: a file the surface could not take was
  refused at the door, and a receipt that waits does so for its capture.
- **Batches after settle.** Photos added after a settle are the next
  batch; with no Case they make a further Vehicle images record for the same
  registration.
- **Registration.** Once a photo has filed against a capture's
  registration, the registration is locked: a different one would be a
  different vehicle. Until its first photo a standing capture is routed
  again each time its details are recorded, so a registration or accident
  date changed before then is routed on; once it holds an item its routing
  stands. A link sent from a Case keeps its Case's registration, which it
  shows read-only ([Photo links](#photo-links)), and a typed one is ignored.
- **Feature off.** With `Features:PhotoCapture` off, `/c`, `/s`, `/capture`
  and `/PhotoCapture` answer 404, and the rail item, Send photo link, the
  Photo links tab and Send a photo link's settings in Administration ›
  Configuration are absent
  ([configuration](../engineering/configuration.md#photo-capture)).

## Acceptance evidence

Core tests in `tests/Pegasus.Core.Tests/PhotoCapture/` cover the token and
slug shapes, the damage areas, the required items, the derived statuses, due
reminders and pairing, the story order and names, the intake rules,
the Case fields a capture offers, which tokens record Opened, receiving an
item, recording the details and routing, minting from a standing link, a
quick link's Type, the hand-on of a quick Case link, standing link
administration (retiring, the typed website address, saved addresses and an
inactive contact) and the settle; Send's two settings are covered with the
workflow configuration in `tests/Pegasus.Core.Tests/Identity/AdministrationPolicyTests.cs`.
Integration tests hold the rest:

- `PhotoCaptureSecurityTests`: the one unavailable answer, byte for byte,
  for an unknown, expired, revoked, paused, archived, retired or
  inactive-contact link, with the capture headers and no cookie; a dead
  link's oversized upload; the page rate limit; and the feature switch;
- `PhotoCaptureWebTests`: a Case link's photos filing on the Case (story
  name, baskets, the issuer's decision, a report-sent Case, images not
  completed by the first photo, the loose damage photo, originals and their
  bytes, the ignored typed registration, one link's token not reaching
  another's items), an archived Case's dead link, and the staff pages (From,
  Remind and Cancel on live rows, who acted, Retire, the website link's
  typed address and defaults, the tab counts, No case yet, Ask for it, the
  Case page's status and Resend, a kept file opening);
- `StandingCaptureWebTests`: a standing capture opened as it starts, waiting
  with no Case and never Unidentified, taking paperwork with no Case, routed
  until its first photo, taking no originals with no Case, filing on the one
  Case its registration matches, the hand-off's session token, and an item
  joining the open batch.

`tests/Pegasus.ArchitectureTests/CaptureAssetContractTests.cs` holds the
app's assets to the security policy (no inline style, handler or script
address), its upload bounds to the API's, its unavailable and done-page
sentences, and its file inputs. Nothing beyond these is claimed as tested.
The staff pages are signed off on the pull request's screenshots (R778).
Deployment and live acceptance are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `EXT-21`, `INT-34`, `MAIL-27`, `UI-20` in
  [capabilities](../capabilities.md).
- Rulings: R773 to R817 in [rulings](../rulings.md).
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

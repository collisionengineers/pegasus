# FRD-28: Guided photo capture

> Owner capabilities: EXT-21, INT-34, MAIL-27, UI-20 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- A **photo link** asks one person for photos of one vehicle. HQ sends it
  from a Case, by e-mail from an approved mailbox, by text from Pegasus's
  own UK mobile number or by Copy link; the
  person opens it on their phone, and a guided app takes the photos and
  sends them. Nothing is installed and nobody signs in.
- A **standing photo link** belongs to a Principal, Claim Source or Repairer
  contact, or is the one website link for private customers. It only starts
  captures; each capture it starts is a photo link of its own.
- Each photo is its own received file on the Photo capture channel. A
  capture for a known Case files each photo on the Case as it arrives; one
  with no Case waits and, when it settles, files on the one Case its typed
  registration and date then match, or becomes a Vehicle images record
  under the registration the sender typed. A capture's photos never become
  Unidentified.
- Inspection images arrive In report; additional information (the scene,
  the other vehicle, paperwork and video) does not. Files go flat into the
  Case's Box folder under story-order names.
- Pegasus never sends a reminder by itself. **Direct links** shows what is
  due and a member of staff resends through the composer, which sends nothing
  until **Send by e-mail** or **Send by text**.

## Purpose

This document owns Pegasus's own guided capture, the product idea of issue
#1683, built under new capability identities (operator, 9 November 2026;
R773 to R817). It serves the PRD outcomes for image-led work and complete
images before Review, while keeping its exclusions: no SMS integration
beyond texting the photo link itself (BND-04; R918), no persistent customer portal (BND-06), no automatic outbound
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

Staff send a photo link from a Case: the ribbon's **Send photo link**, and
the same button in the Files head, opens the composer over the Case, whose
page without script is `/PhotoCapture/Send/{caseId}` (operator, 11 November
2026; v51 K, M; [R948](../rulings.md)). A closed Case cannot send one: the button is
hidden and the Send page refuses it with "The Case cannot take a photo
link."; links sent before the Case closed keep working (operator,
10 November 2026; R847). The Case, its registration and its Principal stand
in the composer's eyebrow. The composer shows one channel at a time,
**Email**, **Text message** or **Link to copy**, each with only its own
recipient, wording and button; Mode, its Type when Quick, and Lasts share
one row (R948).
The customer's name, accident date and mobile start from the Case and may
be changed; the e-mail is typed, because a Case holds no customer e-mail.
Staff choose the mode, **guided** (a page per step, for a customer) or
**quick** (guidance inside the camera, for a firm's staff), **Live camera
only**, the lifetime, 3, 7 or 14 days, and whether full-size originals
follow in the background. Send starts on the mode, the Live camera only and
originals choices and the lifetime an Administrator sets in Administration ›
Configuration
([FRD-17](frd-17-administration-workspace.md#workflow-configuration)),
and offers only the lifetimes set there: guided, both on and
7 days of all three until they are changed (operator, 9 November 2026; R813,
R817). Live camera only covers the vehicle
photos: the scene and other-vehicle photos, paperwork and the walk-around
video can always come from the camera roll (R778; 9 November 2026). The
walk-around video can also be recorded there and then: until it has one,
its row offers **Record**, which opens the camera, and **Choose**, which
opens the phone's own picker (operator, 12 November 2026; R984, R985). The
handover's app only recorded, and from 9 to 12 November 2026 Pegasus left
it to the phone's picker, which on Android Chrome is the gallery alone
(#2095). The link goes by e-mail, by Copy link, or both (R773),
or by text to the mobile on the form ([The photo link
text](#the-photo-link-text); R918, R920); WhatsApp is Copy link and the
staff member's own phone, and Pegasus has no WhatsApp provider.

When Quick is chosen, Send asks who the link is for, **Type**: **Accident
management** or **Bodyshop**, and requires it; a guided link has none
(operator, 9 November 2026; R808). A Quick link sent without a Type is
refused with "Choose a Type for a Quick link." beside the form (operator,
10 November 2026; R868). A quick link sent from a Case opens the
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
the VIN, as a bodyshop's own staff are. A Bodyshop link names no firm, so the
three lines an Accident management link shows with its firm's name are left
out (operator, 10 November 2026; R849). Either form opens with the
customer's name and accident date from the Send page already in it, and the
firm can change them (operator, 10 November 2026; R853). **Send customer a
link** there hands
the same capture on: what the firm typed is kept, and a guided link to the
same Case, carried by the staff member's own phone, opens the customer's
app, so the Case keeps one link. The firm's Opened stands, and **Links we
sent** shows the link as **Sent on to their customer** (operator, 9 November
2026; R798, R808); whether the customer has opened it is not recorded, so
the firm's Opened shows until photos arrive (operator, 10 November 2026;
R848). The hand-on screen calls it a link, no longer "a one-time link"
(operator, 10 November 2026; R872), and once the link is handed on the
firm's phone is headed "Sent to the customer"; a standing link's hand-on
keeps "Instruction received" (operator, 10 November 2026; R873).

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
2026; R809). On a reopened link the review page's strips show only this
visit's pictures, and under a strip whose count includes earlier photos the
app says "Plus {n} sent earlier" with the real number (operator,
10 November 2026; R862). **Opened** is recorded on the link's first call to
the app's API, not when the page is fetched, because link previews fetch pages. A
capture a standing link starts to take the photos now is Opened as it is
made. A hand-off's staff phone carries on under a session credential of its
own, which never records Opened, so the paperwork it sends from its form
leaves the customer's link not opened until the customer opens it
(operator, 10 November 2026; R877).

### Standing photo links

A Principal, Claim Source or Repairer contact may have a standing photo link,
managed on the **Photo links** tab of its edit page (R774): the standing
URL, a QR code, Copy, **Pause** and **Resume**, **Replace link**, **Retire
link**, and its defaults, which are the mode a link sent on to a customer
takes, Live camera only, the lifetime and the originals choice. The one
website link, for private customers, is managed by Administrators on
Administration › Photo links
([FRD-17](frd-17-administration-workspace.md#photo-links); operator, 10
November 2026; [R911](../rulings.md)) with the same controls and defaults,
less the mode, because its captures are guided (operator, 9 November 2026;
R797). Its
panel's buttons read **Pause the link** and **Resume the link**, where a
contact's panel keeps **Pause their link** and **Resume their link**
(operator, 10 November 2026; R867); its address box is labelled **Website
address**, and Retire reads "Retiring…" while it works, on either panel
(operator, 10 November 2026; R869). A contact's link whose contact is
inactive shows the amber chip **Contact inactive**, which it shows in place
of Paused when the link is paused too (operator, 10 November 2026; R865).
Each change to a standing link, the website link's included, is an
action-history event
([FRD-04](frd-04-parties-accounts-and-access.md#contacts-administration);
operator, 10 November 2026; R846).

The tab's **Saved addresses** are offered on the firm's form as its
inspection locations: first those taken from the contact, its own address or
a Principal's inspection locations, which stay read-only, then any added on
the tab, the only ones that can be edited or removed (operator, 9 November
2026; R806, R815). A saved address with no name or no address is refused
with "A saved address needs a name and an address." (operator, 10 November
2026; R868). The website link has no contact and keeps none.

A standing link is `/s/{slug}`. A contact's slug is 12 Crockford base32
characters. The website's is the readable one the Administrator types: 6 to
60 lowercase letters, digits and hyphens, not starting or ending with a
hyphen, and held by no other link, retired ones included; Pegasus never
makes one up (operator, 9 November 2026; R797). An address of the wrong
shape is refused with "A website address is 6 to 60 lower-case letters,
numbers and hyphens, and cannot start or end with a hyphen.", and one
already held with "That website address is already in use.", each beside
its form (operator, 10 November 2026; R868). A slug is not a secret: it
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

On **Standing links**, a capture's Standing link names the contact and how
the photos came: **Their staff took them**, **Sent on to their customer**,
or **Customer, from our website** (R948).

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
3. the number plate, its step labelled **Number plate** (operator,
   10 November 2026; R866), the VIN, and the dashboard with the mileage;
4. additional information: the accident scene, the other vehicle,
   paperwork, a walk-around video, and a note. The other vehicle has a row
   of its own, **Other vehicle photos** ("The other vehicle involved"),
   after **Accident scene photos** ("Where it happened") (operator,
   9 November 2026; R800, R805). The review page heads them the same way,
   **Accident scene photos** and **Other vehicle photos** (operator,
   10 November 2026; R866).

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
(9 November 2026; R808), and not once the firm has handed the link on to
its customer. A bodyshop's staff are asked for "A clear shot of the VIN
plate or chassis number.", and a customer sees the VIN as optional
(operator, 10 November 2026; R874, R808). Extras, additional information
and the note are never required.
**Something missing** is derived from this list. After Send, with items
still missing, the app asks **Could you add a few more?**, lists them, and
offers **Add more** and **That’s everything** (operator, 9 November 2026;
R787). On the damage step the same question appears only when a damaged
area has fewer than 3 photos, naming that area (operator, 10 November
2026; R875).

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
  Box holding. When the capture settles, Pegasus matches again with the
  link's Principal and the typed accident date: one eligible Case takes the
  batch straight onto it, each receipt filed there as the routing's system
  work, with no Vehicle images record; none or several form the batch's
  photographs into one submission group carrying the typed registration and
  accident date, which registers as a Vehicle images record (Awaiting
  instruction, its own Box folder, and staff choosing on the record's page
  when several Cases match). The record pairs when the instruction arrives,
  by FRD-19's rules as they stand, without the link's Principal, and never
  with a Case whose incident date contradicts the typed one
  ([FRD-19](frd-19-image-led-intake-and-pairing.md),
  [FRD-22](frd-22-pre-case-gates-matching-and-association.md#matching-conflicts-and-reversible-association);
  operator, 9 November 2026, R791). Paperwork and video are not members of
  the group: they are kept with the capture and file as documents of type
  Other when its record reaches a Case (R803). There is no check-the-match
  state (R776).

A batch **settles** once it holds a photo and the sender has pressed
**Send**, or the link has expired, or staff cancelled it: a cancelled
link's photos still settle as normal (operator, 9 November 2026; R794). A
Send with no photos settles nothing, but it still counts: a Send that
carried only paperwork lets photos that arrive later settle without a
second Send (operator, 10 November 2026; R851). The Worker looks for
batches to settle every minute while photo capture is on, so a Case moves
within about a minute of Send once every photo has filed (operator,
10 November 2026; R838). A batch on a Case settles when every
photo in it has filed in Box (R792). A photo whose filing has ended for good
without filing no longer holds its batch open: its work failed before its
filing started, its link was withdrawn or moved to another Case, its
filing's replay gave up, or Pegasus linked it and the replay window closed
with its filing never started. A filing that has started holds the batch
until it files or its replay gives up, however old its link, and a photo a
member of staff linked whose filing never starts keeps its batch waiting.
The batch then settles on the photos that did file (operator, 10 November
2026; R840). When at least
one photo of the batch filed on the Case:

- the Case's images complete (R781;
  [FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review)),
  never on the first photo, and once per capture: only for a batch with
  photos (R792; staff cannot mark images incomplete, R996); the Case's
  history gives the reason "The photo link's photos are in: the sender
  pressed Send, or the link ran out or was cancelled with photos on it."
  (operator, 10 November 2026; R871);
- the sender's note is written into the Case's Notes as
  "Customer's note (photo link): …", once; a changed note is appended
  (R792), a note sent while the batch settles included. A Triage Case has
  no Case Notes, so there the note is added to the Triage Case's own notes
  ([FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence)) with
  the same words, a longer note as up to four notes of 500 characters each,
  in order, and nothing on a completed or cancelled Triage Case; the whole
  note always stays on the link's page (operator, 10 November 2026; R845);
- empty Case fields fill from the typed details
  ([Case field filling](#case-field-filling));
- the Case Engineer's bell rings, **Photos received**, once for each batch
  that brought photos (R792). The bell is owed in the same step that
  settles the batch and is cleared once it has rung, so a settle stopped
  in between rings it on its next run: it rings at least once, and twice if
  the Worker stops between ringing it and recording that it rang (operator,
  10 November 2026; R851). A Case with no Engineer has nobody to ring, so
  no bell rings and an Engineer assigned later is not told; the Case's
  history shows the photos (operator, 10 November 2026; R850).

A batch with no photo filed on the Case, and a batch whose Case is
archived, settles with none of these: the photos already filed stay
(R793). A note can still land on a Case archived in the same moment the
settle runs (operator, 10 November 2026; R851). Each batch of a capture
that settled with no Case finishes on its own when its Vehicle images record merges into a Case: the note is written,
the fields fill, that batch's own kept paperwork and video file as Other,
and the Engineer's bell rings once (operator, 9 November 2026; R803,
R804); the record's merge completes the images
([FRD-19](frd-19-image-led-intake-and-pairing.md#pairing-and-merge)) only
when the capture has not completed a Case's images before: a later record
of the same capture does not complete them again (operator, 10 November
2026; R841). A
record is taken to reach its Case only by its merge, not by the pairing's
links before it. The kept files file on whatever Case the record reached, a
Triage Case or a report-sent Case included, as a Case link's photos do
(R790): a member of staff's choice on the record's page files them as that
member of staff, with the reason their link carries, and Pegasus's own
pairing files them as following the record's merge (operator, 10 November
2026; R844). The batch's reach is recorded once each kept file has filed or
its filing has ended.
Photos added after a settle form the next batch, matched again when it
settles; with no Case, it becomes a further Vehicle images record for the
same registration. A photo still uploading when a link is cancelled can
form such a batch, which rings a second bell (operator, 10 November 2026;
R851). A full-size original files as the next version of the same
Case image: the image keeps its tile, tags, report place and crop, Box shows
the compressed picture as the earlier version of the one file (R664, R839), and
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
item between the baskets is the In report toggle. In the report a link's
photos that nobody has ordered follow the story
([FRD-06](frd-06-vehicle-and-engineering-evidence.md#ordinary-image-vrm-and-image-analysis);
operator, 10 November 2026; R854). Paperwork and video file
as documents of type Other, decided by the item's kind: paperwork sent as a
JPEG or PNG is Other too (operator, 9 November 2026; R803).

On the Case, Files → Images shows a **What they told us** card and the note,
then the photos in story groups
([FRD-16](frd-16-case-record-workspace.md#files)).

### Statuses and reminders

A capture's status is derived each time it is read, never stored. Its
Photos count, on both tabs and the link's page, counts photographs, not
paperwork or video:

| Status | When |
| --- | --- |
| Sent, not opened | The link has not been opened (operator, 10 November 2026; R876) |
| Opened | Opened, no photo yet |
| Photos coming in | Photos received, not yet sent; paperwork or video alone is not photos (operator, 10 November 2026; R877) |
| Something missing | Sent, or expired with photos, and a required item is missing |
| Needs a phone call | Two reminders and nothing back since |
| Complete | Sent, or expired with photos, and nothing required is missing |
| Expired | Expired with no photos, or its Case is archived (operator, 9 November 2026; R793) |
| Cancelled | Cancelled by staff |

A reminder is due, and shown, when a link is not opened after 24 hours and
again after 48; when it was started but not finished after 4 quiet hours;
when something is missing once it was sent; and when it is about to expire,
within one day. Pegasus never sends a reminder (R773; MAIL-19 stands):
**Direct links** shows what is due, and a member of staff sends the link
again with **Resend**. Resend and the former Remind sent the same message, so
they are one act: a Case link's Resend counts as a reminder only when one was
due (operator, 10 November 2026; [R914](../rulings.md)). No button sends an
e-mail on its own: Resend opens the composer, the Send chaser's, with From,
To, Subject and Message in the standard wording, and nothing is sent until
**Send by e-mail**; **Create link to copy** makes the link without sending
(operator, 10 November 2026: "any email sending always needs a dialogue box -
a button cannot just send an email on its own"; [R915](../rulings.md)).
**Log call** records a phone call. Only a link sent from a Case is sent
again: **Ask for it**, which made a fresh link for a capture a standing or
website link started (R806, R816, R842), is retired (operator, 11 November
2026; R948).

### The photo link e-mail

The photo link e-mail is a staff send from an Outgoing approved mailbox
through the Graph staff send
([FRD-21](frd-21-outbound-correspondence-and-sent-evidence.md#outbound-correspondence)),
with the template purpose **Photo link**
([FRD-17](frd-17-administration-workspace.md#e-mail-templates)). Its
placeholders are `{registration}`, `{claimant}`, `{photo link}`,
`{link expiry}` and `{principal name}`. `{claimant}` is the first word of
the customer's name, passing over a first word of Mr, Mrs, Miss, Ms, Mx or
Dr: "Mr Leroy Ejiofor" is greeted "Hi Leroy,", and a name that is only a
title and a surname, "Mr Ejiofor", "Hi Mr Ejiofor,"; the hand-on
message greets the customer the same way (operator, 10 November 2026;
R855). Its built-in body:

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

### The photo link text

Staff may text a photo link as well as e-mail it (operator, 10 November
2026; R918). Pegasus sends the text through Azure Communication Services
from an SMS-enabled UK mobile number bought on its resource, over the Web's
managed identity; the number is the `TextMessages:From` setting
([configuration](../engineering/configuration.md)), and while it is empty no
page offers **Send by text**. It texts a UK mobile only: 07… with eleven
digits, or the same written +44 7…, 0044 7… or 44 7…, spaces, dashes, dots
and brackets ignored; any other number is refused with "Enter a UK mobile
number to send by text." beside it. Replies are not read.

**Text message** is one of the composer's channels on Send a photo link and
in the Resend composer (R920, R948); no button texts on its own (R915).
On the Send page the text goes to the form's **Mobile**; in the composer, to
the link's mobile, which staff may change there. The message is the
template purpose **Photo link (text)**
([FRD-17](frd-17-administration-workspace.md#e-mail-templates)) with the
Photo link e-mail's placeholders, drawn and edited as the e-mail is
("Edited for this one only." and **Put the standard wording back**; R806,
R919); a text with no `{photo link}` gets the link on its own line, and one
over 1,530 characters (ten SMS segments) or empty is refused with "A text
message is required.". Its built-in body, the agent's draft awaiting the
operator's sign-off (R919):

```text
Hi {claimant},
Collision Engineers need a few photos of {registration} to assess the damage. Please open this link on your phone:
{photo link}
The link works until {link expiry}.
```

The link is issued as for an e-mail, with a token of its own for the text,
and a texted Resend counts as a reminder as an e-mailed one does (R914,
R920). One operation key sends one text: the act is written to the link's
history before the provider is asked, and a repeated post is refused with
"This text was already sent.". The provider's answer is written after:
"Sent by text · name" with its message id when it accepts the text, and
"Text not sent · name" with its failure code when it refuses it or cannot
be reached; the Send page and the composer then show **Text not sent** and
the code, and a sent text returns from the composer with "Link sent to …
by text". Accepted is not delivered: Pegasus does not yet read delivery
reports (R921; issue #2054).

### Case field filling

When a capture settles on a Case, the claimant name, contact number,
accident date and mileage the sender typed fill the Case's empty fields as
Facts with source kind Photo link, shown with the source tag **Photo link**
([FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md#field-provenance-and-value-kinds)).
A value already on the Case is never overwritten (R783), except a DVSA
mileage estimate (below). The registration is
never written: it routed the capture. The typed inspection location is not
written to the Case: it stays on the capture with the photos, and staff
type the address if they want it (operator, 9 November 2026; R811,
narrowing R783). The sender's views that have no Case field (roadworthy,
preferred outcome, in hire, airbags and steering) also stay on the capture
and show with the photos. A typed mileage ranks below anything from the
instruction and above the DVSA estimate, so the estimate never replaces it,
and the report keeps the instructing principal's sentence whoever typed it
(operator, 9 November 2026; R810). It replaces a DVSA estimate already on
the Case, and only that (operator, 10 November 2026; R843;
[FRD-23](frd-23-case-draft-fields-provenance-and-global-checks.md#vehicle-details-and-mileage-evidence)).

### Staff pages

**Photo capture** (`/PhotoCapture`) is a rail item under Work, after Cases,
with no count (R774;
[FRD-12](frd-12-operator-experience.md#shell-and-routes)). It has two tabs,
named for the two kinds of link Collision Engineers runs (operator, 11
November 2026, approving the [v51 round](https://github.com/collisionengineers/pegasus-design-archive/tree/design/v51-photo-capture/v51_planning)
as drawn; [R948](../rulings.md)):

- **Direct links**, the default: the links HQ sent for a Case, with Vehicle
  (and up to three of its first photographs), Case (with its Principal, read
  when the page is shown; R805), Recipient (with the mobile, or **Forwarded to
  customer** once a quick link's firm has handed it on; R798), Status (with
  "Reminder due · …" when a reminder is due, else how many were sent), Photos
  (with what is missing) and Sent. Its scopes, each with its count, are
  **Awaiting** (a live link not yet complete), **Needs chase** (a live link
  with a reminder due, or one that needs a phone call), **Complete**,
  **Closed** (a dead link not complete) and **All**; the tab counts its
  Awaiting links. A row carries only the act its state calls for: **Resend**
  while the link is live and a reminder is due or it is unopened, opened or
  missing something, else **Log call** when it needs a phone call. Cancel
  link and Extend stay on the link's page and the Case. Log call from a row
  returns to the list with "Call logged".
- **Standing links**: the directory of the standing links that are not
  retired, the contacts' by name and the website's last, each with its
  contact's type (the website's reads **Private customers**), Paused or
  Contact inactive where it is, and how many captures it started; beside it
  the submissions, all of them or one link's, with Vehicle, Standing link
  (and how the photos came: **Their staff took them**, **Sent on to their
  customer** or **Customer, from our website**), Customer, Destination,
  Photos and Received. Any member of staff may copy a link or show its QR
  code and download it; an Administrator also has **Defaults**, to
  Administration › Photo links, and on a chosen link **Manage**, to its
  contact's Photo links tab or, for the website's, to Photo links. The tab
  counts the standing links.

A capture a standing or website link started is never sent again: **Ask for
it** is retired. It reads **Uploading**, with its photos so far, until its
photos settle, and then its photo count, and its status is not graded
against the guided checklist. **Match to a case**, **No case yet**,
**Matched automatically** and **Matched by staff** are retired too: a
submission's Destination is its Case, or the Vehicle images record Pegasus
made for it, **Awaiting instruction**, which pairs as any Vehicle images
record does ([FRD-19](frd-19-image-led-intake-and-pairing.md)) (R948). A row
is one line: a long name, firm or missing item ends in an ellipsis with the
full text on hover, and narrower windows drop columns rather than scroll:
from 1460px Sent and Received, from 1300px the Principal, the Customer and
the photographs, and from 1180px Recipient, Standing link and the missing
items, every one of them still on the link's page (R912, R948).

`/PhotoCapture/Send/{caseId}` sends a link ([Photo links](#photo-links)).
`/PhotoCapture/{id}` is a link's detail (operator, 11 November 2026; v51
A to G; [R948](../rulings.md)). Its eyebrow names its tab and its status
stands beside the registration: a Case link's state, or for a standing or
website capture Uploading, then Received. Its one primary act is Resend when
a reminder is due, Log call when the link needs a phone call, else Resend
while it is live, on a Case link only; then **Open case**, to the Case's
Files, or **Open Vehicle images**; and More with the rest, Extend by 3, 7 or
14 days among them, Cancel link last (R913). Its main column is **Photos**:
on a Case link with something missing one Missing line, filters All,
Overview, Damage and Vehicle ID, and the photographs as tiles that open in
the viewer, each named once, a repeated name numbered, with paperwork and
video as files below. Its 320px aside is one **Summary**: for a Case link
Case, Recipient, Principal, Sent by, Accident, Expires and Chase; for a
standing capture Destination, Standing link, Customer, Accident and
Received; then what the sender told us, the note first and the answers in
short labels; then the folded **History**, which names who acted:
"Cancelled · name", "Link extended · name", "Call logged · name" (operator,
9 November 2026; R805), "Reminded · name", one line for each reminder
(operator, 10 November 2026; R870), and "Sent by text · name" or "Text not
sent · name" (R921). Resend on an
expired link is refused with "The link has expired; extend it first.", and
on a cancelled link with "The link was cancelled; send a new one from the
Case." (operator, 10 November 2026; R863). A live link on a Case also offers
**Cancel link** in the More of its bar in the Case's Files → Images
([FRD-16](frd-16-case-record-workspace.md)). Cancel is the same act wherever
it is offered, with the same refusal, and a repeated click cancels once
(operator, 9 November 2026; R795). Paperwork and video a capture with no
Case received stay kept and unfiled, and each opens from the link's page as
any received file opens; nothing new is filed, chased or deleted, and a
capture that sent only paperwork stays on Standing links (operator, 9 November
2026; R807). The Vehicle images record page, headed **Vehicle images**,
shows its Images first beside a Summary with the source **Photo link**, the
details and the note, on the record of every batch the capture made, not
only its latest, and lists under Retained files only what the Images do not
show (R948); the Case shows the capture's photos once any of those records
has reached it.

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
in Administration › Photo links (R911); Extend and a contact's link defaults keep
all three (operator, 9 November 2026; R813, R817).

## States and transitions

A photo link is live from issue until it expires, is cancelled, or its Case
is archived (operator, 9 November 2026; R793). **Extend**
adds 3, 7 or 14 days to its expiry, from the later of now and the expiry;
**Cancel** revokes every token and stops uploads only: the photos already
received still settle (operator, 9 November 2026; R794). A standing link is active, paused,
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
  still waiting to go is held only in the open page: a reload or a closed
  tab loses it and the sender retakes it, while photos already sent are
  safe (operator, 10 November 2026; R837). A photo
  Pegasus refuses is outlined, does not hold Send back, and the review counts
  "{n} of {m} uploaded" (9 November 2026; R786).
- **A file the phone cannot read.** A file is read from the phone into
  memory a request at a time, 4 MiB at most, as it is sent; a video is never
  read whole. When the phone cannot read a file the sender picked, the app
  says "We couldn’t read that file from your phone. Please add it again.",
  the file is not added and is not tried again, its photo or row is outlined
  like a refused one, and Send is not held back. A full-size original the
  phone cannot read is let go without a word and its photo stands (operator,
  12 November 2026; R983; #2097).
- **Caps.** A request past a rate limit, a size, a per-link count or a
  standing link's daily cap is refused; nothing past the cap is kept. A
  file the app itself refuses for its size is not sent, and the app says
  "The file is larger than this link accepts." (operator, 10 November 2026;
  R878).
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
  batch, matched again when it settles; with no Case they make a further
  Vehicle images record for the same registration (R791).
- **Upload in flight.** A batch with an item still on its way, received in
  the last five minutes with no receipt yet, waits. An older one whose file
  reached intake is counted into the batch as it settles, so a retry finds
  it there; one that never reached intake joins the next batch on its retry
  (10 November 2026; R925).
- **Registration.** Once a photo has filed against a capture's
  registration, the registration is locked: a different one would be a
  different vehicle. Until its first photo a standing capture is routed
  again each time its details are recorded, so a registration or accident
  date changed before then is routed on; once it holds an item its routing
  stands. A link sent from a Case keeps its Case's registration, which it
  shows read-only ([Photo links](#photo-links)), and a typed one is ignored.
- **Feature off.** With `Features:PhotoCapture` off, `/c`, `/s`, `/capture`
  and `/PhotoCapture` answer 404, as does Administration › Photo links
  before sign-in, and the rail item, Send photo link and the Photo links tab
  are absent
  ([configuration](../engineering/configuration.md#photo-capture)).
- **A staff form posted again.** A Send or Resend posted again
  under the same operation key (a refresh, or Back and send) issues, sends
  and texts nothing more. The link was shown to the first post alone, as
  only its digest is kept ([ADR-0090](../adr/0090-public-photo-capture-surface.md)):
  a repeated text is refused with "This text was already sent.", and any
  other repeat opens the link's page, or returns the composer to where it
  was opened from (10 November 2026; R925).
- **A fault while settling.** A fault while a batch fills its Case's fields
  leaves the batch unsettled, and the next settle tries it again; the
  customer's typed values are not dropped (10 November 2026; R925).

## Acceptance evidence

Core tests in `tests/Pegasus.Core.Tests/PhotoCapture/` cover the token and
slug shapes, the damage areas, the required items, the derived statuses, due
reminders and pairing, the story order and names, the intake rules,
the Case fields a capture offers, which tokens record Opened, receiving an
item, recording the details and routing, minting from a standing link, a
quick link's Type, the hand-on of a quick Case link, standing link
administration (retiring, the typed website address, saved addresses and an
inactive contact) and the settle, including the Triage note and the
mileage that replaces an estimate; the
greeting's name, a VIN not required once a link is handed on, a closed
Case's refusal, Resend's two refusals and when a Resend counts as a reminder
are covered there
too. Send's two settings are covered with the
workflow configuration in `tests/Pegasus.Core.Tests/Identity/AdministrationPolicyTests.cs`.
`tests/Pegasus.Core.Tests/Documents/CaseAssetPreparationTests.cs` covers the
story order of a link's unordered report images and
`tests/Pegasus.Core.Tests/Intake/CaseFilingTargetRuleTests.cs` the kept
files that follow a record's merge.
`tests/Pegasus.ArchitectureTests/PendingWorkRecoveryFunctionTests.cs` covers
the settle on every minute, off with the feature and stopped at its budget.
Integration tests hold the rest:

- `PhotoCaptureSecurityTests`: the one unavailable answer, byte for byte,
  for an unknown, expired, revoked, paused, archived, retired or
  inactive-contact link, with the capture headers and no cookie; a dead
  link's oversized upload; the page rate limit; and the feature switch;
- `PhotoCaptureWebTests`: a Case link's photos filing on the Case (story
  name, baskets, the issuer's decision, a report-sent Case, images not
  completed by the first photo, the loose damage photo, originals and their
  bytes, the ignored typed registration, one link's token not reaching
  another's items), an archived Case's dead link, and the staff pages (the
  Principal, Resend through the composer and no Cancel on Direct links rows,
  who acted, Retire, the website link's typed address and defaults on
  Administration › Photo links, the tab counts, Standing links' submissions
  with no Ask for it or Match to a case, the Case page's status and Resend, a
  kept file opening, a closed Case's Send page, the refusal sentences beside
  their forms, Contact inactive, Reminded · name, and a standing link's
  changes written to the action history);
- `WorkflowConfigurationWebTests`: the Photo links settings' refusals;
- `StandingCaptureWebTests`: a standing capture opened as it starts, waiting
  with no Case and never Unidentified, taking paperwork with no Case, routed
  until its first photo, taking no originals with no Case, filing on the one
  Case its registration matches, the hand-off's session token, an item
  joining the open batch, a later record of the same capture not completing
  the images again, and kept paperwork filing on the Case its record
  reached, a Triage Case and a report-sent Case included.

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
- Rulings: R773 to R817, R836 to R878 and R983 to R985 in [rulings](../rulings.md).
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

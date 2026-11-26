# FRD-04: Parties, accounts, and access

> Owner capabilities: ACC-01 to ACC-05, ACC-07 to ACC-11, ACC-15, RPT-08 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Contacts are external organisations. Each has exactly one type, chosen
  when it is added and never changed: Principal, Claim Source or Repairer.
  Staff accounts are separate.
- Each Case keeps its own copy of the parties it was accepted with. Editing
  the Contacts directory later never changes that copy.
- Every staff account has exactly one role: Administrator, Engineer or User.
  Every enabled staff role may perform ordinary casework; Administrator alone
  manages the application.
- Administrators manage accounts from one Settings dialog. No account action
  asks for a reason, and none asks for confirmation except Delete, which
  confirms in the shared dialog (operator, 15 October 2026,
  [v41](https://github.com/collisionengineers/pegasus-design-archive/blob/main/v41_planning/notes/plan-2026-10-15.md)).
- Every change is written to permanent action history with who, when and
  the before and after values.

## Purpose

This document says who the parties on a Case are, how the Contacts
directory works, what each staff role may do, how staff accounts are managed,
and what goes into permanent history. Case identity is in
[FRD-01](frd-01-case-identity-and-lifecycle.md). Edit leases are in
[FRD-14](frd-14-record-edit-leases.md). The Administration screens are in
[FRD-17](frd-17-administration-workspace.md#administration).

## Behaviour

### Parties, principals, organisations, accounts, and access

A Principal is the customer role that instructs and pays Collision
Engineers. Every Contact has one type: Principal, Claim Source or Repairer
(operator, 13 October 2026; the Storage and Third Party Engineer types
removed, operator, 13 November 2026; [R1049](../rulings.md),
[R1050](../rulings.md)). A
Principal never links to another Principal. A Contact of any other type may
link to several Principals; the links are shown on the Contacts list and the
Contact's page. A Case's Claim Source list and its Repairer list draw the
contacts linked to the Case's Principal first, under that Principal's code,
then the others under "Other claim sources" or "Other repairers", each part
in name order. Nothing is hidden: a Principal with no linked contact, and a
Case that names no Principal, keep the one list (operator, 12 November 2026;
[R1013](../rulings.md), [R1015](../rulings.md), [R1016](../rulings.md),
replacing "they do not narrow a Case's Claim Source or Repairer choices",
operator, 13 October 2026). A Claim Source's links also make it an
intermediary for those Principals' mail (operator, 15 October 2026;
[FRD-09](frd-09-principal-and-intermediary-routes.md#accepted-principal-email-routes-and-automatic-association)).
Nothing read a Storage or Third Party Engineer Contact, so neither type is
kept: the Third Party Engineer contacts were removed, and each Storage contact
that held an address and a postcode became an inspection location with the
same name, address, postcode, active state and Principal links, which a Case
of a linked Principal chooses under Inspect at (operator, 13 November 2026;
[R1049](../rulings.md), [R1050](../rulings.md), [R1051](../rulings.md);
[FRD-06](frd-06-vehicle-and-engineering-evidence.md#inspection-address)).
Staff accounts are not Contacts.

An intermediary is a Claim Source Contact. An organisation that is also a
Principal has two Contacts, its Principal Contact and a separate Claim Source
Contact: Sixways, ACSP and CS each have both. Contacts loaded from the
reviewed register are linked to Principals on cited evidence, and a link may
name an inactive Principal (operator, 15 and 16 October 2026;
[ADR-0076](../adr/0076-reviewed-register-is-the-contact-directory-data-contract.md)).

A repairer, broker, agent, client, legal representative, provider, vehicle
keeper or other contact may play different roles on different Cases. The
reusable directory record is separate from the inspection address and party
roles each Case keeps. Raw provider or contact workbooks are evidence, not
import authority; the reviewed register is the accepted data contract
(operator, 15 October 2026;
[ADR-0076](../adr/0076-reviewed-register-is-the-contact-directory-data-contract.md)).

The Repairer directory records each repairer's name, full address and
contacts. A Repairer may relate to several Principals and a Principal to
several Repairers. These directory links never rewrite the address or
party-role copy an existing Case already holds.

A Case records its own repairer: the name and address the instruction gave,
kept as ordinary Case facts with their extraction provenance. A Case made
from a received item is also linked to the active Repairer-role Contact whose
name is the one its instruction gives, ignoring capitals: exactly one such
Contact, otherwise none, and a name that is not a Contact never creates one.
The link copies the Contact's identity, version and name, and the address
stays the instruction's. It is recorded as a choice on the Case is, so staff
change or clear it; Cases made before the change are not linked (operator, 12
November 2026; [R1027](../rulings.md), as [R1017](../rulings.md) and
[R1018](../rulings.md) for a Claim Source). When the instruction's name links
none, the Case is linked to the one active Repairer-role Contact linked to the
Case's Principal that the instruction's text (its subject, body and the text
of its attachments) names, capitals and spaces ignored ("Car Claims" names
"CarClaims"); two or more such Contacts named, or none, link nothing, on new
Cases only (operator, 25 November 2026; [R1264](../rulings.md)). Staff
otherwise either confirm that text or link the Case to an active Repairer-role Contact, chosen
from a list that draws the Principal's linked Repairers first (R1015). Linking
copies that Contact's identity, version, name and address onto the Case, so
a later directory edit never changes the Case. A save keeps the link the Case
holds as it is, whether or not the Contact is still active, and a name or
address typed while it is linked is saved as the Case's own (operator,
12 November 2026, [R1022](../rulings.md)). The confirmed repairer address
is one of the Inspect-at options beside the claimant address, the storage
location and the Principal's own default. The option names the repairer it
came from.

### Staff role access matrix

Staff sign in with Pegasus-managed usernames and passwords. Passwords are
stored as non-reversible hashes. This stays the sign-in route until a
separately accepted identity change replaces it. Each account has exactly one
current role.

**Sign-in from Pegasus Desktop** (operator, 24 and 26 November 2026; R1237,
R1297 to R1299;
[ADR-0092](../adr/0092-staff-sign-in-for-pegasus-desktop.md)). A member of
staff may also sign in to Pegasus from Pegasus Desktop, the Windows
application, with the same username and password. The application opens the
browser; the member of staff signs in to Pegasus there, and a page headed
"Sign in to Pegasus Desktop" names their account ("Signed in as"), says
"Pegasus Desktop will act in Pegasus as you. Everything it does is recorded
under your name.", and offers **Sign in** and **Refuse**. The application
never sees the password. Any enabled account may sign in, of any role; an
account that must change its password changes it first. Either answer is a
permanent history event under the person's name, which the Action logs read
as "Signed in from Pegasus Desktop" and "Refused sign-in from Pegasus
Desktop" (R1304). The sign-in is offered only
where a deployment configures it: production does not, and there is no row
for Pegasus Desktop on the Integrations page. A sign-in lasts 8 hours and is
not extended by use. It ends sooner when the account is disabled, its
password is reset, its role changes or an Administrator forces a logout, and
when the application signs out; stopping the Automation client does not end
it (R1303). Signed in, the member of staff may do through
the application what they could do in Pegasus itself, recorded under their
name (R1300); that is owned by
[FRD-10](frd-10-mcp-automation-and-actor-boundary.md#staff-through-pegasus-desktop).

All three stored staff roles have the same ordinary casework authority.
Administrator additionally holds application-management authority; an
Administrator therefore holds every Engineer ability as well as the
administrator-only ones, which is intended. Signing a report still depends on
the account's sign-off flag and signature, not on role. Every
account must still be enabled and meet any signature, workflow, lease or
state prerequisite for the action.

| Staff role | May view | May create or change | Must not access or perform |
| --- | --- | --- | --- |
| `Administrator` | All authorised application data and settings | Every ordinary Intake, Triage, Case, document, evidence, task and transition action; staff account create, disable, delete access, force logout, role assignment and password reset; sign-off settings; Principals and successor cutover, including a Principal's Principal API credential lifecycle; workflow configuration, including labour-rate cards; e-mail templates; the approved-mailbox allowlist; OAuth-client registration and revocation; the integrations' logins (Glass's valuation, Cazana, Brego and the two Experian accounts), replaced and never shown back (operator, 26 November 2026; R1288) | Pegasus's own cloud or release administration, or any credential secret other than the integrations' logins, through the staff UI; permanent deletion; a generic mailbox-rule editor before its policy is accepted |
| `Engineer` | Cases, inbox items, documents, evidence and details | Every authorised Intake, Triage, Case, document, evidence, task and transition action, including assignment, valuation, findings, estimates, reports and use of the account's own configured Glass's credential | Application management |
| `User` | Cases, inbox items, documents, evidence and details | Every authorised Intake, Triage, Case, document, evidence, task and transition action, including assignment, valuation, findings, estimates, reports and use of the account's own configured Glass's credential | Application management |

Andrew and Alex are the initial Administrators. That assignment is
application data. No person, name, email address or bypass is built into
authorisation. Automated processing uses its own durable machine identity and
only named Core actions. It is not a staff account and sets no policy of its
own.

Core use cases and every caller boundary enforce authorisation. A refusal
reveals no Case or source data. The rules on immutable Principal and
reference, sources, associations, history and Case edit authority apply to
Administrators too. Development routes and data never give production
access.

### Contacts administration

**Contacts** lists the external organisations in one panel per type, in the
order Principals, Claim Sources, Repairers,
each sorted by name; a type with no matching contact is absent. Search, labelled
Name (operator, 29 October 2026, R535), matches the start of a contact's name
and, for a Principal, the start of any code it holds, an earlier code included
(operator, 18 November 2026, [R1190](../rulings.md)); it runs across every
panel, and the Type filter narrows the list to one panel. Each
panel shows 50 contacts at a time; where a type has more, Previous and Next
open that type's list one page at a time. A panel's columns follow its type:
Principals show Principal code, name,
email, phone, last Case and state; Claim Sources show name, email, phone,
linked Principals (their codes), last Case and state; Repairers show postcode
before linked Principals (operator, 13 October 2026). A Claim Source's or
Repairer's last Case is the latest Case that names it. A contact has an
organisation name, email, phone, address, postcode and active state; it holds
no contact person (operator, 13 November 2026; [R1047](../rulings.md)). Phone accepts digits, spaces and one optional leading `+` only. UK
numbers are written with a leading `0` and internal spaces. Letters and other
characters are rejected in the field and on the server.

**Add contact** first asks for the type: Principal, Claim Source or
Repairer. It then creates a new Contact of that type.
Matching names are suggestions only, each opening the existing Contact.
Check existing contacts checks any name of one character or more; when none
matches, the dialog says No contacts found. (operator, 29 October 2026, R537).
Pegasus never merges records by itself, and a Contact's type never changes.

Principal policy lives on its Principal Contact. A Principal code is created
in the same transaction as that Contact and stays with the same
Contact through a code replacement
([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).
That Contact is where a Principal exists: every route that allocates a Case
requires its active Principal row and fails closed without one. The standing
**Private customer** Contact, code `PRIVATE`, is that row for one-off Cases:
created once by migration, it holds the default fee and report sending rules
private work uses, its Cases are referenced by registration
([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)),
and a successor code keeps that (operator, 31 October 2026; R574). Which
Principals have an accepted direct e-mail route, and how each one's
instruction is read, is released code
([FRD-09](frd-09-principal-and-intermediary-routes.md#principal-and-intermediary-routes)),
not directory data; a Principal with no route is a normal state whose
instructions arrive by upload, Triage or the Principal API (operator, 13
October 2026). Recognition of documents and of direct mail routes stays in
code; the directory records who a party is, and a linked Claim Source is an
intermediary (operator, 15 October 2026).
Names are unique within a type: a second Contact of the same type and name,
or a duplicate Principal code, fails without leaving an orphan identity. The screen's visuals and controls are owned by the
[design README](../design/README.md); its layout is in
[FRD-17](frd-17-administration-workspace.md#contacts).

The Principal section of a Contact carries:

- the accepted e-mail identities, read-only, read from the Principal route
  policy in
  [FRD-09](frd-09-principal-and-intermediary-routes.md#principal-and-intermediary-routes);
  a Principal with none shows "No accepted e-mail route. Instructions arrive
  by upload, Triage or the Principal API." in the same box (operator, 13
  October 2026);
- the inspection setting, saved with the contact by Save contact (operator,
  25 November 2026; [R1266](../rulings.md)): Inspection mode, Physical
  address or Image Based Assessment, which fills a new Case
  ([FRD-06](frd-06-vehicle-and-engineering-evidence.md#inspection-address)),
  and for Physical address only an optional default inspection location
  (location label, address and postcode), which the Photo links address list
  reads. A change of mode applies to Cases made afterwards (amending R118).
  There is no separate Image Based Assessment tick, so the two cannot
  disagree;
- the report recipients
  ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md)): who a
  report goes to is set by the report sending rules below; the separate
  Report recipients settings are gone (operator, 6 October 2026). The Claim
  Source is never copied in by default;
- the default fee (operator, 5 October 2026), saved with the contact by
  Save contact (operator, 25 November 2026; [R1266](../rulings.md)). Every
  Principal has one: more than £0 with two decimal places at
  most, the agreed fee's own rule. Every Principal was seeded at £180.00, a
  new Principal starts at £180.00, and a successor code inherits its
  predecessor's. A new Case starts with its Principal's default fee as its
  agreed fee
  ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-readiness));
- the fee note VAT number (operator, 12 November 2026; R1006, R1007), saved
  with the report settings: the VAT number this Principal's fee notes print
  after "VAT No: ". It is any text of 20 characters at most, printed as
  typed; nothing checks it is a real VAT number. Empty is the standard
  number, 262 0937 10, which every Principal starts with. A successor code
  inherits its predecessor's
  ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#companion-documents-and-what-a-delivery-attaches));
- the Audit workflow (operator, 17 November 2026; [R1138](../rulings.md)),
  saved with the report settings: Original engineer report, which every
  Principal starts with, or Garage authorisation & invoice. A standalone Audit
  takes its Principal's workflow when it is created and keeps it, so changing
  the setting changes no Case that already exists. A successor code inherits
  its predecessor's
  ([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity));
- the Principal's own invoice (operator, 17 November 2026;
  [R1147](../rulings.md), [R1148](../rulings.md), [R1162](../rulings.md)),
  saved with the report settings: None, which every Principal starts with, or
  ULA Invoice, the invoice Generate report also makes in the Principal's own
  name. With it go an Invoice VAT number of 20 characters at most and Invoice
  payment terms of 100 at most, each printed as typed and left off the
  invoice when empty. Generate report reads the setting and the two values
  when it freezes the report, and makes the ULA Invoice from them
  ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#companion-documents-and-what-a-delivery-attaches)).
  A successor code inherits its predecessor's. United
  Legal Assistance Limited, code `ULA`, is the one Principal seeded on the
  Garage authorisation & invoice workflow with the ULA Invoice
  ([R1145](../rulings.md), [R1152](../rulings.md)), its report sending rules
  the defaults with Attach External estimates (offered, unticked) and Attach
  Repair invoices (offered, unticked) On every Case ([R1151](../rulings.md));
- the report names (operator, 6 November 2026; R740), saved with the report
  settings: what an Inspection report and an Audit report are called, each
  for a first send and for a re-send, in text with the tokens `{REG}`,
  `{CASE REF}` and `{OUTCOME}` in any letter case. One name covers the filed
  copy and the attachment
  ([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#companion-documents-and-what-a-delivery-attaches)).
  An empty name is the standard name. A name with another token, or one
  whose longest result could not be filed (120 characters), is refused. A
  successor code inherits its predecessor's;
- the report sending rules (operator, 5 and 6 October 2026; rebuilt 25
  November 2026, [R1267](../rulings.md) to [R1270](../rulings.md) and
  [R1272](../rulings.md)), from the Report Sending SOP v8 (15 October 2026),
  and Send from. Every Principal has them. They say where a report goes,
  from where, who is copied, what goes with it, what holds or stops it and
  which Case tasks follow it:
  - Send from, on the Contact tab: the Outgoing mailbox the send form chooses
    to start with for a new message, when there is no instruction e-mail to
    reply from; staff may change it. A reply always leaves from the mailbox
    the instruction arrived in, or for a staff forward held in instructions@
    the mailbox that forwarded it (operator, 3 November 2026; R651, R652);
  - **On every Case** is a list of actions every send takes. **Rules** are
    numbered, and each reads "If … Then …". A rule's If is one or more groups
    joined by or, each group one or more conditions joined by and: "if X is
    not true, and Y is true, or Z is true" is two groups, (not X and Y) or
    (Z). A condition is a fact, "is" or "is not", and values; several values
    of one condition are alternatives. A rule's Then is one or more actions;
  - the facts are the Case's Claim Source or Repairer (a Contact of that
    type), its recorded Outcome, its Case type (Inspection, Audit or
    Inspection and audit; operator, 13 October 2026), the instruction's
    Sender, the report's Reference prefix (`a.PCH26118` has the prefix `a`)
    and Previous job, whether another Case of the same Principal with the
    same vehicle registration already has a sent report (operator, 16
    October 2026). There is no "Instruction mentions": the instruction's
    text fills the Case's Claim Source or Repairer when the Case is made
    ([R1075](../rulings.md), [R1264](../rulings.md)), and a rule reads that
    contact. "Images from" and "Bodyshop mentions" are retired (R1025,
    R1026). Every condition reads a fact the Case holds: "is" does not hold
    while the Case has no value for the fact, and "is not" does; the
    delivery form asks staff nothing (operator, 12 November 2026; R1023,
    R1024; 25 November 2026, [R1269](../rulings.md));
  - the actions are Send to, fixed addresses that replace the original
    instruction sender; Send to only, those addresses and no Cc at all; Reply
    all, which keeps the instruction's own Cc addresses; Cc, always copied;
    Never cc, always removed, even from the instruction's Cc list; Attach and
    Do not attach, each naming a file: Separate fee note, Estimate (the
    Case's filed estimate), Audatex, Vehicle images document, Figure
    breakdown, External estimates (offered, unticked) or Repair invoices
    (offered, unticked) (operator, 17 November 2026; [R1151](../rulings.md));
    Hold, text staff tick as done before Send; Stop, which refuses Send
    unless staff with casework override it with a reason; and Add a Case
    task, a task created when the report is recorded as sent. Nothing
    happens on a send unless an action says so: a Principal that keeps the
    instruction's Cc has Reply all On every Case, and one that attaches the
    separate fee note has Attach Separate fee note. Add a Case task replaces
    Reminders, a rule's Remind, Rate card and Garage figures; garage figures
    is the rule "If Outcome is Repairable, then Add a Case task 'Send the
    figures to the garage.'" ([R1270](../rulings.md)). No estimate is
    checked against a card (operator, 15 and 16 October 2026);
  - On every Case applies first, then each applying rule in order, and rules
    are moved up and down to set it. Addresses and tasks add up; a removal
    wins over an addition; where two disagree on a file or on Send to, the
    later wins; the first applying Stop is the one staff see. Whether the
    report carries vehicle images is a Report generation setting, and what
    the report is called is the report names above; neither is a sending
    rule (R740; [R1272](../rulings.md) item F).

  No text in the rules has a length limit. A refused save keeps the rule's
  dialog open with the problem and what was typed. A Principal the SOP does
  not name, and every Principal created later, starts with Reply all and
  Attach Separate fee note On every Case and no rule. A successor code
  inherits its predecessor's rules. The migration `SendingRulesAreBuilt`
  rewrote every Principal's rules to this shape (25 November 2026,
  [R1267](../rulings.md)); RJS's two "Instruction mentions" rules became
  Claim Source is Claim Specialists and Claim Source is Accident
  Specialists, and its "Luton" alternative waits on issue #2204
  ([R1265](../rulings.md), [R1268](../rulings.md)). The rules were first
  imported from the SOP (the supplied
  [report_sending_sop.v5.yaml](../principal-profiles/sop-guides/report_sending_sop.v5.yaml),
  which keeps its v5 name and holds version 8, 15 October 2026; operator,
  16 October 2026), with the SOP's TL thresholds and QCL's contract repair
  line appended to Notes on every Case. Where the
  [Report Sending Quick Reference](../principal-profiles/sop-guides/report_sending_quick_reference.docx)
  of 29 October 2026 differs it is followed: QCL's line is that reference's
  QCL note, and the job sheet's "Sending Report:" passages are out of the
  notes of the Principals it names, so this tab alone says how a report goes
  (operator, 13 November 2026; [R1034](../rulings.md),
  [R1035](../rulings.md)). The
  delivery behaviour is owned by
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md);
- the Principal API credential (API-04): issue, reset, revoke, pause and
  resume. Each acts on the click and goes into permanent history
  ([v41 A29](https://github.com/collisionengineers/pegasus-design-archive/blob/main/v41_planning/notes/v41-notes.md)).
  The secret is shown once, at issue or reset, and never
  again, even on an exact replay. The response is non-cacheable. The secret
  is never put in TempData, URLs or history; only its hash is kept. The
  credential is handed over with the submission endpoint it authenticates
  ([FRD-09 API-01](frd-09-principal-and-intermediary-routes.md#principal-api-and-contract-boundary))
  and gives no staff access. Resetting a paused credential makes it active
  again. A revoked credential can be reissued, which starts a new secret and
  clears the revocation.

A Principal and a Claim Source contact each carry **Notes on every Case**:
free text stored once on the organisation. The Case record's Case details
show the Claim source notes read-only in the Claim source card and the
Principal notes read-only in the Principal panel, each beside that Case's own
editable notes, and show nothing when the record has none (R618, R619). A Triage Case shows the Principal's notes too, read-only and absent when
the Principal has none (operator ruling, 22 October 2026). The Claim source is chosen on the Case from the active Claim Source
contacts, or none. A Case made from a received item is created with one where the item establishes it: the claim source its
instruction names (DFD's Claim source, SBL's and OAK's Introducer), when that name is an active Claim Source's, ignoring
capitals; otherwise, for mail accepted through an intermediary, the one active Claim Source linked to the route's Principal
that the sender matches, by the rule that made it an intermediary; otherwise the one active Claim Source linked to the
Case's Principal that the instruction's text (its subject, body and the text of its attachments) names, capitals and spaces
ignored ("Car Claims" names "CarClaims") (operator, 25 November 2026; [R1264](../rulings.md)). Two or more matches, or none,
fill nothing, and a name that is not a Contact never creates one. It is recorded as a choice on the Case is, so staff change or clear it; Cases made
before the change are not filled (operator, 12 November 2026; [R1014](../rulings.md), [R1017](../rulings.md),
[R1018](../rulings.md)). The Case keeps a copy of the chosen name, telephone and e-mail, its
notes follow the chosen record, and a changed choice must still be an active
Claim Source when the Case is saved. The Case may override the chosen
source's telephone and e-mail for that Case alone. The override
wins per field; changing or clearing the chosen source clears it. The card's
contact name is the Case's own: a Claim Source holds none to copy, so it is
what staff type on the Case, and a name a Case copied before 13 November 2026
stays on that Case (operator, 13 November 2026; [R1047](../rulings.md),
[R1048](../rulings.md)). Repairer contacts have no notes.

A Principal, Claim Source or Repairer contact may have a **standing photo
link** (operator, 9 November 2026; R774): its URL and QR code, Pause and
Resume, Replace link, Retire link, which ends it for good (operator, 9
November 2026; R796), its saved addresses, offered on its form as
inspection locations (operator, 9 November 2026; R806, R815), and the
defaults its captures take, which are the mode
of a link sent on to a customer, Live camera only, the lifetime and whether
originals follow. The link only starts captures, and none while the
contact is inactive (operator, 9 November 2026; R796); its behaviour is
[FRD-28](frd-28-guided-photo-capture.md#standing-photo-links).

Every change is a permanent action-history event with actor, time, operation
identity and before and after values. Routine settings changes need no
reason. A reason is kept only where the action's rule requires one. A
standing photo link's changes are such events, the website link's included:
"Photo link created", "Photo link paused", "Photo link resumed", "Photo
link replaced", "Photo link retired", "Photo link defaults saved", "Photo
link address added", "Photo link address changed" and "Photo link address
removed", under the area Contact for a contact's link and the area "Website
photo link" for the website link (operator, 10 November 2026; R846).

### Staff accounts

The staff accounts table lists Username, Role, Sign-off, Glass's and State
in compact rows
([v41 A14](https://github.com/collisionengineers/pegasus-design-archive/blob/main/v41_planning/notes/v41-notes.md)).
The table shows 50 accounts at a time with Previous and Next, and its
heading counts every account (operator, 22 October 2026; R233, R417, R418,
R421; built 29 October 2026, R527). Each row opens a Settings dialog for its one role and its account
actions. Create opens its own dialog. If a settings change is refused, the
entered values and the validation message stay visible inside Settings.
Saving Settings posts on the click and records the
`staff_account_settings_updated` history event. No Administration action
asks for a reason, and none opens a confirmation dialog except Delete
(operator, 15 October 2026,
[v41](https://github.com/collisionengineers/pegasus-design-archive/blob/main/v41_planning/notes/plan-2026-10-15.md)).
`Reason` is optional on every staff-account command in Core.

The account actions are Create, Enable, Disable, Delete access, Force logout
and Reset password. Each acts on its click from the Settings dialog, except
Delete, which confirms in the shared dialog.
Disable, Enable and Delete are shown apart from Force logout and Reset
password as adverse actions. There are no periodic reviews, review dates or
review actions. An account cannot disable or delete itself. Concurrent
actions cannot remove the last enabled Administrator.

**Reset password** is Administrator-only. It generates a temporary password
and shows it once on the redisplayed page, so the Administrator can pass it
on. Accounts are not tied to email addresses, so nothing is emailed. The
existing password policy and hash stay in charge of passwords. The account is
put into forced-change state, so it must choose a new password at its next
sign-in. That forced screen asks only for the new password and its
confirmation; the issued password is replaced, not re-entered, and it cannot be
chosen again. A voluntary change still asks for the current password first.
The reset is a permanent history event with actor and time
([v41 A29](https://github.com/collisionengineers/pegasus-design-archive/blob/main/v41_planning/notes/v41-notes.md)).
The temporary secret is never emailed, logged, stored in raw form or sent to
analytics.

Disable, a role change, a reset and Force logout revoke the account's
sessions and tokens. Every change to an account's role, enabled state or
password rotates its security stamp, and each request checks the stamp
against the account. A change made in Pegasus is seen on the next request. A
change made outside it, such as direct SQL or the intake wipe, is seen within
300 seconds, because each request's account read is kept for that long.
Document and thumbnail reads rely on that request check and do not read the
account again. A queued report render reads its images with the authority
the account had when the render was queued. A session is never refreshed into
new authority: it is refused and signs in again. A sign-in from Pegasus
Desktop carries the stamp the account had when it was made, and is refused
the same way once the stamp has moved (R1298). Delete
removes active access, the role and credential material, but keeps the
minimal actor identity that business history and printed reports need. It
never deletes a Case. Disable acts at once from the Settings dialog; Delete
confirms first in the shared dialog (operator, 15 October 2026,
[v41](https://github.com/collisionengineers/pegasus-design-archive/blob/main/v41_planning/notes/plan-2026-10-15.md)).
Force logout also clears the account's Triage
and Image Intake edit scopes
([FRD-14](frd-14-record-edit-leases.md#record-edit-scopes)), so an old token
cannot later change either record type. Administration saves use
expected-version checks. The Case edit lease is handled by the Case
workflow ([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)).

Glass's credentials are protected per staff account, provider and generation.
Administration shows whether one is configured and enabled, its username and
when it was updated, and offers replace and clear. It never shows the stored
password. Replacing or deleting a credential invalidates old sessions.
Disabled or deleted staff cannot start or resume a Glass's session.

The integrations' logins (Glass's valuation, Cazana, Brego and the two
Experian accounts) belong to Collision Engineers, not to a member of staff.
An Administrator replaces each on Administration > Integrations
([FRD-17](frd-17-administration-workspace.md#integrations); R1289), which
shows each login's account and when it was last updated; Pegasus keeps them
encrypted, shows no other stored value back, and records each change
in permanent history, attributed to the Administrator, naming the login and
never a value (operator, 26 November 2026; R1288,
[ADR-0091](../adr/0091-integration-logins-in-the-database.md)). The Brego
account is signed in in its Settings dialog there (R1296), with Refresh
login where the host reads the account's mailbox (R1294), Send sign-in link,
Link account and, when Brego asks for a code, Confirm code, or with its saved
password (R1290).

**Sign-off Engineer** is an Administrator-managed account setting available
for any staff role: a flag, the account's qualifications and a signature
image. An Administrator also marks one flagged account as the default
Sign-off Engineer. Only enabled flagged accounts are offered on a Case; which
one a Case gets by default is defined in
[FRD-13](frd-13-case-lifecycle-and-workflow.md#sign-off-engineer). Reports
render the flagged account's name, qualifications and signature
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#initial-renderer-activation)).
Every change to the flag, qualifications or signature is a permanent history
event shown in Action logs. The initial flagged accounts are A Patterson,
N O'Reilly and E Mawdsley. An account whose qualifications have not been
recorded yet prints its name without a qualification line. Flags,
qualifications and the default are application data, never hard-coded.

### Permanent action history

Permanent business history records every business change, every download or
export, every material refusal or failure, every automated result, and every
external fact that was accepted, linked or used. Each event records the
exact Case when there is one, the source or evidence identity, the staff or
automated actor, the caller, the time, the policy and version, structured
before and after values, the outcome, and a reason where one applies. The
history write is part of the same transaction as the change, so a change can
never succeed unrecorded. History is append-only: a correction or
reassociation adds an event and never rewrites an earlier one.

Sign-ins and authentication failures go to the security log. Routine views,
searches, refreshes, polling, retries, lease renewal, expiry, heartbeats and
adapter mechanics stay as content-safe telemetry.

**Action logs** (Administration › Logs) is the one administration view over
permanent history and the security log. It filters by search text, Area,
Actor, Result, From and To. It sorts newest first with a toggle, and shows
Time, Actor, Area, Action, Reference and Result per row. Account access and
role changes, Principal settings and credential changes, and automation
activity are all read here. There is no separate periodic review or
Automation Activity page.

No identity design, app registration, scope declaration, role table, file or
registration proves that a live caller exists or is accepted.

### Case-party provenance

A Principal instructs and pays. An Intermediary routes work without becoming
the Principal. A Repairer may hold the vehicle and supply evidence. The Image
Source is whoever actually supplied the images; that may be any of these or
an individual. One organisation may do several of these on one Case. The
sender's identity alone never settles who the Principal is. Later directory
corrections never rewrite a Case's accepted party identities, roles or
inspection address.

Initial Administrator assignments are application data, never hard-coded
authorisation. External customers have no staff account.

## States and transitions

| Record | States | How they change |
| --- | --- | --- |
| Contact | Active, inactive | Administrator edits with an expected record version; a stale write is refused |
| Staff account | Enabled, disabled, deleted access; forced password change | Create, Enable, Disable, Delete access, Reset password |
| Principal API credential | Active, paused, revoked | Issue, reset, revoke, pause, resume |
| Sign-off Engineer flag | Set or not; one account marked default | Administrator account setting |

## Edge cases and fail-closed behaviour

- A duplicate organisation name within a type, or a duplicate Principal code,
  is refused and leaves no orphan record.
- A phone number with letters or other characters is refused in the field
  and on the server.
- An account cannot disable or delete itself, and the last enabled
  Administrator cannot be removed.
- A revoked session's token is refused on its next request.
- A sign-in from Pegasus Desktop is not renewed once the account's security
  stamp has moved, or after its 8 hours; where the sign-in is not configured
  there is no such client to sign in to (R1297, R1298).
- The Principal API secret is shown once; a replay shows nothing.
- A refusal never reveals Case or source data.

## Acceptance evidence

Core tests cover the role matrix, account commands, the credential lifecycle
and the history write inside each transaction. Integration tests cover the
Contacts and Staff accounts screens over real HTTP, and the sign-in from
Pegasus Desktop from its page to its tokens, renewal, ending and revocation
(`DesktopSignInTests`). Deployment and live
acceptance are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `ACC-01`–`ACC-05`, `ACC-07`–`ACC-11`, `ACC-15`,
  `RPT-08` in [capabilities](../capabilities.md).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-07](frd-07-case-export.md),
  [FRD-09](frd-09-principal-and-intermediary-routes.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-17](frd-17-administration-workspace.md).
- Technical constraints:
  [ADR-0004](../adr/0004-provider-api-and-staff-mcp-authentication.md),
  [ADR-0048](../adr/0048-principal-report-generation-policies.md),
  [ADR-0076](../adr/0076-reviewed-register-is-the-contact-directory-data-contract.md).

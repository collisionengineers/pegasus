# FRD-04: Parties, accounts, and access

> Owner capabilities: ACC-01 to ACC-05, ACC-07 to ACC-11, ACC-15, CASE-32 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- Contacts are external organisations. One organisation can be a Principal,
  Claim Source, Repairer, Storage provider or Third Party Engineer. Staff
  accounts are separate.
- Each Case keeps its own copy of the parties it was accepted with. Editing
  the Contacts directory later never changes that copy.
- Every staff account has exactly one role: Administrator, Engineer or User.
  Administrator can do everything, including Engineer work.
- Administrators manage accounts from one Settings dialog. No account action
  asks for confirmation or a reason.
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
Engineers. One reusable Organisation can also be a Claim Source, Repairer,
Storage provider or Third Party Engineer. A Principal never links to another
Principal. Each non-Principal contact role may link to several Principals.
Staff accounts are not Contacts.

A repairer, broker, agent, client, legal representative, provider, vehicle
keeper or other contact may play different roles on different Cases. The
reusable directory record is separate from the inspection address and party
roles each Case keeps. Raw provider or contact workbooks are evidence, not
import authority.

The Repairer directory records each repairer's name, full address and
contacts. A Repairer may relate to several Principals and a Principal to
several Repairers. These directory links never rewrite the address or
party-role copy an existing Case already holds.

A Case records its own repairer: the name and address the instruction gave,
kept as ordinary Case facts with their extraction provenance. Staff either
confirm that text or link the Case to an active Repairer-role Contact. Linking
copies that Contact's identity, version, name and address onto the Case, so
a later directory edit never changes the Case. The confirmed repairer address
is one of the Inspect-at options beside the claimant address, the storage
location and the Principal's own default. The option names the repairer it
came from.

### Staff role access matrix

Staff sign in with Pegasus-managed usernames and passwords. Passwords are
stored as non-reversible hashes. This stays the sign-in route until a
separately accepted identity change replaces it. Each account has exactly one
current role.

Administrator holds every permission, including every Engineer capability.
One Administrator role is enough for engineering work, Case assignment and
sign-off eligibility. Adding an Engineer role to an Administrator is neither
needed nor allowed. The account must still be enabled and meet any signature
or workflow prerequisite for the action.

| Staff role | May view | May create or change | Must not access or perform |
| --- | --- | --- | --- |
| `Administrator` | All authorised application data and settings | Every ordinary Intake, Triage, Case, document, evidence, task and transition action; staff account create, disable, delete access, force logout, role assignment and password reset; the Sign-off Engineer account setting; Principals and successor cutover, including a Principal's Provider API credential lifecycle; workflow configuration, including labour-rate cards; the approved-mailbox allowlist; OAuth-client registration and revocation | Pegasus's own credential-secret, cloud or release administration through the staff UI; permanent deletion; a generic mailbox-rule editor before its policy is accepted |
| `Engineer` | Cases, inbox items, documents, evidence and details | Every authorised Intake, Triage, Case, document, evidence, task and transition action | Accounts, roles, Principals, successor cutover, workflow configuration, mailbox allowlist, authentication-client administration, credentials, cloud or release administration, permanent deletion |
| `User` | Cases, inbox items, documents, evidence and details | Every authorised Intake, Triage, Case, document, evidence, task and transition action | Accounts, roles, Principals, successor cutover, workflow configuration, mailbox allowlist, authentication-client administration, credentials, cloud or release administration, permanent deletion |

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

**Contacts** lists every external organisation with its type, name, contact
person, email, phone, last Case, and state filters and sorting. A contact has
an organisation name, contact person, email, phone, address and active
state. Phone accepts digits, spaces and one optional leading `+` only. UK
numbers are written with a leading `0` and internal spaces. Letters and other
characters are rejected in the field and on the server.

**Add contact** first asks for the role: Principal, Claim Source, Repairer,
Storage or Third Party Engineer. It then creates that role on a new identity
or adds it to an existing identity the operator picks. Matching names are
suggestions only. Pegasus never merges records by itself.

Principal policy lives on the Principal role of its Contact. A Principal code
is created in the same transaction as that role and stays with the same
Contact through a code replacement
([FRD-01](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity)).
A duplicate organisation name or Principal code fails without leaving an
orphan identity. The screen's visuals and controls are owned by the
[design README](../design/README.md); its layout is in
[FRD-17](frd-17-administration-workspace.md#contacts).

The Principal section of a Contact carries:

- the accepted route email domains, read-only once activated, read from the
  provider route policy in
  [FRD-09](frd-09-provider-and-intermediary-routes.md#provider-and-intermediary-routes);
- the default inspection location, Image Based Assessment or a physical
  address, saved on the click;
- the report-generation policy and its delivery suggestions, owned by
  [FRD-07](frd-07-eva-and-external-engineering-handoff.md#eva-handoff-routes)
  and [ADR-0048](../adr/0048-principal-report-generation-policies.md).
  Configured additional recipients and the optional original instruction
  sender are the only delivery suggestions. The Claim Source is never copied
  in by default;
- the Provider API credential (API-04): issue, reset, revoke, pause and
  resume. Each acts on the click and goes into permanent history with an
  optional reason. The secret is shown once, at issue or reset, and never
  again, even on an exact replay. The response is non-cacheable. The secret
  is never put in TempData, URLs or history; only its hash is kept. The
  credential is handed over with the submission endpoint it authenticates
  ([FRD-09 API-01](frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary))
  and gives no staff access. Resetting a paused credential makes it active
  again. A revoked credential can be reissued, which starts a new secret and
  clears the revocation.

A Principal and a Claim Source contact each carry **Notes on every Case**:
free text stored once on the organisation. The Case record's Overview shows
the Principal notes and the Claim source notes read-only in its Notes band,
beside that Case's own editable notes, and shows nothing when the record has
none. The Claim source is chosen on the Case from the active Claim Source
contacts, or none. The Case keeps a copy of the chosen name and contact, its
notes follow the chosen record, and a changed choice must still be an active
Claim Source when the Case is saved. The Case may override the chosen
source's contact name, telephone and e-mail for that Case alone. The override
wins per field; changing or clearing the chosen source clears it. Repairer,
Storage and Third Party Engineer contacts have no notes.

Every change is a permanent action-history event with actor, time, operation
identity and before and after values. Routine settings changes need no
reason. A reason is kept only where the action's rule requires one.

### Staff accounts

The staff accounts table lists Name, Username, Role and State in compact
rows. Each row opens a Settings dialog for its one role and its account
actions. Create opens its own dialog. If a settings change is refused, the
entered values and the validation message stay visible inside Settings.
Saving Settings posts on the click and records the
`staff_account_settings_updated` history event. No Administration action
opens a confirmation dialog or asks for a reason, and `Reason` is optional on
every staff-account command in Core.

The account actions are Create, Enable, Disable, Delete access, Force logout
and Reset password. Each acts on its click from the Settings dialog.
Disable, Enable and Delete are shown apart from Force logout and Reset
password as adverse actions. There are no periodic reviews, review dates or
review actions. An account cannot disable or delete itself. Concurrent
actions cannot remove the last enabled Administrator.

**Reset password** is Administrator-only. It generates a temporary password
and shows it once on the redisplayed page, so the Administrator can pass it
on. Accounts are not tied to email addresses, so nothing is emailed. The
existing password policy and hash stay in charge of passwords. The account is
put into forced-change state, so it must choose a new password at its next
sign-in. The reset is a permanent history event with actor, time and reason.
The temporary secret is never emailed, logged, stored in raw form or sent to
analytics.

Disable, a role change, a reset and Force logout revoke the account's
sessions and tokens. The next request sees the current authority. Delete
removes active access, the role and credential material, but keeps the
minimal actor identity that business history and printed reports need. It
never deletes a Case. Disable and Delete act at once from the Settings
dialog, with no confirmation. Force logout also clears the account's Triage
and Image Intake edit scopes
([FRD-14](frd-14-record-edit-leases.md#record-edit-scopes)), so an old token
cannot later change either record type. Administration saves use
expected-version checks. The Case edit lease is handled by the Case
workflow ([FRD-14](frd-14-record-edit-leases.md#case-edit-lease)).

Glass's credentials are protected per Engineer, provider and generation.
Administration shows whether one is configured and enabled, its username and
when it was updated, and offers replace and clear. It never shows the stored
password. Replacing or deleting a credential invalidates old sessions.
Disabled or deleted staff cannot start or resume a Glass's session.

**Sign-off Engineer** is an Administrator-only account setting: a flag, the
account's qualifications and a signature image. An Administrator also marks
one flagged account as the default Sign-off Engineer. Only flagged accounts
are offered on a Case; which one a Case gets by default is defined in
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
| Contact | Active, inactive | Administrator edits under a record edit scope |
| Staff account | Enabled, disabled, deleted access; forced password change | Create, Enable, Disable, Delete access, Reset password |
| Provider API credential | Active, paused, revoked | Issue, reset, revoke, pause, resume |
| Sign-off Engineer flag | Set or not; one account marked default | Administrator account setting |

## Edge cases and fail-closed behaviour

- A duplicate organisation name or Principal code is refused and leaves no
  orphan record.
- A phone number with letters or other characters is refused in the field
  and on the server.
- An account cannot disable or delete itself, and the last enabled
  Administrator cannot be removed.
- A revoked session's token is refused on its next request.
- The Provider API secret is shown once; a replay shows nothing.
- A refusal never reveals Case or source data.

## Acceptance evidence

Core tests cover the role matrix, account commands, the credential lifecycle
and the history write inside each transaction. Integration tests cover the
Contacts and Staff accounts screens over real HTTP. Deployment and live
acceptance are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `ACC-01`–`ACC-05`, `ACC-07`–`ACC-11`, `ACC-15`, `CASE-32` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-01](frd-01-case-identity-and-lifecycle.md),
  [FRD-07](frd-07-eva-and-external-engineering-handoff.md),
  [FRD-09](frd-09-provider-and-intermediary-routes.md),
  [FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md),
  [FRD-17](frd-17-administration-workspace.md).
- Technical constraints:
  [ADR-0004](../adr/0004-provider-api-and-staff-mcp-authentication.md),
  [ADR-0048](../adr/0048-principal-report-generation-policies.md).

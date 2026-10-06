# FRD-17: Administration workspace

> Owner capabilities: MI-01 to MI-03, UI-11, MAIL-24 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- `/Administration` has ten areas: Accounts, Contacts, Workflow
  configuration, Mail settings, E-mail templates, Valuation presets, Service
  health, Logs, Reports and AI jobs. Automation appears only when composed.
- Administration buttons act on the click. There is no confirmation dialog.
- Logs has two tabs: Action logs (who did what) and Intake log (what
  happened to each received file).
- Workflow configuration holds the completeness rules, the chase interval,
  the five due targets and the labour-rate cards.

## Purpose

This document says how the Administration areas behave for an
Administrator. The rules those settings control are owned elsewhere:
accounts and contacts by [FRD-04](frd-04-parties-accounts-and-access.md),
completeness and chasing by
[FRD-13](frd-13-case-lifecycle-and-workflow.md), valuations by
[FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md) and
estimates by
[FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md).

## Behaviour

### Administration

`/Administration` carries **Accounts**, **Contacts**, **Workflow
configuration**, **Mail settings**, **E-mail templates**, **Valuation
presets**, **Service
health**, **Logs**, **Reports**, **Release notes**, **Problem reports** and **AI
jobs**. Automation appears only when
its capability is composed. Its Automation & AI page carries the Send to AI
switch
([FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#send-to-ai-switch)).
There are no separate Principal or Claim Source areas.

Every consequential change (role, account state, principal credential,
automation stop or start) enters permanent history with an optional reason.
Administration pages perform Save, Enable, Disable, Delete, Remove, Clear and
Stop on the click, with no confirmation dialog.

Contact, staff-account and mailbox Settings load their current values and
expected version with editing available immediately. Glass's credentials,
workflow configuration, labour-rate cards, approved categories and valuation
presets are also immediately editable. Each independent change sends its
rendered expected version. A stale change is refused without overwriting the
newer value and asks the Administrator to reload.

Where Automation is composed, each of its scopes (`automation.cases`,
`automation.intake`, `automation.documents`, `automation.assessment`,
`automation.mail`, `automation.jobs`) renders as a plain label (Cases,
Intake, Documents, Assessment, Mail, AI jobs). The underlying key is kept
only as a hover title.

### Accounts

The **Staff accounts & roles** area carries **Reset password** beside the
other account actions. Pegasus generates a temporary password and reveals it
once, on the redisplayed page, to the Administrator. The rule is owned by
[FRD-04](frd-04-parties-accounts-and-access.md#staff-accounts).
Administrators may configure sign-off flags, qualifications and signature
images for any staff role; these controls do not make Administration available
to that account. Each account's Glass's repair-estimate credential is managed
in a dialog on this area, opened from the account's row or settings and
deep-linked with `?glassStaffId=`; the dialog never owns a page of its own,
and the secret is write-only
([FRD-04](frd-04-parties-accounts-and-access.md#staff-accounts)).

### Contacts

Contacts is the one directory for Principals, Claim Sources, Repairers,
Storage and Third Party Engineers. Principal-specific settings are part of
that Contact record. Report generation carries the Default fee (£) box above
Save report settings; it has no Report recipients settings (operator, 6
October 2026). The Report sending panel follows Report generation and is
shown for a Principal only; every Principal has rules to show. Send from is a list of the approved
mailboxes with Not set. Send to, Cc, Never cc and Reminders are repeated
inputs with an Add button; Send to only, Reply all, Garage figures and the
six Attach choices are ticks; Hold and the First and Re-send attachment
names are text, with no length limit. Rules are repeated rows, each with
Match (All or Any condition), three condition lines (a kind, then a Contact
list, the four outcomes, or text for Images from, Instruction mentions,
Bodyshop mentions and Sender is not), and the actions Cc add, Cc remove,
Remind, Hold and Stop. A Remove rule button per row, one spare blank row, an Add rule button,
and one Save report sending. The Salvage matrix panel follows it: one
table per salvage category, with From (£), To (£) and Percentage paid (%)
columns, a Remove button per row, one spare blank row, an Add band button,
and one Save salvage matrix. The list filters live as the operator types, 300
milliseconds after the last keystroke, alongside the Apply control for
no-script use. Server-side filtering is unchanged. A telephone value accepts
digits, spaces and an optional leading `+` only (UK numbers keep a leading
`0` and internal spaces). Letters and other characters are rejected on both
the field and the server. The directory rules are owned by
[FRD-04](frd-04-parties-accounts-and-access.md#contacts-administration).

### Workflow configuration

**Workflow configuration** holds:

- the versioned instruction- and image-completeness rules as required or
  not-required items with exact blockers
  ([FRD-13](frd-13-case-lifecycle-and-workflow.md#readiness-and-review));
- the chase interval, one global whole-calendar-day value
  ([FRD-13](frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing));
- the Work Centre due targets, each in whole calendar days from 0 to 365:
  Unidentified target (default 0, due by the midnight after receipt), Triage
  target (1), Held decision target (7), Review target (1) and AI draft
  target (1);
- labour-rate-card administration: the global versioned cards (name,
  panel-and-paint hourly rate, enabled state) that every estimate version
  selects from. Disabling a card blocks future selection without changing
  history ([FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md#canonical-repair-specifications)).

The six settings and labour-rate-card rows are editable on load. Validation
names the setting and allowed range when a value is refused. There are no
staff instruction-review or image-review settings. Save submits the workflow
settings or one card with its rendered expected version; a stale save is
refused and asks the Administrator to reload. Labour-rate cards stay inside
this area; they are not an area of their own.

### E-mail templates

**E-mail templates** (`/Administration/EmailTemplates`) holds the text of
the staff replies an Administrator may change. Only an Administrator may open
it or save a template. It lists one row per template: its name, who last
changed it and when, and **Edit**. The templates are the Triage outcome reply
([FRD-03](frd-03-triage.md#normal-workflow-and-completion-evidence)), the
Case report delivery
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md)) and the
Case chaser
([FRD-13](frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing)).

**Edit** opens a dialog: the body, up to 5000 characters, a row of
placeholder buttons that insert at the cursor, and Cancel and Save. The
Triage outcome reply's placeholders are `{registration}`,
`{roadworthiness}`, `{repair outcome}` and `{reason}`. The Case report
delivery's are `{case reference}`, `{registration}`, `{outcome}`,
`{principal name}`, `{superseded report date}` and `{greeting}`. `{greeting}`
is "morning" before noon in London, else "afternoon". Its built-in body is the
Report Sending SOP wording: "Good {greeting}, Please see attached report and
fee note. Any issues let us know. Kind Regards". A saved template is never
changed by a new built-in body. The Case chaser's are `{registration}`, `{outstanding material}` (the
Case's missing-material reason while it has due work), `{principal name}` and
`{claimant}`; it has no Case/PO placeholder, since the reference is internal
and means nothing to the party chased (operator, 5 October 2026). Its
built-in body is the one sentence the due-chaser sweep writes, without the
reference, and the sign-off. A body naming any other placeholder is refused,
and the message names it. Save acts on the click with the rendered version. A
stale save is refused and asks the Administrator to reload.

A placeholder with no value renders nothing. A line whose placeholders are
all empty is left out. The subject is not templated: a reply keeps
"Re: {original subject}", a report delivery follows
[FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#report-sending-rules)
(a reply keeps "Re:" the instruction's subject, and a new message is
"{REG} Report"), and a chaser opens with the registration and the claimant.
Until an Administrator saves a template, its built-in body is used.
Staff can edit the rendered text before it is sent or prepared. Each save
enters the Action logs.

### Valuation presets

**Valuation presets** adds and edits inline: a compact add row and all existing
rows editable immediately, with no separate creation or edit dialog. Save and
Remove use each row's rendered expected version. A stale change is refused
and asks the Administrator to reload.
**Remove** is a soft removal that acts on the click. The preset drops out of
the list and out of new selection. A valuation already recorded against it
keeps its own snapshot
([FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md#valuation-sources)).

### Logs

**Logs** (`/Administration/Logs`) has two tabs.

**Action logs** names who acted, never a raw identifier. A staff subject
resolves to their username. An unresolvable staff id (a deleted account)
reads **Former staff**. The Automation client renders as an **AI** chip with
its registered client name. Worker-attributed work reads **Pegasus**. A
legacy security row recorded before the acting principal was captured has no
actor kind and is labelled by its event type, not a guessed user. An **Actor
type** filter (Staff / AI / Pegasus) narrows the list by this kind. An AI job
row links through to the Case or Unidentified record it acted on. Time values
and the From/To period pickers show and accept whole minutes only; storage
keeps its sub-second precision.

**Intake log** lists one row per received file: received, source, item
(opening the original), outcome with its reason, what it became, and
attempts. Its head shows counts (Failed intake and the oldest pending intake). It has filters (search, outcome, source,
principal, from, to), paging, and a row drawer with the retained original,
Open message where it came by e-mail, the processing evidence and the
technical actions that apply
([FRD-02](frd-02-intake-and-source-identity.md#received-file-history-and-technical-actions)).

### Reports

**Reports** shares one period filter across MI-01 Engineer activity,
MI-02 Reports by Principal (per-Principal report counts by type) and MI-03
Turnaround (current holding age, and instruction-to-produced, ready and sent
turnaround). Each has its own totals and a downloadable CSV, and **Download
workbook** gives every report for the period as one `.xlsx` with a sheet
each and a **By month** sheet, typed cells, a frozen filtered header and
totals. Reports and fees are counted per report: the first confirmed report
of the Inspection and, once created, of the Audit of an Inspection + Audit
Case each count once, with their own agreed fee. An Audit report is the
report of a standalone Audit Case or the Audit of an Inspection + Audit
Case. MI-01 counts the queries by type (disputes and amendment requests
within the total), the Audit reports sent (the Audit uplift) and each
person's turnaround to sent, measured from the instruction's receipt, or
for the Audit of an Inspection + Audit Case from Create audit; its columns
sort by person, queries or reports, and a meter beside each count shows it
against the period's largest. MI-02 adds the agreed fees on the reports
produced, and a **By month** table (reports and fee notes produced, reports
sent, agreed fees, per Principal and London month) for invoice generation.
MI-02 shows reports produced, reports sent and agreed fees each as
Inspection and Audit columns beside their unchanged totals, on the page, in
the CSV and in the workbook, its By month sheet included. A section whose
query fails or returns invalid data renders an unavailable state, never a
false zero.

### Release notes

**Release notes** lists every note, newest change first, with its status
(Draft or Published), version and who published it. **New release note**
opens a form with Title and Body; **Save draft** keeps it editable and
**Publish** freezes it, stamping the running build's version and source
SHA. A published note opens read-only. Only a signed-in Administrator can
save or publish: Core grants `PublishReleaseNotes` to no other actor, so the
Automation Actor cannot write a note and nothing reaches staff without an
Administrator's press ([ADR-0054](../adr/0054-release-notes-authored-in-the-application.md)).
The body is shown as typed: blank lines separate paragraphs and lines
beginning with `- ` form a list. Publishing makes the note the shell's
What's new dialog for everyone ([FRD-12](frd-12-operator-experience.md#shell-and-routes)).
A stale save is refused without overwriting the newer draft.

### Problem reports

**Problem reports** lists every report kept, newest first: when, who, the
page, the Case, what the person wrote, its status (Sent with a link to the
issue, or Not sent with the reason) and **Retry** on a report that was not
sent. Retry raises the same report again; a sent report is never raised
  twice. Nothing on the page edits a report
  ([ADR-0055](../adr/0055-github-issues-as-the-problem-report-sink.md)).
  The linked issue contains the reporter's text and captured diagnostics;
  the list remains the record of reports kept and their delivery status.

## States and transitions

Every area renders one of: loading, empty, current, stale (with the
last-good time), partial, unavailable, failed, validation, conflict, or
access denied. Existing settings are editable immediately and each change
checks the expected version inside its mutation transaction.

## Edge cases and fail-closed behaviour

- A refused configuration value names its range; nothing is saved.
- A report section with a failed or invalid query shows unavailable, not
  `0`.
- A legacy log row with no captured actor shows its event type, never a
  guessed user.
- A removed valuation preset never changes a valuation already recorded
  against it.

## Acceptance evidence

Acceptance covers the twelve areas and their routes, the on-click actions
without confirmation, the Action logs actor resolution and filter, the Intake
log columns and actions, the six workflow settings and their ranges, and the
three MI reports with their CSVs. Authenticated Web tests cover server-owned
behaviour. Deployment and live acceptance are separate evidence tiers
([engineering](../engineering.md#required-evidence-tiers)).

## Links

- Capabilities: `MI-01`–`MI-03`, `UI-11` in
  [capabilities](../capabilities.md).
- Related FRDs: [FRD-02](frd-02-intake-and-source-identity.md),
  [FRD-04](frd-04-parties-accounts-and-access.md),
  [FRD-06](frd-06-vehicle-and-engineering-evidence.md),
  [FRD-24](frd-24-engineer-findings-damage-valuation-and-settlement.md),
  [FRD-25](frd-25-repair-estimates-imports-and-glasss-sessions.md),
  [FRD-12](frd-12-operator-experience.md),
  [FRD-13](frd-13-case-lifecycle-and-workflow.md),
  [FRD-14](frd-14-record-edit-leases.md).
- Design: [design](../design/README.md).

# FRD-17: Administration workspace

> Owner capabilities: MI-01 to MI-03, UI-11 · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · Design: [design](../design/README.md)

## Short version

- `/Administration` has nine areas: Accounts, Contacts, Workflow
  configuration, Mail settings, Valuation presets, Service health, Logs,
  Reports and AI jobs. Automation appears only when composed.
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
configuration**, **Mail settings**, **Valuation presets**, **Service
health**, **Logs**, **Reports** and **AI jobs**. Automation appears only when
its capability is composed. Its Automation & AI page carries the Send to AI
connector settings
([FRD-27](frd-27-send-to-ai-reviewed-proposals-and-ai-job-list.md#send-to-ai-connector-settings)).
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

### Contacts

Contacts is the one directory for Principals, Claim Sources, Repairers,
Storage and Third Party Engineers. Principal-specific settings are part of
that Contact record. The list filters live as the operator types, 300
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
this area; there is no tenth area.

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
attempts. Its head shows counts (Failed intake, linking to Operations, and
the oldest pending intake). It has filters (search, outcome, source,
principal, from, to), paging, and a row drawer with the retained original,
Open message where it came by e-mail, the processing evidence and the
technical actions that apply
([FRD-02](frd-02-intake-and-source-identity.md#received-file-history-and-technical-actions)).

### Reports

**Reports** shares one London period filter across MI-01 Engineer activity,
MI-02 Reports by Principal (per-Principal report counts by type) and MI-03
Turnaround (current holding age, and instruction-to-produced, ready and sent
turnaround). Each has its own totals and a downloadable CSV, and **Download
workbook** gives every report for the period as one `.xlsx` with a sheet
each and a **By month** sheet, typed cells, a frozen filtered header and
totals. MI-01 counts the queries by type (disputes and amendment requests
within the total), the reports sent on Audit Cases (the Audit uplift) and
each person's instruction-received-to-sent turnaround; its columns sort by
person, queries or reports, and a meter beside each count shows it against
the period's largest. MI-02 adds the agreed fees on the Cases whose reports
were produced, and a **By month** table (reports and fee notes produced,
reports sent, agreed fees, per Principal and London month) for invoice
generation. A section whose query fails or returns invalid data renders an
unavailable state, never a false zero.

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

Acceptance covers the nine areas and their routes, the on-click actions
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

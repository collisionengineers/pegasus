---
id: PLAT-015
type: ticket
title: >-
  Bring pre-existing operator copy in line with the design authority
  (identifiers, placeholders, narration)
status: backlog
area: platform-operations
assignee: ''
profile: fix
labels:
  - ui
  - design
  - copy
  - follow-up
links:
  - CASE-012
  - AUTO-006
  - INTK-001
  - INTK-004
  - INTK-037
  - INTK-038
refs:
  - docs/frd/frd-12-operator-experience.md
archived: true
created: '2026-08-20T11:33:48.908Z'
updated: '2026-08-25T06:42:34.174Z'
---

## Disposition

Split into focused owners on 2026-08-25:

- Case task identities, retained-evidence presentation, dead Case/Assessment controls, and Case/Assessment narration → [[CASE-012]].
- Triage case, finding, evidence, and reply identities → [[INTK-037]].
- Mail decision and classification metadata → [[INTK-004]].
- Image Intake engine, version, disposition, and case-version presentation → [[INTK-038]].
- Automation Activity target and filter narration → [[AUTO-006]].
- Upload Status lede narration → [[INTK-001]].

Every original bullet has an active owner; this umbrella is archived to prevent duplicate implementation.

## Why

The release-14 copy audit (DELIV-013, 2026-08-20) confirmed the operator's complaint: several pre-existing surfaces breach docs/design/README.md. Word-level breaches were fixed in release 14; the structural ones below need real design work and are deferred here rather than rushed pre-release.

## Scope (all pre-existing at release 13)

- GUID entry/display on case tasks: `_CaseWorkflow.cshtml` "Assignee ID"/"Engineer ID" text inputs and `assignee {GUID}` renders — replace with named staff pickers (reuse `ActorDisplayNames`/`IStaffAccountQueries`).
- Evidence identity dumps: `_CaseSummary.cshtml` "Exact retained report-Sent evidence" panel (EvidenceId, mailbox/folder identities, Graph handles, SHA-256 hashes) and `_CaseWorkflow.cshtml` typed "Report SHA-256" input — show mailbox address, times, and a verified statement; keep handles internal.
- Triage details: linked-case GUID renders, "Case ID" GUID input, finding/evidence GUIDs, reply picker showing InternetMessageIdentity.
- Mail/Message: "Policy {key} version {N}" rows, predicate code keys, decision version integers, kebab-case classification names in the decision row and correction dropdown ("Queue" tile label review).
- ImageIntake/Details: engine key + version + raw enum disposition line; case-version integers.
- Automation/Activity: raw AggregateId in the Target column (resolve to Case/PO reference or omit); "you can filter by" narration.
- Dead placeholder controls for uncomposed capabilities: disabled "Look up vehicle"/"Check vehicle history"/"Raise a query", inert "Open in Glass's/Audatex" spans, inert estimates tab strip, unbound assessment section forms with dead Save buttons ("absent, not disabled" rule).
- UploadStatus lede paragraphs under the H1; `_CaseWorkflow` narration strings ("expose reasoned lifecycle commands", "Due-work version:", "case-completeness-projection" visible input value); assessment "Most of the report is written for you" card.

## How to verify

Every listed surface passes a docs/design/README.md read-through: no GUIDs/hashes/transport handles or code keys operator-facing, no GUID entry, no placeholder controls for uncomposed capabilities, no ledes/narration; integration tests updated alongside.

## Outcome

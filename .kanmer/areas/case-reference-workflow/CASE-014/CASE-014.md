---
id: CASE-014
type: ticket
title: 'An audit''s reference is the case reference, not a second identity'
status: done
area: case-reference-workflow
order: 950
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-22T00:47:37.060Z'
  implementing: '2026-08-22T00:48:07.195Z'
  review: '2026-08-22T00:51:04.446Z'
  verifying: '2026-08-22T04:36:08.213Z'
  done: '2026-08-22T08:01:24.716Z'
labels:
  - qdos26009
  - operator-reported
  - reference
links: []
docs_todo: true
deployment: production
archived: false
created: '2026-08-21T23:30:27.877Z'
updated: '2026-09-03T09:06:47.093Z'
---

## Why — operator direction (2026-08-22)

> "It should be a.QDOS26009 (its an audit, not an audit+inspection). Audits are either a. or ap. depending on whether the original report said it was Repairable or Total Loss. **There is no Case/PO AND audit identity. They are all just Case/PO.**"

## Evidence read from production

```
Cases: Reference='QDOS26009'  Type='audit'  AuditReference='a.QDOS26009'
```

Two identities exist for one audit. The operator says there is only ever one.

## What must change

- An audit case's **own reference** carries the prefix: `a.` when the original report says **Repairable**, `ap.` when it says **Total Loss**.
- The separate `AuditReference` concept goes away for audits — it is not a second identity to allocate, display, or store alongside the case reference.
- `Audit`, `Triage` and `Blocked intake` keep their settled distinct meanings; this is about the **reference**, not the case type.

## Care required

This touches a product invariant: *"Principal and reference are immutable after allocation."* The prefix depends on a fact extracted from the third-party report, so the sequencing question — is the outcome known **before** the reference is allocated? — has to be answered before any code changes. If it is not known at allocation time, this needs an explicit operator decision rather than a guess, because a reference cannot be revised afterwards.

Depends on the report outcome extraction tracked in [[INTK-031]].

## How to verify

An audit whose report says Repairable allocates `a.<ref>`; Total Loss allocates `ap.<ref>`; neither carries a second audit identity anywhere in the model, the UI, or Box.

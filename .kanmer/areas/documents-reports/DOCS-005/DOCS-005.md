---
id: DOCS-005
type: ticket
title: >-
  Box custody: drop binding JSONs, retain instruction attachments as their own
  files
status: review
area: documents-reports
assignee: claude-code
profile: fix
stageEntered:
  preparing: '2026-08-20T19:33:57.962Z'
  review: '2026-08-20T19:56:28.573Z'
taken_at: '2026-08-20T19:34:02.296Z'
branch: task/box-custody
worktree: ../pegasus-worktrees/box-custody
labels:
  - custody
  - box
  - operator-reported
links: []
refs:
  - docs/frd/frd-05-documents-extraction-and-custody.md
archived: false
created: '2026-08-20T19:33:29.208Z'
updated: '2026-08-20T19:56:28.573Z'
---

## Why

Operator, 2026-08-20 (feedback round 2): the Box case folders contain internal "nonsense" files (`pegasus-case-binding.json`, `pegasus-audit-binding.json`, `pegasus-accepted-source-binding.json`), and the retained .eml "seems to contain the PDF extraction". Operator decision on the bindings: **"drop it"** — the DB-stored folder ID is the authority (*"shouldn't the ID be getting stored on our side?"* — it is: custody payloads carry `CaseCustodyRootRemoteId`).

On the .eml: verified in code — `RetainAcceptedIntakeSourceAsync` uploads the source bytes only after `ReadVerifiedSourceAsync` matches the receipt's `SourceHash`, so the retained file **is** the original MIME message; a base64 attachment inside it reads like extracted text. The real gap is that the PDF the operator wants to open is only inside the MIME — attachments are already retained in the artifact store (`IntakeAssetKind.Attachment`) but never surfaced as Box files.

## What

- Stop writing all three binding JSONs: case root and audit folders use the staged create/promote (crash-safe, owner-token staging) without a binding file; existing-folder resolution keeps the name/identity checks; the image-fold keeps its legacy-binding delete so pre-15 folders still fold cleanly. Dead binding helpers removed.
- Retain each attachment asset of the accepted instruction as its own file beside the source in `Evidence/Original instruction` (`002 name.pdf`, …), via a new fail-closed `ICaseCustody.RetainAcceptedIntakeAttachmentAsync` implemented for Box and Local; the custody processor loads the receipt's attachment assets and retains them after the source.
- Deployment step (T10, approved Box write with exact targets): delete existing binding JSONs from live case folders.

## How to verify

Custody integration tests: a new case folder contains no `pegasus-*-binding.json` and does contain the attachment files; fold of a legacy folder with a binding still works. Live after deploy: a new case's Box folder holds the .eml and its PDF side by side, no JSON files.

## Outcome

---
id: INTK-055
type: ticket
title: Implement 15-minute public upload submission session
status: backlog
area: intake-processing
order: 540
assignee: ''
profile: feature
labels: []
groups:
  - EPIC-011
links:
  - DELIV-040
  - INTK-050
  - INTK-051
  - INTK-052
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-09-02T11:20:50.889Z'
updated: '2026-09-03T15:15:28.000Z'
---

Implement the amended D20 session contract from [[DELIV-040]]: the first successfully accepted file starts a fixed non-sliding 15-minute window; failed attempts before success do not start it; additions and replacements are accepted until explicit finalisation or expiry; closure refuses later bytes without Case disclosure. Reuse the request-scoped token and custody owners; do not predetermine larger upload limits.

---
id: ADR-0054
status: accepted
date: 2026-09-20
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: [frd-12, frd-17]
tags: [release-notes, administration, persistence]
---

# ADR-0054: Release notes are authored in the application by an Administrator

## Status

Accepted, recording the operator's 20 September 2026 decision. FRD-12 owns
the What's new dialog and the history page; FRD-17 owns the Administration
area; this record owns where the words come from and where they live.

## Context

After a deployment every staff member should see what changed, once. The
operator's rule is that the words are the operator's own: an AI agent may
propose, but nothing reaches staff unless the operator wrote or approved it.
A file in the repository cannot enforce that (any commit could carry it,
and a merged PR is the only gate), and a note delivered through the bell
would be one row among many rather than something read once and dismissed.

## Decision

Pegasus keeps one **ReleaseNotes** table and one **ReleaseNoteAcknowledgements**
table in the application database, owned by `Pegasus.Core` through a release
note store port. A note is written as a draft under Administration → Release
notes and published by an Administrator's press; Core grants
`PublishReleaseNotes` to a signed-in Administrator only, so the Automation
Actor and every other actor kind are refused. Publishing stamps the running
build's product version and source SHA, so a note is tied to the deployment
it describes, and a published note never changes again. The newest published
note opens once for each person as the shell's What's new dialog until they
press Got it, which writes their acknowledgement row; the Release notes page
lists every published note. Nothing is delivered by e-mail or any channel
outside the application.

## Consequences

- One additive migration and one Core port; no new runtime, queue or
  background process. The Web role reads, inserts and updates both tables and
  is denied DELETE; the Worker has no part in either.
- The shell reads one more indexed query per page (the newest published note
  and the person's acknowledgement). A store failure degrades the dialog
  only: the page renders without it and the failure is logged.
- The first note after this record's release is the operator's to write in
  the application; no draft is shipped in source.
- Wording, paragraphs and "- " lists are the Administrator's as typed; the
  application interprets nothing else.

## Links

- [FRD-12 — What's new](../frd/frd-12-operator-experience.md#shell-and-routes)
- [FRD-17 — Release notes](../frd/frd-17-administration-workspace.md#release-notes)
- [ADR-0053](0053-personal-staff-notification-store.md), the personal
  notification store this decision deliberately does not reuse.

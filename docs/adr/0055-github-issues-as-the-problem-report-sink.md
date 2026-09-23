---
id: ADR-0055
status: accepted
date: 2026-09-23
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: [frd-12, frd-17]
tags: [problem-reports, support, github, persistence]
---

# ADR-0055: GitHub issues as the problem-report sink

## Status

Accepted. Rewritten on 23 September 2026 at the operator's direction after
issues #809 and #810 showed that the previous ID-only issue design did not
serve diagnosis. FRD-12 owns Report a problem; FRD-17 owns the
Administrator's list. This record owns delivery and diagnostic content.

## Context

When something goes wrong a staff member needs one press to say so. The
people who fix Pegasus need the person's explanation and the captured
application state directly in the GitHub issue that they work.

## Decision

Pegasus keeps one **ProblemReports** table in the application database,
owned by `Pegasus.Core` through a problem report store port. A report stores
the person's complete submitted description and a bounded snapshot: build
version and source SHA, UTC time, route and method, trace identifier, staff
name and role, Case reference on screen, the person's last twenty logged
actions, and browser viewport, agent, editing state and last ten script
errors. When a report comes from the Error page, the snapshot also carries
the captured server exception, including inner causes and stack trace. A
report without a captured exception says so plainly; Pegasus does not infer
one from an unrelated request. The application does not automatically
collect documents, images, email bodies or request bodies for a report.

Every report is stored first, then raised as an issue in
`collisionengineers/pegasus` through the GitHub REST API. The issue title
summarises the first line of the description and retains the report ID. Its
body carries the full description and captured snapshot, including server
exception detail when available. The issue is the diagnostic work item;
staff need not copy the report from Administration to make it actionable.
A failed raise leaves the row Not sent with the reason; an Administrator
retries the same stored report from Administration → Problem reports.

The token is a fine-grained personal access token scoped to that repository
with Issues read and write only, held in Key Vault and read as configuration;
it appears in no source, log or document. The sink verifies the repository
identity before creating the issue.

## Consequences

- One Core store port and sink port, one named HttpClient; no queue or
  background process. Web alone writes the table and is denied DELETE.
- Without the token and repository configured (DevelopmentOffline, or an
  environment not yet connected) every report is kept as Not sent with the
  reason, so nothing a person wrote is lost.
- Rotating the token is a Key Vault secret version change and a
  configuration read-back; no release is needed.
- Adding what a report captures or publishes requires a Core change and
  an update to this record's list.

## Links

- [FRD-12 — Report a problem](../frd/frd-12-operator-experience.md#shell-and-routes)
- [FRD-17 — Problem reports](../frd/frd-17-administration-workspace.md#problem-reports)
- [Configuration reference](../engineering/configuration.md#problem-reports)

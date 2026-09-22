---
id: ADR-0055
status: accepted
date: 2026-09-20
supersedes: []
superseded_by: []
related_capabilities: []
related_frd: [frd-12, frd-17]
tags: [problem-reports, support, github, persistence]
---

# ADR-0055: GitHub issues as the problem-report sink

## Status

Accepted, recording the operator's 20 September 2026 decision and the 22
September choice of the public `collisionengineers/pegasus` repository. FRD-12 owns
the Report a problem action; FRD-17 owns the Administrator's list; this
record owns where a report goes and what it carries.

## Context

When something goes wrong a staff member needs one press to say so, and the
people who fix Pegasus need the state around it without asking. The
operator keeps the work in the repository's issues, so a report that
arrives there is already in the queue that gets worked.

## Decision

Pegasus keeps one **ProblemReports** table in the application database,
owned by `Pegasus.Core` through a problem report store port. A report is the
person's own words plus a snapshot the application captures: the build's
version and source SHA, the route and method, the trace identifier, the
person's name and role, the Case reference on screen, the exception the
Error page remembered for that trace, the person's own last twenty acts
from the action log, and the browser's window, agent, editing state and
last ten script errors. It never carries document content, images, e-mail
bodies or a claimant's personal data.

Every report is stored first, then raised as an issue on the configured public
`collisionengineers/pegasus` repository through the GitHub REST API by an
outbound-only sink. The public issue contains only the opaque local report ID;
the full description and snapshot remain visible to authorised staff in
Administration → Problem reports. A failed
raise leaves the row Not sent with the reason; an Administrator retries
from Administration → Problem reports. The token is a fine-grained personal
access token scoped to that repository with Issues read and write only,
held in Key Vault and read as configuration; it appears in no source, log or
document. The sink verifies the repository identity before creating the issue.

## Consequences

- One additive migration, one Core port pair (store and sink), one named
  HttpClient; no queue or background process. Web alone writes the table and
  is denied DELETE.
- Without the token and repository configured (DevelopmentOffline, or an
  environment not yet connected) every report is kept as Not sent with the
  reason, so nothing a person wrote is lost.
- Rotating the token is a Key Vault secret version change and a
  configuration read-back; no release is needed.
- Adding what a report carries is a Core change to the snapshot and this
  record's list, never a silent widening.

## Links

- [FRD-12 — Report a problem](../frd/frd-12-operator-experience.md#shell-and-routes)
- [FRD-17 — Problem reports](../frd/frd-17-administration-workspace.md#problem-reports)
- [Configuration reference](../engineering/configuration.md#problem-reports)

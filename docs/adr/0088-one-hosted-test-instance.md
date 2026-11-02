---
id: ADR-0088
status: accepted
date: 2026-11-02
supersedes: [ADR-0014]
superseded_by: []
related_capabilities: [OPS-10, OPS-11, OPS-28]
related_frd: []
tags: [environment, test-instance, linux, tailscale, graph]
---

# ADR-0088: One hosted test instance

## Status

Accepted on the operator's rulings of 2 November 2026 (R623 to R628). It
supersedes one clause of [ADR-0014](0014-local-to-production-deployment.md),
"There is no Azure development, test, integration, or staging environment", to
the extent of the hosted test instance below. The instance is not an Azure
environment; ADR-0014's prohibition on a non-production Azure environment,
its direct-terminal production route and its isolation of production from
local resources (OPS-10, OPS-11) stand.

## Context

Pegasus had two homes: isolated local development and production. Until the
cutover ruling (R603) production was itself the test estate between intake
wipes, and the UAT pack and proof rounds were written to run there. Production
now holds real Cases, so the operator needs a test version of Pegasus.

A second Azure environment would need the Bicep tree and every release,
smoke, bootstrap and wipe script decoupled from the fixed `pegasus-prod`
target, plus monthly spend. The repository already holds a hosted local test
instance: the `DevelopmentOffline` lifecycle (`scripts/Invoke-LocalDevelopment.ps1`)
runs on Linux with a per-run SQL Server container, Azurite and the Functions
host, and opts into live DVLA/DVSA, Box custody under the local-test root
`425169015650` and Glass's through an owner-only settings file (7 October
2026). It bound to localhost and composed file-based mailbox adapters. The
operator ruled for that lifecycle on an operator-owned Linux/WSL machine on
their Tailscale network, at no Azure cost (R623).

## Decision

- Pegasus has three runtime homes: local development, one hosted test
  instance, and production. The hosted test instance is a `DevelopmentOffline`
  run of the local lifecycle on an operator-owned Linux/WSL machine, reached
  over the operator's Tailscale network through `tailscale serve` at the
  public origin the run records (`-PublicOrigin`), with password sign-in.
  Nothing in Azure is created for it, and a step that would cost Azure money
  is a question to the operator first (R623).
- It serves the operator's and agents' QA walks, staff UAT rounds and
  release rehearsal (R624), with live DVLA/DVSA, Box custody under the
  local-test root, Glass's, and the classifier agent run locally in dry run
  first (R625).
- It reads the production mailboxes and writes nothing to them (R626).
  `Features:LiveGraphMail` composes Production's Graph read side (the mail
  client, the Inbox and Sent sources and their pollers, the mailbox resolver
  and search) over an Entra app registration whose Exchange RBAC grant is
  `Application Mail.Read` on those mailboxes alone. Staff send and report send
  stay `Unavailable`, the answered-instruction mover stays unavailable, and no
  change subscription is registered; the five-minute recovery poll carries
  mail, so the instance needs no webhook.
- Rehearsal data is a SqlPackage copy of production into the run's database
  with every approved mailbox deactivated on import (R627); each copy is a
  live production read the operator approves. The Box custody fence keeps
  the instance on the local-test root whatever the copied rows say.
- The instance proves local behaviour at the Linux runtime production uses.
  The evidence tiers in [engineering](../engineering.md#required-evidence-tiers)
  are unchanged: staff send, Graph subscriptions, OCR and deployment remain
  production proofs, as the
  [verification matrix](../engineering/local-live-verification.md) states.

## Consequences

- PRD exclusions BND-09 and BND-10 narrow to a separate Azure QA/test or
  UAT environment; OPS-28 names the hosted test instance (R628). The UAT
  pack's environment file, kept outside the repository, points at it.
- `AGENTS.md`'s Outlook clause gains the read-only reading of the production
  mailboxes; the approved-test-mailbox acceptance lines in FRD-21 and FRD-26
  stand, because sends are proved only on production.
- The procedure is the [`pegasus-test-instance`](../../.agents/skills/pegasus-test-instance/SKILL.md)
  skill. Its tooling is the lifecycle's `-PublicOrigin`, the
  `Features:LiveGraphMail` opt-in, and `scripts/Copy-ProductionDatabaseToRun.ps1`.
- The read-only Exchange grant is the second fence under the composition: a
  wrongly composed host could still not send or move mail. The app
  registration, its scope and the permitted and denied checks are recorded in
  [operations](../operations.md) when they exist.

## Links

- Superseded clause: [ADR-0014](0014-local-to-production-deployment.md).
- Workstations: [ADR-0039](0039-windows-and-linux-release-workstations.md).
- Rulings: R623 to R629 in [rulings](../rulings.md).
- Procedure: [runbook, local live-integration run](../runbook.md#local-live-integration-run).

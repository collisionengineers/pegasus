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

Amended 3 November 2026 (R673 to R678): the instance has full Graph parity
with production on the mailboxes the operator enables on it, read, send, the
answered-instruction move and change subscriptions, in place of R626's
read-only grant; see Decision. Amended 4 November 2026 (R691): the
instance's own mailbox is `pegasustest@collisionengineers.co.uk`, a shared
mailbox for the test environment alone; a production mailbox is enabled on
it only when the operator says so. Amended 4 November 2026 (R692 to R694):
the instance has one host and its tooling refuses any other machine; it
runs `dev` by default and a branch or pull request head when the operator
names it; its data is not disposable, so a Reset needs the operator's word
and no production extract is kept as a file.

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
  run of the local lifecycle on one operator-owned Linux/WSL machine, reached
  over the operator's Tailscale network through `tailscale serve` at the
  public origin the run records (`-PublicOrigin`), with password sign-in.
  The origin names the host: `scripts/PegasusPlatform.ps1` holds it, and
  `Start -PublicOrigin`, a restart of the run with its recorded origin and
  the production copy refuse any machine other than the Tailscale node that
  owns it (R692); moving the instance changes the constant, the Tailscale
  name and the operations record together. Nothing in Azure is created for
  it, and a step that would cost Azure money is a question to the operator
  first (R623).
- It serves the operator's and agents' QA walks, staff UAT rounds and
  release rehearsal (R624), with live DVLA/DVSA, Box custody under the
  local-test root, Glass's, and the classifier agent run locally in dry run
  first (R625). It runs `dev`, the branch a release is promoted from, so a
  change is vetted on it before `main` moves, and a branch or pull request
  head when the operator names one (R693); the release preflight refuses a
  candidate the instance is not serving (R679, R682), so it is back on
  `dev`'s tip before a release.
- It has Graph parity with production on its own shared mailbox,
  `pegasustest@collisionengineers.co.uk`, created for the test environment
  alone and never approved on production (R691), and on a production
  mailbox only while the operator enables it on the instance (R673, R675,
  superseding R626).
  `Features:LiveGraphMail` composes Production's Graph mail side (the mail
  client, the Inbox and Sent sources and their pollers, the mailbox resolver
  and search, staff and report send, and the answered-instruction move) over
  the tenant's existing `CollisionSpike Graph Intake` app registration, whose
  Exchange RBAC grant becomes `Application Mail.Read`, `Mail.ReadWrite` and
  `Mail.Send` on its existing scope `CollisionSpike-Intake-Prod` alone,
  widened to the test mailbox on 4 November 2026. A send from the instance
  is a real e-mail from that mailbox, and both the instance and production
  ingest the same incoming mail while a production mailbox is enabled on
  both.
- Change subscriptions are the instance's own, never production's: a Graph
  subscription is bound to the notification URL it was created with. Graph
  reaches the instance only through Tailscale Funnel on port 8443 for the one
  path `/hooks/microsoft-graph/mail` (R674); port 443 stays tailnet-only. The
  Worker maintains subscriptions only when `Graph:ChangeNotificationUrl` and
  `Graph:ChangeNotificationClientState` are both set; without them the
  five-minute recovery poll carries mail.
- Rehearsal data is a SqlPackage copy of production into the run's database
  with every approved mailbox deactivated on import (R627), the copied
  subscription rows cleared (R676), the copied Principal API credentials
  paused (R677) and the copied pending answered-instruction tidies closed
  (R678); each copy is a live production read the operator approves, into
  the instance's run on its host alone, and the extract is removed once
  published (R694). The Box custody fence keeps the instance on the
  local-test root whatever the copied rows say.
- The instance's data is not disposable (R694): staff UAT Cases, the mail
  `pegasustest@` brings in and a production copy are people's data. `Stop`
  keeps everything; `Reset` of the instance's run, or of any run holding a
  production copy, needs the operator's word for that run (`-Approve`) and
  refuses without it. Migrations go forward only, so a branch that adds a
  migration is served only when the operator accepts that the return to
  `dev` waits for the branch to land or needs a Reset.
- The Principal API and the Automation MCP run on the instance, tailnet-only:
  Principal keys are issued on the instance, the MCP uses development keys and
  the instance's own client secret, and no connector redirect exists (R677).
- The instance proves local behaviour at the Linux runtime production uses.
  The evidence tiers in [engineering](../engineering.md#required-evidence-tiers)
  are unchanged: staff send and Graph subscriptions can be rehearsed on the
  instance, but they, OCR and deployment remain production proofs, as the
  [verification matrix](../engineering/local-live-verification.md) states and
  the operator kept (R675).

## Consequences

- PRD exclusions BND-09 and BND-10 narrow to a separate Azure QA/test or
  UAT environment; OPS-28 names the hosted test instance (R628). The UAT
  pack's environment file, kept outside the repository, points at it.
- `AGENTS.md`'s Outlook clause names the instance's Graph parity on the
  mailboxes the operator enables on it; the approved-test-mailbox acceptance
  lines in FRD-21 and FRD-26 stand, because production remains the proof of
  record (R675).
- The procedure is the [`pegasus-test-instance`](../../.agents/skills/pegasus-test-instance/SKILL.md)
  skill. Its tooling is the lifecycle's `-PublicOrigin` and Reset's
  `-Approve`, the host gate in `scripts/PegasusPlatform.ps1`, the
  `Features:LiveGraphMail` opt-in with the two webhook keys, Funnel on the
  webhook path, and `scripts/Copy-ProductionDatabaseToRun.ps1`.
- The fences are no longer in the grant. They are the test mailbox being
  the instance's own, the operator enabling any production mailbox on the
  instance, the copy's deactivation and closures, the scope
  that admits only the named mailboxes, and Funnel limited to one path. The
  app registration, its scope, the permitted and denied checks and the Funnel
  origin are recorded in [operations](../operations.md) when they exist.

## Links

- Superseded clause: [ADR-0014](0014-local-to-production-deployment.md).
- Workstations: [ADR-0039](0039-windows-and-linux-release-workstations.md).
- Rulings: R623 to R629, R673 to R678 and R691 to R694 in [rulings](../rulings.md).
- Procedure: [runbook, local live-integration run](../runbook.md#local-live-integration-run).

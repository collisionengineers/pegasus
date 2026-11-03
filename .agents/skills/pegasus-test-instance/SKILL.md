---
name: pegasus-test-instance
description: Stand up, reach, update, seed, copy production data into and stop the one hosted Pegasus test instance (ADR-0088) - the DevelopmentOffline lifecycle on the operator's Linux/WSL machine, reached over Tailscale, with live DVLA/DVSA, Box under the local-test root, Glass's, Graph parity on the production mailboxes the operator enables on it (read, send, move, subscriptions), the Principal API and the Automation MCP, and the classifier agent in dry run. Use whenever the operator mentions the test instance, a test version, test server, dev server, staging, UAT environment, the WSL box, a Tailscale URL, release rehearsal on a copy of production, or wants Pegasus running anywhere that is not production, even if they never say "test instance". Not for production releases (pegasus-release), intake wipes (pegasus-wipe-intake-data), or a plain Offline run on a developer's own machine.
---

# The hosted Pegasus test instance

One instance, on the operator's own Linux/WSL machine, reached over their
Tailscale network. It is the `DevelopmentOffline` lifecycle the runbook owns
([local live-integration run](../../../docs/runbook.md#local-live-integration-run))
with every opt-in on, so the procedure here is the order of the runbook's
commands and the approvals between them. [ADR-0088](../../../docs/adr/0088-one-hosted-test-instance.md)
and rulings R623 to R635 decided its shape; this skill invents none of it.

What it is for (R624): the operator's and agents' QA walks, staff UAT rounds,
and rehearsing a release (migrations included) on a copy of production.

## What never happens here

- **Mail is real.** The instance has Graph parity with production on the
  mailboxes the operator enables on it (R630, R632): a send leaves the
  production mailbox and reaches its recipients, the answered-instruction
  move happens in the shared mailbox, and both the instance and production
  ingest the same incoming mail while a mailbox is enabled on both. Send only
  to addresses the operator names, and enable a production mailbox on the
  instance for the window the operator names.
- **No Azure spend.** Key Vault secret reads, Graph calls and the SqlPackage
  extract to a local file are the only Azure touches (R623). A step that would
  create or run anything in Azure is a question to the operator first.
- **Funnel for the Graph webhook path alone.** `tailscale serve` on 443 keeps
  the origin on the tailnet; `tailscale funnel` on 8443 exposes
  `/hooks/microsoft-graph/mail` and nothing else (R631). Nothing else is
  ever Funnelled.
- **No public origin without password sign-in.** `Start` refuses it; the
  automatic offline sign-in would make every tailnet caller the Administrator.
- **No Reset of the long-lived run without the operator saying so.** Reset
  removes the database with the run. Stop keeps everything.
- **No Box write outside the local-test root `425169015650`.** The custody
  fence enforces it; the holding folder is a child the operator created.

## Approvals

| Step | Needs | Why |
| --- | --- | --- |
| `New-LocalLiveSettings.ps1 -Approve` | the operator's `az` sign-in and their word | reads six Key Vault secrets from `pegasusprodkv252ow37g` (names printed first) |
| Graph identity | a Microsoft 365 administrator (the operator) | the existing `CollisionSpike Graph Intake` registration and scope `CollisionSpike-Intake-Prod`; adds the one role it lacks (`Application Mail.Send`) and the secret; recorded in operations |
| `tailscale funnel` of the webhook path | the operator | the one public path on the machine |
| `Copy-ProductionDatabaseToRun.ps1 -Approve` | the operator's word per copy | a live read of production SQL |
| Enabling a mailbox on the instance | the operator | names which production mailbox the instance reads from, sends from and subscribes to, and for how long |
| A send from the instance | the operator names the recipient | a real e-mail from a production mailbox |
| Anthropic key for the classifier | the operator supplies it | metered spend, not Azure |

Everything else (Initialize, Start, Status, Smoke, Stop, seeding from the
corpus, the verification walk) is ordinary local work.

## 1. Prepare the machine

Read [references/machine-prerequisites.md](references/machine-prerequisites.md)
once per machine. The short form: PowerShell 7.6.3+, .NET SDK 10.0.302,
Node 24, Functions Core Tools 4.12.1, Docker (the operator's user in the
`docker` group), the Azure CLI, SqlPackage, Tailscale, a clone of the
repository, and at least 8 GB for the WSL VM. `Invoke-Doctor.ps1 -Profile
Offline` prints the exact repair command for anything missing; it installs
nothing.

```powershell
git clone git@github.com:collisionengineers/pegasus.git ~/pegasus && cd ~/pegasus
git checkout <branch or sha>
pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline
pwsh ./scripts/Initialize-LocalDevelopment.ps1
```

Initialize needs a clean tree and pulls the pinned SQL Server image once.

## 2. Write the settings file

```powershell
pwsh ./scripts/New-LocalLiveSettings.ps1 -BoxHoldingFolderId <child of 425169015650>
pwsh ./scripts/New-LocalLiveSettings.ps1 -BoxHoldingFolderId <id> -AdministratorPassword (Read-Host -AsSecureString) -Approve
```

The first run prints the secret names and stops; the second, after the
operator's word, writes `artifacts/local-development/local.settings.env`
(mode 600) with live DVLA/DVSA, Box and Glass's on and password sign-in on.
Then append by hand, in the same file:

```text
Features__PrincipalApi=true
Features__AutomationMcp=true
AutomationMcp__UseDevelopmentKeys=true
AutomationMcp__ClientSecret=<32+ characters the operator chooses>
Features__LiveGraphMail=true
Graph__BaseUri=https://graph.microsoft.com/v1.0/
Graph__TenantId=<tenant id>
Graph__ClientId=5d37a155-2af8-4878-b96a-6faad5207137
Graph__ClientSecret=<its secret>
Graph__ChangeNotificationUrl=https://<host>.<tailnet>.ts.net:8443/hooks/microsoft-graph/mail
Graph__ChangeNotificationClientState=<at most 128 characters the operator chooses, not production's>
```

The Graph lines wait until the identity exists (section 4); start without
them if it does not yet, and the run uses the folder mailbox adapters. The two
`ChangeNotification` lines go together and wait until the webhook path is
Funnelled (section 3); without them the run polls and never subscribes. The
Principal API issues its keys on the instance (Administration > Contacts);
the MCP serves the local classifier and tailnet agents, with no connector
redirect (R634). Never put `AutomationMcp__PublicOrigin` or
`Glass__CallbackBaseUri` in the file: the lifecycle owns both and refuses
them.

## 3. Start it and reach it

```powershell
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start -WebPort 7139 -PublicOrigin https://<host>.<tailnet>.ts.net
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Status
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Smoke
```

Keep the run id Start prints: it is the instance for as long as it lives. In
front of it, once, on the Tailscale node that owns the name (the Windows host
through WSL localhost forwarding, or Tailscale inside WSL):

```bash
tailscale serve --bg --https=443 https+insecure://localhost:7139
tailscale funnel --bg --https=8443 --set-path /hooks/microsoft-graph/mail https+insecure://localhost:7139/hooks/microsoft-graph/mail
tailscale serve status
tailscale funnel status
```

`+insecure` is needed because Kestrel presents the development certificate;
the tailnet certificate is what the browser sees, so nothing is exported or
trusted by hand. The second command is the one public path (R631): `funnel
status` must list `:8443` with that path alone, and
`curl -sS -X POST 'https://<host>.<tailnet>.ts.net:8443/hooks/microsoft-graph/mail?validationToken=x'`
must print `x`. Prove the proxy keeps the host:

```bash
curl -sI https://<host>.<tailnet>.ts.net/Cases | grep -i '^location'
```

The redirect to `/Account/SignIn` must be on the `ts.net` host. If it is on
localhost, stop and say so: the Web would need a forwarded-headers branch for
the offline profile, which is a code change, not a configuration one.

Staff accounts for a UAT round are created in Administration > Accounts on
the instance; Glass's logins are per account (ADR-0043) and entered there.

## 4. Mail from the production mailboxes, with parity

Follow the runbook's
[admitting the test instance's Graph identity](../../../docs/runbook.md#runbook-admitting-the-test-instances-graph-identity)
with the Exchange and Funnel commands in
[references/graph-parity-identity.md](references/graph-parity-identity.md).
The operator runs the tenant steps; the agent prepares the exact commands and
the operations record. Then add the Graph lines to the settings file, restart
the run (`Stop`, then `Start -RunId <id>`), and enable the mailbox in
Administration > Mailboxes on the instance, Incoming and Outgoing as
production has it. The Worker log
(`artifacts/local-development/<run>/logs/worker-*.stdout.log`) shows the
instance's own subscription being created within five minutes, the poll, and
a wake on new mail:

```bash
grep -Ei 'graph.*subscriptions' artifacts/local-development/<run>/logs/worker-*.log
grep -i 'graph_subscription_maintenance_failed' artifacts/local-development/<run>/logs/worker-*.log
```

The first grep shows the `POST`; the second must be empty (a failure there
names the mailbox and is usually the Funnel path or the scope). A send from
the instance to an address the operator names then appears in that mailbox's
Sent Items and settles to Sent; after a report send, the log shows the
`/move` of its answered instruction. A mailbox row added before the Graph
lines existed holds a local identity: disable it and add the address again.

## 5. Seed and walk

```powershell
pwsh ./scripts/Invoke-LocalSeed.ps1 -CorpusRoot <path outside the repo> -RunId <id> -Email '<pattern>/*.eml' -Limit 20
pwsh ./scripts/Invoke-LocalVerification.ps1 -RunId <id> -User <administrator account> -UploadFiles <files>
```

The verification matrix
([local-live-verification](../../../docs/engineering/local-live-verification.md))
says what each row proves; results land in
`artifacts/local-verification/<run>/`. A browser walk from the Windows
machine uses the Tailscale origin.

## 6. Move to a new revision

```powershell
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Stop -RunId <id>
git fetch origin && git checkout <sha>          # clean tree
pwsh ./scripts/Initialize-LocalDevelopment.ps1
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start -RunId <id>
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Smoke -RunId <id>
```

Ports and the public origin come from the manifest; `--initialize-development`
migrates the existing database forward and keeps its data. Going back to an
older revision is not supported: migrations run forward only, and that needs
a Reset and a re-seed the operator agrees to.

## 7. Rehearse a release on a copy of production

```powershell
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Stop -RunId <id>
pwsh ./scripts/Copy-ProductionDatabaseToRun.ps1 -RunId <id>            # prints the plan, exit 2
pwsh ./scripts/Copy-ProductionDatabaseToRun.ps1 -RunId <id> -Approve   # after the operator's word
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start -RunId <id>   # migrates the copy forward
```

The copy is a SqlPackage extract of production to a local `.dacpac` under the
run's `state/`, published over the run database with users, logins, role
memberships, permissions and database options left out, every approved
mailbox deactivated (R627), the copied subscription rows cleared (R633), the
copied Principal API keys paused (R634) and the copied pending
answered-instruction tidies closed (R635), so the instance subscribes under
its own webhook, authenticates no production key and moves only what its own
sends answered. The record `state/production-copy-<utc>.json` holds the
migration head, the Case count and the four counts. The
rehearsal is then the next Start: the checked-out revision's migrations run
against production's schema and data. The first run of this script on a
machine is itself a test of the SqlPackage route; read its output before
trusting the copy.

## 8. The classifier agent, locally

Read [references/classifier-local-run.md](references/classifier-local-run.md).
It runs one sweep per `npm start` against the instance's `/mcp` on the public
origin, in dry run first (`CLASSIFIER_DRY_RUN=true`), with the operator's
Anthropic key in an ignored `.env`.

## 9. Stop

```powershell
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Stop -RunId <id>
tailscale funnel reset       # only when the instance is retired; the webhook path goes first
tailscale serve reset
```

## The record

When the identity is admitted and when the instance first runs, write a
dated record in [operations](../../../docs/operations.md): the machine, the
public origin, the Funnel origin and path, the run id, the app registration
(client id, scope, the roles including the added `Mail.Send`, the permitted
and denied checks), and
which mailboxes the instance reads from and sends from. Secret values are
never recorded.

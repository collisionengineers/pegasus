---
name: pegasus-test-instance
description: Reach, update, point at a branch or pull request, seed, copy production data into and stop the one hosted Pegasus test instance (ADR-0088) - the DevelopmentOffline lifecycle on the operator's own WSL box and no other machine, reached over Tailscale, with live DVLA/DVSA, Box under the local-test root, Glass's, Graph parity on its own mailbox pegasustest@ and on the production mailboxes the operator enables on it (read, send, move, subscriptions), the Principal API and the Automation MCP, and the classifier agent in dry run. Use whenever the operator mentions the test instance, a test version, test server, dev server, staging, UAT environment, the WSL box, a Tailscale URL, release rehearsal on a copy of production, or wants Pegasus running anywhere that is not production, even if they never say "test instance". Not for production releases (pegasus-release), intake wipes (pegasus-wipe-intake-data), or a plain Offline run on a developer's own machine.
---

# The hosted Pegasus test instance

One instance, on one machine: the operator's own WSL box, reached over their
Tailscale network. It is the `DevelopmentOffline` lifecycle the runbook owns
([local live-integration run](../../../docs/runbook.md#local-live-integration-run))
with every opt-in on, so the procedure here is the order of the runbook's
commands and the approvals between them. [ADR-0088](../../../docs/adr/0088-one-hosted-test-instance.md)
and rulings R623 to R680 and R693 to R696 decided its shape; this skill
invents none of it.

What it is for (R624): the operator's and agents' QA walks, staff UAT rounds,
and rehearsing a release (migrations included) on a copy of production. Its
data is people's, not a disposable estate's (R696).

## Where it runs, and nowhere else

The host is the Tailscale node that owns the instance's origin,
`https://cemattypc-1.tail432b53.ts.net/`, the operator's WSL box recorded in
[operations](../../../docs/operations.md) (2 November 2026). The instance is
usually running there, so `Status` comes before anything and a second
`Start` never happens. First, on every machine this skill is opened on:

```bash
tailscale status --json | python3 -c 'import json,sys; print(json.load(sys.stdin)["Self"]["DNSName"])'
```

It must print `cemattypc-1.tail432b53.ts.net.`. Anything else, Windows
included, means this is not the host: stop here and say so (R694). The
scripts refuse by themselves (`Assert-PegasusTestInstanceHost` in
`scripts/PegasusPlatform.ps1`: `Start -PublicOrigin`, a restart of the run
with its recorded origin, and the production copy), and a plain Offline run
on a developer's own machine is the runbook's, not this skill's. Moving the
instance to another machine is an operator decision: the origin constant in
`PegasusPlatform.ps1`, the Tailscale name and the operations record change
together, with a ruling.

## What never happens here

- **Mail is real.** The instance's mailbox is
  `pegasustest@collisionengineers.co.uk`, a shared mailbox for the test
  environment alone, never approved on production (R693), and the instance
  has Graph parity with production on it and on any production mailbox the
  operator enables on the instance (R675, R677): a send leaves the mailbox
  and reaches its recipients, the answered-instruction move happens in that
  mailbox, and both the instance and production ingest the same incoming
  mail while a production mailbox is enabled on both. Send only to addresses
  the operator names, and enable a production mailbox on the instance for
  the window the operator names.
- **No Azure spend.** Key Vault secret reads, Graph calls and the SqlPackage
  extract to a local file are the only Azure touches (R623). A step that would
  create or run anything in Azure is a question to the operator first.
- **Funnel for the Graph webhook path alone.** `tailscale serve` on 443 keeps
  the origin on the tailnet; `tailscale funnel` on 8443 exposes
  `/hooks/microsoft-graph/mail` and nothing else (R676). Nothing else is
  ever Funnelled.
- **No public origin without password sign-in.** `Start` refuses it; the
  automatic offline sign-in would make every tailnet caller the Administrator.
- **The data is not disposable (R696).** Staff UAT Cases, the Cases intake
  created from `pegasustest@` and a production copy are people's data.
  `Reset` removes the database with the run and migrations go forward only,
  so `Reset` of this run needs `-Approve` after the operator's word for this
  exact run, and refuses without it, saying what it would remove. `Stop`
  keeps everything. The production extract is removed once published; no
  file of production's data is kept on the machine.
- **No Box write outside the local-test root `425169015650`.** The custody
  fence enforces it; the holding folder is a child the operator created.

## Approvals

| Step | Needs | Why |
| --- | --- | --- |
| `New-LocalLiveSettings.ps1 -Approve` | the operator's `az` sign-in and their word | reads six Key Vault secrets from `pegasusprodkv252ow37g` (names printed first) |
| Graph identity | a Microsoft 365 administrator (the operator) | the existing `CollisionSpike Graph Intake` registration and scope `CollisionSpike-Intake-Prod`; adds the one role it lacks (`Application Mail.Send`) and the secret; recorded in operations |
| `tailscale funnel` of the webhook path | the operator | the one public path on the machine |
| `Copy-ProductionDatabaseToRun.ps1 -Approve` | the operator's word per copy | a live read of production SQL, into this run on this host alone |
| `Invoke-LocalDevelopment.ps1 -Action Reset -Approve` | the operator's word for this run | removes the database and the run root; nothing brings the data back (R696) |
| Serving a branch or pull request | the operator names it | the instance leaves `dev`'s tip (R695, section 6) |
| Enabling a production mailbox on the instance | the operator | names which production mailbox the instance also reads from, sends from and subscribes to, and for how long; `pegasustest@` is the instance's own (R693) |
| A send from the instance | the operator names the recipient | a real e-mail from `pegasustest@` or an enabled production mailbox |
| Anthropic key for the classifier | the operator supplies it | metered spend, not Azure |

Everything else (Initialize, Start, Status, Smoke, Stop, seeding from the
corpus, the verification walk) is ordinary local work.

## 1. The host's tools

Read [references/machine-prerequisites.md](references/machine-prerequisites.md)
when something on the host is missing or stale. The short form: PowerShell
7.6.3+, .NET SDK 10.0.302, Node 24, Functions Core Tools 4.12.1, Docker (the
operator's user in the `docker` group), the Azure CLI, SqlPackage, Tailscale
and at least 8 GB for the WSL VM. `Invoke-Doctor.ps1 -Profile Offline`
prints the exact repair command for anything missing; it installs nothing.
The checkout the instance runs from is the host's primary clone (the
operations record); it has been initialized, and Initialize is repeated at
every move (section 6).

```powershell
pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Status
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
redirect (R679). Never put `AutomationMcp__PublicOrigin` or
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
trusted by hand. The second command is the one public path (R676): `funnel
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
the instance; Glass's logins are per account (ADR-0043) and entered there. The
round itself (#1863) runs on the date the operator sets: the pack's
`environment.md`, kept outside the repository, names the instance's public
origin, and the round's results file is recorded in operations as the first
operator acceptance.

## 4. Mail, with parity

Follow the runbook's
[admitting the test instance's Graph identity](../../../docs/runbook.md#runbook-admitting-the-test-instances-graph-identity)
with the Exchange and Funnel commands in
[references/graph-parity-identity.md](references/graph-parity-identity.md).
The operator runs the tenant steps; the agent prepares the exact commands and
the operations record. Then add the Graph lines to the settings file, restart
the run (`Stop`, then `Start -RunId <id>`), and add
`pegasustest@collisionengineers.co.uk` in Administration > Mailboxes on the
instance, Incoming and Outgoing (R693); a production mailbox the operator
enables is added the same way, as production has it. The Worker log
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

## 6. Follow `dev`, or serve a branch or pull request when asked

The instance runs `dev` by default (R681): after each merge into `dev`, the
agent on the host when asked, or the operator, moves it. When the operator
names a branch or a pull request, it serves that head instead (R695): the
same move, a different revision, and `dev`'s tip again afterwards.

```powershell
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Stop -RunId <id>
git checkout dev && git pull --ff-only origin dev                 # dev's tip, clean tree
git fetch origin pull/<n>/head && git checkout --detach FETCH_HEAD   # or a pull request's head
git fetch origin <branch> && git checkout --detach FETCH_HEAD        # or a branch's tip
pwsh ./scripts/Initialize-LocalDevelopment.ps1
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start -RunId <id>
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Smoke -RunId <id>
```

Before serving a head that is not `dev`, read what it changes in the schema:

```bash
git diff --name-only origin/dev...FETCH_HEAD -- src/Pegasus.Infrastructure/Persistence/Migrations/
```

Empty: the swap is free, and the return to `dev` is the same move. Not
empty: `--initialize-development` migrates the run's database forward to
that branch's head, and migrations go forward only, so the return to `dev`
waits until the branch lands, or needs a `Reset` (`-Approve`, then a re-seed
or a fresh production copy). Say which before the swap; the swap is on the
operator's word.

Ports and the public origin come from the manifest; `--initialize-development`
migrates the existing database forward and keeps its data. Status prints
`SourceSha`; the instance is current when it equals `git rev-parse origin/dev`
or the named head. The release preflight refuses a candidate the instance is
not serving (R684), so the instance is back on `dev`'s tip before a release.
Its `Version` reads `0.0.0-dev`, as in every build that is not a release
(R686); the SHA is its identity. Whoever walks the instance is told which
head it serves; the UAT pack's `environment.md` names it.

## 7. Rehearse a release on a copy of production

```powershell
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Stop -RunId <id>
pwsh ./scripts/Copy-ProductionDatabaseToRun.ps1 -RunId <id>            # prints the plan, exit 2
pwsh ./scripts/Copy-ProductionDatabaseToRun.ps1 -RunId <id> -Approve   # after the operator's word
pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start -RunId <id>   # migrates the copy forward
```

The copy runs only into this run on this host (R694). It is a SqlPackage
extract of production to a `.dacpac` under the run's `state/`, published over
the run database with users, logins, role memberships, permissions and
database options left out, every approved
mailbox deactivated (R627), the copied subscription rows cleared (R678), the
copied Principal API keys paused (R679) and the copied pending
answered-instruction tidies closed (R680), so the instance subscribes under
its own webhook, authenticates no production key and moves only what its own
sends answered. The extract is removed once published, whether or not the publish succeeded,
so the run database is the only copy of production on the machine (R696);
the record `state/production-copy-<utc>.json` holds the extract's size and
SHA-256, the migration head, the Case count and the four counts. The copy carries
production's mailbox rows and not the instance's, so `pegasustest@` is added
again in Administration > Mailboxes after the next Start (R693). The
rehearsal is then the next Start: the checked-out revision's migrations run
against production's schema and data. The rehearsal runs at the candidate's
tip, the branch about to land on `dev` (checked out detached at its SHA when
the branch lives in another worktree) or `dev` itself; its record in
[operations](../../../docs/operations.md) names the copy record, the Case
count, the migration head before and after, and the walk's result (#1864).
The first run on this machine (3 November 2026,
[operations](../../../docs/operations.md)) proved the SqlPackage route; on
another machine the first run is again a test of it, so read its output
before trusting the copy.

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

Stop keeps the database and every file. `Reset` is not a way of stopping:
it needs `-Approve` after the operator's word for this run (R696).

## The record

When the identity is admitted, when the instance first runs and when it first
runs from `dev`'s tip, write a dated record in
[operations](../../../docs/operations.md): the machine, the
public origin, the Funnel origin and path, the run id, the app registration
(client id, scope, the roles including the added `Mail.Send`, the permitted
and denied checks), and
which mailboxes the instance reads from and sends from. Secret values are
never recorded.

# The test instance's Graph identity

The instance has Graph parity with production on the production mailboxes
the operator enables on it: read, send, the answered-instruction move and
change subscriptions (R648, R650). The hosts compose Production's Graph mail
side under `Features:LiveGraphMail`, and the identity they present holds the
three Exchange application roles on the named mailboxes and nothing on any
other. This page is the identity. The runbook section
[admitting the test instance's Graph identity](../../../../docs/runbook.md#runbook-admitting-the-test-instances-graph-identity)
is the procedure; the production mailboxes' own admission is the section
before it.

A Microsoft 365 administrator (the operator) runs these. The agent prepares
them with the real values filled in and writes the operations record
afterwards; it does not run them.

## 1. The app registration: the one that exists

The tenant already holds an app registration admitted to the production
mailboxes through Exchange RBAC: **CollisionSpike Graph Intake**, application
(client) id `5d37a155-2af8-4878-b96a-6faad5207137`, with no API permissions
in its manifest and one client secret (expiry 26 June 2027). Checked on
3 November 2026, its service principal holds `Application Mail.Read` and
`Application Mail.ReadWrite` on the management scope
`CollisionSpike-Intake-Prod`, which covers
`instructions@collisionengineers.co.uk` (`InScope True`); a second scope,
`CS-Intake-EngDigital`, does not. The instance reuses this registration and
this scope. No new registration and no new scope is created, by the
subtract-before-add rule.

The production Web and Worker reach Graph as Azure managed identities, which
cannot issue tokens outside Azure; that is why the instance needs a
registration with a secret at all. The instance has its own secret on the
registration, `pegasus-test-instance (2026-11-03)`, added with
`az ad app credential reset --append` so the orchestrator's secret stays; the
value went straight into the settings file and is recorded nowhere else.

## 2. The one missing role assignment

The identity lacks `Application Mail.Send`. Production's Web identity holds
it; the instance's staff and report send cannot work without it.

```powershell
Import-Module ExchangeOnlineManagement
Connect-ExchangeOnline -Device

New-ManagementRoleAssignment -App 5d37a155-2af8-4878-b96a-6faad5207137 `
    -Role 'Application Mail.Send' -CustomResourceScope 'CollisionSpike-Intake-Prod'
```

Prove it, and record both answers:

```powershell
Get-ManagementScope CollisionSpike-Intake-Prod | Format-List Name, RecipientFilter
Test-ServicePrincipalAuthorization -Identity 5d37a155-2af8-4878-b96a-6faad5207137 -Resource instructions@collisionengineers.co.uk
Test-ServicePrincipalAuthorization -Identity 5d37a155-2af8-4878-b96a-6faad5207137 -Resource <a mailbox outside the filter>
```

A mailbox the filter covers lists `Mail.Read`, `Mail.ReadWrite` and
`Mail.Send` with `InScope True` on `CollisionSpike-Intake-Prod`; one outside
it lists nothing in scope. On 3 November 2026 the filter was exactly the
four approved mailboxes, info@, engineers@, desk@ and instructions@, so any
of the four can be enabled on the instance with no Exchange change; a
denied-mailbox check uses a staff mailbox.

`Connect-ExchangeOnline` accepts the operator's `az` sign-in without a
browser round trip:
`az account get-access-token --resource https://outlook.office365.com`
and `-AccessToken <token> -Organization collisionengineers.co.uk`. (`Get-ManagementRoleAssignment` has no `-App`
parameter in ExchangeOnlineManagement 3.10; `Test-ServicePrincipalAuthorization`
is the query.) Admitting another mailbox to the instance means widening
`CollisionSpike-Intake-Prod` with `Set-ManagementScope`, which widens it for
every identity on that scope, so it is a separate approval. The operations
record names the tenant, the client id, the scope, the roles, the
administrator, the time, and both results.

## 3. The webhook path, public through Funnel

Graph delivers change notifications only to a URL it can reach from the
internet, so the webhook path alone is Funnelled, on its own port; port 443
stays tailnet-only (R649). On the Tailscale node that owns the name:

```bash
tailscale funnel --bg --https=8443 --set-path /hooks/microsoft-graph/mail https+insecure://localhost:7139/hooks/microsoft-graph/mail
tailscale funnel status
curl -sS -X POST 'https://<host>.<tailnet>.ts.net:8443/hooks/microsoft-graph/mail?validationToken=x'
```

`funnel status` must list only `:8443` with that path as public, and the
`curl` must print `x`: that is Graph's own validation handshake. The endpoint
is anonymous by design and accepts only notifications carrying the client
state below, the tenant id, and a subscription id the instance itself holds.

## 4. The settings lines

On the instance's machine, append to `artifacts/local-development/local.settings.env`:

```text
Features__LiveGraphMail=true
Graph__BaseUri=https://graph.microsoft.com/v1.0/
Graph__TenantId=<tenant id>
Graph__ClientId=<client id>
Graph__ClientSecret=<secret>
Graph__ChangeNotificationUrl=https://<host>.<tailnet>.ts.net:8443/hooks/microsoft-graph/mail
Graph__ChangeNotificationClientState=<at most 128 characters the operator chooses, not production's>
```

The two `ChangeNotification` lines go together: one without the other fails
the start naming the missing key; neither leaves the five-minute recovery
poll to carry mail with no subscription. Restart the run, then enable the
mailbox in Administration > Mailboxes on the instance, Incoming and Outgoing
as production has it. The Web's access check is a read; the Worker's next
five-minute pass creates the instance's own subscription and polls. Mail
received before the activation time is never re-ingested, by design.

## What this identity does, by construction

- Reads the Inbox and Sent Items of the mailboxes in scope (`Mail.Read`).
- Sends staff and report mail from them (`Mail.Send`): every send is a real
  e-mail from a production mailbox to its recipients.
- Moves the answered instruction to Deleted Items once a report send on the
  instance is confirmed (`Mail.ReadWrite`): a real move in the shared mailbox.
- Creates and renews its own change subscriptions on the scope's Inboxes
  (`Mail.Read`), under its own client state; it never renews or removes
  production's, whose ids it does not hold.
- Touches no mailbox outside the scope filter: the scope refuses it, and the
  instance reads the address as not found.

When the secret nears expiry, a new secret on the same registration and a new
`Graph__ClientSecret` line are the whole rotation; the orchestrator's own
secret on that registration is not touched.

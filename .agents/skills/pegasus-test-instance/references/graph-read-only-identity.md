# The test instance's read-only Graph identity

The instance reads the production mailboxes and writes nothing to them
(R626). Two fences make that true: the hosts compose only the Graph read side
under `Features:LiveGraphMail`, and the identity they present can only read.
This page is the second fence. The runbook section
[admitting the test instance's read-only identity](../../../../docs/runbook.md#runbook-admitting-the-test-instances-read-only-identity)
is the procedure; the production mailboxes' own admission is the section
before it.

A Microsoft 365 administrator (the operator) runs these. The agent prepares
them with the real values filled in and writes the operations record
afterwards; it does not run them.

## 1. The app registration

In Entra ID, create one app registration named for the instance (for example
`pegasus-test-instance`), single tenant, no redirect URI, with one client
secret. Leave **API permissions** empty, and remove the default
`User.Read` delegated permission: Exchange RBAC below is additive to any
tenant-wide grant, and the point is that this identity holds nothing else.
Note the application (client) id, the tenant id and the secret's expiry.

## 2. The Exchange role assignment, read-only

```powershell
Connect-ExchangeOnline

# The registration's service principal, as Exchange sees it.
New-ServicePrincipal -AppId <client id> -ObjectId <enterprise application object id> -DisplayName 'pegasus-test-instance'

# The mailboxes the instance may read: instructions@ and any other the operator approves.
New-ManagementScope -Name 'Pegasus test instance mailboxes' `
    -RecipientRestrictionFilter "PrimarySmtpAddress -eq 'instructions@collisionengineers.co.uk'"

# Read only. No Mail.ReadWrite, no Mail.Send.
New-ManagementRoleAssignment -App <client id> -Role 'Application Mail.Read' `
    -CustomResourceScope 'Pegasus test instance mailboxes'
```

Prove it, and record both answers:

```powershell
Test-ServicePrincipalAuthorization -Identity <client id> -Resource instructions@collisionengineers.co.uk
Test-ServicePrincipalAuthorization -Identity <client id> -Resource desk@collisionengineers.co.uk
```

The permitted mailbox lists `Mail.Read` alone (`InScope True`); the denied
mailbox lists nothing in scope. The operations record names the tenant, the
client id, the scope filter, the administrator, the time, and both results.

## 3. The settings lines

On the instance's machine, append to `artifacts/local-development/local.settings.env`:

```text
Features__LiveGraphMail=true
Graph__BaseUri=https://graph.microsoft.com/v1.0/
Graph__TenantId=<tenant id>
Graph__ClientId=<client id>
Graph__ClientSecret=<secret>
```

Restart the run, then enable the mailbox in Administration > Mailboxes on the
instance, Incoming only. The Web's access check is a read; the Worker's
five-minute poll then carries new mail. Mail received before the activation
time is never re-ingested, by design.

## What this identity cannot do, by construction

- Send, reply or forward (`Mail.Send`, `Mail.ReadWrite`): not granted.
- Move the answered instruction (`Mail.ReadWrite`): not granted, and the
  mover is not composed.
- Create or renew a change subscription: not composed; the role would allow
  the read subscription, so the composition is the fence here.
- Touch any mailbox outside the scope filter: the scope refuses it, and the
  instance reads the address as not found.

When the secret nears expiry, a new secret on the same registration and a new
`Graph__ClientSecret` line are the whole rotation.

---
id: ADR-0092
status: accepted
date: 2026-11-26
supersedes: [ADR-0027, ADR-0011]
superseded_by: []
related_capabilities: [ACC-01, ACC-03, MCP-01]
related_frd: [FRD-04, FRD-10, FRD-14]
tags: [oauth, sign-in, pegasus-desktop, mcp]
---

# ADR-0092: Staff sign-in for Pegasus Desktop

## Status

Accepted on the operator's rulings of 24 and 26 November 2026 (R1237; R1297
to R1299). It replaces one clause of
[ADR-0027](0027-authorization-code-for-external-mcp-connectors.md), "per-staff
MCP tokens remain excluded", for one client, Pegasus Desktop. Every other
clause of ADR-0027 stands, and so does
[ADR-0087](0087-one-automation-mcp-server-with-per-scope-consent.md): the
Automation client, its Administrator consent and its tokens are unchanged.
[ADR-0011](0011-restrict-mcp-to-automation-actor.md) was unchanged by this
record as first accepted, when no tool acted for a member of staff.

Amended 26 November 2026 (R1300): decision 7 is built. A Pegasus Desktop
token now resolves to the member of staff it names, every connector tool acts
as that person, and ADR-0011's "ordinary staff have no MCP access" is amended
for this one client.

## Context

Pegasus Desktop (repository `collisionengineers/pegasus-desktop`) is a Windows
application in which staff do market research and, in time, other work on a
Case (issue #2166). Until this record an outside application could act in
Pegasus only as the Automation Actor: the one Automation client's tokens name
the client, whichever grant issued them, and ADR-0027 excluded tokens that
name a member of staff.

The operator ruled that a member of staff signs in from Pegasus Desktop with
their own Pegasus details, through the system browser, and that what the
application then does is theirs (R1237). Also decided on 26 November 2026,
and built after this record under the same issue: the application may call
every connector tool the person could do in Pegasus, sending included, under
the person's name; and how the application shows and files a market research
report, which those changes record.

Observed at `52249e648` (26 November 2026). The authorization server is
OpenIddict 7.6 with one seeded confidential client. Codes, access tokens and
refresh tokens are all encrypted, and access tokens last ten minutes. A staff
session ends when the account's security stamp moves: a disable, a role
change, a password reset and a forced sign-out each move it, and each staff
request compares the cookie's stamp with the account
([FRD-04](../frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix)).
The Web's database role may not delete an OpenIddict registration. In
OpenIddict a native application's loopback redirect URI that is registered
without a port matches the same address on any port (RFC 8252 section 7.3);
one registered with a port matches that port only.

## Decision

1. **A second client, public and native.** Pegasus Desktop is the client
   `pegasus-desktop`: a public client with no secret, of the native
   application type. It may use the authorization endpoint, the token endpoint
   and the revocation endpoint, the authorization-code grant with PKCE and the
   refresh-token grant, and it is permitted every automation scope and the
   `/mcp` resource. It has no client-credentials grant, so it can never obtain
   a token that names no person.
2. **Offered only where configured.** The registration follows one setting,
   `AutomationMcp:DesktopRedirectUris`: plain `http` on a loopback IP address
   (`127.0.0.1` or `[::1]`), never a host name. An entry without a port admits
   whichever port the application is listening on; an entry with a port admits
   that port only. With the setting absent there is no such client, and a
   registration left by an earlier configuration is rewritten to permit
   nothing. No deployment definition under `infra/` names the setting, so
   production offers no sign-in from Pegasus Desktop until the operator rules
   otherwise (R1297). There is no row for Pegasus Desktop on the Integrations
   page.
3. **Any enabled member of staff signs in.** `/authorize` stays the one
   authorization endpoint and branches on the validated client. For the
   Automation client it is the Administrator consent, unchanged. For Pegasus
   Desktop it is a sign-in page any enabled member of staff reaches, of any
   role, after signing in to Pegasus in the browser: it names the account,
   says what the application will do, and offers Sign in and Refuse, in the
   operator's words (R1299). There are no scope tick boxes: the token holds
   every scope, and what a call may do is decided by the person's own rights.
   Either answer is permanent history under the person's name.
4. **The token names the person.** A Pegasus Desktop code, access token and
   refresh token carry the member of staff's id as their subject, the
   sign-in's own grant identity (`desktop:` and an identifier), and the
   security stamp the account had at the sign-in. Their audience is the
   Automation tokens'. A token that names a member of staff is never read as
   the Automation Actor.
5. **Checked against the account each time.** A code or a refresh token
   becomes tokens only while the account is enabled, holds a staff role, is
   not waiting on a password change and still has the stamp the token carries.
   The read is the one the staff cookie uses, which every account change made
   in Pegasus evicts, so a disable, a role change, a password reset and a
   forced sign-out end the sign-in at the next renewal. A request that
   presents the access token is checked the same way where a tool acts for
   the person (decision 7).
6. **Eight hours, and revocation.** The refresh token lasts eight hours from
   the sign-in and does not slide: a renewal keeps the first token's end, and
   the access token issued with a late renewal ends with it (R1298). The
   access token stays ten minutes. The application's own sign-out revokes its
   refresh token at `/connect/revoke` (RFC 7009); the Automation client holds
   no permission for that endpoint.
7. **Every tool acts as the member of staff (R1300; built 26 November
   2026).** As first accepted this record delivered the sign-in alone, and
   every tool and both content routes refused a Pegasus Desktop token. Now
   the token resolves to the member of staff it names, at every tool and both
   content routes, after the per-request check of decision 5: the account
   enabled, not waiting on a password change, holding exactly one staff role,
   with the stamp the token carries. The actor is that person with the role
   the account holds at that moment, never one a token carries, so their own
   rights decide what a call may do: every role does casework, sending
   included, and only an Administrator holds the management rights, which no
   tool reaches. Each call is recorded under their name, in Pegasus Desktop's
   area of the Action logs, and what they change carries staff labels, not
   Automation ones. A call the check refuses is recorded as
   `desktop_access_denied` under the person, which is not counted as
   Automation activity. The Automation client's Stop switch is not read for a
   Pegasus Desktop token. `pegasus_session_get` answers who a connection
   acts as. Pegasus Desktop puts each change on a card that names the person,
   which they confirm before it is made; that is the application's rule, and
   Pegasus marks each tool that only reads as read-only so the application
   can tell. [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary)
   owns the behaviour.

## Consequences

- A member of staff who is disabled, reset, given another role or signed out
  by an Administrator cannot renew the application's sign-in, and the access
  token they still hold, ten minutes at most, is refused at the next call by
  the per-request check of decision 5.
- A tool's read-only mark decides whether Pegasus Desktop asks before calling
  it, so a wrong mark is a change with no card. An architecture test holds
  each mark to the tool's inputs and body.
- Case details a member of staff reaches this way may be sent by Pegasus
  Desktop to its AI providers, which the operator accepted on 26 November
  2026 (R1301). That is the application's behaviour, not Pegasus's.
- A public native client cannot prove which program it is. Another program on
  the same computer could start the same sign-in; the page is what stands
  between it and a token, which is why the page always asks and never signs
  in silently, and why its answer is recorded.
- The staff cookie's account read moves to `StaffAccountReader` and gains the
  account's role, one more statement each time the read is made and not on a
  request served from `StaffAccountReads`, so the cookie and the token share
  one cached entry.
- A refused token request from Pegasus Desktop's client is recorded as
  `desktop_token_rejected`, not `automation_token_rejected`, so it does not
  read as Automation activity.
- No data change and no migration: OpenIddict's tables hold the registration,
  the authorizations and the tokens.
- The hosted test instance offers the sign-in when its settings file carries
  the line; production does not.

## Options considered

**A row on the Integrations page, or an Administrator's switch.** Rejected
(R1297): the sign-in is on where its configuration is present and there is no
row for it.

**Fixed ports in the registration.** The application tries ports 51793, 56427
and 62031 in that order. Listing the three addresses works, and a deployment
may still do so; the registration without a port is preferred because the
application's ports can then change without a change to Pegasus.

**A hash of the security stamp in the token.** Needed only if tokens were
signed and not encrypted. They are encrypted, so the token carries the stamp
as the staff cookie does.

**Deleting the registration when the setting is removed.** Not available: the
Web's database role is denied `DELETE` on the OpenIddict tables. The
registration is rewritten to permit nothing.

**A sliding sign-in.** Rejected (R1298): eight hours, not extended by use.
The staff session's absolute lifetime is also eight hours.

## Links

- Rulings: R1237, R1297, R1298, R1299, R1300 and R1301 in [rulings](../rulings.md).
- [FRD-04](../frd/frd-04-parties-accounts-and-access.md#staff-role-access-matrix),
  [FRD-10](../frd/frd-10-mcp-automation-and-actor-boundary.md#mcp-automation-and-actor-boundary)
- [ADR-0027](0027-authorization-code-for-external-mcp-connectors.md),
  [ADR-0087](0087-one-automation-mcp-server-with-per-scope-consent.md),
  [ADR-0011](0011-restrict-mcp-to-automation-actor.md)
- [Current architecture](../current-architecture.md)

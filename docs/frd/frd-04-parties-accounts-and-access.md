# FRD-04: Parties, accounts, and access
> Owner capabilities: ACC (staff roles/access, permanent action history) · Source PRD: [Pegasus product requirements](../prd/pegasus-product.md) · UI behaviour: docs/design/README.md

## Parties, principals, organisations, accounts, and access

Pegasus distinguishes principals, reusable organisations, staff accounts, roles, and case-party roles. A repairer, broker, agent, client, legal representative, provider, vehicle keeper, or other contact may occupy different roles on different cases. Reusable repairer-directory identity is separate from the inspection address and role snapshot retained by each historical case; raw provider/contact workbooks are evidence, not import authority.

A Repairer directory records its name, full address, and contacts. A Repairer
may relate to multiple Principals, and a Principal may relate to multiple
Repairers; these reusable relationships do not rewrite the accepted address or
party-role snapshot on an existing Case.

### Staff role access matrix

Staff accounts use Pegasus-managed usernames and passwords with non-reversible password hashes until a separately accepted identity change supersedes that route.

| Staff role | May view | May create or change | Must not access or perform |
| --- | --- | --- | --- |
| `Administrator` | All authorised application data and settings | Every ordinary Intake, Triage, Case, document, evidence, task, transition, and pre-assignment review action; staff account creation/disable/delete access/force logout/role assignment/password reset (D15); the Sign-off Engineer account setting (D31); principals and successor cutover, including a Principal’s Provider API credential lifecycle; workflow configuration, including labour-rate-card administration (D17); approved-mailbox allowlist; accepted OAuth-client registration/revocation | Pegasus’s own credential-secret, cloud, or release administration through the staff UI; permanent deletion; a generic mailbox-rule editor before its policy is accepted |
| `Engineer` | Cases, inbox items, documents, evidence, and details | Every authorised Intake, Triage, Case, document, evidence, task, transition, and pre-assignment review action | Accounts, roles, principals, successor cutover, workflow configuration, mailbox allowlist, authentication-client administration, credentials, cloud/release administration, or permanent deletion |
| `User` | Cases, inbox items, documents, evidence, and details | Every authorised Intake, Triage, Case, document, evidence, task, transition, and pre-assignment review action | Accounts, roles, principals, successor cutover, workflow configuration, mailbox allowlist, authentication-client administration, credentials, cloud/release administration, or permanent deletion |

Andrew and Alex are the initial `Administrator` assignments held in application data/configuration. No person, name, email address, or bypass is hard-coded into authorization. Automated processing uses a distinct durable machine identity and only named Core actions; it is not a staff account or an independent policy owner.

Authorization is enforced in Core use cases and at every caller boundary. It fails closed without revealing case or source data. Immutable principal/reference, source, association, history, and closed-case rules apply regardless of administrative privilege. Development routes and data never confer production access.

### Principals administration

One **Principals** administration area lists every principal code with its
organisation name, roles, state, and a Settings action. The organisation
remains the reusable directory identity and the owner of case-party roles; an
intermediary-only organisation appears as a row with no principal code.
**Create Principal** creates the backing organisation inline (name and roles)
and allocates the code in one action; successor cutover follows the
[principal-code replacement rule](frd-01-case-identity-and-lifecycle.md#principal-reference-organisation-and-case-party-identity).
Visuals and controls are owned by the
[design README](../design/README.md#operator-experience-requirements).

The Principal settings dialog carries:

- the route e-mail addresses, read-only — they are owned by the provider
  route policy in [FRD-09](frd-09-provider-and-intermediary-routes.md#provider-and-intermediary-routes);
- the two EVA API submission settings (manual, automatic) owned by
  [FRD-07](frd-07-eva-and-external-engineering-handoff.md#direct-eva-api-submission)
  and [ADR-0034](../adr/0034-per-principal-eva-api-submission-settings.md);
  ZIP export needs no setting;
- the Provider API credential (API-04): issue, reset, revoke, pause, and
  resume, each with a reason. The secret is shown once at issue or reset and
  never again; only its hash is retained. The credential is delivered with the
  submission endpoint it authenticates
  ([FRD-09 API-01](frd-09-provider-and-intermediary-routes.md#provider-api-principal-and-contract-boundary))
  and confers no staff access. A reset of a paused credential returns it to
  active; a revoked credential may be reissued, which starts a new secret and
  clears the revocation.

Every change is a permanent action-history event with actor, time, reason, and
before/after values.

### Staff accounts

The staff accounts table lists Name, Username, Role, and State, with role
assignment inline: Save is enabled only once the role has changed and requires
a reason. Account actions are Create, Enable, Disable, Delete access, Force
logout and Reset password. Periodic reviews, review dates and review actions
are removed by the 6 September 2026 operator decision. An account cannot
disable or delete itself, and concurrent actions cannot remove the last
enabled Administrator.

**Reset password** is an Administrator-only account action on the same table
(D15, 2026-09-06). It generates and reveals a temporary password once through
the protected confirmation flow. The existing password policy and
non-reversible hash remain the password owner. The existing
forced-change state is set, so the account must choose a new password at its
next sign-in. The reset is a permanent action-history event with actor, time and
reason. The temporary secret is never emailed, logged, persisted in raw form or
placed in analytics, and no reset email is sent.

Disable, role change, reset and Force logout revoke existing sessions and
tokens; the next request must observe current staff authority. Delete removes
active access, role and credential material while retaining the minimal actor
identity needed by immutable business history and printed reports. It never
deletes a Case. Destructive confirmation names the selected account and its
consequence. Force logout does not clear edit leases: targeted Administrator
lease clearance names the Case or holder, records a reason and invalidates the
old token independently.

Glass's credentials are protected per Engineer, provider and generation.
Administration shows configured/enabled/username/updated state and offers
replace/clear; it never reveals the stored password. Replacement or deletion
invalidates old sessions. Disabled or deleted staff cannot launch or resume.

**Sign-off Engineer** is an Administrator-only account setting (D31,
2026-09-02): a flag, the account's qualifications and a signature image. Only
flagged accounts are offered as a Case's Sign-off Engineer
([FRD-01](frd-01-case-identity-and-lifecycle.md#sign-off-engineer)), and
reports render the flagged account's tuple
([FRD-11](frd-11-reports-correspondence-and-reviewed-proposals.md#initial-renderer-activation)).
Every change to the flag, qualifications or signature image is a permanent
action-history event read in Action Logs. The initial Sign-off Engineer
accounts are A Patterson, N O'Reilly and E Mawdsley (Andy, Neil, Ed). Andy is
the default; Neil's qualifications are recorded later by an Administrator,
and until then his reports print the name without a qualification line. The
flags and qualifications are application data, never hard-coded.

### Permanent action history

Permanent business history records every business mutation; download/export; material denial or failure; automated result; and accepted, linked, or used external fact with the exact affected Case when case-bound, source/evidence identity, trusted staff or automated actor, caller, time, policy/version, structured before/after values, outcome, and reason where applicable. A history write is part of the mutable business transaction; a failed write cannot leave an unrecorded successful mutation. History is append-only: correction and reassociation add events rather than rewrite prior facts.

Sign-ins and authentication failures remain in the security log. Routine views, searches, refreshes, polling, retries, lease renewal/expiry/heartbeat, and adapter mechanics remain content-safe telemetry.

**Action Logs** is the one administration view over permanent action
history and the security log. It is filtered by search text, Area, Actor,
Result, From, and To, sorted newest first with a sort toggle, and shows Time,
Actor, Area, Action, Reference, and Result per row. Account access changes, role
changes, Principal settings and credential changes, and automation activity
are read here; there is no separate periodic review or Automation Activity
page.

No identity design, app registration, scope declaration, role table, file, or registration proves that a live caller exists or is accepted.

# FRD-04: Parties, accounts, and access
> Owner capabilities: ACC (staff roles/access, permanent action history) · Migrated from docs/requirements.md · UI behaviour: docs/design.md

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
| `Administrator` | All authorised application data and settings | Every ordinary Intake, Triage, Case, document, evidence, task, transition, and pre-assignment review action; staff account creation/disable/access review/role assignment; principals and successor cutover; workflow configuration; approved-mailbox allowlist; accepted OAuth-client registration/revocation | Credential-secret, cloud, or release administration through the staff UI; permanent deletion; a generic mailbox-rule editor before its policy is accepted |
| `Engineer` | Cases, inbox items, documents, evidence, and details | Every authorised Intake, Triage, Case, document, evidence, task, transition, and pre-assignment review action | Accounts, roles, access review, principals, successor cutover, workflow configuration, mailbox allowlist, authentication-client administration, credentials, cloud/release administration, or permanent deletion |
| `User` | Cases, inbox items, documents, evidence, and details | Every authorised Intake, Triage, Case, document, evidence, task, transition, and pre-assignment review action | Accounts, roles, access review, principals, successor cutover, workflow configuration, mailbox allowlist, authentication-client administration, credentials, cloud/release administration, or permanent deletion |

Andrew and Alex are the initial `Administrator` assignments held in application data/configuration. No person, name, email address, or bypass is hard-coded into authorization. Automated processing uses a distinct durable machine identity and only named Core actions; it is not a staff account or an independent policy owner.

Authorization is enforced in Core use cases and at every caller boundary. It fails closed without revealing case or source data. Immutable principal/reference, source, association, history, and closed-case rules apply regardless of administrative privilege. Development routes and data never confer production access.

### Permanent action history

Permanent business history records every business mutation; download/export; material denial or failure; automated result; and accepted, linked, or used external fact with the exact affected Case when case-bound, source/evidence identity, trusted staff or automated actor, caller, time, policy/version, structured before/after values, outcome, and reason where applicable. A history write is part of the mutable business transaction; a failed write cannot leave an unrecorded successful mutation. History is append-only: correction and reassociation add events rather than rewrite prior facts.

Sign-ins and authentication failures remain in the security log. Routine views, searches, refreshes, polling, retries, lease renewal/expiry/heartbeat, and adapter mechanics remain content-safe telemetry.

No identity design, app registration, scope declaration, role table, file, or registration proves that a live caller exists or is accepted.

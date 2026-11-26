---
id: ADR-0091
status: accepted
date: 2026-11-26
supersedes: []
superseded_by: []
related_capabilities: [EXT-07, EXT-13, EXT-20]
related_frd: [FRD-04, FRD-24]
tags: [credentials, integrations, data-protection, key-vault]
---

# ADR-0091: Integration logins in the database

## Status

Accepted on the operator's ruling of 26 November 2026 (R1288). It amends
[ADR-0002](0002-dotnet-modular-monolith-on-azure.md)'s "Box credentials and
any third-party secrets are held in Key Vault" and
[ADR-0043](0043-per-engineer-vendor-credential-protection.md)'s "narrow
exception … not a general relocation" for the five logins below; Box's
credentials and every other secret keep their owners.

## Context

Five integrations sign in with an account Collision Engineers holds: Glass's
valuation (username and password,
[ADR-0060](0060-glass-valuation-account-and-valuation-report.md)), Cazana
Companion (e-mail and password,
[ADR-0079](0079-cazana-companion-account-and-companion-report.md)), Brego
(the account's e-mail and its stored sign-in, a refresh token and a session
cookie, [ADR-0084](0084-brego-valuation-account-and-valuation-report.md)),
and the two Experian accounts, history and CAP (client id, client secret,
username and password each, [ADR-0089](0089-experian-vehicle-history-check.md)).
Until this decision they were Key Vault-backed app settings, and Brego's
sign-in a blob, `authentication-ring/brego-session.json`; only a release
could change one. The operator wants an Administrator to replace them, all
on one route, and asked for that route to be the most efficient.

Measured on production over 21 days: a login read from settings costs
nothing; from the database about 2 ms (17 ms at p90, 0.12 ms on the server);
from Key Vault directly 183 to 405 ms. A Get valuation press takes 0.76 s
(Brego) to 1.54 s (Glass's), about 95% of it the provider's site.

## Decision

- **One table, one row per login.** `IntegrationLogins` holds each login's
  values as one protected JSON payload, keyed `glass-valuation`, `cazana`,
  `brego`, `experian-history` and `experian-cap`. Nothing is stored in plain
  text, and no value is ever shown back; status shows the account name (the
  username or e-mail address), when it was last replaced and its version.
- **Protected as staff Glass's logins are.** The payload is protected with
  the Web host's Data Protection key ring under the purpose
  `Pegasus.IntegrationLogin.v1`, the login and its generation
  ([ADR-0043](0043-per-engineer-vendor-credential-protection.md)). A
  replacement moves the generation, so a payload is never read under another.
  A payload the key ring can no longer read is "unreadable", never a value.
- **An Administrator replaces a login.** Replacement takes exactly the
  login's own values, under optimistic concurrency, and writes one permanent
  history row naming the login, its generation and version, never a value.
  Replacing Brego's account clears its stored sign-in: a changed account is
  linked again.
- **Read on each call, no cache.** Each provider reads its login when it is
  used; a login not set or unreadable is that provider's "unavailable", as an
  unresolved Key Vault reference was. A cache would save about 2 ms against a
  press of a second or more. An Experian token and a Brego session kept in
  memory are reused only while the login's generation is the one they were
  issued under, so a replaced login is used on the next press.
- **Brego's sign-in lives in its row.** A renewed refresh token or new session
  cookie is written back in place while the generation is unchanged; a
  renewal is not an Administrator's change, so it moves neither the version
  nor the generation and writes no history. Amended the same day
  ([R1290](../rulings.md)): Brego's login may also hold a password, the one
  optional value of any login. An Administrator's sign-in on the
  Integrations page ([ADR-0084](0084-brego-valuation-account-and-valuation-report.md))
  is a change: it stores the sign-in under the next generation with one
  history row. A sign-in that waits for Brego's e-mailed code is kept in the
  row's pending columns, protected under its own purpose, moving only the
  version; any replacement clears it.
- **One-time import.** The release that ships this imports each login whose
  row is empty from its settings, once, never over a stored login and never
  from an unresolved Key Vault reference; Brego's sign-in comes from its blob.
  The import, the settings, their Key Vault secrets and the blob go in the
  next release (#2177, #2163). Amended for the release after 26 November
  2026: the import and the settings are removed, and nothing reads the
  secrets or the blob.
- **The addresses stay settings.** `Experian:BaseUri`, `Experian:TokenUri`
  and the Glass's repair-estimate origins are not secrets.

## Consequences

- The Data Protection key ring (`authentication-ring/keys.xml`) is now the
  only protection for these logins. If it is lost they read as unreadable and
  are typed again on the Integrations page; nothing falls back to settings or
  plain text.
- A production copy into the hosted test instance keeps the instance's own
  logins: production's rows are protected under production's key ring and
  could not be read there (R708's reason).
- The intake wipe preserves the table.
- The Worker has no Data Protection and uses none of the five integrations;
  its runtime role is denied the table.

## Options considered

- **Keep the Key Vault settings.** Rejected: only a release could replace a
  login.
- **Read Key Vault directly at each press.** Rejected: 183 to 405 ms a read,
  against about 2 ms from the database.
- **Settings for some, the database for others.** Rejected: the operator
  asked for one route.

## Links

- Ruling: R1288 in [rulings](../rulings.md).
- Accounts: [FRD-04](../frd/frd-04-parties-accounts-and-access.md).
- Configuration: [configuration](../engineering/configuration.md).

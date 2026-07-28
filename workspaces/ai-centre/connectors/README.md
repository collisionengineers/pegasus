# Connectors

Connectors isolate external systems and provider SDKs from agents and domain packages. Each connector
must expose a narrow typed contract, delegated/least-privilege authentication, explicit tenant and
case context, provenance, rate-limit handling, redacted logs, audit events, and a test double.

Planned boundaries:

- `outlook/` — delegated message/thread search, attachment import, and draft creation; sending is a
  separate approval-bound operation.
- `case-systems/` — case/instruction/report exchange without making any one vendor schema canonical.
- `vehicle-data/` — identity, MOT, specification, valuation, and other dated vehicle sources.

Do not place credentials, unrestricted Graph clients, browser automation, or vendor response objects
in agent prompts.

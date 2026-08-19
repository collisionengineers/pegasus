# Files — ADR execution boundary

| Path | Change | Risk |
| --- | --- | --- |
| `docs/adr/0028-run-integrated-renderer-in-web-container-app.md` | New thin ADR with required frontmatter/template | Scope creep into behavior |
| `docs/adr/README.md` | Add accepted index row | Index drift |

## Context files

| Path | Why |
| --- | --- |
| `docs/adr/0015-host-web-on-container-apps-consumption.md` | Existing Web host |
| `docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md` | Integration boundary |
| `infra/modules/platform.bicep` | Current Web/Worker runtime facts |
| `TICK-215 research` | Technical evidence/options |
| `AGENTS.md` | ADR conventions |

## Out of scope

- FRD behavior changes, code, IaC, deployment, sizing, or cloud writes.

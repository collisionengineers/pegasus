---
id: ADR-0082
status: accepted
date: 2026-10-22
supersedes: []
superseded_by: []
related_capabilities: [OPS-13]
related_frd: []
tags: [deployment, appservice, quota]
---

# ADR-0082: The deployment plan check does not read the App Service quota

## Status

Accepted (operator rulings R78, 16 October 2026, and R196, 22 October 2026).

R78, answering OQ-24: "record an issue to remove the ADR-0049 quota
pre-flight (OPS-13 dropped quota checks; recorded only)". The issue is #1356.

R196, answering OQ-138: "Script change plus a one-paragraph ADR note: remove
the check and its tests; the note records that the check the script
attributed to ADR-0049 is withdrawn and why; nothing in ADR-0049 itself is
replaced".

This record replaces nothing in [ADR-0049](0049-host-web-on-app-service-code-deploy.md),
which states no pre-flight. The check it withdraws is the quota pre-flight
that `scripts/Test-AzureDeploymentPlan.ps1` ran before provision under
ADR-0049's name. ADR-0049's record of the 2026-09-13 quota reading and its
`webLocation` route stand as filed.

## Decision

`Test-AzureDeploymentPlan.ps1 -Mode PreProvision` does not read the App
Service quota for the Web plan SKU in the Web region and does not stop
provision when that quota reads 0. The check is withdrawn because OPS-13
dropped quota checks (R78).

## Links

- [ADR-0049](0049-host-web-on-app-service-code-deploy.md)
- [Capabilities](../capabilities.md) (OPS-13)

# Research — ADR for renderer execution boundary

## Question

Which existing production composition boundary can carry in-process Chromium rendering, and does the choice require a new ADR?

## Findings

1. ADR-0025 decides integration behind a Core port into an existing application project and permits Web or Worker, but does not select one.
2. ADR-0015 selects Container Apps Consumption for Pegasus.Web generally, not Chromium/report execution.
3. Current IaC deploys Web as a custom Linux container (always-warm in current Bicep) and Worker as code-only .NET isolated Functions on Flex Consumption.
4. CollisionRenderer requires pinned Chromium/native/font dependencies. Current official Azure documentation confirms Flex Consumption does not support custom containers; the Web image can carry those dependencies.
5. A new Container Apps Job/service would be a new deployment unit, contrary to the operator's no-separate-system direction and repository invariant absent another ADR.
6. Selecting Web is a durable technical mechanism with reliability/capacity consequences, so a thin ADR is required. The next unused stable id after the issued set is ADR-0028; ADR-0017 is permanently skipped.
7. The ADR must decide only execution location. FRD-11 owns readiness/behavior; TICK-215/SIMPLI-014 own implementation; PLAT-007 owns Azure changes/proof.

## Implications

- Author ADR-0028 selecting the existing Pegasus Web Container App for in-process rendering.
- Keep Worker unchanged and prohibit separate renderer service/job/API.
- Require durable/idempotent application operation behavior in FRD/Core, but do not duplicate it in ADR.

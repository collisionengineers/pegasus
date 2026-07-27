# Platform and operator experience

## Outcome

CollisionSpike provides a restrained, accessible Windows-operated staff
application and a directly controlled Azure release path with observable,
recoverable behavior. Product UI and platform evidence remain separate from
design registration or infrastructure compilation.

## Settled requirements

- The internal staff experience supports Operations, Intake, Triage, Cases, and
  authorized Administration with keyboard operation, visible focus, semantic
  structure, associated errors, practical targets, forced colours, and reduced
  motion.
- Mobile staff UI is not planned. Constrained-width/zoom reflow does not create
  a mobile product.
- Operations-first is selected for the V1 shell and landing strategy; the
  comparison raster does not override the complete UI specification.
- Windows and PowerShell 7 own repository/release operations. GitHub Actions
  validates but does not deploy.
- The Azure release path requires explicit approval, immutable artifacts,
  migration/health/smoke evidence, telemetry, and tested recovery; dated
  inventory is not live proof.
- Separate staging/UAT/training environments, deployment slots/S1, private
  networking, zone/multi-region resilience, and quarterly restore exercises are
  not planned.

The stable `OPS-*` and `UI-*` outcomes and allocations live in the [capability
inventory](../capabilities.md). UI authority is under [design](../../../design/README.md);
build/deploy/monitor/recover authority is under [operations](../../operations.md).

## Current state and activation

The current Development UI is a narrow intake proof and differs from the
approved design foundation. Azure IaC is a target design, not an executable or
live-accepted deployment. Each UI/platform slice requires one change record,
real caller or release route, proportional evidence, and current owner updates.

Former [platform/release plans](../../history/plans/remainder-delivery/platform/)
and plan-era UI material remain historical evidence only.

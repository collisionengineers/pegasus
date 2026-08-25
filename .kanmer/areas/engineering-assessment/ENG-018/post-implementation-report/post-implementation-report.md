# Post-implementation report — ENG-018

## Result

Export no longer depends on an EVA hand-off activation switch. A case in `Review` proceeds through the existing mapping, verified-image packaging, replay, action-history and first-send-proxy path without any `Eva:AcceptedMapping` configuration. Mapping key/version remain descriptive format/history metadata at version 2.

## Changes

- Core: removed `EvaMappingAcceptance`, activation checking/message, nullable mapping output and its always-empty blocker collection; retained the existing thirteen-field mapper and bundle format guard.
- Infrastructure/Web/Azure: removed the acceptance dependency, configuration binding and three Container App environment values.
- Tests: converted existing mapping tests to the single Export contract, made both export integration journeys run without activation configuration, and added source-shape assertions preventing the type, settings and legacy message from returning.
- Documentation: FRD-07, current architecture and runbook now state there is no separate activation gate; operations records the release-28 regression pending corrective deployment.

## Governing-document fit

FRD-07 names `Review` as the single business readiness decision. This implementation removes the only remaining second switch while retaining real technical validation for readable eligible images, authorization, archive format, replay and history.

## Verification

- `dotnet restore Pegasus.slnx --locked-mode`
- `dotnet build Pegasus.slnx --configuration Release --no-restore` — succeeded, 0 warnings/errors.
- Focused Core export/contract tests — 26 passed.
- Focused end-to-end export tests — 2 passed.
- Architecture tests — 99 passed before the final focused assertion; the final focused architecture test passed after the simplification edit.
- Full solution tests — Core 978 passed; Architecture 99 passed; Integration 946 passed, 16 corpus-dependent skipped.
- `./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local` — passed.
- Markdown placement and documentation link checks — passed.

## Risks and verification on merged dev/main

No database migration or compatibility path exists or is needed. After merge, verify the exact merged SHA, deploy the normal immutable artifact plus Bicep configuration update, then retry an actual Review-case Export and confirm a ZIP response, one action-history record, the first-send proxy behavior, and no legacy EVA hand-off message.

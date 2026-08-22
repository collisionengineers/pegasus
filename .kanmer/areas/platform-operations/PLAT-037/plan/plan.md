# Plan — PLAT-037

A configuration change on the production Web Container App, applied as part of the release that carries [[CASE-019]]. No code change.

## The exact write

Target: `pegasus-prod-web-252ow37gij` in `rg-pegasus-prod`, subscription `e6076573-23a5-46a8-acef-7e22d264e5db`.

```
az containerapp update \
  -g rg-pegasus-prod \
  -n pegasus-prod-web-252ow37gij \
  --set-env-vars \
    Eva__AcceptedMapping__Key=qdos-eva-13-field-mapping \
    Eva__AcceptedMapping__Version=1 \
    Eva__AcceptedMapping__EvidenceReference=docs/frd/frd-07-eva-and-external-engineering-handoff.md
```

`--set-env-vars` adds or replaces only the named variables and leaves the rest of the container's environment untouched. None of the three is a secret, so none becomes a Key Vault reference.

## Why the evidence reference is the FRD

`CaseEvaMapping.MappingKey` is `qdos-eva-13-field-mapping` and `MappingVersion` is `1`; the mapping those constants implement is specified in `docs/frd/frd-07-eva-and-external-engineering-handoff.md`. Naming that file records *what was accepted*, is stable, and is checkable by anyone reading `provenance.json` later. A free-text note would not be.

## Ordering

Applied **after** the image is deployed and **before** live verification, so the first export attempt on QDOS26011 exercises the switched-on path. If the deploy is rolled back the setting is harmless: it only ever unblocks a code path that the previous image does not have.

## What this does not switch on

External EVA delivery. No `IEvaHandoffProxy` adapter is configured, `EvaFirstHandoffProxies` stays empty, and `LocalEvaHandoffProxy` continues to claim no external delivery. This is the mapping gate only.

## Acceptance

`az containerapp show` reports the three variables on the live revision, and QDOS26011's export succeeds with `mapping.key`, `mapping.version` and `acceptanceEvidence` populated in `provenance.json`.

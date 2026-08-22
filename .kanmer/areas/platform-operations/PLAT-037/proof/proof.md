# Proof — PLAT-037

Applied during release 23, source `b6d54ff6…`, revision `pegasus-prod-web-252ow37gij--b6d54ff6-eva`.

## The three settings are live

```
$ az containerapp show -g rg-pegasus-prod -n pegasus-prod-web-252ow37gij \
    --query "properties.template.containers[0].env[].name" -o tsv | grep -i eva
Eva__AcceptedMapping__Key
Eva__AcceptedMapping__Version
Eva__AcceptedMapping__EvidenceReference
```

Values as scoped: `qdos-eva-13-field-mapping`, `1`, and `docs/frd/frd-07-eva-and-external-engineering-handoff.md`. That satisfies `CaseEvaMapping.IsSwitchedOn`, which requires the exact key, the exact version and a non-empty evidence reference. Before the release the container declared no `Eva*` variable at all, so every bundle refused with the activation-gate reason before reading any case data.

Operator confirmation of the evidence reference, 2026-08-22: *"go for the current eva json."*

## The plan named the wrong mechanism, and it mattered

The plan said `az containerapp update --set-env-vars`. That would have worked once and then been silently reverted: `infra/modules/platform.bicep` declares the web container app's `env` array explicitly, so anything set outside it is removed by the next `azd provision`. Applied through bicep instead, and the declaration is committed in PR #518 so the repository matches what is deployed.

## A trap that cost two deployments

The first attempt to provision these settings reported **SUCCESS and deployed nothing**. `platform.bicep` gates the entire web container app on:

```bicep
var webActivationApproved = webActivation == 'approved'
  && startsWith(webImageDigest, 'sha256:')
  && length(webImageDigest) == 71
  && length(webRevisionSuffix) == 12
```

The revision suffix chosen to avoid a collision — `b6d54ff66652-eva` — is 16 characters, so `webActivationApproved` evaluated false, the `webContainerApp` resource was skipped by its `if`, and `azd` still reported success. Nothing errored, nothing changed, and the only signal was reading the env back afterwards.

Resolved with a 12-character suffix, `b6d54ff6-eva`. **An invalid suffix length disables the entire Web deployment silently** — that belongs in the release skill's trap table.

## Deliberately not switched on

External EVA delivery. No `IEvaHandoffProxy` adapter is configured, `EvaFirstHandoffProxies` is empty, and `LocalEvaHandoffProxy` continues to claim no external delivery. This is the mapping gate only.

## Not claimed

That an export has produced a `provenance.json` carrying these values. The settings are demonstrably live; the archive that reads them is [[CASE-019]]'s proof and needs a signed-in browser session.

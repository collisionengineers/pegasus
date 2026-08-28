# Research — PLAT-055

## Question

Is the EVA 401 caused by Key Vault delivery, or by the stored client-secret
material?

## Findings

- Trace `5f00f120eff5dedf6d6bfd977a7eb2ae` reached EVA's token endpoint and
  returned 401 before any instruction JSON was sent.
- Vault `pegasusprodkv252ow37g`, Web managed identity, secret-scoped RBAC,
  version-pinned Container App references and recurring Key Vault sync events
  are healthy.
- The active `eva-client-secret` value is non-empty and structurally valid,
  but the operator confirmed it was duplicated during entry.
- Infisical returns the one-line source value for
  `eva_api_client_secret` successfully. No value is recorded here.
- [[ENG-019]] is the separate live-key swap. This repair preserves the current
  test environment and must not use the live credential.

## Implication

Create one new `eva-client-secret` version from the Infisical value, repoint
both versioned runtime references, restart only as required for resolution,
and verify token authentication without submitting an instruction.

# ADR-0011: Multi-token and SHA-256 API authentication

Status: Accepted

Date: 2026-07-29

Supersedes: ADR-0008 authentication detail only

## Context

ADR-0008 recorded a decision-time baseline of one plaintext `CR_API_TOKEN`.
The retained API source now supports rotation windows and deployments that do
not expose raw configured token values after startup. The portability,
container, endpoint, and other decisions in ADR-0008 remain unchanged.

## Decision

The API accepts bearer credentials through one normalized in-memory set of
SHA-256 token hashes. Configuration may supply:

- `CR_API_TOKEN` for the original single plaintext compatibility contract;
- `CR_API_TOKENS` for a list of plaintext tokens during rotation;
- `CR_API_TOKEN_SHA256` for one pre-hashed token; and
- `CR_API_TOKEN_SHA256S` for a list of pre-hashed tokens.

Plaintext configuration values are hashed during option construction. Request
credentials are hashed before constant-set membership comparison. Empty or
malformed entries do not create an accepted credential. When no accepted token
is configured, the optional-auth behavior remains the one implemented and
documented by the API host; this ADR does not activate a Pegasus caller or
select a deployment secret store.

## Rationale

A token set permits overlap during rotation without downtime. Pre-hashed values
reduce raw-token exposure in process configuration. Keeping the original
variable preserves compatibility while making the current executable contract
explicit.

## Consequences

- Operators may rotate by adding a new value, moving callers, then removing the
  old value.
- Configuration, logs, diagnostics, and evidence must never reveal plaintext
  tokens.
- A caller still needs independent authorization, transport, deployment, and
  acceptance proof.
- Every non-authentication decision in ADR-0008 remains in force.

## Evidence

`src/CollisionRenderer.Api/Program.cs` implements the four environment-variable
forms and normalizes them to SHA-256 hashes. Current architecture and
development documentation own runtime configuration and verification.

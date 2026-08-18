# Files — DELIV-004

## Where the change lands

| Path | Why |
|---|---|
| `AGENTS.md` | Canonical repository-rule owner. Make its binding link to the existing no-disabled-flag policy explicit: a feature behind a closed gate is not shipped, delivered, or claimable. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/engineering.md` | The pre-existing detailed authority: a deferred capability is never a dormant registration, unused endpoint, disabled flag, or dark destructive code; a closed gate is a disabled flag. |
| `docs/index.md` | Confirms that repository rules live in `AGENTS.md` and that Engineering is downstream working guidance. |
| `docs/operations.md` | Uses implemented-but-gated wording for current-state facts; it is context for truthful terminology, not an activation instruction. |
| `AUTO-001` | Related activation ticket; activation is separate work and is not needed to establish the policy. |

## Ripple effects

- Future plans, reviews, release descriptions, and current-state documentation
  must not call closed-gate work shipped or implemented.
- No application code, configuration, feature flag, deployment, credential, or
  external service changes are part of this ticket.

## Out of scope

- Activating `Features:AutomationMcp`, `Features:SendToAi`, or any other
  existing gate.
- Changing feature behaviour, deployment configuration, or the governing
  FRD/ADR for an individual capability.

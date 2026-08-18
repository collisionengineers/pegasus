# Files — DELIV-004

## Where the change lands

| Path | Why |
|---|---|
| `AGENTS.md` | Canonical repository-rule owner. Add the explicit rule that a feature behind a closed gate is not shipped, delivered, or claimable. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/engineering.md` | Already forbids deferring capabilities as dormant registration, unused endpoint, disabled flag, or dark destructive code; the new `AGENTS.md` rule must reinforce this without creating a competing policy. |
| `docs/index.md` | Confirms that repository rules live in `AGENTS.md` and that Engineering is downstream working guidance. |
| `docs/operations.md` | Uses implemented-but-gated wording for current-state facts; it is context for truthful terminology, not an activation instruction. |
| `AUTO-001` | Related activation ticket demonstrating why a closed gate must be treated as unshipped until separately activated and evidenced. |

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

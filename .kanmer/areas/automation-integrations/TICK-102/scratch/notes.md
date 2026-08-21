VERIFY2 lane concurrence + one additional live fact (2026-08-20): read-only `az containerapp show` on `pegasus-prod-web-252ow37gij` (rg-pegasus-prod) confirms the live env-var list contains `Features__AutomationMcp` and `AutomationMcp__*` but **no `Features__SendToAi` setting at all** (`env[?contains(name,'SendToAi')]` → empty), and `Runtime__Profile = Production`. This is the live readback matching the bicep analysis in research.md: gate closed in production by absence AND by the DevelopmentOffline-only fail-closed startup check. Stage disposition (stopped at review, deployment unset) is endorsed. Residual for activation: non-preview transport decision → bicep setting → profile-check contextualization → re-verify live.

## Release 17 review — 2026-08-21: not advanced, deliberately

Reviewed as part of the Release 17 sweep of the 22 `verifying` tickets. **This ticket does
not move to `done`, and no `proof` document was written for it**, because writing one would
enable a move that should not happen.

The reason is the ticket's own checklist item 2, left unchecked by whoever last worked it:

> "All activation conditions are accepted before implementation starts — implementation
> already exists, but the capability row's own stated activation condition ('production
> activation needs a separate non-preview transport decision') has explicitly NOT been
> accepted yet. Left unchecked deliberately — this is the honest reason the ticket does not
> proceed past `review`."

That is a correct call and it is respected here. `CLAUDE.md` is unambiguous: *"A closed
composition or feature gate is a disabled flag, not a partially shipped feature. Do not
ship, release, merge as delivered, claim, or document a feature behind one as delivered."*

Verified during this review:

- the AI-09 code **is** merged and on the deployed revision — `5555440e` (PR #332) is an
  ancestor of `4111ad29`;
- [[TICK-104]]'s connector **configuration** surface is live and reachable
  (`Features__AutomationMcp = true` in production), and closes on that basis;
- what remains unaccepted is the non-preview **transport** decision for AI-09 itself, which
  is an operator decision and not something this review can supply.

**What would unblock it:** an operator decision on the non-preview transport for Send to AI.
Until then the ticket sits where it is with the code deployed but the capability not
claimed as delivered.

Reported to the operator in the Release 17 standup as parked, rather than counted toward
the run's ticket total.

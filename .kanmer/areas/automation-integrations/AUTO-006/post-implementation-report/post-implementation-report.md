# Post-implementation report — AUTO-006

PR [#618](https://github.com/collisionengineers/pegasus/pull/618) → `dev`.
Branch `task/auto-006-automation-admin`, merged up to `origin/dev` at
`b92cb9a7` before any edit (fast-forward, no conflict).

## Delivered

| Contract item (§1.12) | Delivered as |
| --- | --- |
| Automation panel status | `panel-head` heading plus a state chip — Enabled / Stopped — from `AutomationClientStatus.IsEnabled` |
| Registered clients | fact cell: the one registered client's display name from `AutomationClientRegistry` (ADR-0011 — one client per deployment) |
| Active jobs | fact cell: `IAiJobQueries.GetCountsAsync().Active` — the AUTO-011 ledger's own counter |
| Failed jobs | fact cell: the same call's `.Failed` |
| Stop / Start automation, danger → reason | `btn--danger` opening the shared `Pages/Shared/_ReasonDialog`, posting `SetEnabled`; one consequence sentence, and only when stopping |
| AI settings: Timeout, enabled checkbox, Save | one form — Channel address, Timeout in seconds, New channel token, the `asp-for` enabled checkbox, Reason — behind one `SaveAiSettings` |
| AI settings: Proposal | **not drawn** (below) |

Also: `admin-layout` with the shared `_AdminNav` (read, never modified), the
`page-header`, no `back-link`, the channel token's recorded state as a
`definition-list`, and a danger *Remove the channel token* behind its own
reason dialog when one is held.

Three handlers became one. `SetSendToAiEnabled`, `UpdateConnector` and
`RotateChannelToken` are replaced by `SaveAiSettings`, because the contract
gives the panel one Save. The three Core operations are untouched and each
still writes its own attributed permanent history; the switch is written only
when the checkbox differs from the stored state, so a save that changes nothing
about it writes no switch history.

## Removed

Every explanatory paragraph the design authority bans: the "not a staff
account" lede, both "not part of this deployment" narrations, and the five
trailing "applies from the next hand-off" / "refuses new tokens immediately"
sentences. An uncomposed capability is now absent — no registry, no Automation
panel, and `_AdminNav` already omits the row.

## Deliberately not drawn, with reasons

- **"Proposal".** No stored setting backs it: `SendToAiControl` carries
  `Enabled`, `ChannelBaseUrl`, `TimeoutSeconds`, `ChannelTokenProtected` and
  `TokenRotatedAtUtc`, and no Core port exposes a proposal kind. D7 permits a
  disabled control only for a named, ticketed integration seam, which this is
  not, so an inert select would be a defect. Handed to [[AUTO-010]].
- **The Automation Activity link.** §1.14 supersedes that page (→ Action Logs);
  [[PLAT-051]] replaces it and [[UIIMP-009]] deletes it.

## Divergence from the prototype, recorded

The prototype paints Stopped red and AI-enabled green. `docs/design/README.md`
§ Colour reserves green for confirmed completion and red for blocked / failed /
closed-in-error, so both panels use the `_StatusChip` map unchanged — Enabled
navy, Stopped neutral — with no tone override.

## Verification — observed, not claimed

```
dotnet build ./Pegasus.slnx --configuration Release
Build succeeded. 0 Warning(s) 0 Error(s)

dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj \
  --configuration Release --no-build \
  --filter 'FullyQualifiedName~AutomationAdministrationWebTests|FullyQualifiedName~SendToAiIntegrationTests|FullyQualifiedName~AutomationActorLabelTests'
Passed! - Failed: 0, Passed: 14, Skipped: 0, Total: 14
```

The 14 are 4 new `AutomationAdministrationWebTests`, 6 `SendToAiIntegrationTests`
(including the two retargeted connector tests) and 4 `AutomationActorLabelTests`.
No full suite, no Browser category, no snapshot capture — as instructed.

No assertion was weakened. `SendToAiConnectorAdministrationTests` changed only
where the form it posts changed: one `SaveAiSettings` post instead of two. It
still asserts the override reaches the replacement channel with the rotated
token, the token never renders, both `send_to_ai_connector_updated` and
`send_to_ai_channel_token_rotated` are attributed to Staff with outcome
Succeeded, and the stored token is protected at rest.

Snapshots are **not** regenerated here;
`docs/design/test-ui/pages/administration-automation--default.html` is stale
until the merging branch regenerates it, per the epic's once-per-merge rule.

## Commits

- `62c9e2ac` labels
- `ef905e6a` page model — ledger counts, one Save
- `eb41188b` page — the design-system port
- `b4d0f88a` tests

## Left to AUTO-010

Only the "Proposal" control and the Core setting that must back it. Everything
else in AUTO-010's description — status, registered clients, active/failed job
counts, Stop/Start with reason, and the AI settings panel backed by
`ISendToAiControl` and `IAiChannelConnectorStore` — ships here. AUTO-010 should
be rescoped before it is taken, or it will re-do this lane.

## Dispositions

Recorded under "Simplification pass — 2026-08-29" in the plan: five findings
fixed in-lane, two accepted with reasons, and three defects outside this lane
reported (two carried to PLAT-051, one to PLAT-029/UIIMP-009).

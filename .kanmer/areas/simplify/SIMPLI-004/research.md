# Research — Retire NOW.md, Kanmer canonical

Root plan: `docs/temp-plans/retire-now-rewrite-agents.md`. Intent fixed by
`docs/temp-plans/simplify/simplify.md`: Kanmer owns actionable work,
`capabilities.md` stays the catalogue, and NOW.md's genuinely-current
production warnings move to their authoritative docs before retirement.

## NOW.md structure (529 lines, "updated 2026-08-13")
`## Doing` (3–8, live claims) · `## Production state` (10–54) · `## Next`
(56–481, ~30 task/defect entries) · `## Waiting` (483–511) · `## Path`
(513–524, 8-step release gate) · footer links.

Buckets: Doing/Next/Waiting = transient tracking a kanban board replaces.
Production state + Path = durable facts to preserve.

## Duplication analysis (what is safe vs lost)
Already mirrored in `operations.md`/`open-decisions.md` (safe to drop): Box
custody root, `claudeuiverification` removal recipe (operations.md:323–330),
Send-to-AI/MCP composition-off, release table 1–8.

**Genuinely NOT duplicated (loss risk):**
- 2026-08-12 post-release-8 deployment identity + **Worker ENABLED** (12–23) —
  *conflicts with* operations.md:305–322 (Worker disabled 2026-08-10); NOW.md
  is newer truth.
- 3 post-release-8 migrations applied + "don't number a new release yet" (25–31).
- "Nothing live-verified beyond smoke" caveat (33–36).
- `## Path` 8-step sequence + "Explicitly NOT on the path" (513–524) —
  `open-decisions.md:25` explicitly delegates ownership to NOW.md.
- Operator decisions in Next: write-budget rebaseline (88–95, cited by
  `CapacitySoakTests.cs:86`), no-paid-GitHub (265), absorb-not-revert (386),
  no Principal in prod (130–133).

## Reference map (retarget/remove; `.kanmer/` + CHANGELOG excluded)
Canonical: `index.md:9`, `engineering.md:6,16`, `capabilities.md:6`,
`open-decisions.md:25`, `runbook.md:1089`, `README.md:68`; ADR bodies 0021/0023
(handled by [[SIMPLI-006]]). Code comments: `EfApprovedInboxPollStore.cs:116`,
`CapacitySoakTests.cs:86`. temp-plans (37) mostly deleted in [[SIMPLI-005]].
CHANGELOG.md (320) = history, untouched.

## Update (2026-08-13): Worker state live-verified
`az functionapp config appsettings list` on `pegasus-prod-worker-252ow37gij`
(sub `e6076573…`, tenant `858cf5b3…`): all nine `AzureWebJobs.<fn>.Disabled` =
`false` → **Worker enabled**, confirmed 2026-08-13. Confirms NOW.md (2026-08-12)
and supersedes operations.md (disabled, 2026-08-10). The operations.md update is
now evidence-backed, not a doc copy. Preserve the nuance: enabled configuration
≠ a business caller (trigger/poll/intake) has actually run.

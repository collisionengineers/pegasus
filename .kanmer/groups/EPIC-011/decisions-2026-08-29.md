# EPIC-011 orchestration decisions — 2026-08-29

Binding for every member ticket, alongside `context.md` and `waves.md`. Recorded by
the orchestration session after the wave 1–4 branch/PR examination.

## D15 — Proof is written against merged `dev`; `main` is promoted once, at the end

`AGENTS.md` says a ticket's `proof/proof.md` is written on merged `main`.
`waves.md` says `main` is promoted only after wave 5. Both cannot hold while any
ticket reaches Done before wave 5.

**Operator decision:** write each ticket's proof against merged `dev` as its wave
lands, and walk the ticket to Done on that evidence. A single exact-SHA, non-force
`dev` → `main` promotion happens at wave 5 and requires explicit
`MERGE AUTH GRANTED` immediately before the `main` update.

A proof document must therefore name the `dev` SHA it was taken at, and say
explicitly that it is dev-merged evidence pending the wave-5 promotion.

## D16 — The three EPIC-008 administration lanes join EPIC-011

`UIIMP-009` (remove superseded surfaces) cannot finish while the Administration
areas it deletes have no shipped replacement, and those replacements were owned by
tickets outside this epic.

**Operator decision:** pull them into scope. Now EPIC-011 members:

| Ticket | Lane | Owns |
| --- | --- | --- |
| PLAT-025 | Wave 2 · I2 | `Pages/Administration/Configuration.*` |
| PLAT-026 | Wave 2 · I3 | `Pages/Administration/Mailboxes.*`, `MailCategories.*` |
| PLAT-027 | Wave 2 · I1 | `Pages/Administration/Accounts/**`, `Access/**`, `Roles/**` |

`waves.md` already allocated these as lanes I1–I3; they are now members of the epic
and count toward its completion.

## D17 — INTK-001 lands before INTK-047

`INTK-001` ("Make queued upload status honest…") owns `UploadStatus.*` and
`UploadGroupStatus.*` — the exact files `INTK-047` ports. It is now recorded as
blocking `INTK-047` and has joined EPIC-011.

Its state as found: stage `implementing`, assignee `codex-mcp-client`, one commit
`1594ff0e` (2026-08-26) that was **never pushed**, touching 8 files including
`wwwroot/js/site.js` (PLAT-029's file, also targeted by TICK-223). By the repository's
own staleness rule (branch never pushed within 48 hours) the claim is stale, but the
work is real and must not be discarded — push it, bring it up to `dev`, review and
merge it, and only then start INTK-047.

`INTK-047` must not absorb INTK-001's scope (rule 2).

## Corrections to `waves.md` found during the examination

- **CASE-028's `CaseStageCounts` clause is already shipped.** `Core/Operations/DashboardCounts.cs`
  already carries `CaseStageCounts(int NotReady, int Review, int Held, int WithEngineer, int Complete = 0)`
  and `EfDashboardQueries.GetCaseStageCountsAsync` already applies the D3 grouping —
  delivered by CASE-025. Rescope CASE-028 to `CaseTimeline.cs`, `ActionLogs.cs`
  (+ composite index migration and the `ReviewActionLogs` right), `RailCounts.cs`
  and `CountUnreadAsync` before taking it.
- **PLAT-049 is not wave-4 work.** All three of its declared blockers (PLAT-023,
  AUTO-011, PLAT-048) are merged. It runs in parallel with the wave-2 lanes.
- **`Browser/LayoutIntegrityTests` DOES exist** on `dev`
  (`tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs`). An earlier
  readiness report claimed it was missing; that claim was wrong. UIIMP-010 has its
  tooling.
- **File-ownership breach to stop repeating:** `waves.md` allocates
  `Core/Operations/DashboardCounts.cs` to wave-2 lane A (UIIMP-008), but CASE-025
  (lane C1) edited it in `95f69958`, and also edited `TriageQueuesWebTests.cs`. That
  is the direct cause of PR #610's conflict.

## Two shared files that break strict lane isolation

Neither is covered by the "a ticket owns whole files" rule; handle them explicitly.

1. `src/Pegasus.Web/Presentation/OperatorLabels.cs` — `context.md` forces every UI
   label here, so most UI lanes append to it. Each lane appends **only inside its own
   nested static class** and never reorders. Expect textual conflicts, not semantic
   ones.
2. `docs/design/test-ui/catalogue.json` and `docs/design/test-ui/pages/*` — every UI
   lane changes renders. Snapshot regeneration happens **once per merge, on the
   merging branch only**. A lane must not regenerate snapshots in its own worktree.

## Merge ordering constraint

`UIIMP-005` (PR #609) adds the Test UI snapshot **CI gate**. Merging it before the
other Razor-changing lanes would red-wall every one of them, because none carries
regenerated snapshots. **UIIMP-005 merges last among the UI lanes**, after the
snapshot corpus has been regenerated against the final markup.

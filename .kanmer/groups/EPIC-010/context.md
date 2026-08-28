# EPIC-010 — shared context

## What binds this batch

- Remediation work after release 33/34. **No new features** beyond each ticket's stated outcome; scope is the ticket brief.
- Live facts established 2026-08-27 (read-only against prod): the instructions mailbox was re-activated at 10:20:33Z; Graph subscription `09018cc2…` is `Active` (expires 2026-09-02); the webhook path ingests in ~7 s; QDOS26024 and a.QDOS26025 exist; receipt `f2ac0509…` holds a terminal `blocked` automatic-Audit attempt with no staff route.
- App Insights is capped at the **component** level (0.1 GB, 00:00Z reset) and is silent from ~05:30Z each day; prove runtime behaviour from SQL state or after the reset, not from an empty query.
- Every branch: `task/<slug>` from `origin/dev`, worktree `../pegasus-worktrees/<slug>`, PR to `dev`. Canonical gate: `dotnet restore --locked-mode`, `dotnet build -c Release --no-restore`, `dotnet test -c Release --no-build --filter "Category!=Corpus"` (integration ≈ 40 min; chunk if needed and keep the log under `artifacts/`).
- Azure **writes** (cap changes, app settings, deployments) need the operator's explicit approval for the exact target; read-only checks are free. MAIL-020's cap change is therefore a plan + approval, not a unilateral write.
- UI changes (MAIL-018, INTK-044) are bound by `docs/design/README.md` — no explanatory copy.
- Lanes are file-disjoint: MAIL-021 (`RetainedMail.cs` comment), MAIL-019 (release smoke script), MAIL-020 (Worker telemetry config + infra), INTK-044 (`IntakeAllocation` classification/recovery + Intake Details), MAIL-018 (Mailboxes page + queries). MAIL-017 is at Review (PR #571) awaiting an independent reviewer; CI shard `sql-integration (1)` failed on an unrelated SQL connection timeout (`VehicleLookupGapFillTests`), local suite 987/987.

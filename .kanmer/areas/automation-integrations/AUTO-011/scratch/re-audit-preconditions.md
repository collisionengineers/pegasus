## What this ticket actually needs before it can return to Done — 2026-08-29

Established by the cross-model review of [[AUTO-014]] (PR #629), which chased
every capability this ticket's `## What` names rather than only the two AUTO-014
targets. **AUTO-014 is necessary but not sufficient**, and its own plan said
otherwise — corrected there.

### Four preconditions, three of them tickets

| Capability | State | Supplier |
| --- | --- | --- |
| `IAiJobQueries.ListForSubjectAsync` | **wired** — `Pages/Mail/Message.cshtml.cs:706`, rendered `Message.cshtml:525` | [[AUTO-014]] (PR #629) |
| Staff-created `AiJobKind.QueryResponse` | **wired** — control `Message.cshtml:74`, handler `:228`, command `:257` | [[AUTO-014]] (PR #629) |
| `ICancelAiJob` — "cancel (staff)" | its only caller | **[[PLAT-049]]**, merged to `dev` at `0a2d955d` |
| `IConfirmAiJob` | no reachable caller | **[[ENG-028]]** (PR #630, in review) |

### And one that belongs to no ticket

**The `AiJobs` table does not exist in production.** Release 36 (2026-08-28)
predates this ticket's merge, so `20260828084601_AiJobs` and
`20260828084644_GrantAiJobs` are unapplied and the `automation.jobs` scope is
offered by no running revision. **Both of AUTO-014's callers would fault at
runtime in the deployed estate today.**

That is owned by the single wave-5 `dev` → `main` promotion (D25, release 37) and
is not a defect in any lane.

### What the re-audit must say, and must not say

- **Must not** count `ICancelAiJob` or `IConfirmAiJob` until PLAT-049 and ENG-028
  are merged and their callers verified on merged `dev`.
- **Must** state the production-activation gap plainly rather than counting
  around it. Under D21 a capability whose table is not deployed is not delivered,
  however green the branch is.
- **Must** state that the query-response path is **reachable but manual**: the
  only registered classifier (`QdosMailClassificationPolicy.cs:120-151`,
  `DependencyInjection.cs:152`) cannot emit `PostReportEmails`, so every
  post-report query must be reclassified by an operator from the message page's
  "Correct classification" dialog before the control appears. Reachable is not
  routine, and the proof should not let the two read as the same thing.

### One correction to this ticket's existing proof

`proof/proof.md:249` assigns the by-subject caller to [[PLAT-049]]. **That claim
is false** — PLAT-049 loads `ListOpenAsync()` unioned with `ListRecentAsync(200)`
and never calls `ListForSubjectAsync`. It is the reason this ticket was reversed
out of Done, and the re-written proof must not repeat it.

### Gate state, corrected

`Features:SendToAi` governs **AI-09**, the channel hand-off
(`src/Pegasus.Web/AiWork/SendToAi.cs`), **not** the AI-10 job ledger.
`ICreateAiJob`, `IAiJobQueries` and `ISendToAiControl` are registered
unconditionally (`DependencyInjection.cs:351-358`). The administrator AI switch
is a database singleton that **defaults open** — `EfAiWorkRequestStore.cs:277`
returns `control?.Enabled ?? true` and no migration seeds a row — so "Automation
stopped" is a state an administrator chooses, not a shipped-disabled default.

Neither AUTO-014 caller sits behind a closed composition gate. D21's closed-gate
row does not apply to them; the production-migration gap above is the real
constraint.

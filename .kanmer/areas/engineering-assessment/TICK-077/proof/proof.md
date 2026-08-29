# Proof — TICK-077 (EXT-04): direct EVA API submission

## Scope of this proof (decision D15, and D24)

Written against **merged `dev` at `450b9234a6f5626f21adea3c4da244550a3bdace`**
(2026-08-29 18:03:20 +0100).

This is **dev-merged evidence, pending the single wave-5 `dev` → `main`
promotion**. Per D15 the ticket walks to Done on this evidence; the exact-SHA,
non-force promotion to `main` happens once, at wave 5.

TICK-077 is **not an EPIC-011 member**. Decision **D24** pulled it into the
closeout: *"`TICK-077` (EXT-04) | `verifying`; code already shipped in
release 36 | Proof against merged `dev`, walk to Done, remove the
195-file-stale branch and worktree. **No code.**"* No code was written for this
proof.

**Unlike every EPIC-011 ticket, this one also has tier-3 deployed evidence:**
its code is on `main` and in production release 36.

## The work is on `dev` — and on `main`

PR [#574](https://github.com/collisionengineers/pegasus/pull/574) — "EXT-04:
direct EVA API submission of a case and its images (TICK-077)" — landed as
`09beefef` (2026-08-28 02:20:05 +0000). It is a **squash** commit (one parent,
`68adedaf`), which is why the branch's own commits are not ancestors anywhere.

```
git merge-base --is-ancestor 09beefef origin/main   -> exit 0 (ancestor)
git merge-base --is-ancestor 09beefef 450b9234      -> exit 0 (ancestor)
```

`origin/main` is `783b4b88` — "docs(release): record release 36 and the
deployed EVA API route (ENG-023) (#580)".

`docs/operations.md:314` records release 36 (2026-08-28), image
`sha256:5ba65f61…`, revision `pegasus-prod-web-252ow37gij--84132d01`, with
migrations `20260827143132_EvaApiSubmissions` and
`20260827143200_GrantEvaSubmissions`. `docs/operations.md:358` states it
"shipped the EXT-04 EVA API submission route."

### Correction to a premise in the closeout brief

The brief expected `git ls-tree origin/dev src/Pegasus.Core/Eva/` to show
`EvaApiTransport.cs`. **It does not, and should not.** `src/Pegasus.Core/Eva/`
holds `CaseEvaApiMapping.cs`, `CaseEvaMapping.cs`, `EvaApiContracts.cs`,
`EvaBundleSchema.cs`, `EvaSubmissionPolicy.cs`, `EvaSubmissionWorkItem.cs`.
The transport is the **adapter**, and lives where the architecture requires:
`src/Pegasus.Infrastructure/Eva/EvaApiTransport.cs`. Core owns the port
(`IEvaApiTransport`, declared in `EvaApiContracts.cs`); Infrastructure
implements it. The contracts file is present on both `dev` and `main` as
expected.

## Capability → production caller

| Capability the ticket names | Production caller | Evidence |
| --- | --- | --- |
| Direct EVA API submission of a case and its images | Route `@page "/Cases/{caseId:guid}/Eva/Send"` — `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml:1` | handler `OnPostSubmitAsync` at `Send.cshtml.cs:91`, reached by the rendered form at `Send.cshtml:67` `<form method="post" asp-page-handler="Submit" asp-route-caseId="@Model.CaseId">` with its submit button at `:69` |
| …reachable from the Case workspace | `src/Pegasus.Web/Pages/Cases/Details.cshtml:705` `<form method="post" asp-page="/Cases/Eva/Send" asp-route-caseId="@workflow.CaseId" …>` | gated on `@if (Model.CanSubmitToEva)` (`:703`) inside `@if (canSendToEva)` (`:648`), i.e. Case state Review — legitimate state, not a disabled feature |
| The Core submission use case | `ISubmitCaseToEva`, injected at `Send.cshtml.cs:36` and executed by `OnPostSubmitAsync` | not registration-only: a rendered control posts to it |
| The transport adapter | `IEvaApiTransport` registered **unconditionally** at `src/Pegasus.Infrastructure/DependencyInjection.cs:635` `services.AddSingleton<IEvaApiTransport>(provider => new EvaApiTransport(…))` | no `if` guard, no composition flag — it composes in every environment |
| Per-Principal submission settings (ADR-0034) | `EvaSubmissionPolicy.AllowsManualSubmission` — `src/Pegasus.Core/Eva/EvaSubmissionPolicy.cs:68`, consumed at `Pages/Cases/Details.cshtml.cs:486` and `Pages/Cases/Eva/Send.cshtml.cs:86` | administered through the deployed page `/Administration/Principals/EvaSubmission/{organizationId}/{principalId}` (`EvaSubmission.cshtml.cs:88` `updatePrincipalEvaSubmission.ExecuteAsync`), linked from `Administration/Principals/Index.cshtml:94` |
| Submission records persisted | `EfEvaSubmissionQueries` / `EvaSubmissionStore`; migrations `20260827143132_EvaApiSubmissions` + `20260827143200_GrantEvaSubmissions` | both **applied in production** (`docs/operations.md:359`), with grants verified in the same release record (`:362`–`:364`) |

### D21: this is legitimate state, not a closed gate

The submit control is **conditionally** enabled on a named condition — the
Principal's `EvaManualSubmission` setting — and an administrator can turn that
condition on today, through a deployed page, with no redeploy. Per the D21
table that is the row *"Control conditionally disabled with a named condition,
enabled when the condition is met — **Yes**, this is legitimate state, not a
disabled feature."*

This is materially different from a startup composition flag. For contrast,
`Features:ProviderApi` (TICK-058) is set in **no** configuration anywhere in
the repository except one integration test, so nothing behind it can be
reached in any deployed environment without a redeploy — that is the D21
"**No**" row, and TICK-058 is held on it. TICK-077's path composes
unconditionally and is switched by operator data.

## The branch and worktree carry nothing unmerged

Recorded per D24; **not acted on — no branch or worktree was removed.**

- Branch: `origin/task/tick-077-eva-api-submission`, tip `31659613`
  (2026-08-28 02:57:48 +0100 = 01:57 UTC).
- **Decisive check:** `git diff --quiet 31659613 09beefef` → **exit 0, the
  trees are byte-identical.** The squash commit that landed on `main` and `dev`
  carries exactly the branch tip's content. Nothing on the branch is unmerged.
- Staleness: `git rev-list --count origin/task/tick-077-eva-api-submission..origin/dev`
  → **253 commits behind**; `git diff --name-only <branch> origin/dev | wc -l`
  → **324 files differ**. (The brief's "~195 files" was an earlier measurement;
  `dev` has moved on since.)
- Worktree: `C:/Users/PC/Documents/GitHub/pegasus-worktrees/tick-077-eva-api-submission`
  is **not** in the current `git worktree list`, so only the branch remains.

**Conclusion: the branch is safe to delete.** It is fully merged by content and
253 commits stale. Removal is a closeout action for whoever owns it; this proof
only records the finding.

## Commands run, with exit codes

Run in the main checkout on `dev` at `450b9234`, Windows + PowerShell 7.

```
dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false
  -> Build succeeded. 0 Warning(s), 0 Error(s).   exit 0

dotnet test ./Pegasus.slnx --configuration Release --no-build -nodeReuse:false
  --filter "FullyQualifiedName~EvaApiTransportTests"
  -> Passed!  Failed: 0, Passed: 12, Skipped: 0, Total: 12, Duration: 2 s
     exit 0

git diff --quiet 31659613 09beefef        -> exit 0 (identical trees)
git merge-base --is-ancestor 09beefef origin/main -> exit 0
```

No `SqlException` transport-level error and no build file lock occurred, so
the result is a clean PASS rather than INCONCLUSIVE.

## What this evidence does NOT prove

- **Pegasus has still never called EVA, in any environment.**
  `docs/operations.md:387` states this in terms. Release 36 deployed the route,
  the migrations and the credential configuration; it proves **nothing about
  the vendor contract**. The transport is proven against the vendor's own
  recorded traffic in tests, not against the live service.
- **No Principal has either EVA toggle on in production.**
  `docs/operations.md:360`–`:361`: "`EvaSubmissions` reads zero rows and no
  Principal has either EVA toggle on". So the submit control is currently
  disabled for every Principal in the deployed estate. The capability is
  *deliverable and switchable*; it is not *exercised*. The outstanding step is
  "an operator-held live test with `EvaManualSubmission` on a Principal"
  (`:391`), which per **D26** batches into the release under a single approval
  conversation — no lane performs it.
- **The automatic-submission path is not claimed.** Only manual submission has
  a rendered control proven here. `AllowsAutomaticSubmission`
  (`EvaSubmissionPolicy.cs:74`) is off for every Principal, and the D20/D21
  decision record already rules that path undelivered.
- **Live EVA credentials were not swapped or tested.** `docs/operations.md:120`
  records the live-credential swap as a separate operator-gated change; EVA
  serves test and live from one host, so the credential pair alone decides
  which environment a deployment talks to.
- **No browser or layout walk.** **UIIMP-010** owns that.
- **Four of this ticket's 43 checklist items remain unticked** on the board
  record. They are the live-call items above, which cannot be discharged
  without the operator-gated activation.
- **The branch and worktree were NOT removed.** This proof records that they
  can be; it did not do it.

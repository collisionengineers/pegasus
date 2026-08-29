# Plan — AUTO-014

Implemented by `gpt-5.6-sol` (high) in
`../pegasus-worktrees/auto-014-ai-job-callers`. This plan records the approach
taken and the evidence, written alongside the work rather than before it, because
the ticket body already carried a completed audit.

## The two gaps, and where each caller went

The ticket's own body carries the audit that produced it: `ListForSubjectAsync`
existed only as its declaration, its EF implementation and two **test fakes**, and
`AiJobKind.QueryResponse` had no Web caller at all. Both are rule-14 "test-only",
which D20 makes ineligible for Done.

### 1. AI job by-subject query → the Inbox message's Case tab

- Route `GET /Inbox/{message-id}?section=case`
- Caller `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs:706` —
  `AiJobs = await aiJobQueries.ListForSubjectAsync(...)`
- Rendered consumer `src/Pegasus.Web/Pages/Mail/Message.cshtml:525`

The message's linked Case is the natural subject: the operator is already looking
at the record the jobs were raised against. The query runs **only on the Case
tab**, not on every message load.

### 2. Staff-created `QueryResponse` jobs → the linked post-report message

- Rendered control `Message.cshtml:74`
- Handler `Message.cshtml.cs:228`
- Core command `Message.cshtml.cs:257`

Reachable for a linked retained post-report message whose Case is in an eligible
post-report state and whose administrator AI switch is enabled.

## Reused, not rebuilt

`ICreateAiJob` and `IAiJobQueries` exactly as [[AUTO-011]] shipped them — **no
second query, command or port was added**, and `Core/AiWork` is untouched.

`AutomationActorResolver` was deliberately **not** reused: it resolves MCP
automation actors, and this caller is authenticated staff. Reusing it would have
mislabelled the actor on the ledger.

## The disabled control here is legitimate state, not a seam

When the Case is unavailable or ineligible, or the administrator AI switch is
stopped, the action renders as a real disabled button inside `.gated` with a
non-empty `data-condition`.

Under the D21 table this is the **"conditionally disabled with a named condition,
enabled when the condition is met"** row — legitimate state, not a disabled
feature. It is not a D7 integration seam and needs no ticket. `data-condition` is
always set, so PLAT-061's empty-pill defect is avoided.

**No feature flag was touched** (D26).

## One assertion was corrected during the run — it was not a weakening

The first focused run was 1 passed / 1 failed. The new test asserted
`DoesNotContain(messageId)` across the **whole rendered page**, and the page is
`/Inbox/{message-id}` — its own URL, forms and links necessarily contain the
GUID, so that assertion could never pass. It was a wrong assertion in a
brand-new test, not a pre-existing check.

It now asserts the real invariant: the message id does not leak into the **AI
jobs panel**, scoped between
`<section class="panel" aria-labelledby="case-ai-jobs-title">` and `</section>`
(`MailWorkspaceWebTests.cs:336-340`). The job's subject is the Case, not the
message, and that is what the panel must show.

**Verified by the orchestrator** across `origin/dev...HEAD`: **0** removed
`Assert.` lines, **0** new `Skip`/`[Ignore]`, **0** deleted test methods.

## Verification

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false` | exit 0, 0 warnings, 0 errors, **0 `CS####`** |
| Focused filter on the two new tests | **Failed 0, Passed 2** |

## Simplification pass — 2026-08-29

- **Reuse** — extended the existing Mail page and the existing Core ports; no
  parallel route, command or query.
- **Simplification** — no new abstraction, service, partial, script, package,
  migration or configuration.
- **Efficiency** — the subject query runs only on the Case tab; the switch and
  state checks run only for a relevant post-report source.
- **Altitude** — Core keeps authorization, current-state validation, kill-switch
  enforcement and ledger policy. Web owns only route/source validation and
  presentation.

No unapplied findings.

## What this does NOT yet prove

AUTO-011's two caller gaps are removed **on this branch**. AUTO-011 cannot return
to Done until this merges to `dev` and its own re-audit runs against merged
evidence (D15). Independent cross-model review and CI are still outstanding at
the time of writing.

## Commits

- `5ad8b4d3` — feat(ai): wire QueryResponse staff caller
- `448cd14e` — test(ai): prove AI job production callers

## Cross-model pre-merge review dispositions — 2026-08-29

A Claude-family reviewer (this lane was Codex-built) returned
`APPROVE_WITH_FINDINGS`, **no blockers**. It chased every enabling condition to a
rendered control or a provisioned trigger rather than accepting the caller list,
and re-ran a superset the lane had not: the whole `MailWorkspaceWebTests` class,
**43 passed, 0 failed** — so the additions break none of the 41 pre-existing
tests in that file.

### The most important correction — **this ticket is not sufficient for AUTO-011**

The plan said AUTO-011 could return to Done once this merges. **That is wrong**,
and the checklist is corrected accordingly. AUTO-011's own `## What` names two
further capabilities this PR does not supply:

| Still unwired | Owner |
| --- | --- |
| `ICancelAiJob` ("cancel (staff)") | **[[PLAT-049]]** — its only caller lived on that branch, now merged to `dev` |
| `IConfirmAiJob` | **[[ENG-028]]** (PR #630, in review) |

And a third condition that belongs to neither: **the `AiJobs` table does not
exist in production.** Release 36 predates the AUTO-011 merge, so
`20260828084601_AiJobs` and `20260828084644_GrantAiJobs` are unapplied and both
of this ticket's callers would fault in the deployed estate. That is owned by the
single wave-5 promotion (D25, release 37).

**AUTO-011's re-audit must therefore wait on AUTO-014 + PLAT-049 + ENG-028**, and
must state the production-activation gap rather than counting around it.

### Finding (medium) — two AI-job label classes and two AI-job tables · **RECONCILE AT MERGE**

This PR adds `OperatorLabels.QueryResponseJobs` with `PanelTitle = "AI jobs"`.
[[PLAT-049]] adds `OperatorLabels.AiJobs` with `PanelTitle = "AI Job List"` plus
`OpenQuery`/`CompleteJob`/`Cancel`/`ReviewEstimate`. Two label vocabularies and
two rendered tables for one concept, with different titles and different columns
— rule 8 and the "one list per concept" rail, which says a second copy is
duplication "even when it is 'just strings'".

**Ordering decision: PLAT-049 merged first (`0a2d955d`), so its `AiJobs` class is
canonical** — it is the FRD-11-named home ("The AI Job List on `/operations`").
This branch reconciles onto it: reuse `OperatorLabels.AiJobs` for the shared
words and keep only genuinely query-response-specific labels. Fixed here rather
than deferred, because both lanes were open at the same time (D19 case 1).

### Finding (medium) — the by-subject panel sits on a surface no authority names · **KEPT, recorded as interim**

FRD-11 names exactly one job list, on `/operations` (`frd-11:252`). EPIC-011
context §1.3 specifies the message Case tab as "summary card, Open Case, Change
association" — no AI panel. §1.8 puts per-case AI visibility on the Case
workspace **Notes view**, which is [[CASE-029]]'s scope.

**Kept**, because the alternative homes are all in flight, the port is genuinely
reusable and only the render would move, and blocking here would strand AUTO-011
for no product gain. **But recorded as an interim reader with CASE-029 as its
eventual home**, not as "the natural home" — the plan and checklist stated that
more confidently than the evidence supports, and the research document already
conceded it.

The reviewer's separate note is worth carrying: the plan's stated reason ("the
natural subject") is weaker than the real one — every `Cases/Details` file was
claimed by three in-flight branches, so the Case page was unavailable under D19.

### Gate state — a correction to this ticket's own framing

The reviewer established, and I accept:

- **`Features:SendToAi` governs AI-09**, the channel hand-off
  (`src/Pegasus.Web/AiWork/SendToAi.cs`), **not** the AI-10 job ledger.
  `ICreateAiJob`, `IAiJobQueries` and `ISendToAiControl` are registered
  unconditionally (`DependencyInjection.cs:351-358`) and `/Inbox/{id:guid}`
  carries no flag. **Neither caller sits behind a closed composition gate**, so
  D21's closed-gate row does not apply.
- **The administrator AI switch defaults OPEN**:
  `EfAiWorkRequestStore.cs:277` returns `control?.Enabled ?? true`, and no
  migration seeds a row. `QueryResponseCondition = "Automation stopped"` is a
  state an administrator chooses, not a shipped-disabled default. D21 row 2.
- The `data-condition` **cannot** be empty: the else branch renders only when
  `IsQueryResponseSource` is true and `QueryResponseEnabled` false, and both
  `return Page()` sites run `LoadAiJobContextAsync` first. PLAT-061's empty pill
  cannot occur here.

### Findings (low) — accepted with reasons

| Finding | Disposition |
| --- | --- |
| Two sources of truth for "this message's Case" — the handler uses `detail.Summary.CaseId`, the gate uses `CurrentCase` from `AssociationReceipt.CurrentCaseId` | **Accepted.** Core re-validates the subject so no bad job can be written; if they diverged the button renders enabled and the POST fails loudly. |
| `Create = "Draft reply with AI"` is a third phrasing beside "Send to Claude" and "Send Unidentified to AI" | **Accepted**, and reconciled with PLAT-049's labels in the merge fix above. |
| This PR introduces the page's first `.record-bar`, holding one button; §1.3 specifies it as Reply/Forward/Compose/Flag/Delete | **Flagged for [[MAIL-026]]**, which must merge five contract controls into a bar this lane created. The diff's own comment is honest about it. |
| The only registered classifier cannot emit `PostReportEmails`; every post-report query must be manually reclassified first | **Accepted, and it must be stated in AUTO-011's re-audit** so "reachable" is not read as "routine". |

### Assertion integrity — confirmed

`git diff origin/dev...HEAD -- tests/`: **106 insertions, 0 deletions, 1 file.**
Every deleted line in the entire PR is one of four comment lines, listed
individually by the reviewer. No `Skip`/`[Ignore]` added anywhere.

On the rescoped panel assertion: the reasoning holds and it is **not** trivially
passing — the `Between` helper itself asserts the markers exist, so an absent
panel fails rather than vacuously passing. But the reviewer is right that it is
**thin**: the panel renders only Kind, Created and State, so no column could
carry the message id today; it guards a hypothetical future column. And neither
test proves the list is subject-*filtered* — one Case is seeded, so a query
ignoring its argument would pass identically. That semantic is AUTO-011's and
tested there; noted as a small omission, not a defect.

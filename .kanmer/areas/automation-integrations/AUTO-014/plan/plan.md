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

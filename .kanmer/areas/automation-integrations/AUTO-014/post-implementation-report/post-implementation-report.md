# Post-implementation report — AUTO-014

Implemented by `gpt-5.6-sol` (high). Every number below was **re-run by the
orchestrator** rather than taken on report.

## What changed

| File | +/− |
| --- | --- |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml` | 83 / 1 |
| `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` | 115 / 3 |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | 14 / 0 |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | 106 / 0 |

314 insertions, 4 deletions. No migration, no package, no new project.
`Core/AiWork` is **untouched** — this ticket adds callers, not capability.

## The two gaps closed

**1. AI job by-subject query** — the Inbox message's Case tab.

- route `GET /Inbox/{message-id}?section=case`
- caller `Pages/Mail/Message.cshtml.cs:706` —
  `AiJobs = await aiJobQueries.ListForSubjectAsync(...)`
- rendered at `Pages/Mail/Message.cshtml:525`

The query runs only on the Case tab, not on every message load.

**2. Staff-created `QueryResponse` jobs** — the linked post-report message.

- control `Pages/Mail/Message.cshtml:74`
- handler `Pages/Mail/Message.cshtml.cs:228`
- Core command `Pages/Mail/Message.cshtml.cs:257`

Reachable for a linked retained post-report message whose Case is in an eligible
post-report state and whose administrator AI switch is enabled.

## The disabled state is legitimate, not a seam

When the Case is unavailable or ineligible, or the administrator AI switch is
stopped, the control renders as a real disabled button inside `.gated` with a
non-empty `data-condition`.

That is D21's **"conditionally disabled with a named condition, enabled when the
condition is met"** row — legitimate state, not a disabled feature. It is not a
D7 integration seam and needs no ticket. `data-condition` is always set, so
PLAT-061's empty-pill defect does not apply. No feature flag was touched (D26).

## Reused

`ICreateAiJob` and `IAiJobQueries` exactly as [[AUTO-011]] shipped them. No
second query, command or port.

`AutomationActorResolver` was **deliberately not reused**: it resolves MCP
automation actors, and this caller is authenticated staff. Reusing it would have
mislabelled the actor on the ledger.

## Assertion integrity — verified independently

Across `origin/dev...HEAD`: **0** removed `Assert.` lines, **0** new
`Skip`/`[Ignore]`, **0** deleted test methods.

One assertion was corrected mid-run, and it is worth stating precisely because it
looks like a weakening and is not. A **new** test asserted
`DoesNotContain(messageId)` across the whole rendered page. The page is
`/Inbox/{message-id}` — its own URL, forms and links necessarily contain that
GUID, so the assertion could never have passed. It was a wrong expectation in a
test written by this change, not a pre-existing check.

It now asserts the real invariant: the message id does not leak into the **AI
jobs panel**, scoped between
`<section class="panel" aria-labelledby="case-ai-jobs-title">` and `</section>`
(`MailWorkspaceWebTests.cs:336-340`). The job's subject is the Case, not the
message, and that is exactly what the panel must not reveal.

## Verification

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false` | exit 0, 0 warnings, 0 errors, **0 `CS####`** |
| Focused filter on the two new tests | **Failed 0, Passed 2** |

First focused run was 1 passed / 1 failed on the wrong expectation described
above; disclosed by the implementer rather than hidden, and confirmed here.

## File ownership — a D19 case-2 change, declared

`Pages/Mail/**` is MAIL-025's by `waves.md`. MAIL-025 sits in `verifying`, held
by the rule-14 reversal, **with no branch in flight** — verified against every
remote `task/*` branch: only this lane changes `Message.cshtml(.cs)`.

**Consequence for MAIL-025:** its re-prove must merge `dev` forward before
auditing, or it will audit a stale page.

`OperatorLabels.cs` is shared; this appends one nested class and reorders
nothing.

## Simplification pass — 2026-08-29

- **Reuse** — extended the existing Mail page and existing Core ports; no
  parallel route, command or query.
- **Simplification** — no new abstraction, service, partial, script, package,
  migration or configuration.
- **Efficiency** — the subject query runs only on the Case tab; switch and state
  checks run only for a relevant post-report source.
- **Altitude** — Core keeps authorization, current-state validation, kill-switch
  enforcement and ledger policy; Web owns only route/source validation and
  presentation.

No unapplied findings.

## What this does NOT prove

AUTO-011's caller gaps are closed **on this branch only**. AUTO-011 cannot return
to Done until this merges and its own re-audit runs against merged `dev` (D15).
Independent cross-model review and CI are outstanding — and because this was
built by Codex, its review must be **Claude-family**.

One judgement is deliberately left open for that review: whether a
staff-initiated `QueryResponse` job is in alpha scope at all. The ticket named
removal of the kind as the alternative if [[TICK-101]]'s activation gate meant it
was not. Wiring was preferred over deleting a capability Core already constructs
and a migration check constraint already pins — but a reviewer should test that
call rather than inherit it.

## Commits

- `5ad8b4d3` — feat(ai): wire QueryResponse staff caller
- `448cd14e` — test(ai): prove AI job production callers

PR **#629** open against `dev`, not merged.

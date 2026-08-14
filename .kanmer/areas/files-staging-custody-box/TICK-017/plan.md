# Plan — TICK-017 (DOC-01): Automatic Box case-folder creation using the Case/PO name

Written FROM research.md and impact.md. Both establish that the four
locally-provable boundary behaviours (immutable Case/PO naming,
response-loss-safe binding, fail-closed conflict handling, human reasoned
recovery) are implemented and tested against Box fakes through the real
outbox→worker→processor caller. Accept-and-prove plan, not a build plan.

## Approach

Do not change `BoxCaseCustody` / `CustodyNames` / the retry policy — proven and
invariant-bearing. Satisfy the ticket's Verification: record the contract/
caller/failure/tests, and settle activation criteria. Replace the placeholder
`proof.md` ("Operator confirmed") with real local-tier evidence by running the
custody suites, and **explicitly defer the live tier** — "Live controlled Box
target proof, migration, deployment and operator acceptance" — as
`requires-live-approval`. Do NOT wire or call any live Box target: the approved
disposable subtree `392761581105` is documented but unwired, and any live Box
mutation needs explicit per-target approval (CLAUDE.md). This beats "attempt a
live smoke now" (would mutate Box without approval — a stop condition) and
"leave the bogus proof in place" (misrepresents evidence).

## Steps

1. Distil the contract into proof.md's "What was verified": Core port
   `ICaseCustody`; Infra `BoxCaseCustody` over `BoxContentClient`; folder name =
   `CustodyNames.SafeName(reference)` + immutable `pegasus-case-binding.json`;
   predeclared-owner staging + ETag promotion (response-loss-safe);
   occupied-name/duplicate/wrong-type/trashed fail-closed with zero mutation;
   `RetryCaseCustody` staff-reasoned recovery; caller chain
   acceptance-enqueue→worker→`EfQueuedCustodyProcessor`→`CreateCaseRootAsync`.
2. Run the focused custody suites (local caller-proof evidence):
   `ProductionBoxCustodyTests` (adapter, fakes), `CustodyOutboxIntegrationTests`
   (outbox→worker→processor + reasoned retry, real SQL + `LocalCaseCustody`),
   `ProductionCompositionTests` (Box adapter resolves from production
   composition, no network).
3. Overwrite the placeholder `proof.md` with the real pasted `dotnet test`
   output as the local tier. Record in "Not covered": live controlled Box target
   proof, migration, deployment, operator acceptance — each requires-live-approval
   and none runnable under default local/CI composition.
4. Confirm the Case/PO reference forms minted by INT-25
   (`QDOSyyNNN`, `a.`/`ap.`) satisfy `CustodyNames.SafeName` (≤120 chars, no
   reserved names) — closes the blocks-INT-25 handoff.
5. Present the live-tier deferral and the "wire disposable subtree `392761581105`
   for a one-off live smoke — yes/defer?" decision to the user, then move to
   `review` — do NOT self-advance to `done`; live acceptance is the operator's.

## Verification

proof.md is produced from steps 2–3: pasted `dotnet test` summaries for the
three custody suites are the evidence for the four local behaviours. Behaviours
to confirm in output: a folder is named literally after the reference with the
immutable binding; a lost create/upload response reconciles without duplication;
occupied-name / duplicate-child / wrong-type / outside-root all fail closed with
zero mutation and no background retry; staff reasoned retry replays under the
exact taxonomy. Plus the step-4 reference-format check. No live Box call.

## Risks / open questions

- **Live tier is the operator's call**: whether to approve wiring the disposable
  subtree `392761581105` for a live create/reconcile smoke, or defer entirely to
  go-live. Until approved, DOC-01 holds at `review`, not `done`.
- **Placeholder proof**: the current "Operator confirmed" line will be replaced;
  if the operator intends it as a real acceptance record, capture what/when they
  confirmed against which target.
- **Blocked-by INT-25**: reference-format confirmation depends on INT-25's
  pinned contract (step 4).
- **No live mutation without approval** — hard boundary; do not touch the
  production root `405543781910`.

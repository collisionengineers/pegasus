# Checklist

- [ ] Name the triage-request category once; both literal copies use it
- [ ] A classified triage request is `NeedsSorting`, never `CaseCreated`
- [ ] One `AcceptedTriageMatch` evidence entry derived from the classification
- [ ] `IIntakeTriageMatcher`, `NoAcceptedIntakeTriageMatcher`, `IntakeTriageMatch` deleted
- [ ] DI registration and extraction-policy parameter removed; policy version 5 → 6
- [ ] Subject registration rule; vehicle rule stops swallowing the label
- [ ] Triage created when a registration is known
- [ ] Unidentified registered when it is not, and not when a Triage was created
- [ ] Core tests: decision, evidence, both branches, both subject spacings, ambiguity
- [ ] Four integration suites moved off the `AcceptedTriageMatchPolicy` stub
- [ ] Production composition test pins the active route
- [ ] `docs/open-decisions.md` triage-matcher paragraph closed
- [ ] FRD-03, FRD-09, `qdos.md`, `capabilities.md` updated
- [ ] Release build green
- [ ] Core tests green
- [ ] Integration tests green (CI shards on the exact SHA)
- [ ] Simplification pass over the branch diff, recorded in the plan
- [ ] PR into `dev`, independent review, merge
- [ ] Proof on merged `main`

## Progress notes

**2026-08-24** — Implemented on `task/intk-033-triage-from-intake`, commit `7b43ab17`.

Scope deviation from the plan, recorded rather than quietly taken: the plan said
to move four integration suites off the `AcceptedTriageMatchPolicy` stub. I did
not. Those suites test the downstream contract — replay safety, multi-match
fail-closed, case association — and that contract is unchanged, so the stub is
still a valid way to reach it. Rewriting them would have been a large diff for
no added proof. Instead a new suite,
`tests/Pegasus.IntegrationTests/TriageFromIntakeIntegrationTests.cs`, drives the
**real** classification path end to end and proves both branches of the
operator's rule. Three tests, green first run.

Also changed and worth a reviewer's eye:
`ProcessIntakeTests.ClassificationIsRecordedOnlyAndNeverChangesTheIntakeDecision`
pinned "a classification never changes the decision" using a **triage** message.
That invariant was already qualified before this branch — the standalone-Audit
rule at `ProcessIntake.cs:189` has downgraded `CaseCreated` on a classification
fact for some time — and FRD-01 only forbids classification mutating *Case*
state, which this does not do. The test now uses an automatic reply, where the
invariant genuinely holds, and the triage exception is its own named test.

Corpus reading narrowed fault 3: the body-phrase template carries a labelled
`Registration:` line and was already extracted correctly. Only the subject
template needed work.

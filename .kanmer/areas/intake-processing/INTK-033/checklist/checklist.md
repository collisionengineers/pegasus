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

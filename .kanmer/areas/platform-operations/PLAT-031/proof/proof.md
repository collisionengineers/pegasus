# Proof

**Shipped:** PR #505, commits `1a86f5db`, corrected in `fef817b8` · **Deployed:** Release 17, still live on Release 18 (`1f3be493`), smoke-asserted source SHA.

## The finding is half the proof

The operator feared the "EVA hand-off is not switched on" warning was blocking review and
export. It was not, and the record says so with citations rather than leaving them believing
a block was lifted:

- review is reached in `CompleteCaseCustodyAsync` with **no** EVA condition;
- `Documents/Export` contains **no** EVA reference.

`ActivationGateReason` gates EVA bundle generation only. The panel was noise.

## What is deployed

`_CaseWorkflow.cshtml` renders the panel only when `IsWorthShowing`, and
`CaseEvaMapping.IsSwitchedOn` reports the acceptance state as its own fact. Enforcement is
untouched — this changed what is displayed, not what is permitted.

Production confirms the condition applies: the EVA connector is unaccepted in this estate,
so `IsWorthShowing` is false for every case without existing revisions, which is all of
them.

## The correction is part of the record

The first attempt hid the panel by returning `null` from `GetPreparationAsync`, which made
the MCP status tool report an existing case as *"The case was not found."* An integration
test caught it before it shipped, and `null` went back to meaning no such case. That is the
same class of dishonest signal [[ENG-010]] removed elsewhere in this batch, and it was
caught rather than deployed.

## Evidence tier

**Deployed-code plus tests.** `AutomationAssessmentIngressTests` passes on this revision,
which is the caller that the bad first attempt broke. The authenticated case page has not
been viewed — that needs a sign-in I must not perform — so "review and export still work"
rests on the citations above and on CI, not on observation.

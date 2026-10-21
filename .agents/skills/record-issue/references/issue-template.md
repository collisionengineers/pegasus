# Issue template

Title: one line, under 80 characters, naming the area and the symptom or
task, not the cause. `Area: what is wrong or what to do`. Examples from the
repo: "Valuation refusal still says 'Press Use this value again' after the
button was removed", "Work Centre Activity: Sent to Engineer reads every
first-send event ever recorded on each load".

Body: fill the sections that apply and delete the rest. Short is fine; a
later investigation fills in the analysis. Put file paths, type names and
member names in backticks, because the investigation tooling resolves
backticked names to files at the tip and lists the commits that touched them.

```markdown
## What happens

Two to five sentences. What was observed, where (page, Case reference,
URL, mailbox, report), and when. Quote the exact text seen where wording is
the problem. For a task or decision, state the current behaviour instead.

## Expected

What should happen, or the question to settle. Cite the owner if known
(FRD/ADR/capability ID), or say "not covered by an FRD" if that is the case.

## Where in the code

Paths and names, in backticks, only as far as a quick look got:
`src/.../Owner.cs`, `TypeName`, `MemberName`. "Not looked" is acceptable.

## Done when

One or two sentences a reviewer can tick: the observable outcome, not the
implementation.

Raised from <PR #N / session on <date> / Case <ref> / operator remark>.
```

Labels: one `type:*` the repo already has. `type:bug` for behaviour that
contradicts the expected; `type:task` for work with a clear direction;
`type:decision` when the operator has to choose first; `type:feature` for a
new capability. Add nothing else unless the user names it.

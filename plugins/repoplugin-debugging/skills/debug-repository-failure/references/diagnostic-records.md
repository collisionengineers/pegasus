# Diagnostic records

Keep diagnosis evidence small, factual, and attributable. Do not place secrets, corpus bodies, or unrelated user changes in task artifacts.

## Suggested artifact shape

`failure-brief.md`:

- Reported symptom, expected result, impact, environment, and source.
- Exact reproduction command or manual steps.
- Observed result and whether reproduction succeeded.

`facts.md` contains direct observations only. `hypotheses.md` keeps a numbered list with evidence for and against, the next discriminating check, and status: open, rejected, or supported.

`root-cause-report.md` cites the reproduction, facts, rejected hypotheses, caller/policy owner, cause, and remedy. If no conclusion is supported, replace it with `diagnostic-handoff.md` that names the next discriminating action and owner.

`remediation-NNN.md` is an implementation request, not a silent patch: scope, accepted facts, desired fix, risks, and required revalidation.

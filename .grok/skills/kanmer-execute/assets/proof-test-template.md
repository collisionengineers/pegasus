# Test proof — <ticket id>

*The test proof. Not a summary — paste what **actually ran**, not what should have.*

For a `proof:test` requirement. Gathered on the configured integration branch after review and merge, not the feature
branch.

## Commands and output

Real output, pasted. Counts before and after where a test was added, so a
reviewer can see the delta rather than take it on trust.

```
$ <command>
<output>
```

## What the failing case looked like

For a fix: the same test against the unfixed code. A test that passes after a
change proves nothing unless it failed before it.

## Not covered

Paths the tests do not reach, stated rather than left to inference.

## Verification identity and attempts

- Integration branch: resolve from get_status.delivery.integrationBranch.
- Exact SHA, environment, command exit codes and evidence paths.
- Retain every failed or inconclusive attempt and its disposition.
- Deployment and live operator acceptance are separate proof.
- PASS is required for ordinary Done; this template grants no waiver.

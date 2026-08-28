# Post-implementation report — ENG-022

PR: https://github.com/collisionengineers/pegasus/pull/579
Branch: `task/eng-022-parameters-bom`

## What shipped

Three bytes removed from the front of `infra/main.parameters.json`. No content
change; the diff is one line.

## Why it was needed

`azd provision` reads that file with Go's JSON decoder, which refuses a BOM:

```
error unmarshalling Bicep template parameters: invalid character 'ï' looking
for beginning of value
```

TICK-077 rewrote forty files with a UTF-8 BOM. Thirty-nine are C#, Markdown and
bicep, all of which tolerate one. This file is the only one parsed by something
that does not, so it was the only one in scope.

## Deviations from the plan

None. The ticket named the fix exactly.

## Verification

`changes`, `documentation`, `local-development-scripts`, `reference-data` and
`infrastructure` green; the code suites correctly skipped, no build-relevant
path having changed. The real proof is the next one: `azd provision` against
`rg-pegasus-prod` got past `Initialize bicep provider` and completed, which it
could not do before.

## Left for the reviewer

Nothing. The remaining thirty-nine BOMs are cosmetic and out of scope here; if
they are worth removing, that is its own ticket.

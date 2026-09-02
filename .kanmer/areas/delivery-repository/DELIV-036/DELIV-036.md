---
id: DELIV-036
type: ticket
title: >-
  Qdos extraction regexes thrash the static Regex cache and one has no match
  timeout
status: verifying
area: delivery-repository
order: 150
assignee: codex-gpt-5.6-luna
profile: fix
stageEntered:
  preparing: '2026-08-29T16:28:28.550Z'
  review: '2026-08-29T18:43:47.958Z'
  verifying: '2026-08-29T20:56:04.962Z'
taken_at: '2026-08-29T16:29:48.131Z'
branch: task/deliv-036-qdos-regex-cache
worktree: ../pegasus-worktrees/deliv-036-qdos-regex-cache
labels:
  - ci
  - flaky
  - intake
  - performance
groups:
  - EPIC-011
links:
  - DELIV-031
  - DELIV-034
  - DELIV-035
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
commits:
  - a31daccc216e6a9515456e46ccd1ded8cff6b63d
  - 7e2bccb0
prs:
  - '626'
deployment: production
archived: false
created: '2026-08-29T16:03:14.405Z'
updated: '2026-09-01T14:44:16.417Z'
---

## What

CI `unit` failed on PR #625 with:

```
Pegasus.Core.Tests.Intake.Qdos.QdosInstructionExtractionPolicyTests
  .AReportsVehicleLineFillsTheDetailsTheLetterLacks [FAIL]
System.Text.RegularExpressions.RegexMatchTimeoutException
```

This is the **third** distinct CI flake in this programme, after [[DELIV-031]]
(SQL connect timeout) and [[DELIV-034]] (credential tamper no-op). Unlike those
two it is not test-only: the root cause is in production code.

## Root cause

`src/Pegasus.Core/Intake/InstructionFieldExtraction.cs:155-169` builds its
patterns by string interpolation and passes them to the **static**
`Regex.Match` / `Regex.IsMatch` overloads. Those overloads go through
`Regex.CacheSize`, which defaults to **15** and is never raised anywhere in the
repository.

The QDOS policy declares 47 labels
(`src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs:29-98`)
and constructs four distinct patterns per label — `FindCandidates` builds two,
plus `StartsWithKnownFieldLabel`, `TruncateAtFollowingFieldLabel` and
`ContainsLabel`. That is over 150 distinct pattern strings cycling through a
15-entry cache, so in practice **every call re-parses and re-compiles its
`Regex`**.

The budget is `TimeSpan.FromMilliseconds(100)` and is **wall-clock, not CPU
time**. `tests/Pegasus.Core.Tests` has no `xunit.runner.json`, so collections
run at processor-count parallelism. On a four-vCPU runner a scheduling stall
past 100 ms inside a single match is entirely achievable.

**This is not catastrophic backtracking.** No pattern here has nested
quantifiers, back-references, or a quantified group over an overlapping class.
The riskiest constructs — `(?:^|[|;\t]\s*|\s{2,})` before `\s*`, the
`reg\s*no|reg` alternation, the variable-length lookbehind — are all bounded
constant-factor costs. Test inputs are ~55 characters. Do not "fix" this by
rewriting the patterns.

## A second, separate defect in the same file

`QdosInstructionExtractionPolicy.cs:218-245` (`CutAtNextColumnLabel`) calls the
four-argument `Regex.Replace(input, pattern, replacement, options)` overload,
which carries **no** `TimeSpan`, so its `MatchTimeout` is
`Regex.InfiniteMatchTimeout`:

```csharp
private static string CutAtNextColumnLabel(string value) => Regex.Replace(
        value,
        ReportColumnCutPattern,
        string.Empty,
        RegexOptions.CultureInvariant)   // no timeout argument
    .Trim();
```

Every other regex on this path carries a 100 ms budget; this one can hang a
production intake indefinitely on hostile input. It cannot be the source of the
observed exception (it never throws), which is precisely why it went unnoticed.

## Owns

`src/Pegasus.Core/Intake/InstructionFieldExtraction.cs`,
`src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs`,
and their tests.

## Approach

Search before you build — check whether a cached-`Regex` helper already exists
before adding one.

- Hold the per-label patterns as constructed `Regex` instances rather than
  routing them through the static cache. The label set is fixed and known at
  construction, so this is a lookup, not a new abstraction.
- Give `CutAtNextColumnLabel` the same explicit timeout every sibling has.
- Do not weaken or delete the assertions in
  `QdosInstructionExtractionPolicyTests`, and do not widen the 100 ms budget as
  the primary fix — the budget is not the defect.

## Verification

- [ ] `AReportsVehicleLineFillsTheDetailsTheLetterLacks` and the rest of
      `QdosInstructionExtractionPolicyTests` pass, with the assertions intact.
- [ ] No static `Regex.Match`/`IsMatch`/`Replace` call on the QDOS extraction
      path builds its pattern by interpolation.
- [ ] Every regex on the path carries an explicit match timeout.
- [ ] A named production caller still reaches the changed code (rule 14).

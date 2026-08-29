# Post-implementation report — DELIV-036

Implemented by `gpt-5.6-luna` (xhigh) in
`../pegasus-worktrees/deliv-036-qdos-regex-cache`; the orchestrator
independently re-ran every number below and added the third commit.

## What changed

`src/Pegasus.Core/Intake/InstructionFieldExtraction.cs` (+191/−110 before the
cleanup commit) and
`src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs`
(+107/−…). No test file was changed and no assertion was touched.

### Step 1 — `CutAtNextColumnLabel` now has a match timeout

The four-argument `Regex.Replace` overload carries no `TimeSpan`, so its
`MatchTimeout` was `Regex.InfiniteMatchTimeout` — the only regex on the QDOS
extraction path that could hang a production intake indefinitely.

```diff
- private static string CutAtNextColumnLabel(string value) =>
-     Regex.Replace(value, ReportColumnCutPattern, string.Empty, options);
+ private static string CutAtNextColumnLabel(string value) =>
+     ReportColumnCutRegex().Replace(value, string.Empty);

+ [GeneratedRegex(ReportColumnCutPattern, RegexOptions.CultureInvariant, 100)]
+ private static partial Regex ReportColumnCutRegex();
```

`ReportColumnCutPattern` is a `private const string`, so it was directly
eligible for the file's existing `[GeneratedRegex]` convention. Nothing new was
designed.

### Step 2 — fixed patterns off the static cache

Every remaining literal-pattern call in both files became a `[GeneratedRegex]`
partial method alongside the ones already there. The three identical `@"[\s-]"`
literals now share one generated regex — one list per concept, applied to a
pattern.

### Step 3 — the interpolated per-label patterns are built once

This was the actual flake source. A new `LabelRegexCache` is constructed from
the `FieldDefinition` set and holds `Regex[]` arrays per label
(`candidate`, `explicitCandidate`, `startsWith`, `followingLabel`), looked up by
`(definition, labelIndex)` instead of re-interpolating per line. The label set is
fixed at construction, so this is a lookup, not a new abstraction.

**Verified: zero static `Regex.Match` / `IsMatch` / `Replace` calls remain in
either file.**

### Third commit — dead code removed (orchestrator)

`ContainsLabel` had **no caller in `src/` or `tests/` before this ticket and
none after**; `git grep "ContainsLabel" origin/dev -- src/ tests/` returns only
its own definition. The regex work had given it a
`ConcurrentDictionary<string, Regex>` cache — an abstraction serving zero
callers, which the repository's rule ("no abstraction without a second concrete
caller, an external boundary, or an accepted ADR") does not permit.

The method and its dictionary are deleted, which also drops the last
`System.Collections.Concurrent` dependency from the file. This is the only place
the implementation over-built, and it was building on pre-existing dead code
rather than introducing it.

## Reused, not rebuilt

The `[GeneratedRegex]` source-generator convention already used in both files —
`InstructionFieldExtraction.cs:474-487` (`WhitespaceRegex`,
`OrdinalDaySuffixRegex`, `MileageRegex`, `RegistrationRegex`) and
`QdosInstructionExtractionPolicy.cs:597-607`, which already passes the 100 ms
timeout as the generator's third argument.

## Behaviour is unchanged

Pattern bodies and `RegexOptions` are preserved verbatim; only *where the
`Regex` object comes from* changed. No test was modified, and
`AReportsVehicleLineFillsTheDetailsTheLetterLacks` — the test that produced the
original `RegexMatchTimeoutException` — still stands at
`QdosInstructionExtractionPolicyTests.cs:721` with its assertions intact.

Neither of the two things the plan forbade was done: no pattern was rewritten,
and the 100 ms budget was not widened.

## Rule 14 — the production caller

`src/Pegasus.Core/Intake/ProcessIntake.cs:585` invokes the extraction policy;
the production QDOS implementation is registered at
`src/Pegasus.Infrastructure/DependencyInjection.cs:156`. The changed code sits
on the live intake path — this is a fix to reachable code, not new capability.

## Verification (re-run by the orchestrator, not taken on report)

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false` | **Build succeeded**, 0 `CS####` diagnostics |
| `dotnet test … --filter "FullyQualifiedName~QdosInstructionExtractionPolicyTests\|FullyQualifiedName~InstructionField"` | **Failed: 0, Passed: 51** |

Both runs are after the dead-code removal commit.

## What this does NOT prove

The flake was intermittent and load-dependent, so a green local run is not proof
it is gone. The claim is causal, not statistical: 150+ interpolated patterns no
longer contend for a 15-entry `Regex.CacheSize`, so the re-parse cost that made a
100 ms wall-clock budget reachable under parallel load is removed. Watch
`QdosInstructionExtractionPolicyTests` in CI over the remaining merges before
calling it settled.

## Simplification pass — 2026-08-29

- **Reuse** — the existing generated-regex convention was used throughout; no
  new pattern, helper or package.
- **Simplification** — one generated regex replaces three identical `@"[\s-]"`
  literals; the dead `ContainsLabel` and its cache are gone.
- **Efficiency** — this is the ticket's subject: patterns are constructed once
  instead of per call.
- **Altitude** — business policy stays in `Pegasus.Core`; nothing moved layer.

One finding, applied: the `ConcurrentDictionary` serving a callerless method
(third commit above). No unapplied findings.

## Commits

- `a31daccc` — fix(intake): cache QDOS regexes
- `7e2bccb0` — refactor(intake): delete the dead ContainsLabel helper and its cache

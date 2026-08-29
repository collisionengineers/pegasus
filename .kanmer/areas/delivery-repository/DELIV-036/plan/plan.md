# Plan — DELIV-036

Two defects, one file pair. Both are small. This plan is deliberately short
because the change is small — a plan longer than its diff is itself
over-engineered.

## Step 1 — Give `CutAtNextColumnLabel` a match timeout

`QdosInstructionExtractionPolicy.cs:240` calls the four-argument
`Regex.Replace(input, pattern, replacement, options)`, which has **no**
`TimeSpan` parameter, so its `MatchTimeout` is `Regex.InfiniteMatchTimeout`.
Every sibling regex on this path carries 100 ms. On hostile input this one can
hang a production intake indefinitely.

**Reuse:** `ReportColumnCutPattern` is a `private const string`, so it is a
compile-time constant and directly eligible for the file's existing
`[GeneratedRegex]` convention — the same shape already used at
`QdosInstructionExtractionPolicy.cs:597-607`, where the timeout is the
generator's third argument.

Convert it to a `[GeneratedRegex(..., 100)]` partial method and call
`.Replace(value, string.Empty)` on it. That closes the missing timeout and
removes a static-cache entry in one change, inventing nothing.

## Step 2 — Take the fixed patterns off the static cache

Every remaining call in `QdosInstructionExtractionPolicy.cs` (`:265`, `:274`,
`:293`, `:314`, `:345`, `:359`, `:420`, `:427`, `:435`, `:458`, `:472`) and the
fixed ones in `InstructionFieldExtraction.cs` (`:383`, `:397`, `:417`, `:460`)
use literal patterns. Convert them to `[GeneratedRegex]` partial methods
alongside the ones already there.

`:383`, `:397` and `:417` are three copies of the identical `@"[\s-]"` literal —
one generated regex serves all three. That is "one list per concept" applied to
a pattern.

## Step 3 — Cache the interpolated per-label patterns

This is the actual cache-thrash source. `InstructionFieldExtraction.cs` builds
patterns by interpolating each label into a template, at `:158`, `:165`, `:206`,
`:339` and `:401` — four distinct templates per label. With 47 QDOS labels that
is 150+ distinct pattern strings against `Regex.CacheSize == 15`, so essentially
every call re-parses its `Regex`.

They cannot be `[GeneratedRegex]` — the pattern is not a compile-time constant.
Build each label's `Regex` instances **once**, where the label set is already
known, and look them up per line instead of re-interpolating. The definitions
are fixed at construction, so this is a lookup, not a new abstraction — no
second concrete caller is needed to justify it.

Keep the 100 ms timeout on every one.

## What NOT to do

- **Do not rewrite the patterns.** There is no catastrophic backtracking here:
  no nested quantifiers, no back-references, no quantified group over an
  overlapping class. The riskiest constructs are bounded constant-factor costs
  and the inputs are ~55 characters.
- **Do not raise the 100 ms budget as the fix.** The budget is not the defect;
  re-parsing 150 patterns through a 15-entry cache under parallel load is.
- **Do not raise `Regex.CacheSize`.** It is a process-global knob that would
  paper over the problem for every caller in the process and leave the
  interpolation in place.
- **Do not weaken, skip or delete any assertion** in
  `QdosInstructionExtractionPolicyTests`. The existing tests must pass unchanged.

## Verification

- `dotnet build ./Pegasus.slnx --configuration Release -nodeReuse:false` — zero
  `CS####` diagnostics.
- `dotnet test … --filter "FullyQualifiedName~QdosInstructionExtractionPolicyTests"`
  — all pass, assertions unchanged, including
  `AReportsVehicleLineFillsTheDetailsTheLetterLacks`.
- `dotnet test … --filter "FullyQualifiedName~InstructionField"` — extraction
  behaviour unchanged.
- `git diff` shows no static `Regex.Match`/`IsMatch`/`Replace` left on the QDOS
  extraction path that builds its pattern by interpolation.
- Every regex on the path carries an explicit match timeout.
- Rule 14: the changed code keeps its existing production caller — QDOS intake
  reaches it through the instruction-extraction path. Name the `file:line`.

Behaviour must be **identical**. This is a performance and robustness fix, not a
behaviour change; if any extraction result changes, stop and report it.

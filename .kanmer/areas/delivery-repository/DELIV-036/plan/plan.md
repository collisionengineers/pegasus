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

## Cross-model pre-merge review dispositions — 2026-08-29

A Claude-family reviewer (this lane was Codex-built) returned
`APPROVE_WITH_FINDINGS`, **no blockers**. Its method was stronger than reading
hunks: it extracted every string literal from both sides of
`git diff -U0 origin/dev...HEAD` and set-differenced them. All 28 removed and 27
added literals are character-identical except six, each individually accounted
for. That is the strongest available evidence that behaviour is preserved, and it
holds.

It also independently ran a wider net than this lane did —
`--filter "FullyQualifiedName~Intake"` → **652 passed, 0 failed** — finding no
behaviour drift beyond the focused filter.

### Lead finding (medium) — the third-party prefix list gained a second owner · **FIXED**

The one change that went beyond "only where the `Regex` comes from": the
row-skip guard was collapsed from a loop over `ThirdPartyRowPrefixes` into a
hardcoded `[GeneratedRegex(@"(?i)^TP\b", …)]`.

Behaviourally identical today — the array is `["TP"]` and
`Regex.Escape("TP") == "TP"` — but the array still feeds `GuardedPrefixes` at
`:109`. **Adding a second prefix would have extended the per-field guard and
silently not extended the whole-line row skip.** That is precisely the divergence
the file's own comment warns against, one level up: the guard is "applied once to
the whole line rather than being repeated — and forgotten — per rule".

Fixed by building the guard from the one list, once:

```csharp
private static readonly Regex[] ThirdPartyRowRegexes =
    [.. ThirdPartyRowPrefixes.Select(prefix => new Regex(
        $@"(?i)^{Regex.Escape(prefix)}\b",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)))];
```

with the guard now `ThirdPartyRowRegexes.Any(regex => regex.IsMatch(rawLine))`
and the hardcoded generated method deleted. One list, two consumers, still built
once — the cache-thrash property the ticket exists to establish is unaffected,
and the 100 ms timeout is preserved.

The PR body sentence claiming the patterns are unchanged was accurate for every
site **except** this one; it is corrected by the fix rather than by rewording.

### Finding (medium) — `ExtractFields` takes two parameters that must agree · **DEFERRED, with reason**

`definitions` and `regexCache` must describe the same definition set;
`patterns[definition]` throws `KeyNotFoundException` if they ever diverge, and
`FieldDefinition` is a record whose members compare by reference, so even a
`definition with { … }` copy would miss. Correct today — one caller passes the
matching static pair — but the failure mode is an unhandled exception on the live
intake path.

Having `LabelRegexCache` own and expose the definitions would make the invariant
unbreakable and drop a parameter. **Deferred rather than fixed here:** it changes
a public-ish signature on the neutral engine, which is a wider blast radius than
a flake fix warrants, and this branch is otherwise merge-ready. Recorded as a
real design point rather than dismissed.

### Findings (low) — accepted with reasons

| Finding | Disposition |
| --- | --- |
| `patterns[definition]` hashes an 8-member record per label (~47 lookups per call); hoisting it out of the label loop would remove them | **Accepted.** Still an enormous improvement over interpolate + escape + cache miss. A quality nit, not the defect. |
| Nested `DefinitionPatterns` plus double forwarding is one layer more than needed; `Candidate(index, bool)` yields bare-boolean call sites | **Accepted.** Real smell by the repository's own wording, but behaviour-preserving and not worth churning a merge-ready flake fix. |
| Static-initialiser textual-order dependence between `FieldDefinitions` and `FieldRegexCache` | **Accepted.** Would fail loudly at type init, not silently. |
| 17 regexes that previously could not throw now carry a 100 ms budget | **Accepted and now stated explicitly** — see below. |

### The timeout-surface point deserves stating plainly

The reviewer's sharpest observation: **the flake being fixed was a 100 ms
wall-clock overrun under parallel load, and this diff arms 17 further sites with
the same wall-clock budget** on the live intake path. All 17 are linear-time
source-generated patterns over short inputs, and a thrown exception beats an
unbounded hang under the fail-closed invariant — but if scheduling stalls rather
than re-parse cost were the real driver, the surface has widened, not narrowed.

That possibility is **not excluded by anything measured**. Nobody demonstrated
that re-parse cost was what pushed a match past 100 ms; the competing explanation
is that the runner starved the thread for other reasons. The causal argument's
premises all check out — `CacheSize` is nowhere raised, 188 distinct patterns
genuinely exceed a 15-entry cache, every static-overload path is gone — but the
final link is inference, not measurement.

**Disposition: watch `QdosInstructionExtractionPolicyTests` in CI across the
remaining EPIC-011 merges before calling this settled.** Carried into
`proof/proof.md` verbatim rather than left implicit.

### Confirmed by the reviewer, and worth recording

- **All four forbidden actions avoided**, each verified independently: no pattern
  rewritten, no budget raised, no `Regex.CacheSize` touched, no assertion
  weakened.
- **Zero test files changed.** `git diff --name-only origin/dev...HEAD -- tests/`
  returns nothing; `Assert.` count in the flaking test file is 178 before and
  178 after.
- **`ContainsLabel` was genuinely callerless.** `git grep "ContainsLabel"
  origin/dev` over the *whole tree* — not just `src/` and `tests/` — returns
  exactly one line, its own definition.
- **No `SYSLIB` warning was emitted**, which independently proves the source
  generator produced full code for every `[GeneratedRegex]`; a pattern it cannot
  generate warns and silently falls back to a cached `Regex`. None fell back.
- **Rule 14 chain verified** to `ProcessIntake.cs:585` via
  `DependencyInjection.cs:156`.
- **Options census clean:** no pattern gained or lost `IgnoreCase`, and none
  traded an inline `(?i)` for it. `SubjectClientRegex` correctly stayed
  `RegexOptions.None` — had it gained `IgnoreCase`, its title-and-capital logic
  would have widened.

### Scope boundary the reviewer drew, and it is correct

The wider intake path still holds untimed regexes this PR rightly did not touch:
`QdosCaseMatchPolicy.cs` (10 `[GeneratedRegex]`, none timed),
`QdosMailClassificationPolicy.cs` (7), `StaffForwardBodyCleaner.cs` (6). Scope is
the brief. Worth a follow-up if the operator wants that closed; **not** a defect
here.

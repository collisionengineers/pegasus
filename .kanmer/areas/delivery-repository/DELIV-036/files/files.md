# Files — DELIV-036

## Owned and changed

| File | Why |
| --- | --- |
| `src/Pegasus.Core/Intake/InstructionFieldExtraction.cs` | The interpolated per-label patterns at `:158`, `:165`, `:206`, `:339`, `:401` route through the 15-entry static `Regex` cache |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` | Fixed-pattern static calls, and `CutAtNextColumnLabel` at `:240` with no match timeout |
| `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs` | Only if a new regression test is added — existing assertions must not change |

## Not owned — do not touch

No other in-flight lane owns these two files. Verified against the lanes running
concurrently on 2026-08-29:

| Lane | Owns | Overlap |
| --- | --- | --- |
| PLAT-025 / PLAT-026 / PLAT-027 | `Pages/Administration/**` | none |
| PLAT-049 | `Pages/Operations/**` | none |
| ENG-027 | `Core/Assessment/Valuations.cs` and its store | none |
| DELIV-034 | `tests/Pegasus.IntegrationTests/PrincipalCredentialPersistenceTests.cs` | none |
| INTK-047 | `Upload*`, `Uploads/**`, upload presentation | none |
| CASE-027 | `Pages/Cases/Vehicle|Custody|Tasks|Documents` | none |

`InstructionFieldExtraction.cs` is shared *conceptually* with the QDOS policy but
both files are this ticket's, and no other lane is in Core/Intake.

## The static call sites, enumerated

`InstructionFieldExtraction.cs` — **interpolated** (the cache-thrash source):

- `:158` and `:165` — `FindCandidates`, two patterns per label, per line
- `:206` — `StartsWithKnownFieldLabel`
- `:339` — `TruncateAtFollowingFieldLabel`
- `:401` — `ContainsLabel`

`InstructionFieldExtraction.cs` — **fixed** patterns already on static calls,
which should follow the file's own `[GeneratedRegex]` convention:

- `:383`, `:397`, `:417` — `Regex.Replace(value, @"[\s-]", …)`, the same literal
  three times
- `:460` — `Regex.Replace`

`QdosInstructionExtractionPolicy.cs` — **all fixed** patterns:

- `:240` — `CutAtNextColumnLabel`, **no `TimeSpan` argument**, so
  `Regex.InfiniteMatchTimeout`. Its `ReportColumnCutPattern` is a
  `private const string`, therefore a compile-time constant and directly
  eligible for `[GeneratedRegex]`.
- `:265`, `:274`, `:293`, `:314`, `:345`, `:359`, `:420`, `:427`, `:435`,
  `:458`, `:472` — fixed patterns on static overloads with a 100 ms budget.

## The convention to reuse — it is already in this file

`InstructionFieldEngine` is declared `internal static partial class`
(`InstructionFieldExtraction.cs:11`) and **already uses the source generator**
for its fixed patterns:

```
:474  [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
:475  private static partial Regex WhitespaceRegex();
:477  [GeneratedRegex(@"(?<=\d)(?:st|nd|rd|th)\b", …)]
:480  [GeneratedRegex(@"^\s*(?:\d+|\d{1,3}(?:,\d{3})+)\s*(?:miles?|mi)?\s*$", …)]
:483  [GeneratedRegex("^[A-Z0-9]+$", RegexOptions.CultureInvariant)]
:486  [GeneratedRegex(@"[\t|]|\s{2,}|\s+:", RegexOptions.CultureInvariant)]
```

`QdosInstructionExtractionPolicy.cs:597-607` does the same, and passes the
timeout as the generator's third argument. **There is no new pattern to invent
here — the file's own established convention already covers the fixed case.**

## Searched for, and not found

No existing regex-cache helper exists to reuse: `git grep` for
`ConcurrentDictionary<string, Regex>` and `Regex.CacheSize` across `src/`
returns nothing relevant. The per-label patterns therefore need a small cache of
their own, built where the label set is already known.

# Files

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosMailClassificationPolicy.cs` | Second triage tell; `Version` 3 → 4. |
| `tests/Pegasus.Core.Tests/Intake/Qdos/QdosMailClassificationPolicyTests.cs` | The new tell, the forward prefix, both tells together, and two near-misses; the stable-version and predicate-count assertions follow. |

Six lines of production logic and one regex. Everything else in the taxonomy —
the audit and engineer document titles, the ambiguity rule, the fail-closed
default, the standalone-audit evaluation — is untouched.

## Reuse

- The candidate it produces is the **existing** `PreInstructionEmails /
  triage-request` category with the existing `isReplyContext` flag. No new
  category, no new subtype, no new destination — `MailOperationalDestinationPolicy`
  already routes that category to the Triage workflow.
- The reply-prefix concept already exists (`ReplyPrefixRegex`); the new pattern
  absorbs forward prefixes into its own anchor rather than adding a second
  prefix-stripping helper for one caller.

## Two assertions that had to move

- `PolicyKeyAndVersionAreStable`: 3 → 4. The version *is* the tell set.
- `EveryPredicateIsAlwaysRecordedWithAUniqueKey`: 5 → 6 predicates. This is the
  guard that every predicate is always recorded whether or not it fired, so a
  classification decision can be read back years later; adding a tell must move
  it, and it caught the change exactly as designed.

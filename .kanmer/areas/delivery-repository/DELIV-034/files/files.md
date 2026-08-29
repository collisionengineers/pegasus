## Files touched

- `tests/Pegasus.IntegrationTests/PrincipalCredentialPersistenceTests.cs` —
  the flaky assertion at (old) line 62, inside
  `IssueResetPauseResumeRevokeAreHashOnlyReplaySafeAndFailClosed`.

## Files inspected (sweep for the same shape), no change needed

- `git grep -n '\[\.\.\^1\]' -- tests/` hits, checked individually:
  - `tests/Pegasus.Core.Tests/Cases/PrincipalCredentialsTests.cs:32` —
    `PrincipalCredentialPolicy.IsWellFormed(keyId, secret[..^1])`. This
    truncates (removes the last character), it does not replace it, so the
    mutated value is always one character shorter than a well-formed secret
    and `IsWellFormed` checks exact length. Deterministically `false`
    regardless of content — not the same defect shape, no flake.
  - `tests/Pegasus.Core.Tests/Qdos/EvaSubmissionPolicyTests.cs:116,119,120` —
    slices a `delay?[]` array to drop its last (always-null) element; nothing
    to do with a secret or hash. Not in scope.
  - `src/Pegasus.Core/Eva/CaseEvaMapping.cs:303` — slices an address-parts
    array to drop a trailing postcode segment; not a test, not a secret/hash
    mutation. Not in scope.
- `tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs:67` — already
  uses the guaranteed-mutation shape (`secret[..^1] + (secret[^1] == 'A' ? 'B'
  : 'A')`), with a comment recording the exact same root cause. This is the
  existing convention the fix reuses; no change needed here.
- Broader sweep for the same unconditional-append shape against any secret,
  key, hash, or credential value:
  `git grep -nE 'wrong(Secret|Key|Hash|Credential)|invalid(Secret|Key|Hash)|bad(Secret|Key|Hash)|tamper(ed)?(Secret|Key|Hash)' -- tests/`
  — only match is `ProviderApiSubmissionTests.cs`, already correct (above).

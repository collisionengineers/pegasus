---
id: DELIV-034
type: ticket
title: >-
  PrincipalCredentialPersistenceTests tamper-mutation is a no-op when the secret
  already ends in "A"
status: preparing
area: delivery-repository
assignee: claude-code
profile: fix
taken_at: '2026-08-29T14:44:44.197Z'
branch: task/deliv-034-credential-tamper-flake
worktree: >-
  C:/Users/PC/Documents/GitHub/pegasus-worktrees/deliv-034-credential-tamper-flake
labels:
  - ci
  - flaky
  - tests
  - credentials
groups:
  - EPIC-011
links:
  - DELIV-031
  - PLAT-052
refs:
  - docs/frd/frd-09-provider-and-intermediary-routes.md
archived: false
created: '2026-08-29T14:27:28.216Z'
updated: '2026-08-29T14:44:44.197Z'
---

## What

`tests/Pegasus.IntegrationTests/PrincipalCredentialPersistenceTests.cs:62`, in
`IssueResetPauseResumeRevokeAreHashOnlyReplaySafeAndFailClosed`:

```csharp
Assert.Null(await authenticate.ExecuteAsync(firstKeyId, firstSecret[..^1] + "A", default));
```

The intent is to prove a tampered secret fails to authenticate. But the mutation
replaces the final character with `"A"` unconditionally — so when the issued secret
**already ends in `A`**, the "tampered" value is byte-identical to the real one,
authentication correctly succeeds, and `Assert.Null` fails.

The test is therefore non-deterministic on a freshly generated secret. Observed
failing on PR #617's `sql-integration (1)`
(`Assert.Null() Failure: Value is not null`), and diagnosed independently during the
PLAT-052 review, which put the rate at roughly one run in four.

## Why

This is a security-relevant assertion — it is the only place proving that a
near-miss credential is rejected — and it currently fails at random on unrelated
PRs. It has already cost reruns on at least two EPIC-011 lanes and it will keep
doing so. It is distinct from [[DELIV-031]], which is the LocalDB connect-timeout
flake; this one is a logic defect in the test.

Unticketed until now: agents kept correctly diagnosing it as "pre-existing, not my
lane" and moving on, so nobody owned it.

## Approach

Make the mutation guaranteed to mutate. The smallest correct fix picks a character
that differs from the one being replaced, for example:

```csharp
var tampered = firstSecret[..^1] + (firstSecret[^1] == 'A' ? 'B' : 'A');
Assert.Null(await authenticate.ExecuteAsync(firstKeyId, tampered, default));
```

Do not weaken the assertion, and do not delete it — it is proving fail-closed
behaviour. Assert that `tampered != firstSecret` first, so the test fails loudly if
a future change makes the mutation a no-op again rather than silently passing.

Then check whether the same unconditional-mutation shape appears anywhere else:
`git grep -n '\[\.\.\^1\]' -- tests/`.

## Verification

- [ ] The tampered secret is asserted to differ from the real one before it is used.
- [ ] `IssueResetPauseResumeRevokeAreHashOnlyReplaySafeAndFailClosed` passes on ten
      consecutive runs.
- [ ] No other test mutates a secret or hash in a way that can produce the original.

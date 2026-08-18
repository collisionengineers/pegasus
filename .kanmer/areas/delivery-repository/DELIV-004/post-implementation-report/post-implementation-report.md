# Post-implementation report — DELIV-004

## Summary

Makes the existing no-dormancy rule explicit in the repository safety rails:
a closed composition or feature gate is a disabled flag, not a partially shipped
feature. Such a feature cannot be shipped, released, merged as delivered,
claimed, or documented as delivered; it must remain deferred until it has its
real caller and activation evidence.

## Changes

| File | Change | Why |
|---|---|---|
| `AGENTS.md` | Modified | States the required delivery rule in the canonical repository-policy file, alongside verification and other safety rails. |

## Governing docs

No PRD, FRD, or ADR applies. This is a repository-operating-rule clarification,
whose canonical owner is `AGENTS.md`. The edit preserves
`docs/engineering.md` as the detailed authority: its “Abstractions and
deferred capabilities” rule already prohibits dormant registrations, unused
endpoints, disabled flags, and dark destructive code.

## Risks / follow-ups

No implementation or deployment occurs in this change. [[AUTO-001]] remains the
separate automation-gate delivery ticket and must follow this clarified policy.

## Verification hand-off

On the merged target, run:

```powershell
rg -n -i -C 2 "closed composition|feature gate|disabled flag|dormant registration|dark.*code" AGENTS.md docs/engineering.md
git diff --check HEAD^ HEAD
```

Expected result: `AGENTS.md` contains the explicit rule, and
`docs/engineering.md` retains its detailed prohibition; the commit is
whitespace-clean and changes only `AGENTS.md`.

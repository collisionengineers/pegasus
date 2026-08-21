# Post-implementation report — PLAT-018

## Summary

Corrected two self-contradictory design-authority rules in `docs/design/README.md`. The authority no longer bans the operator-facing `Queues` label it mandates, and its no-explanatory-copy exception now permits only individually approved sentences from the closed necessary-copy list.

## Changes

| File | Change | Why |
|---|---|---|
| `docs/design/README.md` | Removed `queue` from the banned operator-copy enumeration. | The same authority mandates `Queues` in the approved shell; the existing `queue mechanics` rule preserves the intended implementation-language restriction. |
| `docs/design/README.md` | Replaced the generic consequence-sentence exception with a reference to the closed approved necessary-copy list. | Prevents authors from treating irreversibility as a self-service test for adding prose. |

Commit: `892fe6a798c808dc110fdf91fbaeeb3140f577aa`.

## Governing docs

No PRD, FRD, or ADR applies or was modified. This change corrects the repository’s existing design authority, which is the canonical home for this convention; it introduces no product behaviour or architectural decision.

## Risks / follow-ups

- [[PLAT-019]] remains the separate removal of unapproved shared reason-dialog copy.
- [[MAIL-006]] remains the separate Inbox-page redesign and any Inbox-local wording correction.
- No deployment or runtime verification is required for this documentation-only change.

## Verification hand-off

On merged `main`, run:

```powershell
git diff --check HEAD^ HEAD
git show --format= --name-only HEAD
rg -n -C 3 -e 'queue mechanics' -e 'approved consequence sentence' -e 'CE logo \\| Dashboard \\| Inbox \\| Upload \\| Queues' docs/design/README.md
```

Expected: whitespace check succeeds; only `docs/design/README.md` is in the commit; the “queue mechanics” restriction and `Queues` shell label remain; the exception names the closed necessary-copy list.

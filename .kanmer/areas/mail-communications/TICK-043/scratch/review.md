# Independent review — 2026-08-19

## Changes

- `MailboxIntake.cs` validates retained metadata before receipt creation and derives retained-mail intake tokens from a SHA-256 of the supplied RFC Internet Message-ID while retaining the provider immutable item ID separately.
- `EfRetainedMailboxMessageStore.cs` treats either mailbox+RFC identity or mailbox+immutable provider identity as an existing row, verifies RFC/content consistency, and scopes thread projections by mailbox and logical folder scope.
- EF model/migration adds a filtered unique index for mailbox + RFC identity.
- Core and integration tests cover missing RFC identity, changed provider identity, contradictory identity, and cross-mailbox thread isolation.
- FRD-08 and the capability registry record the new identity behavior and local-only evidence.

## Comments

1. **Blocking:** the implementation does not use one equality/canonicalization rule for its declared durable duplicate boundary. `PrepareMessage` hashes the raw RFC identity with case-sensitive bytes to derive the intake receipt token. The retained store explicitly compares RFC identity with `OrdinalIgnoreCase`, and its SQL query/index follow SQL Server collation semantics. An equivalent case variant can therefore pass through `ReceiveIntake` under a different token and create another business receipt before `RetainAsync` returns the existing retained row. This contradicts the FRD and report claim that mailbox + RFC identity is one intake/read-model duplicate boundary.
2. **Non-blocking:** the report accurately lists the changed files and correctly qualifies the evidence as local implementation only. The plan's four-lens simplification section is present with an applied disposition. No external write or unsupported architecture boundary appears in the diff.

## Disposition

1. Filed as [[PR-004]], which blocks [[TICK-043]]. Required evidence includes a real poll/EF replay test for equivalent identity forms, a distinct-identity control, and FRD alignment.
2. No change required.

## Verdict

**Needs changes.** Reviewed the complete ticket folder and EPIC-006 context, open questions, plan/checklist/report, FRD-08 and design authority, PR #414 metadata and diff, Core caller, EF store/model/migration, and focused tests. The PR must not merge until [[PR-004]] is resolved and this review is rerun. CI was still in progress when this substantive verdict was recorded.

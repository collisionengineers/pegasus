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

# Independent re-review — 2026-08-19 — commit 195bbf62

## Changes re-checked

- Core now exposes one RFC identity canonicalizer: trim, Unicode Form KC, invariant uppercase; receipt-token hashing uses its output.
- Persistence keeps the raw `InternetMessageIdentity` and adds a separate canonical column; lookup, insert, contradiction checking and race recovery use the Core canonicalizer.
- The canonical column is binary-collated and indexed uniquely with mailbox identity. The replacement migration adds/backfills the column before creating the index.
- FRD-08 and the post-implementation report describe the canonical/raw split.
- New tests cover ASCII case/whitespace replay through Core and the real poll/EF path, plus a distinct canonical identity control.

## Comments

1. **Fixed in PR:** [[PR-004]]'s original inconsistent equality issue is structurally resolved. Receipt hashing, EF lookup/write/comparison and SQL keying now share an explicit canonical representation, and raw evidence is stored separately.
2. **Blocking:** the implementation and FRD promise Unicode NFKC equivalence and verbatim transport evidence, but the tests use ASCII case/whitespace only and assert row counts. No test exercises a Unicode normalization-equivalent pair or proves the retained raw value remains verbatim beside its canonical key.
3. **Blocking:** Core bounds the raw identity at 500 characters before NFKC/case expansion, while the canonical database column is also limited to 500. No validation bounds the canonical output and no accepted-domain proof or regression establishes expansion is impossible. A raw value that passes Core may therefore fail later at persistence rather than the declared fail-closed validation boundary.

## Disposition

1. Fixed by `195bbf62`; [[PR-004]] is archived with its outcome.
2–3. Filed together as [[PR-005]], blocking [[TICK-043]], because both concern proof and validation of the newly introduced canonical representation.

## Verdict

**Needs changes.** The original blocker is repaired, but the canonicalization contract remains incompletely validated and tested. Reviewed the updated ticket plan/report/open questions, PR diff, Core canonicalizer/caller, EF model/store/migration, raw and canonical fields, FRD wording, and current CI metadata. Do not merge until [[PR-005]] lands and re-review passes.

# Independent re-review — 2026-08-19 — commit 09dbeb6a

## Changes re-checked

- Core bounds the final trim/NFKC/uppercase canonical value at 500 characters during retained-metadata validation, before receipt creation or persistence.
- Core regression uses Kelvin-sign/ASCII-K normalization equivalents and covers expansion past the canonical persistence bound.
- Real poll/EF regression proves one staged receipt, one work item and one retained row, while asserting the first raw RFC transport value verbatim and the separate canonical key.
- Plan, FRD, report and PR-005 simplification disposition align with the implementation.

## Comments

1. **Fixed in PR:** [[PR-005]] is fully resolved at `09dbeb6a`.
2. **Blocking CI / branch-related:** SQL shard 2 fails `CommittedMigrationCreatesTheSqlServerSchema` because the committed-migration expectation was not updated for the new MAIL-01 migration.
3. **Blocking CI / branch-related:** SQL shard 3 fails both forms of `KnownSourceTerminalOutcomeSurvivesRestartAndDoesNotBlockLaterMail` (expected two processed messages, actual one). Its independent-message fixtures now collide under the new canonical RFC duplicate boundary and must carry distinct RFC identities.

## Disposition

1. Fixed in PR; [[PR-005]] archived with outcome.
2–3. Filed together as [[PR-008]], blocking [[TICK-043]]. These are direct ripple effects of this branch, not unrelated flakes.

## Verdict

**Substantive implementation passes, but merge is blocked by required CI.** Run 32239604895: changes, documentation, reference-data, unit, browser, SQL shard 1 and SQL coverage passed; infrastructure skipped; SQL shards 2 and 3 failed for the two branch-related omissions above. Do not merge until [[PR-008]] is fixed and required CI is green.

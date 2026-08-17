# Research — BUG-001

## Question

Is the reported defect — receipt of an authorised QDOS email producing neither a Case/PO nor its Box case folder — still present, or has later work resolved it?

## Findings

### 1. The current source implements the missing business chain

- `src/Pegasus.Core/Intake/DurableIntake.cs` contains `ProcessQueuedIntake`; definitive typed intake proceeds into automatic allocation.
- Commit `9393c983` (“Definitive intake allocates its case without a manual gate (INT-25)”) introduced the automatic Case/PO allocation path.
- `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs` processes `create_case_custody` work, calls `ICaseCustody.CreateCaseRootAsync`, retains the accepted source, and records `custody_confirmed`.
- `src/Pegasus.Infrastructure/Custody/BoxCaseCustody.cs` is the production Box implementation. Worker composition tests require this adapter in production.
- Commits `379d7ddd` and `864f46fc` added durable allocation recovery and completed case custody/handoff. Later commits `0743ac32`, `73a3380d`, and `f08e2df6` corrected Audit/forwarded-mail evidence, custody, and replay cases.

**Implication:** the original source-level absence is stale on current `dev`; this is no longer a missing implementation.

### 2. The governing requirements match the implemented order

- `docs/frd/frd-02-intake-and-source-identity.md` requires definitive authorised intake to allocate a Case/PO automatically and requires Box custody only after allocation.
- `docs/frd/frd-05-documents-extraction-and-custody.md` requires every allocated Case/PO to use its immutable reference for the Box case folder and retain accepted sources there.
- `docs/capabilities.md` records INT-25 as replay-safe automatic allocation. DOC-01 states that immutable naming and caller behaviour are proved locally, while live controlled Box proof remains pending.

**Implication:** BUG-001 is governed by FRD-02 and FRD-05; both are linked to the ticket. No new product requirement or ADR is needed.

### 3. Local evidence exists, but this investigation did not complete a fresh test run

Relevant automated coverage includes:

- `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs`: definitive typed instructions allocate one case, replay is bounded, and stranded allocation is re-driven safely.
- `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs`: an accepted case creates custody and retains its exact source replay-safely.
- `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs`: an approved mailbox poll enters normal durable intake.
- `tests/Pegasus.ArchitectureTests/WorkerCompositionTests.cs`: production Worker composition resolves `BoxCaseCustody` and `ProcessQueuedIntake`.

A focused `dotnet test` command covering the allocation suite plus mailbox and custody tests was run with Release/`--no-restore`, but the command exceeded the 120-second tool limit and emitted no final result. A timeout is neither a pass nor a failure.

**Implication:** implementation work should not begin from this ticket. The next phase must finish the focused local evidence on an appropriate runner before claiming current-head verification.

### 4. Production resolution is not proved and current documented deployment predates later fixes

- `docs/operations.md` records production source `dd61ac56840d2cf0c1f0667f995c3941cbb19fc5` and explicitly says no enabled Worker caller, mailbox-to-case journey, or Box-custody journey was live-verified.
- Git ancestry shows that deployed source contains `9393c983`, `379d7ddd`, `864f46fc`, and `efe17b94`.
- It does **not** contain later forwarded/Audit fixes `73a3380d` or `f08e2df6`.
- Archived [[TICK-116]] and [[TICK-117]] were deliberately consolidated into BUG-001; their outstanding checks require one genuine production mailbox-to-Case/PO journey and production Box custody proof.
- Those actions can mutate Outlook, SQL/case state, and Box. Repository rules require fresh explicit approval for the exact targets before performing them.

**Implication:** “resolved” is supportable for the missing implementation on current source, but not for the deployed end-to-end behaviour. BUG-001 must not be closed solely from code inspection.

## Conclusion

Reclassify the work mentally as **verification and disposition**, not a code fix. The best next plan is to (1) complete focused local verification, (2) establish whether the current source containing all relevant fixes has been deployed, and (3) only with exact-target operator approval, exercise one genuine production journey and capture Case/PO plus Box evidence. If all pass, close BUG-001 as resolved by the already-merged commits without changing product code. If a step fails, record the exact failing boundary and create a narrowly scoped follow-up fix rather than reopening this broad historical symptom as an implementation task.

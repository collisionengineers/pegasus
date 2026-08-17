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

## Live-estate comparison — 2026-08-17

### Scope and identity

All live operations in this research pass were read-only. Azure CLI confirmed tenant `858cf5b3-aa0a-47a6-9b40-4851fd0afa94`, subscription `e6076573-23a5-46a8-acef-7e22d264e5db`, resource group `rg-pegasus-prod`, Worker `pegasus-prod-worker-252ow37gij`, SQL database `pegasus`, Application Insights `pegasus-prod-appi-252ow37gij`, and transient intake container `pegcustody252ow37gij/transient-intake`.

The Worker is Running. All nine functions are registered and all nine `AzureWebJobs.<Function>.Disabled` settings read `false`. The later failure was therefore not caused by the earlier Worker containment state.

The current Web version diagnostic reports source `aecad2479f52dadfedca109413a458c60c85323e` (`0.1.0-alpha.1`). That deployed source contains the forwarded-QDOS/Audit fixes through `f08e2df6`. Current `dev` still contains the extraction predicate described below, so this is not simply an old-deployment mismatch.

### Earlier successful test

The 2026-08-14 test receipt `2c4888d6-4098-4d22-a46a-d976286a27b0`:

- was routed `accepted / QDOS / direct_provider`;
- was classified `new-instruction-received / audit / repairable`;
- produced a non-empty field set and an `instruction-structure` evidence item because one Bodyshop report fragment contained both a standalone `QDOS` marker and six recognised instruction labels;
- became `case_created`;
- completed allocation attempt `c3846c0f-ae34-42a2-bec3-d5fc55550a5a`;
- allocated Case/PO `QDOS26001` and Audit reference `a.QDOS26001`;
- completed `create_case_custody` work `3565e349-2535-4f6f-90b3-4e2cc7a5f9b4` on its first attempt;
- recorded Box root remote ID `409001353539` and custody confirmation at `2026-08-14T08:53:41.9988688Z`.

Application Insights corroborates the sequence: successful `IntakeWorkFunction` at `08:52:12Z`, followed by the estate's only observed `ExternalWorkFunction` execution at `08:53:12Z`, also successful.

### Later test with no Box folder

The 2026-08-17 test receipt `9a91fe16-d62f-4477-a11e-830fd96f672a`:

- was retained at `08:11:45.8861210Z`;
- ran successfully through `IntakeWorkFunction` at `08:12:15Z` (5.592 seconds, no exception);
- was routed `accepted / QDOS / direct_provider`;
- was classified `new-instruction-received / audit / repairable`;
- retained the original report and recorded automatic standalone-Audit evidence `23d5877f-dceb-412e-8655-3cc138c0a51e`;
- nevertheless became `needs_sorting` with the exact reason: “The readable content does not provide enough evidence to suggest a principal.”;
- persisted `FieldsJson` as an empty list;
- created zero allocation attempts, zero Cases, and zero Case-intake links;
- therefore created no external-work item and made no Box custody call.

The absence of a Box folder is downstream and expected once allocation never starts. The failure boundary is principal/instruction extraction, before Case/PO allocation and before Box.

### Root cause in the live evidence and current source

`QdosInstructionExtractionPolicy.Extract` only marks a fragment as confirming when the **same content fragment** contains:

1. a match for `\bQDOS\b`; and
2. at least two recognised instruction-field definitions.

That same-fragment rule is deliberate and is pinned by `ProofCannotBeAssembledAcrossSeparateContentFragments`; it prevents unrelated fragments from being combined into false instruction proof.

The later retained source exposes a narrower parser/marker defect:

- its QDOS Audit instruction letter contains recognised labels including Vehicle registration and Accident date;
- PDF text extraction renders the brand line as `Proud Members OfQDOS Accident Assistance Ltd`;
- because `OfQDOS` has no word boundary between `f` and `Q`, `\bQDOS\b` does not match;
- its Bodyshop report contains multiple recognised field labels but no QDOS text;
- its email body contains a standalone QDOS marker but not two instruction labels.

Consequently no individual fragment enters `confirmingFragments`, even though the QDOS instruction letter itself contains the marker (collapsed by PDF extraction) and sufficient labels. The earlier Bodyshop report happened to contain a separately delimited QDOS token plus six labels, so it passed.

This explains why superficially equivalent Audit emails diverged: Box was healthy in the earlier test and was never invoked in the later test. The discriminator was the extracted text shape `OfQDOS`, not Worker activation, queue execution, Audit classification, standalone-report evidence, allocation recovery, or Box configuration.

### Local confirmation and coverage gap

The focused current-source test class passed 8/8 in Release. It explicitly preserves the same-fragment rule, but has no fixture for the observed PDF extraction artifact `OfQDOS`. No test currently proves that a QDOS-branded instruction letter whose text extractor collapses “Of QDOS” can establish the principal while retaining the same-fragment safety boundary.

### Revised conclusion

BUG-001 is a current, reproducible extraction-policy defect. The minimal safe repair is not to weaken the same-fragment rule or treat an accepted sender/classification as sufficient by itself. It is to recognise the exact observed collapsed QDOS brand token within the same instruction fragment, add regression/negative tests, and then re-evaluate the retained later receipt under the corrected deployed policy. Only that re-evaluation (with separately authorised production write scope) should create its Case/PO and custody work.

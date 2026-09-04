# Review record — CASE-009 (PR https://github.com/collisionengineers/pegasus/pull/665)

Head SHA reviewed: `41ad325172034d0d7a3434bee2e682a8d47d0b0c` (branch
`task/case-009-case-queries-correspondence`; confirmed by `git rev-parse HEAD`
in the detached review worktree `.worktrees/case-009-review` — the branch had
not moved).

Reviewer models: independent read by **gpt-5.6-terra at xhigh** (Codex, in the
review checkout); dispositions, independent verification and gate by **Claude
Opus**. Built by gpt-5.6-sol.

## Verdict

**APPROVE.** No findings from either reviewer. Merged after the CI run for this
exact head concluded `success`.

## Findings and dispositions

| # | Severity | Finding | Disposition |
| --- | --- | --- | --- |
| — | — | The independent xhigh read returned **no findings**. | n/a |

Neither reviewer raised a blocker, should-fix or nit. Nothing was deferred,
rejected or accepted as risk, so no follow-up ticket is opened by this review.

## What the review checked, and against what

| Question | Evidence |
| --- | --- |
| Every drawn control has a named handler | The only new control is the routed retained-message link `asp-page="/Mail/Message"` (`_CaseCorrespondence.cshtml:29`), whose handler is `Pages/Mail/Message.cshtml.cs:185` `OnGetAsync`. Raise a Query, Reply, Resolve, Mark resolved and every association control are **absent**, not drawn disabled; the web test asserts their absence over the whole page text. |
| No explanatory copy | The partial renders a heading, a table and a link only (`_CaseCorrespondence.cshtml:4-35`) — no hint, empty-state prose or how-it-works text. A wholly unrecorded sender renders an empty cell; the Inbox's `Sender not recorded` literal is deliberately not copied, and the test asserts it is absent. |
| Empty state = absence | `_CaseFiles.cshtml:15-18` gates the `<partial>` call on `details.QueryEmails.Count > 0`, so heading and table are absent when nothing qualifies (design authority's read-only rule). `CaseFilesOmitsQueriesWhenNoLinkedQueryMailExists` proves it. |
| Labels only in `OperatorLabels` | Five new members in a block delimited `// CASE-009: read-only query correspondence table.` (`OperatorLabels.cs:1372-1377`). The heading is `MailOperationalDestinationLabel(MailOperationalDestination.Queries)` and the classification cell `MailClassification(...)` — never a literal. |
| Owned paths only | `git diff --name-only origin/dev...HEAD` returns exactly the seven paths in the plan's Files table. No migration, no DI change, no `Details.cshtml`, no `docs/design/test-ui/**`, no `TestUiSnapshotTests.cs`, no `scripts/*.ps1`, no `ci.yml`. |
| Core owns policy | The classification set comes from `MailOperationalDestinationPolicy.Query(MailOperationalDestination.Queries)` (`MailOperationalDestinationPolicy.cs:99-103`); only the EF translation is repeated, and it matches `EfRetainedMailboxMessageStore.ApplyClassificationFilter` (`EfRetainedMailboxMessageStore.cs:838-854`) term for term — the `OtherName == null` guard, the direction/family/subtype comparison and the `classified` outcome. No second classification list. |
| Projection correctness — dictionary safety | `linkedQueryReceipts.ToDictionary(item => item.ExternalReceiptToken)` cannot throw: `IntakeReceiptEntity` carries `HasIndex(item => new { item.SourceChannel, item.ExternalReceiptToken }).IsUnique()` (`PegasusDbContext.cs:223`) and the predicate pins `SourceChannel` to the mailbox code, and the property `IsRequired()` so the key is non-null (`PegasusDbContext.cs:210`). The indexer read is over tokens the retained query was itself filtered by, so no `KeyNotFoundException`. Retained rows stay non-unique by token (`MailboxModelConfiguration.cs:82`), as the plan requires. |
| Projection correctness — `Category!` | `MapMailClassificationDecision` returns a null `Category` only when both `OtherName` and `Family` are null (`EfIntakeReceiptStore.cs:665-698`); both predicate branches require `Family != null`, so the null-forgiving deref cannot NRE. |
| Projection correctness — the simplification-pass narrowing | `associatedReceiptIds` (manual associations UNION case intake links for this case) is a superset of every receipt `CurrentIntakeAssociations` would accept for this case: an active manual association to this case has a manual row, and an accepted link is only consulted when no manual row exists at all (`CurrentIntakeAssociations.cs:42-81`). Behaviour-preserving; the authoritative decision still runs afterwards with `TryGetValue`, never the indexer, so a reversed or never-associated receipt is excluded without throwing. |
| Ordering | Newest-first is applied after the retained-row join (`EfCaseQueryStore.cs:251-267`), `ReceivedAtUtc` descending then `RetainedMessageId`. |
| Tests prove the claim; none weakened | The two web tests assert rendered output (heading, four column headers, office time, effective-sender precedence, subject, classification label, both `/Inbox/{id}` hrefs) and the absence of `<form>`, `<button>`, `disabled` and the four manual-control strings. The persistence test seeds its own case/association fixture and asserts the exact ordered id list `[billing, sharedFirst, sharedSecond, query, dispute]`, proving Billing/billing-query inclusion, the shared-token pair, and exclusion of another case, a non-Query classification, a reversed association and a receipt with no association at all — the last without throwing. No pre-existing assertion is weakened, deleted or made vacuous; both tests fail if the production change is reverted. |
| Migration and grants | No schema change in the diff, so `Test-MigrationGrants.ps1` is not applicable. |
| Report and checklist match the diff | The seven files, the absence of a snapshot/migration/DI/`Details.cshtml` change, and the whitespace fix (gate moved to the `<partial>` call site, internal conditional removed) are all present as described. The reported test counts reproduce exactly. |
| Simplification pass honesty | All three applied findings are in the code — the receipt-query narrowing (`EfCaseQueryStore.cs:189-200`), `EfIntakeReceiptStore.ToCode(IntakeSourceChannel.Mailbox)` instead of the `"mailbox"` literal, and `EfIntakeReceiptStore.MapMailClassificationDecision` instead of a hand-rebuilt `MailCategory` (`EfCaseQueryStore.cs:262-263`). The five rejections are defensible and reasoned. |
| D44–D50 | No staff review flag or action, no damage type, no crop surface, no EVA state change, no repairer address, no vehicle-record extension, no Create Case route appears anywhere in the diff. |

## Commands and exit codes (run by the reviewer in `.worktrees/case-009-review` at 41ad325)

```
git rev-parse HEAD                                                    → 41ad325172034d0d7a3434bee2e682a8d47d0b0c
dotnet restore ./Pegasus.slnx --locked-mode                           → RESTORE_EXIT=0
dotnet build ./Pegasus.slnx --configuration Release --no-restore      → BUILD_EXIT=0 (0 warnings, 0 errors)
dotnet test ./tests/Pegasus.Core.Tests --configuration Release --no-build
                                                                      → CORE_EXIT=0 (1225 passed, 0 failed)
dotnet test ./tests/Pegasus.ArchitectureTests --configuration Release --no-build
                                                                      → ARCH_EXIT=0 (100 passed, 0 failed)
dotnet test ./tests/Pegasus.IntegrationTests --configuration Release --no-build \
  --filter "FullyQualifiedName~CaseDetailsWebTests|FullyQualifiedName~RetainedMailPersistenceTests"
                                                                      → INTEG_EXIT=0 (100 passed, 0 failed)
codex exec -m gpt-5.6-terra -c model_reasoning_effort=xhigh (independent read)
                                                                      → CODEX_EXIT=0
```

**Why that scope covers the change.** The diff changes one Core read record
(`CaseQueries.cs`), one Infrastructure projection (`EfCaseQueryStore.cs`), two
Razor partials, five `OperatorLabels` constants, and the two test classes that
cover exactly those types. `Pegasus.Core.Tests` covers the Core record and its
consumers; `Pegasus.ArchitectureTests` covers the Core/Infrastructure/Web
dependency direction the new `Pegasus.Core.Intake` using introduces;
`CaseDetailsWebTests` renders the routed Case page through the changed partials;
`RetainedMailPersistenceTests` exercises `EfCaseQueryStore.GetAsync` against
LocalDB with the full receipt/association/retained-row estate. No
`docs/design/test-ui/**` file is in the diff, so there is no regenerated
snapshot artifact for this reviewer to open; the routed output is proven
byte-identical by the implementer's scoped `-Verify` and re-proven by CI's
`test-ui` job. Per the EPIC-012 build policy the full integration and browser
suites are GitHub CI's gate, not a local reviewer's, and the merge waited on
that run's conclusion for this exact head.

## CI gate

`gh run list --branch task/case-009-case-queries-correspondence --limit 1`
returned `headSha 41ad325172034d0d7a3434bee2e682a8d47d0b0c`, database id
`33915295061`. The merge was taken only once that run reported
`status: completed`, `conclusion: success` on the reviewed head. No job was
re-run.

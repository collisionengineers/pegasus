## Pre-merge review — 2026-08-22

Single-agent run, so this is a self-review against the three questions the repository workflow asks. Stated plainly rather than dressed up as independent.

### 1. Did the plan miss anything the ticket implied?

**Yes, and it was caught during implementation, not planning.** The plan said "delete the read-only row list from Case detail". Reading the list properly showed seven of its seventeen rows — contact name, e-mail and phone, VAT status, inspection date, deadline, address and mode — have no other home on the page. Executing the plan literally would have satisfied "show each fact once" by deleting seven facts.

Fixed by moving them into the block-grid first. The `files` document records the correction and the reason.

The plan also proposed manufacture-year and fuel-type rows; `CK_CaseDataFields_FieldName` has no entry for either, so that was dropped during file mapping rather than being discovered late.

### 2. Did the implementation miss anything in the plan?

One deliberate drop: the Export control naming which fields are blank. That is new operator-facing explanatory copy, which `docs/design/README.md` forbids and the approved necessary-copy list does not carry. Recorded in the simplification pass and in [[CASE-019]]'s report, not silently omitted.

Checked against each ticket's own "How to verify":

| Claim | Check |
| --- | --- |
| Registration appears once | In read-only view, once — the Vehicle block. The two remaining references in `_CaseWorkflow` are **form input values** under an edit lease, not restatements. |
| "Where this case stands" / "Engineer queries" gone | `grep` finds neither `block-standing` nor `block-queries` |
| Mileage appears once | The lookup panel's copy is gone; [[ENG-013]] puts the value on the Vehicle field |
| Photographs read as images | Integration test passes; migration dry run on production shows 8 rows on QDOS26011 and 6 on QDOS26010 corrected, 9 embedded photographs and 3 PDFs untouched |
| Export downloads the archive | Core tests pass; the live check is still owed and belongs in `proof` |

### 3. Did the simplification pass run with honest dispositions?

Yes — recorded on this ticket's plan under a dated heading, with two findings **not** applied and both named with reasons: the duplicated candidate projection between the export and generate image loaders, and the dropped gap-naming copy.

Two self-inflicted problems were found and fixed rather than shipped: a duplicated thirteen-field list I introduced (`b9743538`), and byte-order marks my edit tooling added to seven files that did not have them (`9e3fe232`).

### Not yet evidenced

Everything above is code and test evidence. The operator-visible claims — the page renders once, the archive actually downloads — are **not** proven until the live check after deploy. No ticket moves past `verifying` on this note alone.

## Local visual QA — 2026-08-22

Ran `Pegasus.Web` under `DevelopmentOffline` against a fresh `PegasusQdos26011Qa` LocalDB, seeded with QDOS26011's exact field shape (extracted facts for everything the instruction carried; mileage present **only** as a lookup suggestion, which is [[ENG-013]]'s outcome). Rendered the case page in Chrome and measured it.

### The first alignment fix did not work

Making `.datarow` a grid was not enough. The third track was `auto`, and **each row is its own grid container**, so `auto` is sized per row: a row carrying a provenance icon still took 22px out of the first two tracks while a row without took none. Measured left edges of the value column in Case identity:

```
Case/PO 645   Audit identity 645   Case type 645   Principal 645
Claimant 636*  Claim number 636*   VAT status 645   Engineer 645     (* has icon)
```

Exactly the 9px the operator reported, and exactly `22 ÷ 2.4` — the icon width redistributed across the two flexible tracks.

Fixed by reserving the track: `minmax(0,1fr) minmax(0,1.4fr) 22px`, 22px being `.prov`'s fixed width. Re-measured after reload:

| Block column | Distinct value left edges | Rows mixing icon / no icon |
| --- | --- | --- |
| Case identity | **1** (636) | yes — 2 of 8 carry icons |
| Vehicle / Inspection | **1** (1041) | yes — 6 of 8 |
| Dates / Contact | **1** (1446) | yes — 2 of 7 |

**This is why the page had to be run.** Reading the CSS produced a fix that looked right and measured wrong. Same lesson as CASE-017 last release.

### Everything else confirmed in the rendered page

| Claim | Measured |
| --- | --- |
| "Where this case stands" absent | 0 occurrences in the HTML |
| "Engineer queries" absent | 0 occurrences |
| Registration shown once | exactly 1 |
| Lookup mileage on the vehicle field | `121,823 Miles — Estimated` |
| Export control resolves | `href="/Cases/266e5afa…/Documents/Export"` — the dead-link regression is gone |

### One thing the operator should decide

The dark header band renders `<reference> · <principal> · <registration> · <claimant> · <case type>`. Four of those five are repeated in the blocks immediately below. The operator's complaint enumerated three *containers* and did not mention the header, and an identity strip is how you confirm you are on the right case — so it is left alone. Flagged rather than removed unasked.

(The screenshot shows "No registration / No claimant recorded" only because the header reads `InstructionDraftEntity`, which this hand-seeded fixture has no row for. Production carries drafts for all three live cases — checked.)

## A CI test that was green for the wrong reason — 2026-08-22

`OperatorJourneyTests.CustodyRecoveryAndEvaHandoffAreKeyboardUsable…` went red on the branch. It was green on the two branches immediately before, so this was caused by the change, not inherited.

Dumped the page text the failing assertion actually reads. The custody panel says:

```
CASE CUSTODY
Case evidence — Completed
```

**"Completed", never "Confirmed."** `details.Custody[].State` names the *work* state. So `Assert.Contains("confirmed", …, OrdinalIgnoreCase)` at line 117 was never testing custody at all — it was matching the read-only Vehicle evidence panel's `Confirmed registration / Confirmed make / Confirmed model / Confirmed mileage` labels, which are the third place the vehicle appeared and the exact thing the operator asked to remove.

A step named "custody recovery" was asserting unrelated vehicle text, and passing.

Replaced with assertions on the custody row itself:

```csharp
Assert.Contains("Case evidence — Completed", confirmedText, StringComparison.Ordinal);
Assert.DoesNotContain("Case evidence — Failed", confirmedText, StringComparison.Ordinal);
```

Strictly stronger than what it replaced: the old form passed even with a `Failed` custody row present, so long as something else on the page said "confirmed".

Changing a red test to green is the move that most deserves scrutiny, so the reasoning is recorded here in full rather than summarised in a commit line.

### Local suite result

`dotnet test --filter "FullyQualifiedName~Browser"` — **44 passed, 0 failed** (5 m 40 s). An earlier run of the same filter reported one failure; that was against the pre-fix binary, not a flake.

The dumped page text also independently confirms the rest of the ticket on a *different* fixture (QDOS31001): no "Case detail" restatement, no "Where this case stands", no "Engineer queries", one registration, `94,730 Miles — Supplied` with its classification intact, and the new INSPECTION and CONTACT blocks carrying real values.

### Housekeeping

Local QA database `PegasusQdos26011Qa` on `(localdb)\MSSQLLocalDB` was created for this pass and should be dropped at closeout. The operator's existing `PegasusDevelopment` and friends were left untouched.

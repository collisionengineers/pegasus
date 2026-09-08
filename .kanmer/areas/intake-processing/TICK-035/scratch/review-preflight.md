# Bounded independent pre-PR correction review — 2026-09-08

Reviewer: /root/principal_delivery_audit, distinct from intake_audit author.
Disposition: needs changes. This is preflight source evidence, NOT an exact-PR
attestation, and performs no stage move or public review. Root accepted both
findings after its own source inspection and assigned one bounded correction.

Reviewed census: .worktrees/tick-035, HEAD
b47d8cc27aeba391e6cd650db3dc30d382f7e06e plus these four uncommitted author
files. Whole plan4ace80f09f19ad4a/filescd54f2533b15aca0/reporta3098d77497f9600
were read, with current ticket/gates and EPIC-014 context. Earlier PCH conflict
veto and QDOS proved-original findings remain fixed; not reopened here.

SHA256 of reviewed bytes:
- AlsInstructionExtractionPolicy.cs:
  63ADF72044F3C536BF2402728F53CA47666DC344AC4DFEC81947B7D06C45A8A5
- YmlInstructionExtractionPolicy.cs:
  85A26246D3A4BB20D0FDC1C5AB91E5637463AA8E8CB8ABA065D46A113FA906E1
- QdosAllocationRecoveryTests.cs:
  020A3933851C270A765CF0B9DA4960D271F0939C1F74441F01A0239A950FB1EF
- Top15InstructionCorpusTests.cs:
  2C1C7C28CA82827F500551FE3EFA73E452A3666CD82DA09B639DB205413C07A8

Also inspected root-generated docs/design/test-ui/index.html and
pages/administration-principal-settings--default.html. Git-normalized diff
against HEAD is empty for both (reported dirty bytes are line-ending shape).
Byte SHA256 respectively:
C48AEFB6A8D72F0C4433F04827FA8A616EBA0C549BA590E9998C92520EAD0202 and
E9F42E892940D4EC45CC06FDF233A8C3A93EDC6050DA6810FF1A6C94C748FE4C.
No new visual pass or capture claim by this reviewer.

## F-P01 — major, open: receipt roundtrip drops exact field provenance

InstructionFieldEngine.SourceStructure.Bound preserves source-cell Locator and
RawValue in InstructionFieldCandidate. EfIntakeReceiptStore.SerializeFields
around1239 and DeserializeFields around1251 map only Value/Source/SourceLabel;
PersistedFieldCandidate around1625 contains only those three members.
The persisted receipt therefore loses both optional provenance members.

Root's current actual ALS SQL/mail test reaches correct typed values and Case
allocation, then fails at candidate.Locator.Kind (test line180); this is not
a labelled-engine failure or an assertion to remove. Root's four-case run
reported FW/SBL/YML PASS and ALS FAIL. Reviewer ran no test.

Approved disposition in progress: map existing Locator/RawValue through that
existing JSON record and both mapper directions, with same-fixture full
roundtrip proof. No schema/new provenance store, no weakened locator assertion.

## F-P02 — major, open: table-number collision across physical instructions

New ALS InstructionFields groups by physical DocumentIdentity but yields the
original table numbers from all groups into one ExtractFields call.
InstructionExtractionPolicySelector around220 can select multiple matching
physical documents for the same profile. SourceStructure around158–181 indexes
only integer Table; TableIndex.Add around346 assigns cells[(row,column)].
Thus the next document's table1/row4/column2 replaces the first document's cell
before the shared candidate-conflict logic sees both. Contradictory client VRMs
can become a single last-document candidate instead of ambiguity.

Approved disposition in progress: key the existing SourceStructure table index
by (physical DocumentIdentity, table), preserving actual locators/source labels,
and add one structural two-document conflicting-cell probe. No artificial
renumbering, new parser/engine, or duplicated policy.

## Otherwise bounded findings

YML's closing issuer lookup now starts after the proven Dear boundary and keeps
missing-closing refusal; its original-PDF explicit identity check remains.
The genuine HD4021 email is correctly retained as accepted-route report
correspondence/no draft/no Case, not mined for a new instruction in two-deep
history. Its missing positive initial-envelope coverage is explicitly disclosed.

ALS's paired client/third-party header and single client value-cell checks
exclude third-party columns and refuse missing/duplicate client values; known
owner/claimant and exact typed source expectations are stronger than the old
nullable-output assertions. Both occurrences and replay remain in each of the
four independently reported source tests. No further material source finding
in this bounded correction census.

Stop for the author batch and root's focused results. Formal independent
kanmer-review must bind the final pushed head, refreshed report/plan, live
checks and threads; this preflight record cannot authorize merge.

---
kind: review-attestation
pr: "698"
head_sha: "1fb0a7a1d907e6a82a586ea8ba14d4a9c58d75b4"
verdict: pass
reviewer: "intake_audit"
independent: true
plan_hash: "94560a72b43c9aad"
ticket_updated: "2026-09-08T04:58:37.791Z"
board_sha: "9baa2f4ea19e7321cd406ed9654b314218750ec0"
expected_reviewers:
  - intake_audit
threads_snapshot: []
findings:
  - id: F-001
    severity: major
    summary: "Pre-runtime import authority allowed non-engineering lifecycle states."
    disposition: fixed
  - id: F-002
    severity: minor
    summary: "Pre-runtime successful hash replay discarded valid browser edit authority."
    disposition: fixed
  - id: F-003
    severity: minor
    summary: "Pre-runtime optional initial Web name was lost across pending OCR."
    disposition: fixed
  - id: F-004
    severity: note
    summary: "Fifth genuine YL69YFO OCR and full-row oracle remain final acceptance."
    disposition: accepted-risk
    reason: "Root explicitly authorizes integration as a prerequisite to the deployed canary; the same ticket must remain Verifying until genuine retained Azure output and the independently reviewed fifth oracle pass. This is not a waiver or a Done decision."
---

# Independent consolidated review — TICK-085

## Decision and immutable inputs

PASS for the approved integration milestone only. No remaining material
source defect was found in the bounded PR. This is not proof of merged code,
deployment, provider acceptance, all five documents or final Pegasus v1.

Distinct roles: principal_delivery_audit authored; intake_audit is root's sole
expected independent reviewer and authored none of this PR. Public exact-head
review [5137483957](https://github.com/collisionengineers/pegasus/pull/698#pullrequestreview-5137483957)
was posted before this whole attestation. The expected set is settled.

- PR 698, source repository collisionengineers/pegasus, base dev,
  base observed 493f7460d7728a6576d240d4feb7d0bf2a377ec5.
- Local HEAD, remote author ref and GitHub head all equal the full SHA above.
  The author worktree is clean. The branch is TICK-085-glass-pdf-import;
  no reviewer checkout, source edit or claim mutation occurred.
- Author baseline cdaa02584c38ecc27d3bd24784f59da189138bc1.
  Full scoped diff: 29 files, +1692/-464; GitHub file census agrees.
- Research d3759f8ba28174b4; files 53c037ca57c47e28;
  plan 94560a72b43c9aad; report 204ae8cecdb2274d;
  checklist 343e0a28b27fdb85; questions 69cbccb26204d8a0.
- Ticket Review at the bound timestamp, revision rev1:30c7e40a95173131.
  No prior formal remediation round is recorded. Enter-Review gates pass;
  proof remains absent and Done is not authorized.
- Read current packet, governing FRD-06/07/10/11 and relevant UI authority,
  EPIC-009/011 context, and the explicit root integration milestone.
  Historical group provisions do not override the current user/root scope.
- Board was pushed, ahead/behind 0/0 at the bound board SHA.

## Source and caller review

Read every changed production/test/doc file, including both new PDF readers,
the shared parser contract and its adapters, Core import and import-specific
validation, real EF persistence/metadata, Web/MCP/Glass callers, composition,
and the affected caller and parser fixtures.

The retained source is authorized before content/hash replay and binds Case,
occurrence, exact version, content length and hash. The EF metadata join now
requires occurrence.VersionId == version.Id without wrongly excluding a
correct historical tuple. Bounded logical bytes are checked again. A single
registered PDF container positively selects Glass or Audatex; contradictory
or absent provider signatures refuse the whole import. JSON/XML adapters
retain their existing parsing rules.

OCR uses the existing source/page-bound operation identity and store. Pending,
Unknown and failed states return durable status without partial rows; a
completed result must agree with the retained operation and qualified pages.
There is no second provider client, queue, polling loop, worker, schema or
arbitrary OCR fallback. The fifth real OCR result is not yet evidence.

The import-specific persistence path shares the existing serializable
estimate transaction. Case version/lease and assessment editability are
checked before replay and again for the write. Same-source replay preserves
the existing ID and authority without duplicate rows or history. Imported rows
are unconfirmed Drafts, including Engineer imports. Ordinary Automation
SaveEstimate remains AI-job-bound, and only the existing Engineer action can
accept/make Current. Source rates/VAT do not invent the chosen rate card or
repairer status.

Web now retains once and calls the same Core import as MCP and Glass's.
Fresh staff retention consumes exactly one Case version; automatic custody
confirmation does not. The code never adopts an arbitrary latest version.
The retained-source action posts exact occurrence/version/hash plus current
authority, without a new upload. Source replay leaves browser authority for
the normal GET to reconcile. The existing dialog is not expanded into the
separate ENG-033 whole-page-drop scope. Razor review covered real forms,
antiforgery, actor/state gating, concise status labels and accessible controls;
no browser/manual visual pass is claimed.

The Glass coordinate reader preserves ordered main/included rows, guide
identity, source operations/overlap, unambiguous parts and provenance.
Appendices are not charged twice; repeated printed operations are not deduped.
Whole-row/section/document arithmetic refuses incomplete or contradictory
evidence. The independent four-original oracles assert all 244 main rows and
65 part entries, including disclosed clipped text and guide ambiguity.

## Prior findings and failure dispositions

F-001 fixed at this author head: existing AssessmentAccessPolicy.IsReadOnly
is reused by EfRepairSpecificationStore.RequireImportAuthorityAsync and the
final imported save. Actual MCP rejects before native handoff with zero byte,
OCR and Draft effects; SQL covers Review/NotReady/Held refusal before both
checks. ReportPreparation/PostReport remain the existing editable states.

F-002 fixed at this author head: Details.ImportRetainedEstimateAsync preserves
the validated request's browser authority after success. Existing GET
restoration clears it only after a committed mutation consumed the server
lease. The Web real-Core replay regression asserts one retained source, one
save, unchanged version/lease and no unnecessary recovery prompt; actual
SQL/MCP replay and history assertions provide persistence evidence.

F-003 fixed at this author head: the initial Web Name field and argument were
removed; immediate and pending imports use the same provider-sequence owner.
The existing estimate editor still renames Drafts; explicit Core/MCP names
remain supported. No optional UI input is silently discarded across OCR.

The first root Release build failed with five test compiler/analyzer errors,
58.02s, zero warnings, after locked restore passed. The four-file correction
qualified the existing XML fixture, fixed a constructor argument and used
the existing Assert.Single predicate form; no tests ran on that attempt.

The next root integration cohort had four failures. Two stopped at obsolete
fabricated email setup: the correction uses the existing processed receipt
fixture through real store/acceptance, not a new instruction or relaxed
classification. It is not source-byte intake evidence. The other two were
MCP omitted-null serialization and existing Engineer-refusal wording assertions;
operation/state/no-write assertions remain. All 17 helper consumers plus
those two exact methods then passed. Earlier failures are preserved, not erased.

## Runtime evidence read independently

No reviewer build, test, CI, capture or provider call ran. The following
existing TRXs were read and their counters/SHA-256 independently checked:

| Existing artifact | Actual result | SHA-256 |
| --- | --- | --- |
| Core TestResults/tick-085-core-compile-corrected.trx | 61/61 PASS, no skip | E75A52450E4A99CEFEBA665D6C4B21B291FA55E5AB4E72E90314E3C4C6658FB2 |
| Integration TestResults/tick-085-integration-compile-corrected.trx | 113 PASS / 4 FAIL of 117, no skip | 2AE32A27F7B0E8B526D5D199C2FC89278C46B3423884DA6B779A94125A74194D |
| Integration TestResults/tick-085-mcp-fixture-corrected.trx | 19/19 PASS, no skip | F433320506201D247CFCD02990361D8F2CA1E134526982B0E389BDD2957FFAD3 |
| Integration TestResults/tick-085-case-capture.trx | 3/3 PASS, no skip | F732D7D897C3F0E0D407329096A89411867C41356BE78A104E78136028AD3BAB |

Paths are under the respective tests/Pegasus.*.Tests projects in the author
worktree. Report retains exact commands, timings and all attempts.
Root recorded incremental builds PASS 17.71s and 24.32s, zero warnings/errors.
The corrected cohort was not a repeated unchanged 113-test run.

Root's fresh three canonical Case captures and scoped snapshot update 3 PASS,
verify 3 PASS, catalogue 60 routes / 67 prototypes / 0 broken are recorded.
The reviewer independently compared all four generated file blob identities:
index and default/conflict/unavailable pages equal HEAD exactly. There is no
tracked snapshot delta, fabricated capture or manual visual acceptance.

## GitHub freshness and remaining acceptance

At final gather, PR is OPEN, MERGEABLE/CLEAN, same exact head; reviews contain
this sole expected review. GraphQL returned zero review threads with
hasNextPage=false. No conversation was silently discarded.

Classic dev branch protection returned HTTP 404; applicable dev branch rules
returned an empty list. Status checks are empty, not represented as PASS.
Root-approved [skip ci] uses the converged final-CI policy; no required live
check was bypassed. The automated security status comment
5579490710/IC_kwDOThBrk88AAAABTJBFlg was running with mergeGateEnabled=false,
contained no finding, and is informational rather than an expected reviewer.
Any later substantive comment or changed head/plan/timestamp requires freshness
review before merge.

F-004 is accepted only as integration sequencing. Exact deployed caller and
Worker are prerequisites to the genuine YL69YFO canary. TICK-085 stays
Verifying until real retained Azure output and the independently reviewed
fifth full ordered oracle pass alongside exact-merge evidence, coordinated
with PLAT-065/TICK-041. Six-page qualification and protocol fakes cannot
satisfy that requirement. No final acceptance has been waived or transferred.

Root owns final guarded merge and any single Review-to-Verifying move.
This reviewer neither merges, moves stages, writes proof nor closes claims.

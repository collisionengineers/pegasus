# Stream B post-implementation report

## Status

This is a provisional implementation handoff for CASE-047 at candidate head
`b8d7cd325f7ce76e442706237b8a3424d84accfe`. The frozen B source tip
`83e875022a1f732af3bbbb2c60f431cd2323bfa6` remains in the candidate
history. Implementation is complete through B08, and B09 source review has no
unresolved source or caller finding.

Final CI run 34126704865, the formal exact-head review, integration into
`dev`, merged-head proof, and the open `dev` to `main` PR remain
outstanding. This report does not claim final verification, integration,
deployment, release, or Done.

## Scope delivered

The implementation follows the original B01-B09 file map and residual
acceptance in
`pegasus_pack/astra_output/v1_implementation_plans/streams/B-casework.md`,
the authoritative file-ownership register, canonical plan version
`57789d3887881907`, and approved closeout version
`adfb276e7412137b`.

The delivered B scope includes:

- the routed Case workspace and its bounded header, document, history and
  estimate queries;
- named estimate creation, duplication, version selection, comparison,
  discard and explicit current-version behavior;
- vehicle, valuation and sourced engineering evidence;
- Glass's credential administration, launch, resume, callback, durable session
  claim, custody import, replay and expiry behavior;
- immutable report and fee-note generation, signatory selection, prepared
  image evidence and routed report delivery;
- Case file viewing, upload-request metadata and asset preparation;
- VAT, discount and estimate preparation controls; and
- authorization, lease, concurrency, failure and persistence coverage for the
  production callers above.

The complete changed-path ownership remains the B01-B09 map referenced by
`files/files.md`; shared A/C files retain their own owner records. The
Accepted/Superseded projection correction uses persisted raw and printed totals
without recalculating them under current policy. Its fixture compares every
`EstimateTotals` value and the anomaly sequence separately, without weakening
the assertion.

## Caller and source review

The independent B review passed the B01-B09 production sources through
`b49e8c14c97237183f651b86cd75b99f3ebc94a7`, including frozen raw and
printed totals, confirmed report image sources, Glass's
authorization/session/custody, signatories, report preparation and delivery,
and transactional workspace, valuation and asset paths.

Later B-affecting deltas were reviewed independently. Generated report
attachment version identities compare as nullable values in
`EfStaffMailSendStore`. The estimate fixture-only correction received a
source PASS with all value and anomaly assertions retained. The Glass fixture
change first exposed serializable primitive values, then added 17 unique short
scenario labels; all 17 launch and four relay inputs and assertions remain
unchanged. The strict shard coverage guard is unchanged.

The one-worker Core CI correction at `12f45d193` preserves production regex
grammar, time budgets, filters and assertions. The SQL job correction at
`2506f85d18` only raises the existing allowance from 20 to 30 minutes; its
independent B review found no change to commands, sharding, the four-thread cap,
artifacts, coverage or assertions. The generated snapshot at `b8d7cd325`
received an independent A source PASS.

Recovery backups and source histories remain preserved. No new business-policy
owner, compatibility path, provider operation or deployment was added.

## Verification observed

Current retained evidence includes:

- Release build at `401d`: 0 warnings, 0 errors, 94.15 seconds;
- exact local and hosted one-worker Core at `12f45d193`: 1,818 passed,
  0 failed and 14 conditional skips; Architecture: 109 passed, 0 failed;
- hosted browser at `12f45d193`: 139 passed, 0 failed;
- affected custody, public-upload and Glass classes: 131 passed, 0 failed,
  followed by the conditional mapping-photo method at the correct corpus root:
  1 passed, 0 skipped;
- Glass-focused verification at `f5d1f1b12`: 69 passed, 0 skipped; discovery
  and partition verification found 1,848 unique tests, including all refusal
  rows, with exact one-shard coverage;
- exact-replay correction at `565`: 3 passed;
- hosted SQL shard 3: 597 passed and 1 conditional skip, exactly matching all
  598 assigned tests; and
- fresh UI group capture: 10 passed, 0 skipped in 85 seconds; the generated
  `b8d7cd325` snapshot received an independent A PASS; retained plus fresh
  full verification passed 2 tests in 66 seconds; the catalogue reports
  62 routed sources, 69 prototypes and 0 broken references.

Earlier failures remain evidence. A hosted SQL shard 3 result recorded 605
passed, 4 failed and 1 skipped before the custody fixture correction; the
focused 131-test and mapping-photo passes followed it. SQL shard 2 later
recorded 607 passed, 1 failed and 1 skipped before the focused three-test replay
correction passed. SQL shard 1 exceeded its 20-minute job limit, produced no
completed TRX and is not a PASS. The bounded 30-minute allowance still requires
the pending CI rerun.

The earlier custom-`Reply` theory shape enumerated two method rows but ran 21
expanded cases. Primitive MemberData then exposed all 21 rows. A later coverage
run found duplicate displayed names where long response bodies were truncated;
the unique first-position scenario labels correct that discovery identity.
The 69-test focused pass and 1,848-test discovery/partition result at
`f5d1f1b12` validate the bounded fixture correction without changing any
business behavior or assertion.

The earlier PNG missing-sample skip was retained as non-PASS history. After the
operator supplied the pinned extensionless source object and its SHA-256 was
verified, the byte-identical ignored `.png` copy selected through
`PEGASUS_CORPUS_ROOT` passed the genuine PNG test at built `f5d1f1b12`:
1 passed, 0 skipped in 29 seconds. The six scan-only MP samples remain INCONCLUSIVE release
evidence; they are not presented as passing.

## Remaining terminal steps

Final CI run 34126704865 must complete on the exact candidate and retain every
result. A fresh independent formal B09 review must then bind PR 674, the final
head, canonical plan, post-implementation report and ticket versions, comments,
threads, dispositions and pushed board tip.

After that pass, the authorized closeout may merge PR 674 once into `dev`,
verify its SHA, write the member-owned post-merge proof, and open the unmerged
`dev` to `main` PR with auto-merge off. No deployment, provider write,
mailbox mutation, mail send, Box write, reset, force-push or cleanup is
authorized.

# Research — DELIV-059: restore release-39 operational history

## Question

Which dated release-39 facts can be restored to the current canonical
`docs/operations.md`, with provenance clear enough not to present an old PR
narrative as a fresh Azure observation or reopen superseded release work?

## Findings

- [Documentation index](docs/index.md) names `docs/operations.md` the canonical
  owner for the last observed deployed estate and operational support. It names
  `docs/current-architecture.md` the owner of source structure, while Kanmer
  owns current work and delivery evidence.
- Current `dev` at `05995d325cc4c1ccd44096bf69d05fd42eeda3d2` has no
  release-39 entry in `docs/operations.md`. Its 6 September observation names
  release 38 and links the older baseline operations record
  `af1625fae8ac8018054c95e988907f6c44fa4639`; that referenced record likewise
  contains no release-39 source SHA, manifest, image digest, replacement ZIP or
  migration/reset record.
  - Source: read-only Git object comparison and text census, 8 September 2026.
- PR #676 head `93255e5c188865e5b0dab95d6c628ab49a902d99` contains the missing
  dated 7 September release-39 narrative in its unmerged
  `docs/operations.md` (lines 437–530).
  - It records source `3da60bd0c270111d5168dc17246dc831882108ea`, image digest
    `sha256:cc1a5077efdc761e359667a1bfc73b7cf8851e88437cd95fec2615fc41b1fe73`,
    manifest hash `F9C64EF7729484EF3BD3EFBF24F0791ADA4F53857EA05F5A80E8453A549D3E08`,
    migration head `20260907100000_RemoveAutomaticEvaSubmission`, the
    rejected manifest Worker ZIP and the replacement Worker ZIP/provenance
    deviation.
  - It also records the partial migration sequence and two separately
    authorised test-data deletions, plus the no-promotion and CI-waiver
    narrative. This is a retained historical PR claim, not a fresh cloud
    observation or an available immutable-artifact receipt.
- The claimed source SHA is a real commit and an ancestor of current `dev`.
  Git records it as the 7 September integration of PR #674. That establishes
  source identity only; it does not establish the deployed image, migration,
  Worker package, smoke or current production state.
- GitHub Actions run
  [34132950893](https://github.com/collisionengineers/pegasus/actions/runs/34132950893)
  is bound to that source SHA and independently confirms a failed `test-ui`
  job. Its `browser` job completed successfully, rather than being cancelled.
  The PR's stronger assertion that the operator waived both lanes, its
  `all 651 captures passed` detail, and its claimed cancellation therefore
  lack a matching retained receipt in this audit.
- No retained release-39 artifact directory was present at the path named by
  PR #676 (`../pegasus-releases/0.1.0-alpha.1-3da60bd0`) on this workstation,
  in the DELIV-048 worktree, or in the checked bounded Kanmer evidence census.
  The original manifest, rejected Worker ZIP, replacement ZIP and
  `config-zip` deployment identifier were not independently readable here.
  Their hashes, package-byte difference, deployment outcome, Azure observation
  and reset details must consequently be attributed to the historical PR record
  rather than described as newly verified.
- [[DELIV-054]] / PR #703 merge
  `6509746913eda16d2c4440add20e7f6793500f0b` is an ancestor of current
  `dev`. Its exact-merge proof independently establishes the later
  three-script source correction: `ZipFile` packaging plus manifest-bound
  Worker `.azurefunctions/` and Web `.playwright/` entry checks. Its proof
  explicitly does not claim an actual release package or deployment.
- [[DELIV-048]] retains portable workstation evidence and an inconclusive final
  artifact obligation; [[DELIV-047]] retains the former Linux-only release
  history and its separate unfinished live-release proof. Neither ticket is
  evidence that release 39's claimed Azure operation happened, and neither is
  in this ticket's edit scope.

## Implications

- Add one compact, dated historical release-39 record to
  `docs/operations.md`. It must preserve the failed initial Worker ZIP,
  replacement-package provenance deviation, partial migration state and
  authorised reset as historical evidence, not as a fresh current-state claim.
- Cite the exact PR #676 head/operations record and the CI run for the limited
  facts each actually supports. State the CI discrepancy honestly: the retained
  run proves the `test-ui` failure, but not browser cancellation or an operator
  waiver.
- Do not copy release-39 operational identifiers into current architecture,
  reintroduce Linux-only procedure text, expose secret values, reconstruct
  Azure commands, or claim that artifacts/telemetry/reset receipts were
  re-read. The current cross-platform release procedure and DELIV-054 source
  fix remain unchanged.
- A later plan should confine the diff to prose in `docs/operations.md`, use
  documentation-only validation and an independent semantic review. After the
  exact merged documentation SHA is proved, root can use it with PR #703's
  merge SHA when deciding the explicit PR #676 disposition.

## Open questions

- None requiring an operator decision: the ticket already directs historical,
  explicitly qualified recording rather than a fresh Azure verification.

# Stream B implementation plan

## Objective and authority

Finish all B01-B09 implementation and residual acceptance in
`pegasus_pack/astra_output/v1_implementation_plans/streams/B-casework.md`, following the current
DECISIONS.md, SHARED-CONTRACTS.md and COORDINATION.md in that pack.
The operator has moved all A/B/C work to this controller and host. This
supersedes the former cross-machine ownership and open-unmerged stop.

## Starting state

Common baseline: `3284f93fc3ea9fd3bbbea9405ec92dc7818378f2`.
CASE-047 remains implementing on `task/pegasus-v1-casework`, worktree
`../pegasus-worktrees/v1-casework`, PR 672. Published takeover checkpoint:
`58c17375f`. Preserve all published helpers and inspect their exact diffs
before adoption. The transferred lease belongs to the current controller.

## Preserved original plan

The original 89,900-byte plan remains byte-for-byte at board commit
`059e22bd5ba035dcccd4a3b44885983adc4ef2e7`, path
`.kanmer/areas/case-reference-workflow/CASE-047/plan/plan.md`.
SHA-256: `7E5C8884A8E75E7D161601241D36F1CF6072601F69C71CFC84DF4927AA8A24CA`.
All its requirements and residual acceptance remain binding except the
operator's explicit ownership and terminal-route changes below. This compact
index only makes the bounded execution packet readable.

## Expected files

Use the exact B01-B09 map in the authoritative stream plan and the single
file-ownership.csv/.json manifest. Shared foundation changes still publish
once as the same Git objects and are consumed by all owner branches.

## Ordered steps

1. Recover B owner and helper commits; preserve PR 670 and other original
   behavior according to the existing hunk dispositions.
2. Complete B01 Case workspace and real production commands.
3. Complete B02 estimates, including named tabs, create, duplicate, compare,
   immutable versions, discard and explicit Use as Current. Compare is included
   acceptance and is not silently deferred.
4. Complete B03 vehicle, valuation and sourced engineering data.
5. Complete B04 Glass's launch, resume, callback, custody/import and expiry.
   Consume G27 typed expected refusals; unexpected faults must surface.
6. Complete B05 report generation, immutable artifacts and real routed proof.
7. Complete B06 image preparation and optional EVA handoff semantics.
8. Complete B07 fee notes, preparation and actual report-delivery callers.
9. Complete B08 approved operator surfaces and B09 independent review, exact
   standalone/combined checks, all findings and final evidence dispositions.

Each step reuses the existing Core ports, EF stores, Razor handlers/partials
and test helpers named by its authoritative section; no parallel implementation,
new dependency or compatibility mechanism without a current requirement.

## Acceptance and commands

All named B01-B09 and ticket-by-ticket residual acceptance in the pack applies.
Prove actual callers, precise authorization, retry/replay and failure behavior.
Run locked restore, Release solution build, non-Corpus and Corpus tests,
migration grant census, current UI snapshot capture/verification and catalogue
checks using the repository runbook. Preserve every failed run; no skip or
INCONCLUSIVE is promoted to PASS. Live provider/workload acceptance remains
separately identified where the approved plan explicitly defers activation.

## Stop condition

After all remaining A/B/C work is reviewed and verified, integrate it and any
non-superseded original PR work into `dev`, then open the resulting PR to
`main`. Do not merge main. No deployment, reset, live provider write, mailbox
mutation or force-push is authorized. Preserve existing dirty work and Git
history. Record proof only after the authorized integration merge.

## Consolidated closeout under operator authority

The operator has moved all A/B/C operations and remaining scope to this host
and controller. This closeout uses the scoped procedure now recorded in
AGENTS.md: merge the frozen B and C source-owner histories into A, compare
the resulting source tree with the combined validation tree, then advance
B/C only by strict fast-forward to the reviewed common source head. Never
merge the verification branch into a source branch, rewrite history, add a
temporary compatibility path, create a fourth implementation PR, or merge
main. This supersedes the original cross-stream source-merge prohibition
for this consolidated closeout only.

Frozen source inputs were A dfb0d3f0bcc56a3b5aad44c50518961ffc6344e9,
B 83e875022a1f732af3bbbb2c60f431cd2323bfa6, and
C 5eb00580263773ac20cdbbe4b20771995a7cc8b4. A source merge
b32eaf14d83fb87b25845e88b865efe8c2a0441d contains all three histories.
Its only tree differences from validation commit
543dbbd5841736258ec38231bf0501c506f0620a are the AGENTS.md and NOW.md
closeout procedure records. The recorded conflict resolutions preserve
the already-validated Glass composition, OCR poison handling, current
snapshot fixtures and scoped typed callers. Later fixes and captures must
receive their own current-head checks; this checkpoint is not a final PASS.

Keep each ticket's original file/scope manifest and independent review.
The existing three PRs remain review vehicles for their scopes even when
they share the final source head. One normal GitHub merge of that head
into dev contains every owner commit. Verify the other PRs' actual GitHub
state and report containment honestly rather than inventing separate merges.
Verify the exact resulting dev commit, then open dev to main and leave it
unmerged. All cloud/deployment/provider/mail/Outlook/Box write prohibitions
remain unchanged.

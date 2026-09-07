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

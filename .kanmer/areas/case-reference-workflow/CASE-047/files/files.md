# Stream B file ownership

The authoritative exact-file/deepest-prefix manifest is
`pegasus_pack/astra_output/v1_implementation_plans/registers/file-ownership.csv`
and its JSON companion. The complete B01-B09 file map, production callers,
tests and residual acceptance remain in
`pegasus_pack/astra_output/v1_implementation_plans/streams/B-casework.md`.
Also read COORDINATION.md, DECISIONS.md, SHARED-CONTRACTS.md and
handoffs/B-foundation-requirements.json under the same implementation pack.

## Preserved original

The original 75,026-byte manifest is preserved byte-for-byte at board commit
`059e22bd5ba035dcccd4a3b44885983adc4ef2e7`, path
`.kanmer/areas/case-reference-workflow/CASE-047/files/files.md`.
SHA-256: `06E1F0BB37FA8AF842810A6BC67193881B92A5BDD98F1EDD56D88BF9858E118E`.
This compact index removes duplicated pack text, not requirements.

## Current ownership

On 7 September 2026 the operator reassigned all A, B and C work to the same
controller on this host. Preserve separate existing owner branches/PRs and
shared contract commit identities. The controller may now change all three
streams within their approved plans. Corpus remains immutable; no deployment
or live provider/mailbox/Box/cloud write is authorized.

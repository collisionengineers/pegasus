# File ownership

The complete authoritative ownership manifest is:

- `pegasus_pack/astra_output/v1_implementation_plans/registers/file-ownership.csv`
- `pegasus_pack/astra_output/v1_implementation_plans/registers/file-ownership.json`

Exact file then deepest prefix wins; ties are defects. Stream C may edit only
its assigned files. Stream A owns Foundation files and explicit contract-only
pre-Foundation exceptions; Stream B and Stream C retain their respective domain
behaviour.

The executable Stream C file map, production callers, tests, handoffs and
residual acceptance are maintained in:

- `pegasus_pack/astra_output/v1_implementation_plans/streams/C-intake.md`
- `pegasus_pack/astra_output/v1_implementation_plans/COORDINATION.md`
- `pegasus_pack/astra_output/v1_implementation_plans/SHARED-CONTRACTS.md`
- `pegasus_pack/astra_output/v1_implementation_plans/handoffs/C-foundation-requirements.json`

The exact previous contents of this document are preserved on this ticket in
`scratch/files-archive-part-1.md`,
`scratch/files-archive-part-2.md`, and
`scratch/files-archive-part-3.md`. Concatenate their payload sections in
numeric order to reconstruct the original. Original SHA-256:
`4e9a7be093f1f8d708a0264ca206a10017dd99e7b807b78524fec570dcbd1058`.

## Execution boundary

Before editing, resolve each path against the ownership manifest and the
corresponding C01–C09 section of the Stream C plan. Do not modify Foundation,
Stream A, Stream B, board, corpus, deployment, mailbox, Box, Glass's, EVA, or
cloud-owned paths. Stop on a material contract or ownership change.

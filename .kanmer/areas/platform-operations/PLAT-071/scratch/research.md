## 2026-09-03 deployment verification

- Azure read-only Container Apps readback: `pegasus-prod-web-252ow37gij--0f0e90ae44ff` is Healthy/Provisioned/RunningAtMaxScale, active, and receives 100% traffic.
- The active Web image is the immutable digest `sha256:b791d9587224d30d68fd6abcbd1e1d5f389f2baefc3702d9ec2d2f37398eef15`, matching the release-38 record in `docs/operations.md`.
- Release-38 source `0f0e90ae44ffda7339ca2a460310deeb98121afa` contains implementation commit `c7457628cbf883843aaad1539f94fee49fef5cc7` as an ancestor and contains the DOC/MSG dispatch, parsers, composition, and reader-version marker.
- No project-declared external research sources exist for this ticket context (`get_sources`: zero declarations).

# Research — analyzer strictness

## Question

Which analyzer and warning policy applies when CollisionRenderer source becomes production Pegasus code?

## Findings

1. Root `Directory.Build.props` governs production projects with nullable enabled, latest language, deterministic builds, `AnalysisLevel=latest-recommended`, and `TreatWarningsAsErrors=true`.
2. The renderer workspace stops root MSBuild inheritance and explicitly sets `TreatWarningsAsErrors=false`, omits `AnalysisLevel`, suppresses CS1591, and carries standalone product/package metadata.
3. ADR-0025 says integrated source leaves `workspaces/` and becomes part of the application; the existing repository convention wins. There is no second caller or architectural reason for a renderer-specific analyzer policy.
4. Build warnings revealed by migration are integration defects to fix. Broad suppression or disabling warnings would create a lower-quality enclave inside production.
5. XML documentation generation is already off by default in production projects, so the workspace's CS1591 suppression should not be needed unless a migrated project explicitly enables docs. Standalone package metadata/version should not survive as a competing product identity.

## Implications

- Integrated renderer code inherits the root analyzer/warnings policy unchanged.
- Fix all resulting warnings; add only narrow, justified suppressions at the smallest scope when a concrete false positive exists.
- Remove the workspace `Directory.Build.props` with the workspace after migration.
- Use Pegasus product/version/repository metadata, not standalone CollisionRenderer metadata.

# Extraction evidence record

Copy this template for a completed port unit or release candidate.

```yaml
claim_id: EV-
date_utc:
managed_commit_or_tree_hash:
port_units: []
formats: []
scope:
explicit_exclusions: []
specifications:
  - name:
    revision_or_date:
    sha256:
secondary_sources: []
fixture_manifest:
fixture_ids: []
commands:
  - command:
    exit_code:
    input_class:
    boundary:
    limitations:
environment:
  os:
  dotnet_sdk:
  architecture:
results:
  passed:
  failed:
  skipped:
differential_oracles:
  - name:
    version:
    command:
    comparator:
    tolerances:
security_and_resource_limits:
known_gaps: []
artefacts: []
reviewer:
```

When a claim uses a secondary oracle, include its exact name and revision under `secondary_sources`; this is not a default field when no secondary oracle was used.

A record supports only the named scope. “All tests passed” does not claim support for unlisted features, format revisions, item classes, platforms or nested content.

# Pegasus source workspaces

`workspaces/` is the source-only boundary accepted by ADR 0013. Its document
extraction, report renderer, AI Centre, and Agent Skills imports are delivered
sequentially by the active Pegasus orientation change record.

| Workspace | Imported source/provenance | Repository status |
| --- | --- | --- |
| `report-renderer` | Source review #7 at `493189012afee158793d1f5d1602b5708b33e530` | Source-only, independently built, no Pegasus caller |

This directory carries no production caller, deployment unit, package dependency,
or Pegasus business-policy authority. Each import must extend this manifest with its
reviewed source identity; a later manifest may add committed-blob inventories without
replacing these provenance statements.

# Report renderer workspace uplift — task plan

Transient task plan for the `report-renderer-workspace-uplift` claim
(`task/report-renderer-workspace-uplift`, taken 2026-08-05). It is deleted with
the `report-renderer-integration-*.md` set by the post-merge maintenance push.

## Relationship to the plan set in this directory

The sibling `report-renderer-integration*.md` files are the **specification**
this task implements. They were produced by the `report-renderer-integration`
planning claim (PR 331) and never merged. That PR is closed as superseded by
this one, which carries its plan set unmodified so the reasoning lands with the
work it authorises rather than being lost.

Read the master plan's "this task produces planning documents only" scope
statement as describing **that** claim, not this one. This task writes code.

## Scope

The unblocked, operator-decided portion of the plan set — everything that lives
inside `workspaces/report-renderer/` and needs no answer that the operator has
not already given. Nothing here relocates source, adds a caller, or advances a
capability.

| # | Work | Authority |
| --- | --- | --- |
| 1 | Remove the WinUI 3 desktop host and its `design/assets/report-renderer/gui/` package assets | Operator 2026-08-03; pre-authorised by `design/README.md`'s renderer boundary table |
| 2 | Upgrade Scriban 5.12.1 → 7.2.6; retire the `NU1901`–`NU1904` suppression | Operator decision B4 |
| 3 | Uplift the six remaining projects `net8.0` → `net10.0`, with the SDK pin, package bumps and script paths | Runtime-uplift plan, enabled by 1 |
| 4 | Repair the Dockerfile's non-existent `v1.61.0-jammy` base tag | Runtime-uplift plan, open question M3 |
| 5 | Replace `Format.Today()`'s machine-local `DateTime.Now` with a `TimeProvider`/Europe-London seam, with golden `en-GB` formatting tests | Runtime-uplift plan, step 8 |
| 6 | Correct `docs/operations.md`'s wrong Windows-only TFM row and the workspace's stale documentation | Same-commit authority rule |

`PreviewComposer`, `IPreviewComposer`, `PreviewResult`, the factory method and
all their tests are **retained** (operator decision 2026-08-03: the HTML preview
is wanted, separated from the GUI). Every template, stylesheet, logo and
signature asset is retained untouched.

## Out of scope — and why

Each of these is blocked on a question the plan set records as unanswered.
Implementing any of them would breach a stop condition.

| Excluded | Blocked on |
| --- | --- |
| Relocation into `src/`, any `Pegasus.slnx` change, any Core render port, Infrastructure adapter or Pegasus caller | B6 (parity-first vs workspace retirement), H1 (where rendering executes in production), H2 (unaccepted wording and signature images in the production assembly), B5 (licence-conclusion home) |
| MCP consolidation, `.mcpb` retirement, tool-inventory change | B2 (the parity definition), H3 (no capability ID under which a render tool is `Now` work) |
| Template bodies, `report.css`, design tokens, any RPT/EXT work | Deferred by B1; report wording is an open decision |
| Any capability band, target, activation or acceptance claim | B3: Stage 1 advances no capability identifier |
| Analyzer strictness under the root build properties | Open question M1 — recorded as deferred in ADR-0014 |
| Lock files | Open question M4 |
| CI runner-OS change, Linux lane, deleting the Dockerfile | Explicit non-goals of the runtime-uplift plan |

## Verification and evidence tier

**Applicable tier: 1 — static/build/architecture.** The workspace has no Pegasus
caller, no route, no persisted result and no deployed artefact, so no higher
tier is reachable and none is claimed.

| Check | Result |
| --- | --- |
| `dotnet restore` / `build -c Release` | Clean, 0 warnings, 0 errors, six projects |
| `dotnet test -c Release` | 236 passing, 0 failing (216 before; +20 additive) |
| `dotnet list package --vulnerable --include-transitive` | No vulnerable packages, suppression removed, `NuGetAuditMode=all` on `net10.0` |
| `dotnet sln CollisionRenderer.sln list` | Exactly six projects; no dangling GUI GUID |
| `Pegasus.ArchitectureTests` | Passing — the workspace is still outside `Pegasus.slnx` |
| `scripts/Test-DocumentationLinks.ps1` | All relative Markdown links resolve |
| Composed-HTML parity | 12 identifiers × 3 densities, byte-identical at every stage |

### Deliberate substitution

The desktop-removal plan's V5 asks for rendered-PDF SHA-256 parity. That was
replaced with **composed-HTML** parity, and the substitution is an improvement
rather than a shortfall: Scriban's entire contribution is the HTML string, so
comparing it isolates the change under test instead of interposing Chromium,
whose PDF output embeds a creation timestamp and document identifier and is not
byte-stable across runs. `pdftoppm` was also unavailable on this workstation, so
the rasterised route could not have run.

### Not verified

- **The container is not built.** Docker is absent. The two corrected base-image
  tags are a configuration fix, not a proven build. The `v1.61.0-jammy` tag
  genuinely does not exist, so no valid "before" image exists to compare against,
  and the move to noble shifts Ubuntu 22.04 → 24.04 with its font and ICU
  versions.
- **The `.mcpb` bundle was not built or launched** under .NET 10.

## Stop conditions honoured

No governed asset was touched; no accepted ADR body was edited; no capability
identifier moved; nothing claims a caller, deployment, activation or acceptance.
The three new ADRs (0012, 0013, 0014) each name exactly which prior decision they
supersede, per the workspace ADR index rules.

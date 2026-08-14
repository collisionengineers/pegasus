# Report renderer MCP consolidation — draft supporting plan

This is a draft supporting plan for the `report-renderer-integration` task. It
covers one seam only: bringing the report renderer's pre-existing MCP
functionality into Pegasus as the replacement for the `.mcpb`/stdio
implementation. The Core-owned render port itself is designed in the parallel
seam plan and is treated here as a stated dependency and assumption.

## Operator decision, 2026-08-03 — parity first, then delete

Open question 1 below was answered: **build the Pegasus `/mcp` render tools
first, keep the `.mcpb` working until parity is demonstrated, then delete.**

Amendments to this plan:

- Section 6's deletion inventory becomes a **second-phase** action. Nothing under
  `CollisionRenderer.Mcp/` is deleted until parity is signed off.
- **"Parity" needs a definition before it can gate anything.** It cannot mean
  seven-tool parity, because four of the seven are dropped on ADR-0011 and
  security grounds. Proposed definition, for operator confirmation: *the three
  retained tools — template list, validate, render — produce byte-identical PDFs
  for the same payload through the Pegasus port as through the stdio host, on the
  same machine and the same pinned browser build.* Byte identity is the right bar
  here precisely because both sides run on one machine; the workspace's own
  guidance that byte identity must not be promised *across* environments does not
  apply to a same-machine comparison.
- The external valuation connector keeps working throughout. Its long-term fate
  is **not** resolved: Pegasus renders for a Case and the connector has no Case,
  so the two-artifact valuation contract still has no Pegasus home. Open question
  7 stays live.
- **This collides with the Stage 1 decision to retire the workspace.** The stdio
  host is built from `CollisionRenderer.Mcp`, which references
  `CollisionRenderer.Core` — both inside the tree Stage 1 deletes. Three options
  are set out as open question B6 in the consolidated questions document; the
  recommendation there is to build the `.mcpb` once from the current commit,
  hand the frozen artefact to whoever runs the valuation connector, and delete
  the source. That is the only option that neither duplicates the engine nor adds
  a fifth project.

Separately, the Scriban decision recorded in the consolidated questions document
removes this plan's dependency-provenance concern in section 9.4: Scriban is
upgraded to a clean release rather than carried with suppressed advisories.

## 1. Target design

### 1.1 The decision

The renderer's render capability enters Pegasus as **additional
`[McpServerToolType]` classes registered on the existing Pegasus `/mcp`
streamable-HTTP surface**. The `CollisionRenderer.Mcp` stdio host, its `.mcpb`
packaging, and every host-local delivery mechanism it carries are deleted.
There is no second MCP server, no second transport, no second credential
boundary, and no second tool namespace.

Concretely, `src/Pegasus.Web/Mcp/` gains one file, `ReportMcpTools.cs`,
registered exactly like its three siblings at `AutomationMcpExtensions.cs:101-103`:

```
.WithTools<CaseMcpTools>()
.WithTools<IntakeMcpTools>()
.WithTools<DocumentMcpTools>()
.WithTools<ReportMcpTools>();
```

Everything else about the ingress is unchanged: the same
`AddMcpServer(...).WithHttpTransport(t => t.Stateless = true)` composition, the
same OpenIddict client-credentials token endpoint, the same
`AutomationMcp.EndpointPolicy`, the same
`AutomationActorResolver.RequireAsync(...)` first line in every tool body, the
same `AutomationMcpAuditor` action-history record, and the same
`Features:AutomationMcp` composition gate that keeps the whole surface absent by
default.

### 1.2 Why, against ADR-0004

`docs/adr/0004-provider-api-and-staff-mcp-authentication.md:105` is the decisive
precedent and it is already settled policy:

> A local MCPB/stdio bridge was rejected because the required case system is an
> internet-hosted application and the bridge would introduce a second client and
> credential boundary.

The `.mcpb` bundle is exactly that rejected shape, and worse than the general
case the ADR contemplated. `manifest.json` declares `server.type: "binary"`,
`entry_point: "bin/collisionrenderer-mcp.exe"`, `env.MCP_TRANSPORT: "stdio"`,
and `compatibility.platforms: ["win32"]`. `Program.cs` composes
`AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly()` with **no
authentication of any kind** — the security boundary is "whoever can start the
process". Pegasus's case system is an internet-hosted ASP.NET Core application
whose every automation action must be attributable to a durable actor with
permanent history. A stdio child process of a desktop client cannot supply that.

### 1.3 Why, against ADR-0011

ADR-0011 constrains the result in four ways the design must satisfy explicitly:

- *"MCP is a management/development-controlled ingress for one named,
  vendor-neutral Automation Actor, not a staff interface."* The render tools
  join the **existing** Actor's inventory. They do not get their own client,
  audience, or kill switch.
- *"The Actor may invoke only its approved inventory of ordinary operational
  Core use cases. It has no Administrator, configuration, credential, cloud,
  release, deletion, or other management authority."* This kills
  `install_browser` and `render_health` (section 2).
- *"MCP tools call the same Core use cases as Web and Worker callers."* This
  forces the Core-owned port (section 5) and is also the design's weakest point
  today, because there is **no** Web caller for report rendering yet.
- *"A tool schema, OAuth registration, endpoint, or client log neither
  authorizes a business action nor proves a caller."* Restated verbatim by
  `docs/requirements.md:884-889`. This governs the verification plan.

### 1.4 A different design was considered and rejected

The alternative: do not add render tools to MCP at all; land the render engine
as an Infrastructure adapter behind a Core port and let the staff Web UI be the
only caller (which is what RPT-01/EXT-08 describe). That alternative is *better
on the merits for report production* — reports are produced by Pegasus for
Pegasus, and the Automation Actor has no evident business need to author one.

It is rejected as the *whole* answer because it does not satisfy the directive,
and because the two designs are not actually in conflict: the render engine
enters through the Core port **in either case**. The honest framing this plan
adopts is that the port and its Infrastructure adapter are the substance; the
MCP tool class is a thin adapter over the port, identical in shape to
`DocumentMcpTools`; and the Web caller (RPT-01) is separate, later, and not in
this task. That framing is what keeps ADR-0011's "same Core use cases as Web and
Worker callers" clause honest over time.

### 1.5 What this design does not license

It does not activate report generation, change `Features:AutomationMcp`
defaults, or move MCP-01–04 onto the critical path (`NOW.md:98` explicitly keeps
them off it). `RPT-01` and `EXT-08` are allocated `Later / 1.1.0`. Creating an
MCP caller for a `Later / 1.1.0` capability inside a `Now / 0.1.0-alpha.1` gated
surface is a capability-allocation question for the operator, not something this
plan can settle.

## 2. Tool-by-tool disposition

Seven renderer tools become three Pegasus tools. Pegasus's asserted tool
inventory goes from **9 to 12**.

| Renderer tool | Disposition | New Pegasus tool | Scope | Core use case | Justification |
| --- | --- | --- | --- | --- | --- |
| `list_templates` | Keep | `pegasus_report_template_list` | `automation.reports` | `IListReportTemplates` | Read-only catalogue. No side effect, no host state, no filesystem. The cleanest survivor |
| `validate` | Keep | `pegasus_report_validate` | `automation.reports` | `IValidateReportPayload` | Dry-run schema/policy check. Already calls `PayloadValidator.Validate(..., allowLocalFilePaths: false)`, the correct server posture. Does not launch a browser |
| `render` | Keep, reshaped | `pegasus_report_render` | `automation.reports` | `IRenderCaseReport` | The genuine capability. Reshaped from "render anything, return a `file://` URI" to "render for a named Case, retain the artifact in canonical custody, return occurrence/version identifiers" |
| `install_browser` | **Drop** | — | — | — | ADR-0011: the Actor has no configuration, cloud or release authority. This tool downloads a browser from the internet (`OpenWorld = true`) and mutates the host filesystem. Under ADR-0015 the Web image is a digest-pinned OCI archive; browser provisioning is an image concern. A tool that mutates the deployed runtime is release authority by another name |
| `render_health` | **Drop as an MCP tool** | — | — | — | Two reasons. (a) Operational introspection, not an ordinary operational Core use case: it returns `browsers_path`, `driver_present`, `output_dir`, `evidence_root` and `bundled_shell_present`, i.e. host filesystem layout, to the Actor. (b) It warms Chromium as a deliberate side effect, so it is a cost amplifier. If a liveness signal is wanted it belongs on the existing `/diagnostics/*` surface |
| `open_valuation_output` | **Drop** | — | — | — | **This tool calls `Process.Start` with `UseShellExecute = true` and `explorer.exe /select,` on the machine running the server; on an internet-hosted Container App that is remote process execution triggered by a bearer token, and it must never exist in Pegasus in any form** |
| `render_valuation_outputs` | **Drop as a tool; salvage its mapper** | folded into `pegasus_report_render` | `automation.reports` | `IRenderCaseReport` | The two-artifact envelope, the `file://` descriptors, the `%LOCALAPPDATA%\CollisionRenderer\output` write and the `%LOCALAPPDATA%\CollisionEngineers\evidence` read are all host-local custody, which Pegasus does not have. The valuable part — `ValuationPayloadMapper`'s recursive snake_case→camelCase mapping and URL normalisation — is pure and migrates |

### 2.1 `render_valuation_outputs` and the `%LOCALAPPDATA%` evidence root

`Valuation/EvidencePathResolver.cs` is careful work: it constrains reads to a
canonical root, requires a `.pdf` extension, caps at 2,000,000 bytes, and —
unusually and correctly — requires a **mandatory** matching SHA-256 so a relayed
`{evidence_path, sha256}` pair cannot be redirected. Its own doc comment states
it is "deliberately NOT a relaxation of the render pipeline's
`AllowLocalAttachmentPaths=false` stance".

None of that survives contact with the Pegasus custody model, because the
premise is wrong for Pegasus, not the implementation:

- In Pegasus an artifact belongs to a **Case**, not to a folder. Custody is
  `ICaseCustody` plus `IDocumentContentStore`, whose implementations verify
  SHA-256 and length on both write and read and treat identical content as
  replay. Box is the durable store.
- The renderer's evidence root is a per-user directory on one Windows machine,
  shared out-of-band with a separate capture connector. There is no such shared
  directory on a Container Apps replica with `minReplicas: 0`, and there must
  not be.
- The correct Pegasus shape for "attach a previously captured PDF" is therefore
  **`(caseId, occurrenceId, versionId)`**, resolved through
  `IDownloadCaseDocument` / `IDocumentContentStore` inside the composition root
  — never a path supplied by the caller.

The integrity property `EvidencePathResolver` protected is already supplied
structurally: the content store verifies the expected SHA-256 on read, so the
mandatory-hash guarantee is preserved without a path.

`RenderRequest.TrustedLocalAttachmentPaths` (`Contracts.cs:62`) is the intended
seam: "Exact server-generated attachment paths accepted when arbitrary local
paths are disabled. Cloud callers cannot populate this set; composition roots
own it." The Infrastructure adapter materialises custody-resolved bytes to a
request-scoped temporary file and populates that set. The Core port never
exposes it.

### 2.2 Resulting tool signatures (sketch)

`pegasus_report_render` follows the `DocumentMcpTools.AddAsync` contract
precisely, because it is a case mutation:

- `caseId` (Guid), `templateId`, `payloadJson`, optional
  `attachments: [{ occurrenceId, versionId }]`, `density`;
- `expectedCaseVersion` and `editLeaseToken` — a render that retains an artifact
  is a case write and takes the same lease and optimistic concurrency guard as
  `pegasus_document_add`;
- `operationKey` prefixed `mcp:`, validated by
  `AutomationMcpErrors.RequireOperationKey`;
- returns occurrence/version identifiers, `sha256`, `contentLength`, `pageCount`,
  `warnings`, `isReplay`, `operationKey`, `correlationId` — the same
  bounded-inline-content discipline as `DocumentDownloadToolResult`, never a
  `file://` URI and never an unbounded base64 blob.

`ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false,
UseStructuredContent = true`.

## 3. The scope decision

**Recommendation: a new `automation.reports` scope. Do not reuse
`automation.documents`.**

- ADR-0011 grants the Actor an *approved inventory*, and the per-area scopes are
  the only per-area lever that exists — the kill switch is all-or-nothing for
  the whole client. Folding render into `automation.documents` means any token
  minted for network-drive scanning and submission silently also gains the
  ability to drive Chromium.
- Render is a materially different authority: it consumes CPU and memory
  disproportionate to every other tool, it makes outbound network requests
  (section 9), and it *creates* content rather than moving it.
- The existing code makes the addition nearly free, which removes the usual
  argument against a new scope.

### 3.1 Exact edits

`src/Pegasus.Web/Mcp/AutomationMcp.cs` — the only literal edits:

- after line 22, add `public const string ReportsScope = "automation.reports";`
- change lines 26-27 to
  `Scopes { get; } = [CasesScope, IntakeScope, DocumentsScope, ReportsScope];`

`src/Pegasus.Web/Mcp/AutomationMcpExtensions.cs` — **no scope edits are
required**, which is worth stating because it is easy to miss. Three sites
already project from `AutomationMcp.Scopes` and pick the new value up
automatically: `:41` `RegisterScopes`, `:79` `ScopesSupported` in the RFC 9728
resource-metadata document, and `:88-92` the `EndpointPolicy` assertion. The one
edit that file needs is `.WithTools<ReportMcpTools>()` at `:103`.

`src/Pegasus.Web/Mcp/AutomationClientRegistry.cs` — also **no edit required**:
`CanonicalDescriptor` at `:196-199` loops `foreach (var scope in
AutomationMcp.Scopes)`. Note the reconciliation timing: `EnsureRegisteredAsync`
short-circuits on a 24-hour `EnsuredCacheKey`, so an existing registration gains
the new scope permission on the next process start, not mid-process. State that
in the evidence notes so a stale-permission observation is not misdiagnosed.

`tests/Pegasus.IntegrationTests/AutomationMcpIngressTests.cs:19` — the
`AllScopes` constant gains `automation.reports`; `:21-32` `ExpectedTools` gains
the three new names.

## 4. Package reconciliation

The renderer uses `ModelContextProtocol 1.4.0` (`net8.0`); Pegasus uses
`ModelContextProtocol.AspNetCore 2.0.0` (`net10.0`). Different package *and*
different major version. Checked against the actual packages in the local NuGet
cache, not from memory.

| Concern | 1.4.0 | 2.0.0 | Impact |
| --- | --- | --- | --- |
| Package graph | `ModelContextProtocol` → `ModelContextProtocol.Core 1.4.0` | `.AspNetCore` → `ModelContextProtocol [2.0.0]` → `.Core [2.0.0]` | Same three-package family; AspNetCore is a strict superset and exact-pins its dependencies |
| Attribute namespace | `ModelContextProtocol.Server` | identical | **No change** |
| `McpServerToolAttribute` properties | includes `TaskSupport` | `TaskSupport` **removed** | The renderer tools do not use it; zero migration cost |
| `McpServerToolCreateOptions` | includes `Execution` | `Execution` **removed** | Not used by either side |
| `McpException` | `ModelContextProtocol.McpException` | identical | **No change** |
| `WithTools<T>` / `WithToolsFromAssembly` | present | present, identical signatures | Both styles work in 2.0.0 |
| `WithStdioServerTransport` | present | present | Still available in 2.0.0 — its absence is not what retires the stdio host; the section 1 decision is |
| `WithHttpTransport` / `Stateless` | not in the base package | `.AspNetCore` only | Confirms why Pegasus takes the AspNetCore package |

**Conclusion: there is no MCP API migration to perform.** The renderer's tool
attributes compile unchanged against 2.0.0. This is the single biggest
de-risking fact in the plan and should be stated up front so nobody budgets for
an SDK upgrade.

The real reconciliation costs are *build*, not API: the `net8.0` → `net10.0`
retarget; the root `TreatWarningsAsErrors=true` / `AnalysisLevel=latest-recommended`
versus the workspace's `TreatWarningsAsErrors=false` and Scriban `NoWarn`; and
the three packages `Pegasus.Infrastructure` would take.

**Marked unverified.** Neither SDK was executed, so these are assumptions to
confirm during implementation:

- whether 2.0.0's JSON-schema generation for `JsonElement` / `JsonNode` tool
  parameters behaves identically to 1.4.0. The renderer tools use `JsonElement`
  in and `JsonNode` out; Pegasus tools use typed records with
  `UseStructuredContent = true`. **Recommendation: convert to typed records and
  sidestep the question entirely** — it is also the house style everywhere in
  `src/Pegasus.Web/Mcp/`.
- whether `Stateless = true` interacts badly with a long-running render. A
  stateless streamable-HTTP request has no session to resume; a 30-second render
  inside one POST is untested here.

## 5. The dependency-direction problem

`Pegasus.Core` cannot take Scriban, Playwright, or PDFsharp.
`DependencyDirectionTests.cs:19-35` forbids a list of prefixes in Core's
referenced assemblies, and `:32` already forbids `ModelContextProtocol` itself.

**Note the gap:** that forbidden list does **not** currently contain `Scriban`,
`Microsoft.Playwright`, `PDFsharp`, or `CollisionRenderer`. Adding them is a
required part of this work, otherwise the guard that is supposed to protect the
new seam does not cover it.

### 5.1 The assumed port shape (dependency on the parallel seam plan)

The MCP tool class must reach the renderer **only** through a Core-owned port
implemented in `Pegasus.Infrastructure`. That port is designed by the parallel
seam plan; this plan assumes the following shape and **flags it as an assumption
to be reconciled**:

```
namespace Pegasus.Core.Reports;

public interface IListReportTemplates      // -> IReadOnlyList<ReportTemplate>
public interface IValidateReportPayload    // -> ReportValidationResult
public interface IRenderCaseReport         // -> RenderedReportResult
```

with request/result types built exclusively from BCL primitives. No
`RenderRequest`, no `RenderResult`, no `IDocumentRenderer`, no `JsonNode` crosses
into Core.

### 5.2 The unresolved blocker

Two options for how Infrastructure reaches the renderer, and **neither is
permitted by the accepted architecture as it stands**:

1. `ProjectReference` from `src/Pegasus.Infrastructure` into
   `workspaces/report-renderer/...`. Contradicts `docs/architecture.md:395-405`.
2. Vendor the needed renderer source into `src/`. This is what the parallel seam
   plan recommends, and it requires the accepted ADR that plan drafts.

`docs/architecture.md:407-415` says a workspace "may enter the application only
through a separately accepted contract" with a real caller, parity evidence,
security/licence evidence, migration behaviour, failure behaviour, and operator
acceptance. **This is a stop condition.** It is owned by the parallel seam plan,
but this plan cannot proceed past a compiling stub without it.

## 6. Exact deletion inventory

`git ls-files` reports **14 tracked files** under
`workspaces/report-renderer/src/CollisionRenderer.Mcp/`. All 14 go, in three
groups.

### 6.1 Packaging — delete outright (3 files)

| File | Note |
| --- | --- |
| `build-mcpb.ps1` | 115 lines. Throws unless `$IsWindows`; self-contained single-file publish; overlays the loose `.playwright` driver; installs `chromium --only-shell` into the bundle; zips to `dist/collisionrenderer-mcp-<version>.mcpb`. Invoked by **no workflow** |
| `manifest.json` | `manifest_version "0.3"`, `server.type "binary"`, `compatibility.platforms: ["win32"]`, and a hand-maintained 7-tool list that is a second source of truth for the inventory |
| `.mcpbignore` | 3 lines; only excludes `*.pdb` |

No `.mcpb` artifact is tracked, so there is nothing to delete from history.

### 6.2 Source — delete outright (7 files)

| File | Why it dies |
| --- | --- |
| `Program.cs` | The stdio host itself. All three of its concerns vanish on HTTP |
| `CollisionRenderer.Mcp.csproj` | `OutputType Exe`, `net8.0`, `ModelContextProtocol 1.4.0` |
| `BrowserBootstrap.cs` | P/Invokes `kernel32.dll` `GetStdHandle`/`SetStdHandle` to redirect native stdout→stderr around a Playwright install. The technique exists only because stdio MCP owns stdout. As deployed code it is a Windows P/Invoke inside a Linux container image |
| `Tools/OutputAccessTools.cs` | `Process.Start` / `explorer.exe`. See section 2 |
| `Tools/HealthTools.cs` | Dropped tool |
| `Valuation/ArtifactOutput.cs` | Writes to `%LOCALAPPDATA%\CollisionRenderer\output` and returns `file://` URIs. Replaced by case custody |
| `Valuation/EvidencePathResolver.cs` | Reads `%LOCALAPPDATA%\CollisionEngineers\evidence`. Replaced by `(occurrenceId, versionId)` resolution |

### 6.3 Source — migrate, reshaped (4 files)

| File | Destination and reshape |
| --- | --- |
| `Tools/RenderTools.cs` | The three surviving tool bodies are rewritten as `src/Pegasus.Web/Mcp/ReportMcpTools.cs`. Retain: `ParseOptions` density mapping, `WarnOnSnakeCaseKeys`, `AllowLocalAttachmentPaths = false`, `allowLocalFilePaths: false` in validate. Discard: `Console.Error` logging, `JsonNode` returns, `BrowserBootstrap.EnsureChromium()`, `ArtifactOutput.Write`, `install_browser` |
| `Tools/ValuationOutputsTool.cs` | Deleted as a tool; contract folded into `pegasus_report_render` only if the valuation pairing survives |
| `Valuation/ValuationPayloadMapper.cs` | Migrates essentially intact. Pure recursive mapping plus `NormalizeUrl`. Belongs in `Pegasus.Infrastructure` |
| `Valuation/ValuationOutputsRenderer.cs` | Migrates only if the valuation pairing survives, and only after stripping `BrowserBootstrap.EnsureChromium()` and both `ArtifactOutput.Write` calls. `Preflight`, `UnwrapJsonString` and `SanitizeUnresolvableImagePaths` are worth keeping |

### 6.4 `.gitignore` entries

- `workspaces/report-renderer/.gitignore` — remove the packaged-bundles block
  (2 entries + 1 comment).
- Root `.gitignore` — **do not touch.** `/workspaces/**/dist/` sits in the
  imported-source-workspace outputs block and applies to the other workspaces
  too.

### 6.5 Solution file

Remove the `CollisionRenderer.Mcp` and `CollisionRenderer.Mcp.Tests` project
entries plus their `GlobalSection` configuration and nesting rows.

### 6.6 Test migration — counts

`tests/CollisionRenderer.Mcp.Tests/` holds **10 `.cs` files** (8 test classes +
2 helpers) plus a `.csproj`. Measured: **51 `[Fact]` + 3 `[Theory]` = 54 test
methods**, with 12 `[InlineData]` cases.

**Die outright — 4 files, 34 methods:**

| File | Methods | Why |
| --- | --- | --- |
| `BrowserBootstrapTests.cs` | 7F + 1T (3 inline) | Tests the `RunEnsureOnce` latch for a deleted class |
| `OutputAccessToolsTests.cs` | 8F + 1T (2 inline) | Tests path helpers for a deleted tool |
| `HealthToolsTests.cs` | 3F | Tests a dropped tool |
| `EvidencePathResolverTests.cs` | 14F | Tests deleted local-path resolution. The *properties* it proves — containment, extension, size cap, mandatory hash — must reappear as assertions against custody resolution, but as new tests, not migrated ones |

**Migrate — 4 test files + 2 helpers, 20 methods**, of which **12 need
rewriting:**

| File | Methods | Treatment |
| --- | --- | --- |
| `ValuationPayloadMapperTests.cs` | 4F + 1T (7 inline) | Migrate near-verbatim; the mapper is pure |
| `ContractConformanceTests.cs` | 3F | Migrate; note it silently `return`s when the sibling connector schema is absent, so it may be a no-op in Pegasus — decide whether to keep that tolerance or fail loudly |
| `ValuationOutputsRendererTests.cs` | 9F | **Rewrite.** Every assertion touching `ArtifactOutput` / `file://` / evidence paths changes |
| `RenderToolsTests.cs` | 3F | **Rewrite** against the Core port, plus new `Pegasus.IntegrationTests` coverage |
| `StubPdfEngine.cs`, `ValuationFixtures.cs` | 0 | Helpers; migrate as-is / adapt |

34 + 20 = 54.

## 7. Verification plan

Mapped to the evidence tiers in `docs/engineering.md`. The integration coverage
mirrors `AutomationMcpIngressTests` exactly — same facts, extended, same
`[Trait("Category", "SqlServer")]`, same `IntakeWebApplicationFactory` +
`WithAutomationMcp` composition.

| # | Evidence | Tier | Where |
| --- | --- | --- | --- |
| 1 | Solution compiles; `ForbiddenCoreDependencyPrefixes` gains `Scriban`, `Microsoft.Playwright`, `PDFsharp`, `CollisionRenderer`, with matching `[InlineData]` rows | 1 | `DependencyDirectionTests.cs` |
| 2 | Port contract behaviour with a fake renderer: unknown template, invalid payload, oversized attachment, cancellation, warning propagation | 2, 3 | `tests/Pegasus.Core.Tests` |
| 3 | **Gate off exposes no surface** — extend `GateOffExposesNoAutomationSurface` unchanged; the new tools must add nothing when `Features:AutomationMcp` is absent | 5, 9 | `AutomationMcpIngressTests` |
| 4 | **Tool-inventory assertion updated** — `ExpectedTools` goes from 9 to 12; `tools/list` must return exactly those 12 | 5 | same file |
| 5 | **Token issuance** with `automation.reports` in the requested scope; resource metadata advertises it | 5, 9 | same file |
| 6 | **Transport denial** — unauthenticated `/mcp` still 401s with `resource_metadata` in `WWW-Authenticate`, and still writes an `automation_access_denied` security event | 5, 9 | same file |
| 7 | **Scope denial** — a token holding only `automation.documents` calling `pegasus_report_render` is refused, the refusal names `automation.reports`, and one `automation_scope_denied` event is written | 5, 9 | `ToolCallsAttributeHistoryAndEnforcePerAreaScopes` |
| 8 | **Real tool call with action-history proof** — `pegasus_report_template_list` succeeds and writes `ActionHistory` with `ActorKind = 'Automation'`, `EventKind` matching the tool, `Outcome = 'Succeeded'` | 5, 4 | same test |
| 9 | **Validation failure proof** — `pegasus_report_render` with `caseId = Guid.Empty` returns a content-safe refusal and writes `Outcome = 'Failed'` | 5 | same test |
| 10 | **Lease and concurrency proof** — render against a stale `expectedCaseVersion` or an absent lease fails closed, like `pegasus_document_add` | 4, 5 | new fact |
| 11 | **Kill switch** — extend the disable test to drive a render tool, proving disable takes effect on the already-issued token and refuses new ones with `unauthorized_client` | 5, 9 | same file |
| 12 | Custody proof — a rendered artifact lands as a case document version with verified SHA-256 and length, replayable by operation key | 4 | `Pegasus.IntegrationTests` |
| 13 | Rate-limit proof — the render tools are rate-limited, and the limiter's actual partition key is asserted (see 9.5) | 9 | `Pegasus.IntegrationTests` |

**Tier reached: 5**, together with tiers 1, 2, 4 and 9, via in-process HTTP
against the composed application.

**Explicitly not reached, and stated as outstanding:**

- **Tier-5 evidence from a real external client.** `WebApplicationFactory` is
  not an external caller.
  `docs/operations.md#automation-mcp-is-implemented-but-gated-off` records the
  current state as tier 2–4 with real-external-client evidence outstanding.
  This work does not change that; it enlarges what the eventual external run
  must cover.
- Tier 7 and tier 10. Adding a Chromium-driven surface materially raises the
  tier-10 debt and nothing here pays it.
- `docs/requirements.md:887-889` requires per-tool "an exercised real caller,
  expected success result, authorization failure, validation failure, and
  action-history proof". Items 7-9 supply four of five for each new tool; the
  "real caller" leg is the outstanding external-client item.

## 8. Interaction with the two queued NOW.md MCP follow-ups

**(1) Record tier-5 evidence from a real external client.** Runs **beside** this
work, and this work **enlarges** it. Neither absorbed nor blocked. The dependency
runs one way: if that evidence run happens before this task merges, it is
recorded against a 9-tool inventory and must be re-run or supplemented for the
12-tool inventory. Recommendation: sequence the external-client run **after**
this task and have it exercise all 12 tools in one session, so there is a single
tier-5 record rather than two partial ones.

**(2) Promote the settled Automation Actor contract to an ADR.** This work
**should absorb it.** Item 2 exists to freeze the tool inventory and the scope
set, and this task changes both — it adds a fourth scope and three tools.
Writing the ADR first would make it stale within one task; writing it after
leaves the contract unowned for the duration. Writing it *as part of* this task
is the only ordering that produces a correct artifact once. Recommendation: this
task produces the ADR, recording the Actor identity, the client-credentials
contract, the four scopes, and the 12-tool inventory, superseding nothing —
ADR-0011 stays the access-boundary decision and the new ADR is its activation
contract.

Neither follow-up affects `NOW.md:98`, which keeps MCP-01–04 off the critical
path. This work must not change that line.

## 9. Security review

Adding a Chromium-driven renderer behind an authenticated ingress is the single
largest change to Pegasus's attack surface that this plan proposes.

### 9.1 SSRF via renderer-fetched remote resources — highest severity

The renderer's own guard, `Format.SafeUrl` (`Format.cs:27-39`), deliberately
**permits** `http://` and `https://`:

```
var safe = lower.StartsWith("http://") || lower.StartsWith("https://")
           || lower.StartsWith("mailto:") || !lower.Contains(':');
```

That is correct for its purpose — blocking `javascript:`, `data:` and `file:`
hrefs. It is not an egress control. A caller-supplied payload containing an
`http(s)` image or link source causes the **server's** Chromium to issue an
outbound request to an attacker-chosen host. Against a Container App that means
requests originating from inside the application's network position, reaching
the platform metadata endpoint, any private endpoint, and any internal service
reachable from the replica.

Neither the renderer nor Pegasus has an outbound allowlist today.

Required mitigations, in preference order:

1. Run the render with network access disabled at the browser level; supply
   every image as bytes resolved from case custody and inlined, never as a
   remote URL.
2. Failing that, strip all remote URL sources from the payload in the
   Infrastructure adapter before the render (the renderer has a precedent in
   `SanitizeUnresolvableImagePaths` — invert it).
3. An explicit egress allowlist is the weakest option and should not be the
   primary control.

This must be closed before any environment where the replica has non-trivial
network reachability.

### 9.2 Local-file read — `AllowLocalAttachmentPaths`

**Verified: `RenderRequest.AllowLocalAttachmentPaths` defaults to `true`**
(`Contracts.cs:56`), documented as "True for the desktop app and CLI (the user
picks their own files); the cloud API sets this false so a caller cannot make
the server read arbitrary local files."

**It must be `false` for a server caller, and it must not be reachable from the
tool surface at all.** The Core port must not expose the flag; the Infrastructure
adapter hard-codes it on every constructed `RenderRequest`. Every existing
server-side caller in the renderer already does this correctly, so the pattern
is established; the risk is a new construction site missing it, which an
architecture or unit test should catch.

Related: `Format.SafeUrl` returns the value as safe when `!lower.Contains(':')`,
i.e. any relative URL. Whether a relative URL can resolve against a `file://`
base inside the composed document depends on how the HTML is handed to Chromium
and **was not verified**. Add it to the implementation review checklist.

### 9.3 Attachment size limits — the stated limit is not enforced

`DefaultMaxAttachmentBytes = 15_000_000` (`Validators.cs:17`), but at `:281-283`
exceeding it produces `r.Warnings.Add(...)` — a **warning, not a rejection**. The
render proceeds. This is a real gap: 15 MB is advisory today.

The tool boundary must impose its own hard cap, mirroring
`DocumentMcpTools.MaximumDocumentBytes` (10 MiB) and enforced through
`AutomationMcpErrors.DecodeContent`, which rejects rather than warns. Cap total
attachment bytes per render as well as per attachment.

### 9.4 HTML injection

The renderer's posture is sound and should be stated as a positive finding:
`Format.Enc` and `Format.Attr` HTML-encode via `WebUtility.HtmlEncode`,
`Format.SafeUrl` blocks `javascript:`, `data:` and `file:` schemes, and the class
doc states "Every text helper HTML-encodes by default". Templates are first-party
embedded resources, never caller-authored, and payload data is passed as *values*
to Scriban rather than compiled.

Two residual review items:

- `Format.Raw` exists as the deliberate escape hatch. Confirm no payload-derived
  value reaches `Raw` on any surviving path.
- The Scriban `NU1901-NU1904` advisories become a Pegasus dependency-provenance
  question once Scriban is referenced from `src/`. Re-decide rather than copy
  the suppression.

### 9.5 Resource exhaustion — and a correction to the rate-limit claim

ADR-0015 allocates Pegasus.Web **0.5 vCPU, 1 GiB memory, minimum zero replicas,
maximum one replica**. A Chromium headless shell driving PDF generation does not
fit comfortably in that envelope, and there is no second replica to absorb a
stall. A single render that pins the vCPU takes the whole application with it.

**Correction to a commonly stated fact.** The `AutomationMcp` rate limit is
described as 120 requests per *client* per minute, and the constant is named
`RequestsPerClientPerMinute`. But the policy in `Program.cs:280-289` partitions
on `context.Connection.RemoteIpAddress?.ToString() ?? "unknown"` — it is **120
requests per remote IP per minute**, not per client identity. Behind Container
Apps ingress the observed remote address may collapse to a small set, and the
practical effect is a shared bucket. For nine cheap tools that mattered little.
For a tool that can consume a full vCPU for seconds, 120/minute in one bucket is
an availability incident, not a limit.

Required mitigations:

- a dedicated, much lower rate-limit policy for the render tools (single-digit
  per minute), partitioned on the authenticated client identity rather than the
  connection address;
- a hard in-process concurrency gate (`SemaphoreSlim(1)`) around the render so
  at most one Chromium page exists at a time;
- an absolute per-render timeout, cancelled through the tool's
  `CancellationToken`;
- a page-count or output-size ceiling, since a hostile payload can produce an
  arbitrarily long document.

### 9.6 Chromium in the deployed image

ADR-0015 requires the release to be built once locally as an OCI Linux/AMD64
archive, uploaded with ORAS, and provisioned by exact `sha256` digest, with "no
placeholder image, Docker daemon, ACR build, `azd up`, remote build, or
release-time rebuild". A browser downloaded at runtime — which is exactly what
`install_browser` and `BrowserBootstrap.EnsureChromium()` do — is incompatible
with that model. The browser must be baked into the image, which materially
changes image size, build time, and the dependency-provenance record.

### 9.7 Pre-existing local-evidence choices, restated

`AutomationMcpExtensions.cs:46-55` uses `AddEphemeralEncryptionKey()`,
`AddEphemeralSigningKey()`, and `DisableTransportSecurityRequirement()`, each
with an in-code comment justifying it as a DevelopmentOffline-only choice.
Adding a render surface does not change their correctness today, but it raises
the cost of ever activating this ingress: a surface that can consume a vCPU and
make outbound requests should not be reachable over plain HTTP with ephemeral
keys under any circumstances. Flag for the eventual activation decision; change
nothing now.

## 10. Non-goals, stop conditions, open questions

### Non-goals

- Production activation of any part of the MCP ingress.
- Moving MCP-01–04 onto the critical path.
- Report lifecycle: versioning, correction/addendum, finality, issued artifact
  identity, delivery evidence. Rendering bytes is not issuing a report.
- Any change to Box custody policy, `ICaseCustody`, or the Case/PO folder model.
- Any staff Web UI for rendering (RPT-01), and any `Send to AI` / AI-proposal
  transport.
- `file://` delivery, `%LOCALAPPDATA%` output, evidence-root reads, or any
  host-local artifact custody.
- Runtime browser download in any environment.

### Stop conditions

1. The Core-owned render port is not landed by the parallel seam plan.
2. Reaching the renderer from `src/` requires a `ProjectReference` into
   `workspaces/` and no accepted decision supersedes `docs/architecture.md:395-405`.
   This is the most likely blocker.
3. The capability allocation is unresolved: `RPT-01`/`EXT-08` are
   `Later / 1.1.0` while `MCP-01–04` are `Now / 0.1.0-alpha.1`. There is no
   allocated capability under which an MCP render tool is `Now` work.
4. The SSRF finding cannot be closed by inlining custody-resolved bytes and
   disabling browser network access.
5. Any proposed change would allow the render surface to exist outside the
   `DevelopmentOffline` runtime profile.
6. Deleting the stdio host would break a live external workflow before a
   replacement exists (question 1 below).

### Open questions for the operator

1. **Confirm the intended reading of "replacement for the `.mcpb` style of
   implementation currently being used".** Three readings are plausible and they
   imply different tasks:
   (a) delete the stdio host and `.mcpb` now, and expose render on Pegasus
   `/mcp` — what this plan assumes;
   (b) build the Pegasus `/mcp` render tools first, keep the `.mcpb` working
   until parity is demonstrated, then delete;
   (c) something narrower — replace only the *transport and packaging* while
   leaving the seven-tool shape intact.
   This matters operationally: `render_valuation_outputs` and
   `open_valuation_output` exist to serve a valuation connector/skill that lives
   **outside this repository** and consumes the report-renderer-compatible
   `{artifacts, validation}` envelope. Deleting the stdio host today breaks that
   workflow, and Pegasus cannot replace it (Pegasus renders for a Case; the
   valuation connector has no Case). Reading (b) is the safe default unless the
   operator says otherwise. **This plan cannot proceed past design without an
   answer.**
2. **Under which capability ID does the MCP render tool set sit?** `MCP-05` is
   `Next / 0.3.0` and scoped to the classified-email workspace. `RPT-01`/`EXT-08`
   are `Later / 1.1.0`. A new ID, or a re-allocation, appears to be required.
3. **Is `automation.reports` the accepted scope name?**
4. **Where does a rendered artifact land in custody?** The natural fit is
   `DocumentSemanticRole.EngineerReport` with an automation source, but "the
   Automation Actor produced an Engineer report" may be a claim the operator does
   not want the data model to make. A distinct role or source may be wanted.
5. **Should this task write the Automation Actor contract ADR**, as section 8
   recommends, or should that queued item be sequenced after?
6. **Chromium in the production image.** Accept the image-size, build-time, and
   ADR-0015 sizing consequences now, or defer the whole render capability to a
   separate deployment unit and keep Pegasus.Web browser-free?
7. **Does the valuation two-artifact contract survive into Pegasus at all**, or
   is it permanently a connector-only concern? If the latter,
   `ValuationOutputsRenderer.cs` and `ValuationPayloadMapper.cs` die with the
   rest, the deletion inventory grows from 7 to 9 source files, and the migrated
   test count drops from 20 methods to 8.
8. **Does the Automation Actor have a genuine business need to render at all?**
   ADR-0011 says "MCP tools call the same Core use cases as Web and Worker
   callers", and there is no Web caller for render yet. If the honest answer is
   "no, the Web UI is the real caller", the correct scope of this task shrinks
   to: retire the `.mcpb`, land the Core port and Infrastructure adapter, and
   expose *only* `list_templates` and `validate` on MCP — leaving `render` to
   RPT-01. That would be a smaller, safer, and more defensible task.

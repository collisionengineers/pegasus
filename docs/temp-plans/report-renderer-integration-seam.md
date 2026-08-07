# Report-renderer integration seam — primary draft architecture plan

This is the **primary draft architecture plan** for the
`report-renderer-integration` task. It decides the exact seam and area in the
main Pegasus repository into which `workspaces/report-renderer/`
(CollisionRenderer) source is integrated, so that the workspace can be
retired. It contains an implementation plan, the required architecture-test
edits, a staging route that does not overclaim capability, and a draft ADR
body ready to be filed under `docs/adr/` at the next available number.

## Recommendation in one sentence

**Absorb the renderer into the existing four-project boundary: `Pegasus.Core`
gains a new, package-free `src/Pegasus.Core/Reports/` area that owns the
report render port, artifact identity, schema version and issue/correction
versioning; `Pegasus.Infrastructure` gains
`src/Pegasus.Infrastructure/Reports/` containing the relocated
Scriban/Playwright/PDFsharp engine as `internal sealed` adapters registered in
`AddPegasusInfrastructure`; no new project, no new deployment unit, no new
solution entry, and `workspaces/report-renderer/` is deleted.**

## Operator decision 2026-08-03 — the HTML preview is retained

The first draft of this plan listed `PreviewComposer.cs` among the files
deleted with the desktop hosts. **The operator directed on 2026-08-03 that the
HTML preview is wanted, integrated and separated out from the GUI.** This plan
is amended accordingly and the file-by-file split below reflects the decision.

Consequences carried through this plan:

- `PreviewComposer` is **relocated, not deleted**. It is validation-free and
  Chromium-free, which is precisely the property that makes it viable in a
  synchronous request path where the full Chromium render is not. It is a
  second, distinct seam — not a mode of the PDF renderer.
- Core therefore declares a second port, `IReportPreviewComposer`, alongside
  `IReportRenderer`. Both are package-free; the implementations differ in that
  the preview implementation never launches a browser.
- The preview is a **library and port capability at Stage 1**. It gains no
  staff-facing surface here. Two constraints stand in the way of one, and
  neither is resolved by the decision to keep the composer:
  1. **No capability ID allocates a report preview surface.** A staff-visible
     preview is a UI capability, and `design/README.md:48` requires every
     deferred UI capability to re-enter specification, alternatives,
     independent review, explicit approval, visual generation and manual
     visual review before implementation. `0.1.0-alpha.1` admits no control,
     navigation, workflow or placeholder for a deferred capability.
  2. **Composed preview HTML rendered into a staff browser is an injection
     surface.** `HtmlComposer` encodes through `Format.Enc` / `Format.Attr`
     and `Format.SafeUrl` blocks `javascript:`, `data:` and `file:` hrefs, but
     the preview path is explicitly the *validation-free* one. Any future Web
     caller must isolate the output in a sandboxed frame under a restrictive
     content security policy, and must never interpolate composed preview HTML
     into a Razor page. Carried in the risk register as R13.

## Operator decisions, 2026-08-03 — Stage 1 authorised; Scriban upgraded

Two further decisions amend this plan.

**Stage 1 proceeds now, advancing no capability.** The staging route in this plan
is authorised as written: relocate source, land both Core ports, register the
fail-closed renderer and the preview composer, edit the architecture tests,
extend the CI build-path pattern, file the ADR. Review must reject any PR that
claims more than the honest one-line status this plan already states.

**Scriban is upgraded, not suppressed.** The check this plan called for was run on
2026-08-03 with `dotnet list package --vulnerable --include-transitive`:

| Version | Result |
| --- | --- |
| Scriban 5.12.1 (the current pin) | **14 advisories: 1 Critical, 9 High, 4 Moderate** |
| Scriban 7.2.6 (current stable, `net10.0`) | **No vulnerable packages** |

The Critical is `GHSA-5wr9-m6jw-xx44`, CVSS 9.1, patched in 7.0.0 — a sandbox
escape where `TemplateContext` caches type accessors by `Type` only, built from
the then-current `MemberFilter`, so a reused context with a tightened filter keeps
exposing previously hidden members. **That is not obviously inapplicable here:**
`HtmlComposer` caches parsed templates in a `ConcurrentDictionary` and reuses
composition state across renders. Read whether it reuses a `TemplateContext`
across renders with differing member exposure before anyone argues the advisory
does not apply.

Amendments:

- **No `NoWarn` is added anywhere** — not root, not project-scoped, not
  item-scoped. Root `TreatWarningsAsErrors=true` applies unmodified. The
  paragraph in "Infrastructure adapter shape and registration" that specifies an
  item-scoped `NoWarn` is struck.
- `Pegasus.Infrastructure` takes **`Scriban 7.2.6`**, not 5.12.1.
- **Risk R8 is closed.** Stop condition 1 and open question 1 are struck.
- New work, and it is not free: 5.12.1 → 7.2.6 crosses two major versions and
  will produce breaking changes in `Templating/HtmlComposer.cs` and possibly the
  `.scriban` bodies. **Sequence the upgrade and its render-parity proof before
  the code move**, while the workspace still has its relaxed build settings and
  its own visual-regression script.

**Templates are untouched by Stage 1.** The operator decided the C# renderer is
the authoritative design, so the four `.scriban` bodies and `report.css` relocate
unchanged and no template work enters this task.

**One conflict is unresolved.** This plan's Stage 1 deletes
`workspaces/report-renderer/`; the parity-first MCP decision requires the stdio
host — built from projects inside that tree — to keep working until parity. See
open question B6 in the consolidated questions document. It gates the deletion
commit, not the relocation.

## Verified basis

| Claim | Verification |
| --- | --- |
| Seven projects in `Pegasus.slnx`, all `net10.0` | `Pegasus.slnx`; each `.csproj` |
| `Pegasus.Core.csproj` has zero `PackageReference` and zero `ProjectReference` | `src/Pegasus.Core/Pegasus.Core.csproj` is 8 lines |
| Core forbidden-dependency guard, solution-shape guard, workspace-reference guard | `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs:19-36`, `:123-149`, `:151-169` |
| Port idiom: interface in Core beside its records, `internal sealed` adapter in Infrastructure, registration in `AddPegasusInfrastructure` | `IDocumentContentStore` at `src/Pegasus.Core/Documents/DocumentContracts.cs:280-300`; `src/Pegasus.Infrastructure/DependencyInjection.cs:31`, `:279-282`, `:439-440` |
| Single-implementation, internal, exactly-named assertion idiom | `DependencyDirectionTests.cs:219-230` |
| Engine-port idiom closest to a renderer | `IVrmRecognitionEngine` at `src/Pegasus.Core/ImageIntake/VrmRecognition.cs:45-50` |
| Determinism precedent | `src/Pegasus.Core/Eva/EvaBundleSchema.cs:164-186` |
| Activation-gate precedent | `EvaMappingAcceptance` at `src/Pegasus.Core/Eva/CaseEvaMapping.cs:55-61`, wired at `DependencyInjection.cs:51` |
| Fail-closed adapter precedent | `internal sealed class UnavailableCaseCustody` at `src/Pegasus.Infrastructure/Custody/LocalCaseCustody.cs:350` |
| Renderer package surface is confined to three files | `Microsoft.Playwright` only in `Rendering/PdfEngine.cs`; `PdfSharp` only in `Rendering/PdfEvidenceAppender.cs`; `Scriban` only in `Templating/HtmlComposer.cs`. The other 17 Core files are BCL-only |
| Renderer determinism defect | `Core/Format.cs:106` — `Today()` uses `DateTime.Now` |
| Renderer assets already live in the top-level design system | `CollisionRenderer.Core.csproj` links `design/assets/report-renderer/templates/**/*`, `design/brand/logos/logo_no_margin.png`, `design/brand/signatures/**/*` |
| Pegasus already carries Playwright — in tests only | `tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj:17` `Microsoft.Playwright 1.61.0`; CI `browser` lane installs Chromium |

Two facts materially change the shape of this plan and were not anticipated:

1. **There is no Web Dockerfile.** `src/Pegasus.Web/Pegasus.Web.csproj`
   declares no `ContainerBaseImage`, and `scripts/Build-ReleaseArtifacts.ps1`
   builds the deployed image with `dotnet publish /t:PublishContainer
   -p:ContainerImageFormat=OCI`. The base image is therefore the default
   `mcr.microsoft.com/dotnet/aspnet:10.0`, which contains neither Chromium nor
   Liberation/DejaVu fonts, and `PublishContainer` cannot run `apt-get`.
2. **The CI build lane is path-gated and excludes `design/`.**
   `.github/workflows/ci.yml` gates every build/test lane on a pattern that
   does not include `design/`. Once Infrastructure embeds
   `design/assets/report-renderer/**` and `design/brand/**`, a template or
   stylesheet change would compile into the product without triggering a
   single build lane. That pattern must be extended in the same change set.

## Option evaluation and verdicts

### (a) Absorb into Core + Infrastructure — **ADOPT**

- The dependency split is already clean. Only three of twenty renderer Core
  files touch a forbidden package. Nothing that must go to Core carries a
  package.
- Infrastructure already owns exactly this class of adapter: `PdfPig`,
  `DocumentFormat.OpenXml`, `MimeKit`, `SkiaSharp`,
  `Microsoft.ML.OnnxRuntime` with vendored ONNX weights, `Box.Sdk.Gen`. A
  headless-browser PDF adapter is the same category as the ONNX vision engine
  accepted by ADR-0019.
- Infrastructure is already the manifest-resource owner, asserted by
  `DependencyDirectionTests.cs:212-216`. The renderer's templates, stylesheet,
  logo and signatures land where the existing architecture test already points.
- ADR-0009's decisive sentence is satisfied literally: *"Future rendering
  consumes a Core-owned render contract; report policy does not move into
  Infrastructure or the renderer."*
- It adds **no** top-level project, store, runtime, migration stream or
  deployment unit, so `docs/requirements.md:1052` does not bite.
- The two hardest architecture tests need **no relaxation at all**. Deleting
  the workspace makes both assertions *stronger*, not weaker.

Cost: Infrastructure gains three packages and a large native/browser runtime
dependency, and its published output grows by the Playwright node driver. That
cost is real and is carried in the risk register, not hidden.

### (b) A new `src/Pegasus.Rendering` project — **REJECT**

`docs/requirements.md:1052` requires an accepted ADR proving the existing
boundary cannot carry the work. **That proof does not exist and cannot be
constructed.** The only argument for a separate project is package
containment, and Infrastructure already contains eight adapter package
families including a native ONNX runtime and native SkiaSharp assets. A fifth
production project would break ADR-0002's four-project boundary and require
weakening two guards to solve a problem the current boundary does not have.

### (c) Keep the workspace and reference it — **REJECT (banned)**

Forbidden twice over: `DependencyDirectionTests.cs:151-169` fails any
`ProjectReference` containing `workspaces`, and ADR-0009 forbids it until a
capability-specific ADR defines the Core contract and proves the caller. A
referenced workspace also cannot be retired, which is the task's goal. It
further fails on mechanics: `net8.0` under a workspace props that sets
`TreatWarningsAsErrors=false` cannot be referenced from a `net10.0`,
warnings-as-errors project without weakening the root props for every project.

### (d) Separately deployed service — **REJECT now; retain as the named Stage-3 fallback**

`azure.yaml` declares exactly two services. A third is a new deployment unit,
gated behind the same ADR proof that does not exist. It also drags in a
security and operations surface with no accepted decision: the workspace API's
own bearer-token scheme (workspace ADR-0011, `CR_API_TOKEN*`) contradicts
ADR-0004 and ADR-0011; it would need its own registry repository, managed
identity, ingress, network path, health probe, alert rules and operator
acceptance. ADR-0014 also records that Pegasus has no non-production Azure
environment in which such a service could be proved.

Retained as the **named fallback** for one specific failure: if Stage 3
evidence shows Chromium cannot be provisioned into the Web container within
ADR-0015's sizing and release mechanism, the fallback is a separately deployed
render service **behind the same unchanged Core port**, decided by its own
ADR. Choosing seam (a) now costs nothing if that fallback is later taken.

## File-by-file split of `CollisionRenderer.Core`

All twenty files, 3,877 lines. "Core" means `src/Pegasus.Core/Reports/`;
"Infra" means `src/Pegasus.Infrastructure/Reports/`.

| Source file | Lines | Packages | Destination | Reason |
| --- | ---: | --- | --- | --- |
| `Contracts.cs` | 180 | none | **Split.** `RenderRequest`/`RenderResult`/`RenderOptions`/`ValidationResult` are *re-expressed*, not moved, as Core records. `Density`, `DensityFit`, `DensityFitProfile`, `PdfPageSettings`, `ComposedDocument`, `TemplateDescriptor`, `CrJson`, `RenderValidationException` → Infra, all `internal` | Page geometry, density auto-fit, Scriban resource names and JSON tolerance are engine detail |
| `DocumentRenderer.cs` | 213 | none | **Infra** `ChromiumReportRenderer.cs`, `internal sealed`. `BuildFileName`/`Slug` (`:149-181`) move to Core `ReportArtifactSchema` | Orchestration is adapter mechanics; artifact file naming is issued-artifact identity, therefore policy |
| `Rendering/PdfEngine.cs` | 262 | **Playwright** | **Infra**, `internal sealed` | Forbidden package |
| `Rendering/PdfEvidenceAppender.cs` | 83 | **PDFsharp** | **Infra**, `internal static` | Forbidden package |
| `Rendering/PdfPageCounter.cs` | 34 | none | **Infra**, `internal` | Byte-level PDF parsing |
| `Rendering/BrowserLaunchPlan.cs` | 63 | none | **Infra**, `internal`. Rename `COLLISIONRENDERER_BROWSER_CHANNEL` to a Pegasus configuration key | The env-var name is a product-identity leak |
| `Templating/HtmlComposer.cs` | 542 | **Scriban** | **Infra**, `internal sealed` | Forbidden package |
| `Design/BrandAssets.cs` | 83 | none | **Infra**, `internal sealed` | Reads embedded manifest resources; must live in the embedding assembly |
| `Design/EmbeddedResources.cs` | 59 | none | **Infra**, already `internal static` | Same |
| `Models/Documents.cs` | 245 | none | **Infra**, made `internal` | Template-shaped presentation view-models. Promoting them to Core would create a **second owner of case truth** beside `Pegasus.Core.Cases` |
| `Validators.cs` | 303 | none | **Split.** Attachment-path/security policy → Infra, `internal`. "Accepted data is complete enough to issue" moves to a Core `ReportPayloadPolicy` **at Stage 2** | Writing the Core validator now would encode invented field names |
| `Format.cs` | 160 | none | **Infra**, `internal static`. `Today()` (`:106`) **deleted**; date comes from `RenderReportRequest.IssuedAtUtc`. `Uk` culture replaced by `ReportArtifactSchema.RenderCulture` | Presentation formatting is not policy; `DateTime.Now` is the determinism defect |
| `TemplateCatalog.cs` | 166 | none | **Infra**, `internal sealed`. Core owns `ReportKind` and the kind→template-key binding | Scriban resource names are engine identity; the *set of report kinds* is policy |
| `LenientStringConverter.cs` | 74 | none | **Infra**, `internal` | Serves the string-typed money fields of the Infra models only |
| `PreviewComposer.cs` | 105 | none | **Infra**, `internal sealed`, implementing a new Core `IReportPreviewComposer` port | **Operator decision 2026-08-03: retained.** Validation-free, Chromium-free HTML composition — a distinct seam from the PDF renderer, viable in a request path where Chromium is not |
| `CollisionRendererFactory.cs` | 50 | none | **Deleted** | Its job is composition; `AddPegasusInfrastructure` is Pegasus's composition root |
| `AuthoringCatalog.cs` | 819 | none | **Deleted** | Blank-form/draft-payload authoring for the GUI, CLI and API. Pegasus payloads come from accepted case data, never from a hand-authored form |
| `StarterComposer.cs` | 167 | none | **Deleted** | Consumed only by `AuthoringCatalog` |
| `PlaceholderScanner.cs` | 85 | none | **Deleted** | Draft-authoring guillemet detection |
| `JsonPath.cs` | 184 | none | **Deleted** | Mutates a payload tree so multipart uploads can inject temp-file paths — a mechanism Pegasus deliberately does not adopt |
| **Totals** | **3,877** | | **2,572 relocated, 1,305 deleted, ~280 new Core lines** | |

Assets relocated as `EmbeddedResource` entries on
`Pegasus.Infrastructure.csproj` with explicit `LogicalName`s, matching the
existing `provider-domains.v1.json` convention rather than the workspace's
wildcard glob:

- `design/assets/report-renderer/templates/*.scriban` (4 files)
- `design/assets/report-renderer/templates/report.css`
- `design/brand/logos/logo_no_margin.png`
- `design/brand/signatures/*.png` (3 files)

Explicit per-file entries matter: a wildcard makes the embedded set invisible
to review and lets a new signature enter the production assembly silently.
`docs/requirements.md:949` classifies signatures as provenance-sensitive
document assets, so the embedded set must be enumerable and hash-assertable.

### One deliberate behavioural improvement carried in the move

The workspace API accepts client-uploaded files, writes them to
`Path.GetTempPath()`, and injects the resulting paths into the payload via
`JsonPath.Set`, guarded by `AllowLocalAttachmentPaths=false` plus allowlists.
Pegasus adopts none of that machinery. The Core contract references
attachments by **document-version identity** (`Guid DocumentVersionId` +
`Sha256` + `ContentLength`), and the Infrastructure adapter resolves them
through the existing `IDocumentContentStore`, which already verifies SHA-256
and length on read. The renderer therefore never accepts a filesystem path
from any caller, and `AllowLocalAttachmentPaths` disappears entirely rather
than becoming a flag someone can flip. This is why `JsonPath.cs` is deleted
rather than relocated.

## Fate of every renderer host project

| Project | Fate | Where its behaviour lands |
| --- | --- | --- |
| `CollisionRenderer.Core` | **Absorbed**, split per the table above | `src/Pegasus.Core/Reports/` + `src/Pegasus.Infrastructure/Reports/` |
| `CollisionRenderer.Cli` | **Deleted** | `install-browser` becomes a repository script/CI step modelled on the existing `browser` lane. `list`/`forms`/`render` authoring commands are not a Pegasus capability and are not recreated |
| `CollisionRenderer.Api` | **Deleted** | Nothing carried forward as HTTP. Pegasus's caller is an in-process Core port. Its security invariants are preserved structurally: no caller-supplied paths, and its batch cap is superseded by the fact that Pegasus renders one issued report per authorised action. Its bearer-token scheme is explicitly **not** adopted |
| `CollisionRenderer.Mcp` | **Deleted** | Handled by the parallel MCP-consolidation plan. The `Valuation/` subtree is not carried forward: Pegasus payloads originate from accepted case data |
| `CollisionRenderer.Gui` | **Deleted by the parallel desktop-removal plan** | This plan takes a dependency on that removal. It owns the consequence: the `design/README.md` GUI-asset row must be deleted in the same documentation change. Note the preview composer the GUI hosted is **retained** per the operator decision above |
| `.sln`, `Directory.Build.props`, `global.json`, `Dockerfile`, `scripts/`, `tests/`, `docs/`, `NOTICE.md`, `README.md` | **Deleted with the directory** | `Dockerfile` content becomes Stage-3 input for the browser-provisioning decision. `NOTICE.md`'s licence conclusions **must be carried into the Pegasus notice surface** — a licence-evidence activation condition. Workspace ADRs are handled by the parallel documentation-migration plan |

## The Core contract

Two new files, no packages, no `ProjectReference` — `Pegasus.Core.csproj` is
unchanged. Shapes follow `VrmRecognition.cs` (closed outcome enum + one result
record with nullable failure fields + a port interface),
`DocumentContracts.cs`, `EvaBundleSchema.cs` and `EvaMappingAcceptance`.

### `src/Pegasus.Core/Reports/ReportContracts.cs`

```csharp
using Pegasus.Core.Identity;

namespace Pegasus.Core.Reports;

/// <summary>
/// The closed set of report kinds Pegasus can issue. The set is policy: it
/// names what may be produced, never how it is laid out. Adding a member is a
/// requirements change, not a template change.
/// </summary>
public enum ReportKind
{
    MarketValuationEvidence,
    AdvertEvidencePack,
    FeeNote,
    ExpertReport,
    BlankLetterhead,
    RepairableContractRepair,
    TotalLoss,
    Addendum,
    DiminutionRebuttal,
    RoadworthyCriminal,
    Part35Response,
    ResponseLetter
}

/// <summary>
/// How an issue relates to its predecessors. A correction or addendum always
/// creates a new issue; it never replaces an earlier artifact.
/// </summary>
public enum ReportIssueKind
{
    Original,
    Correction,
    Addendum
}

/// <summary>
/// The closed render outcome taxonomy the operator surface must distinguish.
/// An absent artifact is never rendered as success, and an unavailable
/// renderer is never presented as a validation failure.
/// </summary>
public enum ReportRenderOutcomeKind
{
    Rendered,
    PayloadRejected,
    AcceptedDataIncomplete,
    RendererUnavailable,
    TechnicalFailure
}

/// <summary>
/// Binds a report kind to the exact template asset that produced an artifact.
/// The version is declared by policy; the hash is observed by the adapter at
/// render time, so a silently edited template cannot reuse a version number.
/// </summary>
public sealed record ReportTemplateBinding(
    ReportKind Kind,
    string TemplateKey,
    int TemplateVersion,
    string TemplateSha256);

public sealed record ReportPayload(
    string SchemaVersion,
    string Json,
    string Sha256);

/// <summary>
/// One already-computed presentation figure. Core computes every figure once
/// and hands the renderer literal values; the renderer performs no arithmetic,
/// no rounding, and no currency conversion.
/// </summary>
public sealed record ReportFigure(string Key, string Value);

public sealed record ReportComputedFigures(
    string PolicyKey,
    int PolicyVersion,
    IReadOnlyList<ReportFigure> Figures);

/// <summary>
/// An attachment named by durable document-version identity, never by a
/// filesystem path. The adapter resolves content through the document content
/// store, which verifies the hash and length on read.
/// </summary>
public sealed record ReportAttachmentReference(
    string FieldPath,
    Guid DocumentId,
    Guid DocumentVersionId,
    string Sha256,
    long ContentLength,
    string MediaType);

public sealed record RenderReportRequest(
    Guid CaseId,
    Guid ReportId,
    Guid ReportIssueId,
    int IssueVersion,
    ReportIssueKind IssueKind,
    Guid? SupersededIssueId,
    ReportKind Kind,
    ReportPayload Payload,
    ReportComputedFigures Figures,
    IReadOnlyList<ReportAttachmentReference> Attachments,
    ActionActor RequestedBy,
    DateTimeOffset IssuedAtUtc,
    string OperationKey);

/// <summary>
/// The immutable identity of one issued artifact. Every field needed to
/// reproduce or challenge the artifact is recorded here.
/// </summary>
public sealed record RenderedReportArtifact(
    Guid ReportIssueId,
    Guid ReportId,
    Guid CaseId,
    int IssueVersion,
    ReportIssueKind IssueKind,
    string ArtifactSchemaVersion,
    ReportTemplateBinding Template,
    string PayloadSchemaVersion,
    string PayloadSha256,
    string FiguresPolicyKey,
    int FiguresPolicyVersion,
    string ArtifactSha256,
    long ContentLength,
    int PageCount,
    string FileName,
    string MediaType,
    string RendererKey,
    string RendererVersion,
    DateTimeOffset IssuedAtUtc);

public sealed record RenderReportResult(
    ReportRenderOutcomeKind Kind,
    RenderedReportArtifact? Artifact,
    IReadOnlyList<string> ValidationErrors,
    IReadOnlyList<string> Warnings,
    string? FailureCode = null,
    string? FailureReason = null);

/// <summary>
/// The report render port. Accepted data and computed figures in, an artifact
/// descriptor plus bytes out. The renderer decides no business outcome, writes
/// no case state, computes no figure, and reads no file the request did not
/// name by document-version identity.
/// </summary>
public interface IReportRenderer
{
    Task<RenderReportResult> RenderAsync(
        RenderReportRequest request,
        Stream destination,
        CancellationToken cancellationToken);
}

/// <summary>
/// The HTML preview seam, retained by operator decision 2026-08-03. It is
/// deliberately validation-free and browser-free: it composes markup from a
/// possibly incomplete payload so a caller can see shape before an issue
/// exists. It never produces an artifact, never allocates an issue identity,
/// and its output is never evidence of anything.
/// </summary>
public sealed record ReportPreviewRequest(
    ReportKind Kind,
    ReportPayload Payload,
    ReportComputedFigures? Figures);

public sealed record ReportPreviewResult(
    bool Composed,
    string? Html,
    IReadOnlyList<string> Warnings,
    string? FailureCode = null,
    string? FailureReason = null);

public interface IReportPreviewComposer
{
    Task<ReportPreviewResult> ComposeAsync(
        ReportPreviewRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// The wording activation gate. Report wording (salvage Categories N, A, B and
/// N/A; recovery and storage; the final statement of truth; named
/// qualifications) is an open decision. Until an exact wording set is accepted
/// by key and version, every render returns RendererUnavailable and no
/// artifact is produced.
/// </summary>
public sealed record ReportWordingAcceptance(
    string? WordingKey,
    int? WordingVersion,
    string? EvidenceReference)
{
    public static ReportWordingAcceptance Unaccepted { get; } = new(null, null, null);
}
```

### `src/Pegasus.Core/Reports/ReportArtifactSchema.cs`

```csharp
namespace Pegasus.Core.Reports;

/// <summary>
/// Produces replay-identical issued-report identity without inspecting a
/// rendered byte. Schema version, media type, render culture, the pinned PDF
/// document timestamp, the file-name policy and the hash format are explicit
/// so two runs of the same accepted data on different machines are comparable.
/// </summary>
public static class ReportArtifactSchema
{
    public const string SchemaVersion = "pegasus-report-v1";
    public const string MediaType = "application/pdf";
    public const string RenderCulture = "en-GB";
    public const string RenderDateFormat = "dd/MM/yyyy";

    /// <summary>
    /// The fixed CreationDate/ModDate written into every issued PDF, mirroring
    /// EvaBundleSchema.DeterministicTimestamp. The real issue time lives in
    /// RenderedReportArtifact.IssuedAtUtc and case action history, never in
    /// document metadata, so the bytes stay reproducible.
    /// </summary>
    public static readonly DateTimeOffset DeterministicDocumentTimestamp =
        new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static string TemplateKey(ReportKind kind) => /* exhaustive switch */;

    public static string FileName(
        ReportKind kind,
        string caseReference,
        int issueVersion,
        ReportIssueKind issueKind) => /* Slug(caseReference) + suffix + version */;

    public static string Slug(string? value) => /* relocated from DocumentRenderer */;

    public static bool IsSha256Hex(string? value) => /* 64 lowercase hex chars */;
}
```

### `src/Pegasus.Core/Reports/ReportIssueVersioning.cs`

Stage 1 ships **only** the identity and no-overwrite rules, deliberately not a
lifecycle:

- `NextIssueVersion(int current)` — strictly monotonic from 1.
- `EnsureIssueIsNew(...)` — a `Correction` or `Addendum` must name a
  `SupersededIssueId` that exists and must allocate a new `ReportIssueId`;
  reusing an existing `ReportIssueId` is a fault, not an update.
- `EnsureArtifactImmutable(...)` — an artifact whose `ArtifactSha256` already
  exists under a different `ReportIssueId` is a replay, matching
  `IDocumentContentStore`'s identical-content-is-replay rule.

The correction/reopen **state machine** is deliberately absent: the CASE-23
post-report lifecycle is an open decision and `docs/requirements.md` records
its exact states and transitions as unresolved. Encoding one here would invent
policy.

## Infrastructure adapter shape and registration

New directory `src/Pegasus.Infrastructure/Reports/`, everything `internal`:

```text
Reports/
  ChromiumReportRenderer.cs        internal sealed : IReportRenderer
  UnavailableReportRenderer.cs     internal sealed : IReportRenderer  (fail-closed)
  HtmlReportPreviewComposer.cs     internal sealed : IReportPreviewComposer
  RenderPipeline.cs                internal engine-side records/enums
  TemplateCatalog.cs               internal sealed
  Format.cs                        internal static
  Validators.cs                    internal sealed  (attachment/security policy)
  Models/Documents.cs              internal records
  Design/BrandAssets.cs            internal sealed
  Design/EmbeddedResources.cs      internal static
  Templating/HtmlComposer.cs       internal sealed  (Scriban)
  Rendering/ChromiumPdfEngine.cs   internal sealed  (Playwright)
  Rendering/PdfEvidenceAppender.cs internal static  (PDFsharp)
  Rendering/PdfPageCounter.cs      internal static
  Rendering/BrowserLaunchPlan.cs   internal
```

`Pegasus.Infrastructure.csproj` gains:

```xml
<PackageReference Include="Microsoft.Playwright" Version="1.61.0" />
<PackageReference Include="Scriban" Version="5.12.1" />
<PackageReference Include="PDFsharp" Version="6.2.4" />
```

`1.61.0` is chosen for two verified reasons: it already matches
`tests/Pegasus.IntegrationTests/packages.lock.json`'s Playwright, so the CI
browser cache key and the existing `playwright.ps1 install chromium` step keep
working; and the renderer's own comment records that 1.49.x hangs launching
the headless shell on Windows (playwright#34306), which matters because CI and
the supported development platform are Windows.

`src/Pegasus.Infrastructure/packages.lock.json` must be regenerated in the
same change set — `.github/actions/dotnet-build` restores `--locked-mode` and
`scripts/Build-ReleaseArtifacts.ps1` runs two more `--locked-mode` restores.

Scriban's `NU1901`–`NU1904` advisories are suppressed in the workspace's own
props. **They cannot be suppressed that way in Pegasus**: the root
`Directory.Build.props` sets `TreatWarningsAsErrors=true` for all seven
projects and adds no `NoWarn`. A blanket root-level suppression is not
acceptable — it would silence advisories for the eight existing adapter
package families too. The suppression must be scoped to the Scriban
`PackageReference` item via `<NoWarn>NU1901;NU1902;NU1903;NU1904</NoWarn>`
metadata, with the workspace ADR-0010 rationale restated in the new ADR. If
item-scoped suppression proves insufficient, the fallback is a project-scoped
`NoWarn` on Infrastructure only, never on the root props.

Registration in `DependencyInjection.cs`, following `EvaMappingAcceptance`:

```csharp
services.AddSingleton(provider =>
    reportWordingAcceptanceFactory?.Invoke(provider) ?? ReportWordingAcceptance.Unaccepted);
services.TryAddSingleton<IReportRenderer, UnavailableReportRenderer>();
services.TryAddSingleton<IReportPreviewComposer, HtmlReportPreviewComposer>();
```

`AddPegasusInfrastructure` gains one optional parameter,
`Func<IServiceProvider, ReportWordingAcceptance>?
reportWordingAcceptanceFactory = null`, mirroring `evaMappingAcceptanceFactory`.
A separate opt-in extension `AddChromiumReportRendering(...)` registers
`ChromiumReportRenderer` and is called by no composition root at Stage 1.
`TryAddSingleton` means the fail-closed implementation loses to an explicit
registration and wins everywhere else.

Note the preview composer is registered unconditionally: it needs no browser,
so there is no provisioning risk and no reason to gate it behind the Chromium
opt-in. It still has no caller at Stage 1.

## Architecture-test edits

File: `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs`.

### Assertions that must change

| Location | Change | Why |
| --- | --- | --- |
| `:19-36` `ForbiddenCoreDependencyPrefixes` | **Add** `"Microsoft.Playwright"`, `"Scriban"`, `"PdfSharp"`, `"PDFsharp"` | Core must be provably unable to take a rendering package. The guard compares assembly names at `:41-43` and `Include` attributes at `:380-391`, so both spellings matter |
| `:46-70` guard fixture theory | **Add** `("Microsoft.Playwright", true)`, `("Scriban", true)`, `("PdfSharp", true)`, `("Scribanish", false)`, `("PdfSharpener", false)`, `("Microsoft.PlaywrightExtras", false)` | The guard's own fixture coverage is what makes the prefix rule trustworthy |
| `:123-149` `ApplicationSolutionExcludesSourceWorkspaces` | **No change** | The seven ordinal paths are unchanged and the `workspaces/` clause becomes vacuously true once the directory is deleted |
| `:151-169` `ApplicationProjectsDoNotReferenceSourceWorkspaces` | **No change** | Nothing ever references `workspaces/` |
| `:106-121` `ProjectReferencesFollowTheModularMonolithDirection` | **No change** | No project graph edge is added |
| `:189-217` workbook/script exclusion | **No change required**, but verify: renderer assets are `.scriban`, `.css`, `.png`, none of which match the `.xlsx`/`.py`/`.ps1` bans | Cheapest proof the asset move did not smuggle a workbook or script into a runtime project |

### New assertions to add

1. `ReportRenderBoundaryHasOnlyInternalInfrastructureImplementations` —
   modelled on `:219-230`: assert `typeof(IReportRenderer).Assembly ==
   typeof(CoreAssembly).Assembly`; enumerate non-abstract implementations in
   the Infrastructure assembly; assert the ordinal name set is exactly
   `["ChromiumReportRenderer", "UnavailableReportRenderer"]`; assert
   `Assert.False(type.IsPublic)` for both. Repeat for `IReportPreviewComposer`
   with the single expected name `HtmlReportPreviewComposer`.
2. `ReportRenderingPackagesStayOutOfCoreWebAndWorker` — load all four runtime
   csprojs and assert `Microsoft.Playwright`, `Scriban` and `PDFsharp` appear
   as a `PackageReference` in `Pegasus.Infrastructure.csproj` **and nowhere
   else**. Stronger than the Core-only guard: it stops a later change adding
   Playwright to Web "just for the browser install".
3. `IssuedReportArtifactIdentityIsComplete` — assert
   `RenderedReportArtifact`'s property set contains, by name,
   `ArtifactSchemaVersion`, `Template`, `PayloadSchemaVersion`,
   `PayloadSha256`, `ArtifactSha256`, `IssueVersion`, `IssueKind`,
   `IssuedAtUtc`, `RendererKey`, `RendererVersion`. A reflection assertion on
   a record's shape is unusual, but "immutable issued artifact identity and
   hash" is exactly the kind of requirement a future refactor silently drops.
4. `ReportRenderingIsUnavailableUntilWordingIsAccepted` — construct
   `AddPegasusInfrastructure` with no wording factory and assert the resolved
   `IReportRenderer` is `UnavailableReportRenderer`, and that
   `ReportWordingAcceptance.Unaccepted` is the registered default.
5. `ReportTemplateAssetsAreEnumerableAndHashed` — assert the Infrastructure
   assembly's manifest resource names contain exactly the expected nine
   `Pegasus.Infrastructure.Reports.Assets.*` names, and that each embedded
   signature resource is in the allowlist. This is the
   `docs/requirements.md:949` provenance-sensitive-asset guard.
6. `ReportPreviewComposerNeverTakesABrowserDependency` — assert the preview
   composer type's transitive constructor dependencies do not include the
   Chromium engine type. The whole value of retaining the preview is that it
   is browser-free; nothing else enforces that.

### Test edits outside the architecture project

- `.github/workflows/ci.yml`: extend `$buildPattern` with
  `|^design/assets/report-renderer/|^design/brand/` so an embedded-asset
  change triggers the build/test lanes.
- `.github/workflows/workspaces.yml`: delete the "Validate report-renderer
  workspace" step. Leave the workflow otherwise intact for the two remaining
  workspaces.
- Workspace tests worth carrying forward: `AdvertEvidencePackCompositionTests`,
  `LenientStringConverterTests`, `CoreTests`, `IntegrationTests`,
  `PreviewAndStarterTests`' five `PreviewComposerTests` (retained with the
  composer), and `FakePdfEngine.cs`, which is the mechanism the verification
  plan depends on. `JsonPathTests` dies with `JsonPath.cs`. They land in
  `tests/Pegasus.Core.Tests` (contract/schema, no packages) and
  `tests/Pegasus.IntegrationTests` (engine tests, which already has Playwright
  and a browser CI lane).

## Sequencing and staging

The hardest constraint, confronted directly: `docs/requirements.md:53-56`
sequences *"accepted `CASE-31`, `ENG-01`, and `ENG-02` data/workflow precede
`EXT-08` and `RPT-01`–`RPT-05` rendering"*. `docs/capabilities.md:248, 263-267`
allocates `EXT-08` and `RPT-01`–`RPT-05` to `Later / 1.1.0`. The current
release is `0.1.0-alpha.1` and `NOW.md` puts the active path on the QDOS
cutover to EVA handoff, noting *"EVA keeps engineering and reports"*. CASE-31,
ENG-01 and ENG-02 are not built. Report wording is an open decision. `EXT-08`'s
own note reads *"Imported renderer source is not activation."*

**Therefore this integration cannot activate a report capability now, and the
plan must not claim it does.**

### Stage 1 — relocate source and land the contract (this task's scope)

Deletes `workspaces/report-renderer/`; adds `src/Pegasus.Core/Reports/` (both
ports, records, schema, versioning rules, wording gate); adds
`src/Pegasus.Infrastructure/Reports/` with the relocated engine and preview
composer as internal adapters and embedded assets; adds three packages and
regenerates the Infrastructure lock file; registers
`UnavailableReportRenderer`, `HtmlReportPreviewComposer` and
`ReportWordingAcceptance.Unaccepted`; edits the architecture tests and CI
patterns; rewrites `design/README.md`'s renderer boundary table and the
`workspaces/README.md` register row; files the ADR.

**Advances:** nothing in the capability register. It is a structural change.

**Does NOT advance:** `EXT-08`, `RPT-01`–`RPT-05`, `MAIL-17`, or any `MI-*`
capability. No report is rendered, no caller exists, no operator sees anything,
no database column is added, no migration is written. The workspace activation
conditions are **partially** met at the end of Stage 1: an accepted Core render
contract (yes) and a licence-evidence carry-over (yes); representative parity,
security evidence, a real caller, rollback/recovery and operator acceptance
remain **unmet**.

The honest one-line status after Stage 1: *the renderer's source now lives in
the product boundary behind a Core-owned contract; the report capability is not
activated and no report can be produced.*

### Stage 2 — the caller (blocked)

Requires, in order: accepted CASE-31/ENG-01/ENG-02 data and workflow; an
accepted wording set so `ReportWordingAcceptance` can be populated; a Core
`ReportPayloadPolicy` and figure-computation policy; a persisted report-issue
table and migration; the Web caller and its authorised-review action;
`AddChromiumReportRendering` invoked from a composition root; and the
browser-provisioning decision. A staff-facing preview surface, if wanted,
enters here through the full design route with its own allocated capability ID.
**Advances:** `RPT-01` and `EXT-08` in part.

### Stage 3 — acceptance

Deployed browser provisioning proved, cold-start and memory measured under
ADR-0015's sizing, determinism proved on the deployed image, recovery and
rollback proved, operator acceptance recorded. **Advances:** `EXT-08` and
`RPT-01` to accepted; `RPT-02`–`RPT-05` remain separate wording-and-data work.

## Determinism plan

`RPT-01` says "deterministic". Today the renderer is not, and the defect is not
only `Format.Today()`.

| Source of non-determinism | Fix | Where |
| --- | --- | --- |
| `Format.Today()` uses `DateTime.Now` (`Format.cs:106`) | Delete. Dates come from `RenderReportRequest.IssuedAtUtc`, derived from the injected `TimeProvider` already registered at `DependencyInjection.cs:51` | Infra `Format.cs`, Core caller |
| Ambient culture | `Format` pins `en-GB` correctly, but the CLI sets `InvariantGlobalization=false` and the container sets `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false`. Replace the private field with `ReportArtifactSchema.RenderCulture` and assert the resolved culture at render entry, failing with `TechnicalFailure` if ICU is unavailable | Infra `Format.cs` |
| Chromium embeds a PDF `CreationDate`/`ModDate` | Post-process with PDFsharp — already a dependency — rewriting `/CreationDate`, `/ModDate`, `/Producer` and `/Creator` to `DeterministicDocumentTimestamp` and fixed strings before the artifact is hashed. Same trick `EfDocumentCustodyStore.cs:526-568` uses on zip entries | Infra, new `DeterministicPdfMetadata` step |
| PDF `/ID` array | Set a deterministic document `/ID` derived from `PayloadSha256 + TemplateSha256 + IssueVersion`, not from a random seed | Same step |
| Font substitution | `report.css:18` uses `Arial, Helvetica, sans-serif`. Windows resolves Arial; Linux resolves Liberation Sans (metric-compatible) only if `fonts-liberation` is installed. **The deployed Web base image has neither.** Determinism is therefore *conditional on the image*. Risk R2 | Stage 3 |
| Chromium version | The Core artifact records `RendererKey` + `RendererVersion` so a byte difference across Chromium versions is attributable rather than mysterious. **Determinism is asserted within one pinned browser build, never across builds** | Core contract |
| Density auto-fit | Auto-fit re-renders at Normal → Compact → UltraCompact and the chosen density affects the bytes. **Recommendation: issued reports render at a fixed density chosen by policy; auto-fit is not part of the issued-artifact contract.** This deletes a whole class of irreproducibility | Core `ReportKind` binding |

**How "deterministic" is actually proved (Stage 2/3):** render the same
`RenderReportRequest` twice in the same process, twice in two fresh processes,
and twice on two machines (Windows development host and the linux-x64 container
image), and assert byte-identical output by SHA-256 for all six artifacts
across all twelve report kinds. Anything weaker — a page count, a visual diff,
a one-machine repeat — is not determinism evidence. Until the Linux image can
supply the fonts, only the same-machine and same-image legs can pass, and that
limitation is stated rather than papered over.

## Risk register

| # | Risk | Severity | Mitigation / decision |
| --- | --- | --- | --- |
| R1 | **The Web release mechanism cannot provision Chromium.** `Build-ReleaseArtifacts.ps1` builds the image with `dotnet publish /t:PublishContainer`; `Pegasus.Web.csproj` sets no `ContainerBaseImage`, so the base is `aspnet:10.0` with no browser. `PublishContainer` cannot run `apt-get` | **High — blocking for Stage 2** | Three routes, all Stage 3 ADR decisions: (i) set `ContainerBaseImage` to a Playwright image, accepting a much larger CVE surface for the whole Web app; (ii) introduce a Dockerfile and abandon SDK container publish, amending ADR-0015's build route; (iii) take option (d) as the fallback. **None is decided by this plan.** Stage 1 registers `UnavailableReportRenderer` precisely so this stays undecided without blocking the move |
| R2 | **Fonts.** Liberation/DejaVu are `apt` packages; the deployed base image has neither, so Arial silently falls back and layout drifts | **High** | Same three routes as R1, plus a font-availability probe at renderer construction that returns `RendererUnavailable` rather than producing a wrong-metric report |
| R3 | **ADR-0015 sizing.** Web runs at 0.5 vCPU / 1 GiB, min 0 / max 1 replica. A Chromium page render peaks well above the remaining headroom | **High** | Amend ADR-0015's sizing as part of Stage 3, with measured evidence. Do not render on a synchronous operator request path under any sizing |
| R4 | **Cold start.** Scale-to-zero plus a first-render browser launch compounds two cold starts | **Medium** | Measure at Stage 3; the render path must be an explicitly asynchronous, progress-reporting staff action, never a page load |
| R5 | **`--no-sandbox`.** `PdfEngine.cs:168` launches with `--no-sandbox` | **Medium** | Container Apps runs the container as an isolation boundary, and the renderer only ever loads first-party HTML composed in-process — it never navigates to a URL. Document that as the accepted rationale; do not remove the flag without proving the container can supply user namespaces |
| R6 | **Worker cannot host rendering.** `azure.yaml` deploys Worker as `host: function`, published as `worker.zip`. A Functions ZIP deployment has no route to install Chromium | **High if Worker is assumed** | Do not assume Worker. Recorded so the question is not reopened without evidence |
| R7 | **Playwright driver in the migration bundle.** `dotnet ef migrations bundle` uses `Pegasus.Infrastructure` as `--project` | **Low** | Verify bundle size and that `--self-contained` still succeeds |
| R8 | **Scriban advisories** become production-assembly advisories under `TreatWarningsAsErrors=true` | **Medium** | Item-scoped `NoWarn`, re-accepted in the new ADR. Operator confirmation required |
| R9 | **Unaccepted report wording compiled into the product.** Relocating the `.scriban` bodies embeds prose that is an open decision | **High** | `ReportWordingAcceptance.Unaccepted` gate plus architecture test 4. The assets ship but cannot be rendered |
| R10 | **Signature images become production assembly content** | **Medium** | Explicit per-file `EmbeddedResource` entries, the existing `BrandAssets` allowlist, and architecture test 5 |
| R11 | **Workspace-local ADRs 0001–0011 are orphaned by deletion** | **Low** | The new ADR names each and states what it supersedes or preserves; the documentation-migration plan owns the file moves |
| R12 | **Silent asset drift.** `design/` is outside the CI build-path pattern | **Medium** | Extend `$buildPattern`; architecture test 5 pins the resource name set |
| R13 | **Preview HTML is an injection surface.** The retained preview path is deliberately validation-free; rendering its output into a staff browser without isolation is an XSS vector | **Medium — Stage 2** | No Web caller at Stage 1. When one is built: sandboxed frame, restrictive CSP, never interpolated into a Razor page. Architecture test 6 keeps the composer browser-free so it cannot quietly become a second render path |

**Which process renders — recommendation.** *Neither, today.* Stage 1
registers the fail-closed adapter in every profile; the real Chromium adapter
is composed only in local `DevelopmentOffline` evidence runs. For production
the recommended target is the **Web container** — the only deployment unit
whose image Pegasus controls (R6 rules out Worker) — with the render executed
off the synchronous request path, under an amended ADR-0015 sizing, and with
the separately deployed service retained as the named fallback if R1/R2/R3
cannot be closed inside the Web image.

## Verification plan

| Check | Tier | Stage | What it proves |
| --- | --- | --- | --- |
| Seven projects build Release with `TreatWarningsAsErrors=true`; `--locked-mode` restore succeeds with the regenerated Infrastructure lock | 1 | 1 | Consistency only |
| `DependencyDirectionTests` full run, including the six new assertions | 1 | 1 | Core takes no rendering package; exactly two internal renderer implementations and one preview implementation; artifact identity is complete; the wording gate defaults closed; the embedded asset set is exact; the preview stays browser-free |
| `workspaces/report-renderer/` absent from `git ls-files`; `Pegasus.slnx` unchanged; no `ProjectReference` contains `workspaces` | 1 | 1 | The workspace is retired, not hidden |
| `ReportArtifactSchema` file-name/slug/hash-format tests; `ReportIssueVersioning` monotonicity, correction-does-not-overwrite, replay-of-identical-hash | 2 | 1 | Issued-artifact identity and correction rules, positive and contradictory cases |
| `ReportWordingAcceptance` gate: unaccepted → `RendererUnavailable` with no artifact; wrong key or wrong version → same | 2 | 1 | The open wording decision cannot be bypassed |
| Preview composer: composes from an incomplete payload, never allocates an issue identity, never returns an artifact, never launches a browser | 2 | 1 | The retained preview seam behaves as a preview and not as a second render path |
| Renderer against `FakePdfEngine`: HTML composition per template, validation errors, warnings, attachment resolution through a fake `IDocumentContentStore` | 3 | 1 | Deterministic adapter failures and stable contract codes without a browser |
| Real Chromium: twelve kinds render; six-way byte-identical determinism matrix; cancellation honoured mid-render; browser-crash recovery; PDF metadata pinned | 3 + 12 | 2 | Genuine engine behaviour |
| Attachment safety: a request naming a document version the case does not own is refused; no code path accepts a filesystem path | 9 | 1 structural, 2 behavioural | The `JsonPath`/temp-file class of issue is designed out |
| Report-issue persistence, migration, rollback, action-history atomicity | 4 | 2 | — |
| Authorised staff render action reaches Core; authorization failure; validation failure; action-history actor | 5 | 2 | — |
| Operator review of status, validation and failure presentation without implying delivery | 7 | 2 | — |
| Memory, cold start, concurrent renders under ADR-0015 sizing | 10 | 3 | — |
| Deployed image browser and font provisioning; digest-pinned release; rollback | 11 | 3 | — |

**Honestly unproved at the end of Stage 1:** everything in tiers 4, 5, 6, 7, 8,
10, 11 and 12 for reporting; every determinism leg that requires a Linux image;
parity against the workspace's own visual-regression evidence; licence
re-verification for PDFsharp and ModelContextProtocol, which the workspace
notice records as *"No conclusion stated in the retained notice"*; and every
activation condition in `workspaces/README.md` except the Core contract and the
licence carry-over. **Stage 1 produces tier 1 and tier 2/3 evidence only.**

## Draft ADR

To be filed at the next available number (the last accepted is ADR-0020).

---

### ADR-NNNN: Integrate the report renderer behind a Core-owned render contract

- Date: TBC
- Status: proposed
- Owners: Collision Engineers product owner and Pegasus development team
- Relation: supersedes ADR-0009's `report-renderer/` workspace clause only;
  supersedes the CollisionRenderer workspace ADRs listed below; takes no
  ADR-0015 hosting decision

#### Context

ADR-0009 admitted `workspaces/report-renderer/` as durable source with no
Pegasus caller, and recorded that *"Future rendering consumes a Core-owned
render contract; report policy does not move into Infrastructure or the
renderer."* The workspace has since been maintained in place: it targets
`net8.0` under workspace-local build settings that disable
`TreatWarningsAsErrors` and suppress the Scriban advisories, it carries its own
solution, lock, CI lane, Dockerfile, API, CLI, MCP and WinUI hosts, and it links
its templates, stylesheet, logo and signatures out of the top-level `design/`
tree. Keeping it costs a parallel build, a parallel dependency and licence
surface, a parallel ADR store, and a second place where report-shaped decisions
can be made.

Reading the source establishes that the split is clean. Of twenty files in
`CollisionRenderer.Core`, exactly three touch a package Core may not take. A
further four files — 1,305 lines of authoring catalog, starter composer,
placeholder scanner and JSON-path mutation — exist only to serve the CLI, the
API and the WinUI desktop app, none of which Pegasus wants. The HTML preview
composer is retained by operator decision of 2026-08-03 as a separate,
browser-free seam.

Pegasus already carries this class of adapter inside `Pegasus.Infrastructure`:
PdfPig, DocumentFormat.OpenXml, MimeKit, SkiaSharp, and — since ADR-0019 — an
in-process ONNX runtime with vendored, hash-pinned model weights.

Three constraints bound what this decision may claim. `EXT-08` and
`RPT-01`–`RPT-05` are allocated to `1.1.0`; `docs/requirements.md` sequences
accepted `CASE-31`, `ENG-01` and `ENG-02` ahead of them, and none is built.
Report wording is an open decision. And the deployed Web image is produced by
`dotnet publish /t:PublishContainer` onto the default ASP.NET base image, which
contains neither Chromium nor Liberation fonts and cannot install them.

#### Decision

1. The renderer is **absorbed into the existing four production projects**. No
   fifth production project, no third deployment unit, and no change to
   `Pegasus.slnx`.
2. `Pegasus.Core` gains `src/Pegasus.Core/Reports/`, containing the
   `IReportRenderer` and `IReportPreviewComposer` ports and their records,
   `ReportArtifactSchema`, `ReportIssueVersioning`, and
   `ReportWordingAcceptance`. `Pegasus.Core.csproj` remains free of every
   `PackageReference` and `ProjectReference`.
3. Core owns report policy in full: the closed `ReportKind` set, the
   kind→template binding and version, payload schema version, computed figures,
   issued-artifact identity and hash, and the rule that a correction or addendum
   creates a new issue and never replaces an earlier artifact. The renderer
   computes no figure, decides no outcome, writes no case state, and reads no
   file the request did not name by document-version identity.
4. `Pegasus.Infrastructure` gains `src/Pegasus.Infrastructure/Reports/`, holding
   the relocated engine and the retained HTML preview composer entirely as
   `internal` types, and the templates, stylesheet, logo and three signatures as
   explicitly named `EmbeddedResource` entries. `Microsoft.Playwright 1.61.0`,
   `Scriban 5.12.1` and `PDFsharp 6.2.4` are added to `Pegasus.Infrastructure`
   and to nowhere else. Scriban's advisories are accepted item-scoped, on the
   rationale that templates are first-party embedded artifacts never authored at
   runtime and all payload data is HTML-encoded and passed as values.
5. `AddPegasusInfrastructure` registers `ReportWordingAcceptance.Unaccepted` by
   default, `UnavailableReportRenderer` and `HtmlReportPreviewComposer` via
   `TryAddSingleton`. The Chromium adapter is composed only by an explicit
   opt-in extension, which no composition root calls under this decision. **This
   decision activates no report capability.**
6. `CollisionRenderer.Cli`, `CollisionRenderer.Api` and `CollisionRenderer.Mcp`
   are deleted, not ported. The API's bearer-token scheme is not adopted;
   ADR-0004 and ADR-0011 continue to own authentication and the single
   Automation Actor MCP ingress. `CollisionRenderer.Gui` is removed by the
   separately owned desktop-removal decision; the preview composer it hosted is
   retained per decision 2 above.
7. Attachments are referenced by document-version identity and resolved through
   `IDocumentContentStore`. No renderer code path accepts a caller-supplied
   filesystem path.
8. `Format.Today()` and its use of `DateTime.Now` are deleted. Issue time comes
   from the request, derived from the injected `TimeProvider`. PDF creation,
   modification, producer, creator and document-ID metadata are pinned. Issued
   reports render at a policy-fixed density; density auto-fit is not used for an
   issued artifact.
9. The activation route is staged. Stage 1 is this relocation, with no caller
   and no capability claim. Stage 2 adds the caller and is blocked on accepted
   CASE-31/ENG-01/ENG-02 data and on an accepted wording set. Stage 3 is
   deployed proof and operator acceptance. Stage 1 advances **no** capability
   identifier.
10. Where rendering executes in production is **not decided here**. The
    recommended target is the Web container; a separately deployed render
    service behind this same unchanged port is the named fallback. Either route
    requires its own decision amending ADR-0015's image, base and sizing
    clauses.
11. A staff-facing report preview surface is **not** created by this decision.
    The preview port and its implementation exist with no caller; any staff
    surface requires an allocated capability identifier and the full design
    route in `design/README.md`.

#### Consequences

- `workspaces/report-renderer/` is deleted, its row leaves the
  `workspaces/README.md` register, and its validation step leaves
  `.github/workflows/workspaces.yml`. The `document-extraction/` and
  `ai-centre/` workspaces are unaffected and ADR-0009's workspace boundary
  remains in force for them.
- This supersedes ADR-0009 **only** in that `report-renderer/` is no longer an
  independently built, non-caller source workspace. ADR-0009's rule that
  `Pegasus.slnx` must not reference a workspace, its four-production-project
  boundary, its one-Core ownership rule, and its requirement that any
  integration define the Core contract before a caller exists all remain — and
  are satisfied, not waived.
- The CollisionRenderer workspace ADRs are superseded as follows. ADR-0001
  (headless Chromium), ADR-0004 (typed model plus Scriban body), ADR-0005
  (reuse of the brand CSS design system), ADR-0006 (Chromium header/footer
  paged-media furniture) and ADR-0010 (constrained Scriban advisory acceptance)
  are **carried forward** and cease to exist as separate records. ADR-0002
  (shared core, thin clients), ADR-0003 (unified .NET 8 stack), ADR-0008
  (ASP.NET Core API and Docker cloud portability) and ADR-0011 (multi-token
  SHA-256 API authentication) are **superseded and not carried forward**.
  ADR-0007 (density auto-fit) is **retained as engine behaviour but is not used
  for issued artifacts**. ADR-0009 (reference-material handling) is superseded
  by the repository's own corpus and secret-material rules.
- `Pegasus.Infrastructure`'s published output grows by the Playwright node
  driver, and the EF migration bundle built from that project grows with it.
- `design/README.md`'s renderer boundary table is rewritten: the templates and
  stylesheet row names `Pegasus.Infrastructure` instead of the workspace path,
  the temporary-GUI row is deleted, and the signature row continues to state
  that signatures are embedded document evidence and never Web decorative
  imagery.
- `.github/workflows/ci.yml`'s build-path pattern gains
  `design/assets/report-renderer/` and `design/brand/`, so an embedded-asset
  change can no longer reach the product without running a build lane.
- The licence conclusions retained in the workspace notice move into the
  Pegasus notice surface. Two of them record no conclusion and require
  verification before Stage 3.
- This decision proves architecture only. No report is rendered, no caller
  exists, no operator sees a report, and no deployment, live verification or
  operator acceptance is implied.

---

## Non-goals

- Activating `EXT-08` or any `RPT-*` capability.
- Choosing report wording, qualifications or the statement of truth.
- Defining the CASE-23 post-report query/dispute lifecycle or the
  correction/reopen state machine.
- Adding a report table, migration, Web page, MCP tool or Worker function.
- Creating any staff-facing preview surface.
- Deciding the deployed hosting, base image or sizing for rendering.
- Removing the WinUI GUI, uplifting the workspace to .NET 10, consolidating
  MCP, intaking `docs/reference/rendererref1`, or migrating the workspace ADR
  files — each is a parallel plan.
- Preserving the renderer's authoring, starter-payload, CLI, HTTP API or
  standalone MCP surfaces.

## Stop conditions

1. The operator declines to re-accept the Scriban advisories for a production
   assembly. Without that, Infrastructure cannot compile under
   `TreatWarningsAsErrors=true` and the seam changes.
2. The operator declines to embed unaccepted report wording and signature
   images in the production assembly even behind the closed wording gate. The
   fallback is to relocate code in Stage 1 and defer the asset move to Stage 2.
3. `dotnet restore --locked-mode` cannot be satisfied after regenerating the
   Infrastructure lock file, or the three packages drag in a transitive that
   trips an existing forbidden-prefix guard.
4. Any architecture-test edit would need to *weaken* an existing assertion.
   Under this seam none should; if one does, the seam has been mis-chosen.
5. The desktop removal plan has not landed. `CollisionRenderer.Gui` references
   `CollisionRenderer.Core`, so deleting the workspace before the GUI is
   removed breaks that plan's starting state. **Sequence the GUI removal
   first.**

## Open questions for the operator

1. **Scriban advisories.** Are `NU1901`–`NU1904` re-accepted for a production
   assembly, on the same first-party-template rationale the workspace used?
2. **Unaccepted wording in the binary.** Is it acceptable to embed the four
   `.scriban` templates and `report.css` — which contain unaccepted report
   prose — in `Pegasus.Infrastructure` behind a closed activation gate, or must
   the assets wait for Stage 2?
3. **Signature assets.** Are the three engineer signature images acceptable as
   embedded production-assembly content now, given the provenance-sensitive
   classification?
4. **Report kinds.** The workspace ships twelve template identifiers, of which
   eight share one body. Is the twelve-member `ReportKind` set the accepted
   Pegasus set, or does Pegasus need a different set derived from CASE/ENG
   work?
5. **Density.** Confirm that issued reports render at a fixed density and that
   auto-fit is not part of the issued-artifact contract.
6. **Rendering host.** Confirm the Web container as the intended Stage-3
   target, or direct that the separately deployed service fallback be planned
   instead.
7. **Licence verification.** Who verifies the PDFsharp licence, for which the
   retained notice states no conclusion, before Stage 3?
8. **Preview surface allocation.** The preview composer is retained per your
   2026-08-03 decision, but no capability ID allocates a staff-facing preview.
   Should one be allocated in `docs/capabilities.md` now, so the design route
   can start — or does the preview stay a library capability with no surface
   until the Engineer workbench (UI-15) absorbs it?

## Dependencies on parallel plans

| Parallel plan | Relationship | Required order |
| --- | --- | --- |
| Desktop/WinUI GUI removal | `CollisionRenderer.Gui` references `CollisionRenderer.Core`; this plan deletes both. The preview composer the GUI hosted is retained | **GUI removal first**, or both in one change set |
| .NET 8 → .NET 10 uplift | Relocated code compiles under `net10.0` with `TreatWarningsAsErrors=true` regardless of whether the workspace was uplifted first | Independent; uplifting the workspace first is wasted work if this plan lands |
| MCP consolidation onto Pegasus `/mcp` | This plan deletes `CollisionRenderer.Mcp` outright and creates no render tool | Either order; state the deletion so the MCP plan does not port the four render tools |
| `docs/reference/rendererref1` template intake | That work may change `design/assets/report-renderer/templates/**`, which this plan turns into product-embedded resources under a new `LogicalName` scheme | **This plan first**, or the intake must be re-pathed |
| Documentation/ADR migration | Owns moving or retiring workspace ADRs 0001–0011; this plan's ADR states the supersession | This plan's ADR supplies the supersession text |

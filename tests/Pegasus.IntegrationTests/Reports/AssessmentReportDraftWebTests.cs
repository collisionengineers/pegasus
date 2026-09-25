using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pegasus.Core.Actors;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests.Reports;

/// <summary>
/// Proves the report-draft entry point is actually reachable from
/// the web: a complete case renders and returns a PDF, and an incomplete
/// case fails closed with its readiness reasons named instead of throwing.
/// <see cref="IAssessmentReportRenderer"/> is substituted with a fast fake so
/// this suite does not lay out pages or validate rendered PDF appearance.
/// Everything upstream of the renderer (the projection, the readiness gate,
/// the page wiring, authorisation) is exercised for real.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class AssessmentReportDraftWebTests
{
    private static readonly DateTimeOffset ReportFixtureAtUtc =
        new(2026, 8, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PreviewReportDateUsesLondonAtBstMidnight()
    {
        var caseId = Guid.NewGuid();
        var renderer = new FakeRenderer([1, 2, 3, 4]);
        var preview = new GenerateCaseAssessmentReportDraft(new FakeGetAssessmentAccess(true),
            new FakeProjectionSource(ReadyInput(caseId) with { ReportDate = null }),
            new GenerateAssessmentReportDraft(renderer),
            new ReportClock(new DateTimeOffset(2026, 9, 6, 23, 30, 0, TimeSpan.Zero)));

        var result = await preview.ExecuteAsync(caseId,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), CaseReportArtifactKind.AssessmentReport);

        Assert.Equal(GenerateCaseAssessmentReportDraftOutcome.Generated, result.Outcome);
        Assert.Equal(new DateOnly(2026, 9, 7), renderer.Snapshot!.ReportDate);
    }

    [Fact]
    public async Task UserGeneratesACompleteCaseReportDraftAndReceivesThePdf()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        var pdfBytes = new byte[] { 1, 2, 3, 4 };
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer(pdfBytes));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");
        Assert.DoesNotContain(AssessmentReportProjection.RepairCostRequirement, html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=GenerateReportDraft&section=report",
            Form(AntiforgeryValue(html), ("id", caseId.ToString("D")), ("operationKey", NewOperationKey())));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(pdfBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UserCanPreviewTheReportDraftThroughTheCaseHandler()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        var pdfBytes = new byte[] { 4, 3, 2, 1 };
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer(pdfBytes));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add("X-Test-Roles", "User");

        using var response = await client.GetAsync(
            $"/Cases/{caseId:D}?handler=PreviewReportDraft&section=report");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(pdfBytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task EnhancedPreviewReturnsATypedRefusalWhenTheReportIsNotReady()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId) with { CurrentEstimate = null }),
            new FakeRenderer([1]));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Cases/{caseId:D}?handler=PreviewReportDraft&section=report");
        request.Headers.Add("X-Pegasus-Document-Preview", "1");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var detail = Assert.IsType<string>(problem.RootElement.GetProperty("detail").GetString());
        Assert.Contains(
            AssessmentReportProjection.RepairCostRequirement,
            detail,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task IncompleteCaseFailsClosedNamingWhatIsMissingInsteadOfThrowing()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId) with { CurrentEstimate = null }),
            new FakeRenderer([1]));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");
        Assert.Contains(AssessmentReportProjection.RepairCostRequirement, html, StringComparison.Ordinal);
        // FRD-13: the readiness list links the blocker to the section that clears it.
        AssertBlockerLinks(
            BlockerRow(BlockerList(WebUtility.HtmlDecode(html)), AssessmentReportProjection.RepairCostRequirement),
            caseId,
            "estimate");
        // FRD-11: the control stays, disabled with its condition — no
        // submittable Generate form and no Preview link are offered.
        Assert.DoesNotContain("handler=\"GenerateReportDraft\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Preview report draft", html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=GenerateReportDraft&section=report",
            Form(AntiforgeryValue(html), ("id", caseId.ToString("D")), ("operationKey", NewOperationKey())));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/Cases/{caseId:D}?section=estimate", response.Headers.Location?.OriginalString);

        var afterHtml = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");
        Assert.Contains(AssessmentReportProjection.RepairCostRequirement, afterHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaseOutsideTheCurrentExportedReviewCycleCannotGenerateDirectly()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId),
            FullAssessmentProjection(caseId),
            new FakeProjectionSource(ReadyInput(caseId)),
            new FakeRenderer([1]),
            canOpen: false);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");
        // D11: the workspace has not opened, so the control stays disabled
        // with its condition instead of offering a form that 404s.
        Assert.DoesNotContain("handler=\"GenerateReportDraft\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Preview report draft", html, StringComparison.Ordinal);

        using var response = await client.PostAsync(
            $"/Cases/{caseId:D}?handler=GenerateReportDraft&section=report",
            Form(AntiforgeryValue(html), ("id", caseId.ToString("D")), ("operationKey", NewOperationKey())));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CaseGetUsesMetadataReadinessWithoutOpeningPreviewOrRendering(bool eligibleSignatory)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        var source = new FakeProjectionSource(ReadyInput(caseId));
        if (!eligibleSignatory)
        {
            source.Readiness = source.Readiness with { EligibleSignOffEngineers = [] };
        }
        var renderer = new FakeRenderer([1]);
        using var factory = Compose(baseFactory, new FakeGetCase(caseId),
            FullAssessmentProjection(caseId), source, renderer,
            failIfReportServicesResolved: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report");
        var readiness = CaseReportReadiness.Evaluate(source.Readiness);
        Assert.Equal(eligibleSignatory, readiness.IsReady);
        Assert.Equal(1, source.MetadataReads);
        Assert.Equal(0, source.PreviewReads);
        Assert.Null(renderer.Snapshot);
        Assert.DoesNotContain(CaseReportReadiness.CurrentEstimateRequirement, html, StringComparison.Ordinal);
        foreach (var reason in readiness.Reasons)
        {
            Assert.Contains(WebUtility.HtmlEncode(reason.Requirement), html, StringComparison.Ordinal);
            if (CaseWorkspaceLabels.Report.BlockerSection(reason) is { } key)
            {
                Assert.Contains($"data-report-blocker=\"{key}\"", html, StringComparison.Ordinal);
            }
        }
        if (eligibleSignatory)
        {
            Assert.Contains("Ed Mawdsley", html, StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("Preview report draft", html, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// FRD-13 (issue #834): every report blocker is a row that names what is
    /// missing and links to the Case section that clears it, and the Next
    /// action links the first blocker to its own section rather than to
    /// Valuation.
    /// </summary>
    [Fact]
    public async Task EachReportBlockerLinksToTheSectionThatClearsIt()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        var source = new FakeProjectionSource(ReadyInput(caseId));
        var full = FullAssessmentProjection(caseId);
        source.Readiness = source.Readiness with
        {
            // A missing Vehicle finding, basis retail and outcome, and no
            // sign-off, repair spec, adoption or images.
            Assessment = full with
            {
                Fields =
                [
                    .. full.Fields.Where(field => field.Path is not (AssessmentVocabulary.VehicleCondition
                        or AssessmentVocabulary.ValueRetail or AssessmentVocabulary.Outcome)),
                ],
            },
            EligibleSignOffEngineers = [],
            CurrentEstimate = null,
            AppliedValuation = null,
            Preparations = [],
        };
        var readiness = CaseReportReadiness.Evaluate(source.Readiness);
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Pre-incident condition"] = "vehicle",
            ["Retail value"] = "valuation",
            ["Assessment outcome"] = "settlement",
            [CaseReportReadiness.SignatoryRequirement] = "overview",
            [CaseReportReadiness.CurrentEstimateRequirement] = "estimate",
            [CaseReportReadiness.EngineerValueRequirement] = "valuation",
            [CaseReportReadiness.CloseUpImageRequirement] = "files",
            [CaseReportReadiness.OverviewImageRequirement] = "files",
        };
        Assert.Equal(
            expected.Keys.Order(StringComparer.Ordinal),
            readiness.Reasons.Select(reason => reason.Requirement).Order(StringComparer.Ordinal));
        Assert.Equal("Pre-incident condition", readiness.Reasons[0].Requirement);
        using var factory = Compose(
            baseFactory, new FakeGetCase(caseId), full, source, new FakeRenderer([1]));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = WebUtility.HtmlDecode(await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report"));

        var list = BlockerList(html);
        // A list that stays, not a warning the operator can dismiss.
        Assert.DoesNotContain("data-dismiss", list, StringComparison.Ordinal);
        Assert.DoesNotContain("notice--warning", list, StringComparison.Ordinal);
        foreach (var reason in readiness.Reasons)
        {
            var key = expected[reason.Requirement];
            Assert.Equal(key, CaseWorkspaceLabels.Report.BlockerSection(reason));
            AssertBlockerLinks(BlockerRow(list, reason.Requirement), caseId, key);
        }

        var nextAction = NextActionRegex().Match(html);
        Assert.True(nextAction.Success, "The Case aside must state its Next action.");
        var panel = nextAction.Value;
        Assert.Equal(
            $"{readiness.Reasons[0].Requirement} · {readiness.Reasons.Count - 1} more",
            NextLabelRegex().Match(panel).Groups["label"].Value);
        var link = SectionJumpRegex().Match(panel);
        Assert.True(link.Success, "The Next action must link to a section.");
        Assert.Equal("vehicle", link.Groups["key"].Value);
        Assert.Contains($"href=\"/Cases/{caseId:D}?section=vehicle#section-vehicle\"", link.Value, StringComparison.Ordinal);
        Assert.Equal("Vehicle", link.Groups["label"].Value);
        Assert.DoesNotContain("section=valuation", panel, StringComparison.Ordinal);
    }

    /// <summary>
    /// Issue #834: a fact the report prints but the Case lacks is a named
    /// blocker, not a failure. The Case page loads (it answered 503 while the
    /// report wording projected an unready Case) and links the blocker to the
    /// section that records the fact, and the enhanced preview refuses naming
    /// it without rendering.
    /// </summary>
    [Fact]
    public async Task TheCasePageLoadsAndNamesAMissingPrintedFact()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        var ready = ReadyInput(caseId);
        var input = ready with
        {
            Assessment = ready.Assessment with
            {
                CaseOwned = ready.Assessment.CaseOwned with { ClaimantName = null }
            }
        };
        var renderer = new FakeRenderer([1]);
        using var factory = Compose(
            baseFactory, new FakeGetCase(caseId), input.Assessment, new FakeProjectionSource(input), renderer);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = WebUtility.HtmlDecode(await GetHtmlAsync(client, $"/Cases/{caseId:D}"));
        AssertBlockerLinks(BlockerRow(BlockerList(html), "Claimant name"), caseId, "claim");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/Cases/{caseId:D}?handler=PreviewReportDraft&section=report");
        request.Headers.Add("X-Pegasus-Document-Preview", "1");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var detail = Assert.IsType<string>(problem.RootElement.GetProperty("detail").GetString());
        Assert.Contains("Claimant name", detail, StringComparison.Ordinal);
        Assert.Null(renderer.Snapshot);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WorkspaceSavedReportAndSettlementFieldsReachTheActualPreview(bool overrideDate)
    {
        await using var harness = await CaseDataCompletenessPersistenceTests.CaseDataHarness.CreateAsync();
        var engineer = ActionActor.Staff(Guid.Parse(harness.StaffActor.SubjectId), [StaffRole.Engineer]);
        var existing = ReadyInput(harness.CaseId);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE CaseWorkflows SET State = {nameof(CaseLifecycleState.ReportPreparation)} WHERE CaseId = {harness.CaseId}");
            // Arrange already accepted assessment inputs; this field-edit test must
            // not pretend that a workspace save can adopt an Engineer's Value.
            context.CaseAssessmentFields.AddRange(existing.Assessment.Fields.Select(field => new CaseAssessmentFieldEntity
            {
                WorkId = harness.CaseId,
                FieldPath = field.Path,
                Value = field.Value,
                RecordedByKind = nameof(ActorKind.Staff),
                RecordedBy = engineer.SubjectId,
                RecordedAtUtc = ReportFixtureAtUtc
            }));
            // The report's Assessed date is the Case's Inspection date: the
            // harness's intake suggests one, and the fixture confirms its own.
            context.Set<CaseDataFieldEntity>().RemoveRange(await context.Set<CaseDataFieldEntity>()
                .Where(field => field.WorkId == harness.CaseId
                    && field.FieldName == CaseDataFieldNames.InspectionDate
                    && field.ValueKind == CaseDataCodes.Confirmed)
                .ToArrayAsync());
            context.Set<CaseDataFieldEntity>().Add(new CaseDataFieldEntity
            {
                WorkId = harness.CaseId,
                FieldName = CaseDataFieldNames.InspectionDate,
                ValueKind = CaseDataCodes.Confirmed,
                ValueType = CaseDataCodes.Date,
                Value = "2026-08-03",
                SourceKind = CaseDataCodes.StaffCorrection,
                SourceIdentity = engineer.SubjectId,
                SourceLabel = "Report fixture",
                PolicyKey = CaseDataPolicy.EditPolicyKey,
                PolicyVersion = CaseDataPolicy.EditPolicyVersion,
                ConfirmedByActor = engineer.SubjectId,
                ConfirmedAtUtc = ReportFixtureAtUtc
            });
            await context.SaveChangesAsync();
        }
        var initial = await harness.GetRequiredDataAsync();
        var lease = await harness.AcquireLeaseAsync(initial.Version, engineer, "lease-preview-fields");
        var saved = await harness.WorkspaceStore.SaveAsync(new(
            harness.CaseId, initial.Version, engineer, "save-preview-fields", "Recorded the Case workspace", lease.Token)
        {
            // The minimal intake harness has no incident date. Record the
            // existing report fixture's through the real writer.
            Overview = new(initial.Claimant.Name.Current?.Value,
                initial.Claimant.ContactNumber.Current?.Value, initial.Claimant.Address.Current?.Value,
                initial.Claim.Number.Current?.Value, initial.Contact.Name.Current?.Value,
                initial.Contact.EmailAddress.Current?.Value, initial.Contact.PhoneNumber.Current?.Value,
                existing.Assessment.CaseOwned.IncidentDate, initial.Accident.Circumstances.Current?.Value,
                initial.Instruction.VatStatus.Current?.Value,
                initial.Inspection.RepairerAddress?.Current?.Value, initial.Workspace?.ClaimSource),
            Vehicle = new(initial.Vehicle.Registration.Current?.Value,
                initial.Vehicle.Make.Current?.Value, initial.Vehicle.Model.Current?.Value,
                null, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    [AssessmentVocabulary.HistoryCheck] = "History clear"
                },
                // The year is a Case fact now, recorded through the same writer.
                Year: "2012"),
            Settlement = new(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [AssessmentVocabulary.SettlementExcess] = "250.00",
                [AssessmentVocabulary.SettlementBetterment] = "0.00",
                [AssessmentVocabulary.SettlementClaimantVatRegistered] = "false"
            }),
            Report = new(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [AssessmentVocabulary.EngineersComments] = "Scuffed",
                [AssessmentVocabulary.AgreedFee] = "120.00",
                [AssessmentVocabulary.FeeDescriptionLines] = "Assessment report",
                [AssessmentVocabulary.ReportDiscloseGuideSource] = "true",
                [AssessmentVocabulary.ReportValuationCommentary] = "false",
                [AssessmentVocabulary.ReportIncludeUnrelatedDamage] = "false",
                [AssessmentVocabulary.ReportDateOverride] = overrideDate ? "true" : "false"
            }, Guid.Parse(engineer.SubjectId), new DateOnly(2026, 8, 19))
        }, CancellationToken.None);
        var assessmentStore = new EfCaseAssessmentStore(harness.Factory, harness.TimeProvider,
            new EfRepairSpecificationStore(harness.Factory, harness.TimeProvider));
        var persisted = await assessmentStore.GetAsync(harness.CaseId, CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Equal(saved.Version, persisted.CaseVersion);
        var input = existing with
        {
            Assessment = persisted,
            OurReference = saved.Data.Identity.Reference,
            ReportDate = null
        };
        var source = new FakeProjectionSource(input);
        var renderer = new FakeRenderer([4, 3, 2, 1]);
        using var baseFactory = new IntakeWebApplicationFactory();
        using var factory = Compose(baseFactory, new FakeGetCase(harness.CaseId), persisted, source, renderer)
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new ReportClock(new DateTimeOffset(2026, 9, 6, 23, 30, 0, TimeSpan.Zero)));
            }));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        using var response = await client.GetAsync($"/Cases/{harness.CaseId:D}?handler=PreviewReportDraft&section=report");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1, source.PreviewReads);
        var snapshot = Assert.IsType<AssessmentReportSnapshot>(renderer.Snapshot);
        Assert.Equal(AssessmentReportProjection.BuildSettlement(persisted, input.CurrentEstimate), snapshot.Settlement);
        Assert.Equal(250m, snapshot.Settlement.Excess);
        Assert.False(snapshot.Settlement.ClaimantVatRegistered);
        Assert.Equal("History clear", snapshot.HistoryCheck);
        Assert.Equal("Scuffed", snapshot.EngineerComments);
        Assert.Equal(120m, snapshot.AgreedFee);
        Assert.Equal(["Assessment report"], snapshot.FeeDescriptionLines);
        Assert.Equal(new CaseReportContentSwitches(true, false, false), snapshot.Content);
        Assert.Equal("Ed Mawdsley", snapshot.Signatory.PrintedName);
        Assert.Equal(overrideDate ? new DateOnly(2026, 8, 19) : new DateOnly(2026, 9, 7), snapshot.ReportDate);
        Assert.Equal(overrideDate, snapshot.ReportDateOverridden);
        Assert.Equal("2026-08-19", persisted.Field(AssessmentVocabulary.ReportDate)?.Value);
        Assert.Equal(new DateOnly(2026, 8, 3), snapshot.Assessed);
        // The claimant and Your Ref are the Case's own facts, read with the assessment.
        Assert.Equal(saved.Data.Claimant.Name.Current?.Value, snapshot.ClaimantName);
        Assert.Equal(saved.Data.Claim.Number.Current?.Value, snapshot.YourReference);
    }

    private static WebApplicationFactory<Program> Compose(
        IntakeWebApplicationFactory baseFactory,
        IGetCase getCase,
        CaseAssessmentProjection assessment,
        IAssessmentReportProjectionSource projectionSource,
        IAssessmentReportRenderer renderer,
        bool canOpen = true,
        IGenerateCaseReport? generateReport = null,
        IPrepareCaseReportDelivery? prepareDelivery = null,
        ISendPreparedCaseReport? sendPreparedReport = null,
        bool failIfReportServicesResolved = false) =>
        baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGetCase>();
                services.RemoveAll<IGetCasePageFrame>();
                services.RemoveAll<IGetCaseVehicleSection>();
                services.RemoveAll<IGetCaseValuationSection>();
                services.RemoveAll<IGetCaseNotesSection>();
                services.RemoveAll<IGetCaseFilesSection>();
                services.RemoveAll<IGetCaseAssessment>();
                services.RemoveAll<IGetAssessmentAccess>();
                services.RemoveAll<IGetAssessmentWorkspace>();
                services.RemoveAll<IAssessmentReportProjectionSource>();
                services.RemoveAll<ICaseReportSnapshotSource>();
                services.RemoveAll<IAssessmentReportRenderer>();
                services.RemoveAll<IDocumentContentStore>();
                if (failIfReportServicesResolved)
                {
                    services.RemoveAll<GenerateCaseAssessmentReportDraft>();
                    services.RemoveAll<IGenerateCaseReport>();
                    services.AddScoped<GenerateCaseAssessmentReportDraft>(static _ =>
                        throw new InvalidOperationException("Case GET must not resolve draft rendering."));
                    services.AddScoped<IGenerateCaseReport>(static _ =>
                        throw new InvalidOperationException("Case GET must not resolve report rendering."));
                }
                if (getCase is IAcquireCaseEditLease leases)
                {
                    // v26: the Report head's controls render inside the edit
                    // session, which the fake answers with a lease of its own.
                    services.RemoveAll<IAcquireCaseEditLease>();
                    services.AddSingleton(leases);
                }
                if (generateReport is not null)
                {
                    services.RemoveAll<IGenerateCaseReport>();
                    services.AddSingleton(generateReport);
                }
                if (prepareDelivery is not null)
                {
                    services.RemoveAll<IPrepareCaseReportDelivery>();
                    services.AddSingleton(prepareDelivery);
                }
                if (sendPreparedReport is not null)
                {
                    services.RemoveAll<ISendPreparedCaseReport>();
                    services.AddSingleton(sendPreparedReport);
                }
                services.AddSingleton(getCase);
                if (getCase is IGetCasePageFrame pageFrame)
                {
                    services.AddSingleton(pageFrame);
                }
                if (getCase is IGetCaseVehicleSection vehicleSection)
                {
                    services.AddSingleton(vehicleSection);
                }
                if (getCase is IGetCaseValuationSection valuationSection)
                {
                    services.AddSingleton(valuationSection);
                }
                if (getCase is IGetCaseNotesSection notesSection)
                {
                    services.AddSingleton(notesSection);
                }
                if (getCase is IGetCaseFilesSection filesSection)
                {
                    services.AddSingleton(filesSection);
                }
                services.AddSingleton<IGetCaseAssessment>(new FakeGetCaseAssessment(assessment));
                services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess(canOpen));
                services.AddSingleton<IGetAssessmentWorkspace>(new FakeGetAssessmentWorkspace(
                    AssessmentWorkspaceTestData.Create(assessment)));
                services.AddSingleton(projectionSource);
                services.AddSingleton((ICaseReportSnapshotSource)projectionSource);
                services.AddSingleton(renderer);
                services.AddSingleton<IDocumentContentStore>(new ThrowingDocumentContentStore());
            }));

    internal static AssessmentReportProjectionInput ReadyInput(Guid caseId)
    {
        var image = new byte[] { 137, 80, 78, 71, 1, 2, 3, 4 };
        var photo = new ReportImageEvidence(
            "site.jpg", "image/jpeg", image, Convert.ToHexStringLower(SHA256.HashData(image)));
        var source = new AcceptedReportSource("instruction.pdf", "1", new string('a', 64));
        return new AssessmentReportProjectionInput(
            FullAssessmentProjection(caseId),
            OurReference: "CE-100",
            ReportFor: ["Approved Principal"],
            ReportDate: new DateOnly(2026, 8, 19),
            Photos: [photo],
            Sources: [source],
            CurrentEstimate: CurrentEstimate() with { CaseId = caseId },
            Signatory: new ReportSignatory("Ed Mawdsley", "ATA VDA AQP", [1, 2, 3], "image/png"));
    }

    /// <summary>
    /// The Current estimate the ready fixture prices from: 50 parts, five
    /// panel hours at 30, 20 materials and 5 specialist, at 20 per cent VAT.
    /// </summary>
    internal static RepairSpecificationVersion CurrentEstimate()
    {
        var draft = new RepairSpecificationVersion(
        Guid.NewGuid(), Guid.NewGuid(), 2, RepairSpecificationState.Draft,
        new(RepairSpecificationSourceRoute.Manual, null, null, null),
        [
            EstimateLine(1, "repair", "Nearside door", 5m, null),
            EstimateLine(2, "new_part", "Door skin", null, 50m) with { Materials = 20m },
        ],
        null, "engineer-1", ReportFixtureAtUtc, "engineer-1", ReportFixtureAtUtc, null, null,
        new EstimateDetails("Repairer", 30m, 5m, 20m), IsCurrent: true);
        return draft with
        {
            State = RepairSpecificationState.Accepted,
            RecordedTotals = EstimateTotals.Compute(draft),
        };
    }

    private static CaseEstimateLineRecord EstimateLine(
        int position, string type, string description, decimal? workUnits, decimal? price) => new(
            Guid.NewGuid(), position, type, null, description, workUnits, price, false, null, null,
            "confirmed", "case", "Test evidence",
            ActorKind.Staff, "engineer-1", ReportFixtureAtUtc,
            Quantity: 1);

    /// <summary>
    /// Every assessment field <see cref="AssessmentPolicy.EvaluateReadiness"/>
    /// requires, confirmed, and every Case fact the report prints — the same
    /// fixture shape as the Core projection tests
    /// (<c>tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs</c>),
    /// so a "ready" web test genuinely reaches the renderer rather than
    /// tripping over the shared readiness rail.
    /// </summary>
    internal static CaseAssessmentProjection FullAssessmentProjection(Guid caseId)
    {
        var confirmedAt = DateTimeOffset.UtcNow;
        AssessmentFieldValue Field(string path, string value) => new(
            path, value, ActorKind.Staff, "engineer-1", confirmedAt);

        var fields = new[]
        {
            Field(AssessmentVocabulary.VehicleType, "car"),
            Field(AssessmentVocabulary.VehicleCondition, "good"),
            Field(AssessmentVocabulary.ImpactSeverity, "moderate"),
            Field(AssessmentVocabulary.ImpactLocation, "right_rear"),
            Field(AssessmentVocabulary.ValueRetail, "5000.00"),
            Field(AssessmentVocabulary.ValueTrade, "4000.00"),
            Field(AssessmentVocabulary.ValueEngineer, "5000.00"),
            Field(AssessmentVocabulary.Outcome, "repairable"),
            Field(AssessmentVocabulary.LegalStatus, "roadworthy"),
            Field(AssessmentVocabulary.HistoryCheck, "History clear"),
            Field(AssessmentVocabulary.EngineerName, "A Patterson"),
            Field(AssessmentVocabulary.EngineerQualifications, "M.Inst.IAEA"),
            Field(AssessmentVocabulary.EngineerSignature, "andy_patterson"),
            Field(AssessmentVocabulary.AgreedFee, "120.00"),
        };
        var caseOwned = new AssessmentCaseOwnedData(
            Registration: "PK12TMZ",
            Make: "Ford",
            Model: "Focus",
            Year: "2012",
            Mileage: 80_000,
            MileageUnit: "miles",
            MileageSource: "online_data",
            IncidentDate: new DateOnly(2026, 8, 1),
            ReceivedDate: new DateOnly(2026, 8, 2),
            InspectionMode: "ImageBasedAssessment",
            InspectionAddress: null,
            InspectionDate: new DateOnly(2026, 8, 3),
            ClaimantName: "Alex Example",
            ClaimNumber: "P-100");
        return new CaseAssessmentProjection(
            caseId, "CE-100", 0, CaseLifecycleState.Review, Guid.NewGuid(), fields, [], caseOwned);
    }

    private static string NewOperationKey() => Guid.NewGuid().ToString("N");

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static FormUrlEncodedContent Form(
        string antiforgeryToken, params (string Name, string Value)[] values)
    {
        var fields = values
            .Select(item => new KeyValuePair<string, string>(item.Name, item.Value))
            .Append(new("__RequestVerificationToken", antiforgeryToken));
        return new(fields);
    }

    private static string AntiforgeryValue(string html)
    {
        var tag = AntiforgeryTagRegex().Match(html);
        Assert.True(tag.Success, "The case action must render an antiforgery token.");
        var value = ValueRegex().Match(tag.Value);
        Assert.True(value.Success, "The case antiforgery token must have a value.");
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    [GeneratedRegex("<input[^>]*name=\"__RequestVerificationToken\"[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTagRegex();

    [GeneratedRegex("value=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ValueRegex();

    /// <summary>The Report section's readiness list in decoded markup.</summary>
    private static string BlockerList(string html)
    {
        var list = BlockerListRegex().Match(html);
        Assert.True(list.Success, "The Report section must list what the report still needs.");
        return list.Value;
    }

    /// <summary>The readiness list's row that names <paramref name="requirement"/>.</summary>
    private static string BlockerRow(string list, string requirement)
    {
        var row = Regex.Match(
            list,
            $"<li class=\"blocker\"[^>]*>\\s*<strong>{Regex.Escape(requirement)}</strong>.*?</li>",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);
        Assert.True(row.Success, $"The readiness list must name {requirement}.");
        return row.Value;
    }

    /// <summary>
    /// The row names the section that clears it and jumps there, labelled as
    /// the section row labels that section.
    /// </summary>
    private static void AssertBlockerLinks(string row, Guid caseId, string key)
    {
        Assert.Contains($"data-report-blocker=\"{key}\"", row, StringComparison.Ordinal);
        var link = SectionJumpRegex().Match(row);
        Assert.True(link.Success, "A blocker with a section must link to it.");
        Assert.Equal(key, link.Groups["key"].Value);
        Assert.Contains(
            $"href=\"/Cases/{caseId:D}?section={key}#section-{key}\"",
            link.Value,
            StringComparison.Ordinal);
        Assert.Equal(
            OperatorLabels.CaseWorkspace.Sections.Single(section => section.Key == key).Label,
            link.Groups["label"].Value);
    }

    [GeneratedRegex("<div[^>]*data-report-not-ready[^>]*>.*?</ul>\\s*</div>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex BlockerListRegex();

    [GeneratedRegex("<section[^>]*data-next-action[^>]*>.*?</section>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex NextActionRegex();

    [GeneratedRegex("<span data-next-label>(?<label>.*?)</span>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex NextLabelRegex();

    [GeneratedRegex("<a[^>]*data-section-jump=\"(?<key>[^\"]+)\"[^>]*>(?<label>[^<]*)</a>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex SectionJumpRegex();

    private sealed class FakeGetCase(Guid caseId) :
        IGetCase,
        IGetCasePageFrame,
        IGetCaseVehicleSection,
        IGetCaseValuationSection,
        IGetCaseNotesSection,
        IGetCaseFilesSection,
        IAcquireCaseEditLease
    {
        private CaseEditLeaseSnapshot? activeLease;

        /// <summary>The page's own Edit Case claim, answered so the Report section edits.</summary>
        public Task<CaseEditLease> ExecuteAsync(
            ClaimCaseEditLeaseRequest request, CancellationToken cancellationToken)
        {
            activeLease = new(
                request.Actor.SubjectId, request.Actor.Kind, DateTimeOffset.UtcNow.AddMinutes(5), request.OperationKey);
            return Task.FromResult(new CaseEditLease(
                request.CaseId, "held-report-lease", request.Actor.SubjectId, request.ExpectedVersion,
                DateTimeOffset.UtcNow.AddMinutes(5)));
        }

        public Task<CaseDetails?> ExecuteAsync(GetCaseQuery query, CancellationToken cancellationToken)
            => Task.FromResult(Details(query.CaseId));

        Task<CasePageFrame?> IGetCasePageFrame.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            var details = Details(query.CaseId);
            return Task.FromResult<CasePageFrame?>(details is null
                ? null
                : new(
                    new(details.Summary, details.Workflow, details.ActiveEditLease),
                    details.Documents,
                    details.AvailableReportSentEvidence,
                    details.RecordNotes,
                    details.Data!));
        }

        Task<CaseVehicleSection?> IGetCaseVehicleSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            var details = Details(query.CaseId);
            return Task.FromResult<CaseVehicleSection?>(details is null
                ? null
                : new(
                    new(details.Summary, details.Workflow, details.ActiveEditLease),
                    query.AssessmentWorkspace?.Data ?? details.Data!,
                    null,
                    query.AssessmentWorkspace?.Assessment));
        }

        Task<CaseValuationSection?> IGetCaseValuationSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            var details = Details(query.CaseId);
            return Task.FromResult<CaseValuationSection?>(details is null
                ? null
                : new(
                    new(details.Summary, details.Workflow, details.ActiveEditLease),
                    query.AssessmentWorkspace?.Data ?? details.Data!,
                    query.AssessmentWorkspace?.Assessment));
        }

        Task<CaseNotesSection?> IGetCaseNotesSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            var details = Details(query.CaseId);
            return Task.FromResult<CaseNotesSection?>(details is null
                ? null
                : new(new(details.Summary, details.Workflow, details.ActiveEditLease), []));
        }

        Task<CaseFilesSection?> IGetCaseFilesSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken)
        {
            var details = Details(query.CaseId);
            return Task.FromResult<CaseFilesSection?>(details is null
                ? null
                : new(
                    new(details.Summary, details.Workflow, details.ActiveEditLease),
                    details.Documents,
                    null,
                    CaseCustodyState.Pending,
                    []));
        }

        private CaseDetails? Details(Guid requestedCaseId)
        {
            if (requestedCaseId != caseId)
            {
                return null;
            }

            var identity = new CaseIdentity(caseId, "QDOS", 2026, 42, "QDOS-2026-00042");
            var workflow = new CaseWorkflowRecord(
                caseId, identity, CaseLifecycleState.ReportPreparation, null, null,
                null, null, null, null, null, 0);
            var summary = new CaseSearchItem(
                caseId, identity.Reference, null, CaseType.Inspection, "Approved Principal",
                workflow.State, null, "AB12CDE", "Alex Example", "P-100",
                DateTimeOffset.UtcNow, "Email", DateTimeOffset.UtcNow);
            var assessment = new CaseAssessmentProjection(
                caseId, identity.Reference, workflow.Version, workflow.State, null, [], [],
                new(null, null, null, null, null, null, "tbc", null, new DateOnly(2026, 8, 2), null, null, null, null, null));
            CaseDetails details = new(
                summary, workflow, activeLease, [], null, CaseCustodyState.Pending, [], [])
            {
                Data = AssessmentWorkspaceTestData.Create(assessment).Data,
            };
            return details;
        }
    }

    private sealed class FakeGetCaseAssessment(CaseAssessmentProjection projection) : IGetCaseAssessment
    {
        public Task<CaseAssessmentProjection?> ExecuteAsync(Guid caseId, CancellationToken cancellationToken) =>
            Task.FromResult<CaseAssessmentProjection?>(projection);
    }

    internal sealed class FakeProjectionSource(AssessmentReportProjectionInput input)
        : IAssessmentReportProjectionSource, ICaseReportSnapshotSource
    {
        public int MetadataReads { get; private set; }
        public int PreviewReads { get; private set; }
        public CaseReportReadinessInput Readiness { get; set; } = Metadata(input);

        public Task<AssessmentReportProjectionInput?> GetAsync(
            Guid caseId, ActionActor actor, CaseWorkSelector work, CancellationToken cancellationToken = default)
        {
            PreviewReads++;
            return Task.FromResult<AssessmentReportProjectionInput?>(input);
        }

        Task<CaseReportFreezeInputs?> ICaseReportSnapshotSource.GetAsync(
            Guid caseId, ActionActor actor, CaseWorkSelector work, CancellationToken cancellationToken)
        {
            MetadataReads++;
            return Task.FromResult<CaseReportFreezeInputs?>(new(
                input with { Photos = [] }, Readiness, input.OurReference, input.Assessment.CaseVersion));
        }

        private static CaseReportReadinessInput Metadata(AssessmentReportProjectionInput projection)
        {
            var signatoryId = Guid.NewGuid();
            var preparations = new List<CaseAssetPreparation>();
            var sources = new Dictionary<Guid, DocumentVersion>();
            var photo = Assert.Single(projection.Photos);
            foreach (var role in new[] { CaseAssetReportRole.CloseUp, CaseAssetReportRole.Overview })
            {
                var occurrenceId = Guid.NewGuid();
                var documentId = Guid.NewGuid();
                var versionId = Guid.NewGuid();
                preparations.Add(new(projection.Assessment.CaseId, occurrenceId, documentId, versionId,
                    1, photo.Sha256, photo.ContentType, role, null, CaseAssetRotation.None,
                    CaseAssetCrop.Full, 1, "engineer-1", ReportFixtureAtUtc));
                sources.Add(occurrenceId, new(versionId, documentId, 1, photo.CustodyReference,
                    photo.ContentType, photo.Content.Length, photo.Sha256, DocumentCustodyStatus.Confirmed,
                    ReportFixtureAtUtc, "engineer-1", true, false, null));
            }
            var signatory = projection.Signatory!;
            return new(projection.Assessment, signatoryId, null,
                [new(signatoryId, signatory.PrintedName, signatory.Qualifications,
                    signatory.SignatureContent, signatory.SignatureContentType, IsDefault: true)],
                projection.CurrentEstimate,
                new AppliedValuation(Guid.NewGuid(), projection.Assessment.CaseId, 1, Guid.NewGuid(),
                    ReportFixtureAtUtc, new(5000m, false, 0m, 5000m, null, 0m, [], 0m, 0m, 5000m),
                    5000m, "engineer-1", ReportFixtureAtUtc, "Accepted the guide value", "case-valuation-calculation/v1"),
                preparations, sources);
        }
    }

    private sealed class FakeRenderer(byte[] pdfBytes) : IAssessmentReportRenderer
    {
        public string EngineVersion => "fake";

        public AssessmentReportSnapshot? Snapshot { get; private set; }

        public Task<RenderedReportArtifact> RenderAsync(
            AssessmentReportSnapshot snapshot,
            CaseReportArtifactKind kind,
            CancellationToken cancellationToken = default)
        {
            Snapshot = snapshot;
            return Task.FromResult(new RenderedReportArtifact(
                $"{kind}.pdf", pdfBytes, 1,
                Convert.ToHexStringLower(SHA256.HashData(pdfBytes)),
                AssessmentReportContract.TemplateVersion, EngineVersion));
        }
    }

    private sealed class ReportClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ThrowingDocumentContentStore : IDocumentContentStore
    {
        public Task StoreAsync(
            Guid caseId, string caseReference, Guid versionId, ReadOnlyMemory<byte> content,
            string expectedSha256, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Assessment GET must not write document content.");

        public Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            Stream content,
            long contentLength,
            string expectedSha256,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Assessment GET must not write document content.");

        public Task<Stream> OpenReadAsync(
            Guid caseId, string caseReference, Guid versionId, string expectedSha256,
            long expectedLength, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Assessment GET must not read document content.");

        public Task DeleteAsync(
            Guid caseId, string caseReference, Guid versionId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Assessment GET must not delete document content.");
    }
}

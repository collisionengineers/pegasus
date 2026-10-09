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
/// Proves the report-draft Preview is actually reachable from
/// the web: a complete case renders and returns a PDF, and an incomplete
/// case names its readiness reasons and offers neither Generate nor Preview.
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
    public async Task IncompleteCaseNamesWhatIsMissingAndOffersNeitherGenerateNorPreview()
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
            "estimate",
            AssessmentReportProjection.RepairCostRequirement);
        // FRD-11: the control stays, disabled with its condition — no
        // submittable Generate form and no Preview link are offered.
        Assert.DoesNotContain("data-generate-report", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Preview report draft", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CaseOutsideTheCurrentExportedReviewCycleOffersNeitherGenerateNorPreview()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        // The page takes its access answer from the Case's state; Held is a
        // state the assessment cannot open.
        using var factory = Compose(
            baseFactory,
            new FakeGetCase(caseId, CaseLifecycleState.Held),
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
        Assert.DoesNotContain("data-generate-report", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Preview report draft", html, StringComparison.Ordinal);
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
    /// missing and links to the Case section that clears it. With Engineer
    /// the rows are the Next action itself (issue 899): no one-line summary
    /// beside them, and no second list in the Report section.
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
            // A missing Vehicle finding, retail value, outcome and agreed fee,
            // and no sign-off, repair spec, applied valuation or images.
            Assessment = full with
            {
                Fields =
                [
                    .. full.Fields.Where(field => field.Path is not (AssessmentVocabulary.VehicleCondition
                        or AssessmentVocabulary.ValueRetail or AssessmentVocabulary.Outcome
                        or AssessmentVocabulary.AgreedFee)),
                ],
            },
            EligibleSignOffEngineers = [],
            CurrentEstimate = null,
            AppliedValuation = null,
            Preparations = [],
        };
        var readiness = CaseReportReadiness.Evaluate(source.Readiness);
        var expected = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Pre-incident condition"] = "vehicle",
            ["Retail value"] = "valuation",
            ["Assessment outcome"] = "settlement",
            ["Agreed fee"] = "report",
            // No Case section clears the Sign-off Engineer: an Administrator
            // sets the name and signature in Accounts (operator, 26 September
            // 2026), so the row sends an Administrator there and nobody else
            // anywhere.
            [CaseReportReadiness.SignatoryRequirement] = null,
            [CaseReportReadiness.CurrentEstimateRequirement] = "estimate",
            [CaseReportReadiness.OverviewImageRequirement] = "files",
        };
        // The jump opens the tab inside the section that clears the blocker
        // (operator, 1 October 2026): Fee for the agreed fee, Images for an
        // image the report needs; every other section opens as it is.
        var expectedTabs = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Agreed fee"] = "fee",
            [CaseReportReadiness.OverviewImageRequirement] = "images",
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
            var row = BlockerRow(list, reason.Requirement);
            if (key is null)
            {
                Assert.DoesNotContain("data-section-jump", row, StringComparison.Ordinal);
                // The link is the how; the row carries no sentence on resolving
                // it (v36 item AB, 9 October 2026).
                Assert.DoesNotContain(reason.HowToResolve, row, StringComparison.Ordinal);
                var accounts = AccountsLinkRegex().Match(row);
                Assert.True(accounts.Success, "An Administrator's Sign-off blocker links to Accounts.");
                Assert.Contains("href=\"/Administration/Accounts\"", accounts.Value, StringComparison.Ordinal);
                // The requirement is the link, under the Accounts heading
                // (operator, 8 October 2026).
                Assert.Equal(reason.Requirement, accounts.Groups["label"].Value);
                Assert.Contains($"<div class=\"blocker-group-head\">{OperatorLabels.Admin.Accounts}</div>", list, StringComparison.Ordinal);
            }
            else
            {
                AssertBlockerLinks(row, caseId, key, reason.Requirement);
                Assert.Equal(expectedTabs.GetValueOrDefault(reason.Requirement), CaseWorkspaceLabels.Report.BlockerTab(reason));
                if (expectedTabs.TryGetValue(reason.Requirement, out var tab))
                {
                    Assert.Contains($"data-section-tab=\"{tab}\"", row, StringComparison.Ordinal);
                }
                else
                {
                    // The anchor tag helper writes a null tab as an empty one.
                    Assert.DoesNotMatch("data-section-tab=\"[^\"]", row);
                }
            }
        }

        // The development identity above is the Administrator; the Engineer's
        // view needs the header-authenticated factory, which reads the role
        // from the request.
        using var engineerBase = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var engineerFactory = Compose(
            engineerBase, new FakeGetCase(caseId), full, source, new FakeRenderer([1]));
        using var engineer = engineerFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        engineer.DefaultRequestHeaders.Add("X-Test-Roles", StaffRoleNames.Engineer);
        var engineerRow = BlockerRow(
            BlockerList(WebUtility.HtmlDecode(await GetHtmlAsync(engineer, $"/Cases/{caseId:D}?section=report"))),
            CaseReportReadiness.SignatoryRequirement);
        Assert.Contains(CaseReportReadiness.SignatoryRequirement, engineerRow, StringComparison.Ordinal);
        Assert.False(
            engineerRow.Contains("href=", StringComparison.Ordinal),
            $"An Engineer's Sign-off blocker links nowhere: {engineerRow}");

        // The Next action is one step: the first blocker in page order, in
        // full (operator, 8 October 2026).
        var panel = CaseWebTestSupport.NextActionRegex().Match(html).Value;
        var first = CaseWorkspaceLabels.Report.InPageOrder(readiness.Reasons)[0];
        Assert.Single(Regex.Matches(panel, "data-next-label"));
        Assert.Contains($"<strong data-next-label>{first.Requirement}</strong>", panel, StringComparison.Ordinal);
        // The step's control is the how; no sentence repeats it (v36 item AB).
        Assert.DoesNotContain(first.HowToResolve, panel, StringComparison.Ordinal);
        var report = CaseWebTestSupport.Section(html, "section-report-title");
        Assert.Contains("data-report-gate", report, StringComparison.Ordinal);
        Assert.DoesNotContain("data-report-not-ready", report, StringComparison.Ordinal);
    }

    /// <summary>
    /// Issue 898: a repairer VAT blocker opens the Repair Spec for editing on
    /// the Current spec, with the control that clears it named for focus,
    /// from the readiness list and from the Next action alike.
    /// </summary>
    [Theory]
    [InlineData(false, "#estimate-vat-status")]
    [InlineData(true, "[data-vat-reset]")]
    public async Task ARepairerVatBlockerOpensTheRepairSpecOnTheControlThatClearsIt(bool handPicked, string focus)
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        var ready = CurrentEstimate() with { CaseId = caseId };
        var estimate = ready with
        {
            Details = ready.Details with
            {
                Vat = handPicked
                    ? new EstimateVatPolicy(RepairerVatStatus.Registered, EstimateVatCategories.Parts, true)
                    : EstimateVatPolicy.For(RepairerVatStatus.Unknown),
            },
        };
        var source = new FakeProjectionSource(ReadyInput(caseId));
        source.Readiness = source.Readiness with { CurrentEstimate = estimate };
        var blocker = Assert.Single(CaseReportReadiness.Evaluate(source.Readiness).Reasons);
        Assert.Equal(
            handPicked ? CaseReportReadiness.RepairerVatHandPicked : CaseReportReadiness.RepairerVatStatusUnknown,
            blocker);
        using var factory = Compose(
            baseFactory, new FakeGetCase(caseId), FullAssessmentProjection(caseId), source, new FakeRenderer([1]),
            currentSpecification: estimate);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = WebUtility.HtmlDecode(await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report"));

        var row = BlockerRow(BlockerList(html), CaseReportReadiness.RepairerVatRequirement);
        var panel = CaseWebTestSupport.NextActionRegex().Match(html).Value;
        foreach (var place in new[] { row, panel })
        {
            Assert.Contains($"action=\"/Cases/{caseId:D}?handler=ClaimLease\"", place, StringComparison.Ordinal);
            Assert.Contains("name=\"section\" value=\"estimate\"", place, StringComparison.Ordinal);
            Assert.Contains($"name=\"estimate\" value=\"{estimate.SpecificationId:D}\"", place, StringComparison.Ordinal);
            Assert.Contains($"data-edit-focus=\"{focus}\"", place, StringComparison.Ordinal);
            Assert.DoesNotContain("data-section-jump", place, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Accounts are offered and the Case has none chosen, so staff clear the
    /// Sign-off Engineer blocker on Case details: the row says so and links
    /// there, for an Administrator too (operator, 26 September 2026).
    /// </summary>
    [Fact]
    public async Task TheSignOffBlockerLinksCaseDetailsWhenNoOfferedAccountIsChosen()
    {
        using var baseFactory = new IntakeWebApplicationFactory();
        var caseId = Guid.NewGuid();
        var source = new FakeProjectionSource(ReadyInput(caseId));
        source.Readiness = source.Readiness with
        {
            PersistedSignOffEngineerId = null,
            AssignedEngineerId = null,
            EligibleSignOffEngineers =
            [
                .. source.Readiness.EligibleSignOffEngineers.Select(profile => profile with { IsDefault = false }),
            ],
        };
        var reason = Assert.Single(CaseReportReadiness.Evaluate(source.Readiness).Reasons);
        Assert.Equal(CaseReportReadiness.SignOffEngineerNotChosen, reason);
        using var factory = Compose(
            baseFactory, new FakeGetCase(caseId), FullAssessmentProjection(caseId), source, new FakeRenderer([1]));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var html = WebUtility.HtmlDecode(await GetHtmlAsync(client, $"/Cases/{caseId:D}?section=report"));

        var row = BlockerRow(BlockerList(html), CaseReportReadiness.SignatoryRequirement);
        // The link to Case details is the how; the row carries no sentence
        // (v36 item AB, 9 October 2026).
        Assert.DoesNotContain("Choose the Sign-off Engineer on Case details.", row, StringComparison.Ordinal);
        AssertBlockerLinks(row, caseId, "overview", CaseReportReadiness.SignatoryRequirement);
        Assert.DoesNotContain("data-blocker-accounts", row, StringComparison.Ordinal);
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
        AssertBlockerLinks(BlockerRow(BlockerList(html), "Claimant name"), caseId, "claim", "Claimant name");

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
    public async Task WorkspaceSavedReportAndSettlementFieldsReachTheActualPreview(bool recordDate)
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
            // The accepted Case already holds its Principal's default fee.
            context.CaseAssessmentFields.RemoveRange(await context.CaseAssessmentFields
                .Where(field => field.WorkId == harness.CaseId)
                .ToArrayAsync());
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
                [AssessmentVocabulary.SettlementClaimantVatRegistered] = "false"
            }),
            Report = new(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [AssessmentVocabulary.EngineersComments] = "Scuffed",
                [AssessmentVocabulary.AgreedFee] = "120.00",
                [AssessmentVocabulary.FeeDescriptionLines] = "Assessment report",
                [AssessmentVocabulary.ReportDiscloseGuideSource] = "true",
                [AssessmentVocabulary.ReportValuationCommentary] = "false",
                [AssessmentVocabulary.ReportIncludeUnrelatedDamage] = "false"
            }, Guid.Parse(engineer.SubjectId), recordDate ? new DateOnly(2026, 8, 19) : (DateOnly?)null)
        }, CancellationToken.None);
        var assessmentStore = new EfCaseAssessmentStore(harness.Factory, harness.TimeProvider,
            new EfRepairSpecificationStore(harness.Factory, harness.TimeProvider));
        var persisted = await assessmentStore.GetAsync(harness.CaseId, CaseWorkSelector.Current, CancellationToken.None);
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
        // The claimant's VAT answer is saved on the Case and is no part of
        // the report; only a contract repair carries an agreed sum.
        Assert.Equal("false", persisted.Field(AssessmentVocabulary.SettlementClaimantVatRegistered)?.Value);
        Assert.Equal(new ReportSettlement(), snapshot.Settlement);
        Assert.Equal("History clear", snapshot.HistoryCheck);
        Assert.Equal("Scuffed", snapshot.EngineerComments);
        Assert.Equal(120m, snapshot.AgreedFee);
        Assert.Equal(["Assessment report"], snapshot.FeeDescriptionLines);
        Assert.Equal(new CaseReportContentSwitches(true, false, false), snapshot.Content);
        Assert.Equal("Ed Mawdsley", snapshot.Signatory.PrintedName);
        Assert.Equal(recordDate ? new DateOnly(2026, 8, 19) : new DateOnly(2026, 9, 7), snapshot.ReportDate);
        Assert.Equal(recordDate ? "2026-08-19" : null, persisted.Field(AssessmentVocabulary.ReportDate)?.Value);
        Assert.Equal(new DateOnly(2026, 8, 3), snapshot.Assessed);
        // The claimant and Your Ref are the Case's own facts, read with the assessment.
        Assert.Equal(saved.Data.Claimant.Name.Current?.Value, snapshot.ClaimantName);
        Assert.Equal(saved.Data.Claim.Number.Current?.Value, snapshot.YourReference);
    }

    private static WebApplicationFactory<Program> Compose(
        IntakeWebApplicationFactory baseFactory,
        FakeGetCase caseReads,
        CaseAssessmentProjection assessment,
        IAssessmentReportProjectionSource projectionSource,
        IAssessmentReportRenderer renderer,
        bool canOpen = true,
        IGenerateCaseReport? generateReport = null,
        ISendCaseReport? sendReport = null,
        bool failIfReportServicesResolved = false,
        RepairSpecificationVersion? currentSpecification = null) =>
        baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
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
                // v26: the Report head's controls render inside the edit
                // session, which the fake answers with a lease of its own.
                services.RemoveAll<IAcquireCaseEditLease>();
                services.AddSingleton<IAcquireCaseEditLease>(caseReads);
                if (generateReport is not null)
                {
                    services.RemoveAll<IGenerateCaseReport>();
                    services.AddSingleton(generateReport);
                }
                if (sendReport is not null)
                {
                    services.RemoveAll<ISendCaseReport>();
                    services.AddSingleton(sendReport);
                }
                services.AddSingleton<IGetCasePageFrame>(caseReads);
                services.AddSingleton<IGetCaseVehicleSection>(caseReads);
                services.AddSingleton<IGetCaseValuationSection>(caseReads);
                services.AddSingleton<IGetCaseNotesSection>(caseReads);
                services.AddSingleton<IGetCaseFilesSection>(caseReads);
                services.AddSingleton<IGetCaseAssessment>(new FakeGetCaseAssessment(assessment));
                services.AddSingleton<IGetAssessmentAccess>(new FakeGetAssessmentAccess(canOpen));
                services.AddSingleton<IGetAssessmentWorkspace>(new FakeGetAssessmentWorkspace(
                    AssessmentWorkspaceTestData.Create(assessment) with { CurrentSpecification = currentSpecification }));
                services.AddSingleton(projectionSource);
                services.AddSingleton((ICaseReportSnapshotSource)projectionSource);
                services.AddSingleton(renderer);
                services.AddSingleton<IDocumentContentStore>(new ThrowingDocumentContentStore());
            }));

    /// <summary>The bytes the ready fixture's photo opens to.</summary>
    internal static readonly byte[] ReadyImage = [137, 80, 78, 71, 1, 2, 3, 4];

    internal static AssessmentReportProjectionInput ReadyInput(Guid caseId)
    {
        var image = ReadyImage;
        var photo = new ReportImageEvidence(
            "site.jpg", "image/jpeg", ReportImageContent.Opened(_ => Task.FromResult(image)), Convert.ToHexStringLower(SHA256.HashData(image)));
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
    /// panel hours at 30, 20 materials and 5 specialist, at 20 per cent VAT
    /// for a VAT registered repairer. A report is not ready until the
    /// repairer's VAT status is recorded.
    /// </summary>
    internal static RepairSpecificationVersion CurrentEstimate() => new(
        Guid.NewGuid(), Guid.NewGuid(), 2, RepairSpecificationState.Draft,
        new(RepairSpecificationSourceRoute.Manual, null, null, null),
        [
            EstimateLine(1, "repair", "Nearside door", 5m, null),
            EstimateLine(2, "new_part", "Door skin", null, 50m) with { Materials = 20m },
        ],
        "engineer-1", ReportFixtureAtUtc,
        new EstimateDetails(
            "Repairer", 30m, 5m, 20m, Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
        IsCurrent: true);

    private static CaseEstimateLineRecord EstimateLine(
        int position, string type, string description, decimal? workUnits, decimal? price) => new(
            Guid.NewGuid(), position, type, null, description, workUnits, price, false, null, null,
            "case", "Test evidence",
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
        var recordedAt = DateTimeOffset.UtcNow;
        AssessmentFieldValue Field(string path, string value) => new(
            path, value, ActorKind.Staff, "engineer-1", recordedAt);

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

    private static async Task<string> GetHtmlAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// The aside's Report not ready card in decoded markup: the readiness list
    /// (operator, 8 October 2026).
    /// </summary>
    private static string BlockerList(string html)
    {
        var card = CaseWebTestSupport.ReportNotReadyRegex().Match(html);
        Assert.True(card.Success, "The Case aside must list what the report still needs.");
        return card.Value;
    }

    /// <summary>The readiness list's row that names <paramref name="requirement"/>.</summary>
    private static string BlockerRow(string list, string requirement)
    {
        var row = Regex.Matches(
                list,
                "<li class=\"blocker\"[^>]*>.*?</li>",
                RegexOptions.Singleline | RegexOptions.CultureInvariant)
            .FirstOrDefault(match => match.Value.Contains($">{requirement}</", StringComparison.Ordinal));
        Assert.True(row is not null, $"The readiness list must name {requirement}.");
        return row!.Value;
    }

    /// <summary>
    /// The row's requirement is its link to the section that clears it
    /// (operator, 8 October 2026), and it jumps there.
    /// </summary>
    private static void AssertBlockerLinks(string row, Guid caseId, string key, string requirement)
    {
        Assert.Contains($"data-report-blocker=\"{key}\"", row, StringComparison.Ordinal);
        var link = SectionJumpRegex().Match(row);
        Assert.True(link.Success, "A blocker with a section must link to it.");
        Assert.Equal(key, link.Groups["key"].Value);
        Assert.Contains(
            $"href=\"/Cases/{caseId:D}?section={key}#section-{key}\"",
            link.Value,
            StringComparison.Ordinal);
        Assert.Equal(requirement, link.Groups["label"].Value);
    }

    [GeneratedRegex("<a[^>]*data-blocker-accounts[^>]*>(?<label>[^<]*)</a>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex AccountsLinkRegex();

    [GeneratedRegex("<strong data-next-label>(?<label>.*?)</strong>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex NextLabelRegex();

    [GeneratedRegex("<a[^>]*data-section-jump=\"(?<key>[^\"]+)\"[^>]*>(?<label>[^<]*)</a>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex SectionJumpRegex();

    private sealed class FakeGetCase(
        Guid caseId, CaseLifecycleState state = CaseLifecycleState.ReportPreparation) :
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

        Task<CasePageFrame?> IGetCasePageFrame.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<CasePageFrame?>(Frame(query.CaseId) is { } frame
                ? new(frame, [], [], CaseRecordNotes.None, Data(frame))
                : null);

        Task<CaseVehicleSection?> IGetCaseVehicleSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseVehicleSection?>(Frame(query.CaseId) is { } frame
                ? new(
                    frame,
                    query.AssessmentWorkspace?.Data ?? Data(frame),
                    null,
                    query.AssessmentWorkspace?.Assessment)
                : null);

        Task<CaseValuationSection?> IGetCaseValuationSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseValuationSection?>(Frame(query.CaseId) is { } frame
                ? new(
                    frame,
                    query.AssessmentWorkspace?.Data ?? Data(frame),
                    query.AssessmentWorkspace?.Assessment)
                : null);

        Task<CaseNotesSection?> IGetCaseNotesSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseNotesSection?>(Frame(query.CaseId) is { } frame ? new(frame, []) : null);

        Task<CaseFilesSection?> IGetCaseFilesSection.ExecuteAsync(
            GetCaseSectionQuery query,
            CancellationToken cancellationToken) =>
            Task.FromResult<CaseFilesSection?>(Frame(query.CaseId) is { } frame
                ? new(frame, [], null, CaseCustodyState.Pending, [])
                : null);

        /// <summary>The Case's frame as a focused read returns it; null for any other Case.</summary>
        private CaseSectionFrame? Frame(Guid requestedCaseId)
        {
            if (requestedCaseId != caseId)
            {
                return null;
            }

            var identity = new CaseIdentity(caseId, "QDOS", 2026, 42, "QDOS-2026-00042");
            var workflow = new CaseWorkflowRecord(
                caseId, identity, state, null, null,
                null, null, null, null, null, 0);
            var summary = new CaseSearchItem(
                caseId, identity.Reference, null, CaseType.Inspection, "Approved Principal",
                workflow.State, null, "AB12CDE", "Alex Example", "P-100",
                DateTimeOffset.UtcNow, "Email", DateTimeOffset.UtcNow);
            return new(summary, workflow, activeLease);
        }

        private static CaseDataProjection Data(CaseSectionFrame frame) =>
            AssessmentWorkspaceTestData.Create(new CaseAssessmentProjection(
                frame.Summary.CaseId, frame.Summary.Reference, frame.Workflow.Version, frame.Workflow.State, null, [], [],
                new(null, null, null, null, null, null, "tbc", null, new DateOnly(2026, 8, 2), null, null, null, null, null))).Data;
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
            Guid caseId, ActionActor actor, CaseWorkSelector work, ReportProjectionReuse? reuse, CancellationToken cancellationToken)
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
                // In the report, wearing the tag that prints it as this role.
                preparations.Add(new(projection.Assessment.CaseId, occurrenceId, documentId, versionId,
                    1, photo.Sha256, photo.ContentType, true, null, CaseAssetRotation.None,
                    CaseAssetCrop.Full, 1, "engineer-1", ReportFixtureAtUtc)
                {
                    TagIds = [role == CaseAssetReportRole.CloseUp ? ImageTagVocabulary.CloseUpId : ImageTagVocabulary.OverviewId],
                    SourceFileName = photo.CustodyReference,
                    RecordedAtUtc = ReportFixtureAtUtc,
                    CanPrint = true
                });
                sources.Add(occurrenceId, new(versionId, documentId, 1, photo.CustodyReference,
                    photo.ContentType, ReadyImage.Length, photo.Sha256, DocumentCustodyStatus.Confirmed,
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

using System.Data.Common;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.AiWork;
using Pegasus.Core.Assessment;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Triage;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Reports;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed partial class AssessmentPersistenceIntegrationTests
{
    private static readonly DateTimeOffset StartUtc =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task DamageImpactsPersistAndClearTheirCoreDerivedHeadlineRows()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-damage-derivation");
        var caseId = outcome.Identity.CaseId;
        var lease = await harness.AcquireLeaseAsync(caseId, 0, harness.AutomationActor, "damage-lease-1");
        var saved = await harness.SaveAssessment.ExecuteAsync(new(
            caseId, lease.Version, harness.AutomationActor, "damage-save-1", "Record damage.", lease.Token,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [AssessmentVocabulary.DamageImpacts] = "[{\"areas\":[\"front\"],\"severity\":\"light\",\"note\":\"Bonnet\"},{\"areas\":[\"right_rear\"],\"severity\":\"heavy\",\"note\":\"Quarter\"}]"
            }), CancellationToken.None);

        Assert.Equal("multiple", saved.Field(AssessmentVocabulary.ImpactLocation)?.Value);
        Assert.Equal("heavy", saved.Field(AssessmentVocabulary.ImpactSeverity)?.Value);
        Assert.All(saved.Fields.Where(field => field.Path is AssessmentVocabulary.DamageImpacts or AssessmentVocabulary.ImpactLocation or AssessmentVocabulary.ImpactSeverity),
            field => Assert.Equal(ActorKind.Automation, field.RecordedByKind));

        var clearLease = await harness.AcquireLeaseAsync(caseId, saved.CaseVersion, harness.AutomationActor, "damage-lease-2");
        var cleared = await harness.SaveAssessment.ExecuteAsync(new(
            caseId, clearLease.Version, harness.AutomationActor, "damage-save-2", "Clear damage.", clearLease.Token,
            new Dictionary<string, string?>(StringComparer.Ordinal) { [AssessmentVocabulary.DamageImpacts] = null }), CancellationToken.None);

        Assert.Null(cleared.Field(AssessmentVocabulary.DamageImpacts));
        Assert.Null(cleared.Field(AssessmentVocabulary.ImpactLocation));
        Assert.Null(cleared.Field(AssessmentVocabulary.ImpactSeverity));
    }

    [Fact]
    public async Task AssessmentWorkspaceLoadsInExactlyFiveReaderCommands()
    {
        var counter = new ReaderCommandCounter();
        await using var harness = await Harness.CreateAsync(counter);
        var outcome = await harness.AcceptAsync("assessment-workspace-query-count");
        await SetReportPreparationAsync(harness.Factory, outcome.Identity.CaseId);
        counter.Reset();

        var workspace = await new EfAssessmentWorkspaceSource(harness.Factory)
            .GetAsync(outcome.Identity.CaseId, CaseWorkSelector.Current);

        Assert.NotNull(workspace);
        Assert.Equal(5, counter.ExecutedReaderCommands);
    }

    [Fact]
    public async Task ReportDraftGenerationThroughProductionProjectionResolvesSignOffAndFailsClosedWithoutIt()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-report-photo-batch");
        await SetReportPreparationAsync(harness.Factory, outcome.Identity.CaseId);
        await SeedPhotosAsync(harness.Factory, outcome.Identity.CaseId, 2);
        var contentStore = new RecordingDocumentContentStore();
        await using var staffContext = await harness.Factory.CreateDbContextAsync();
        var source = new EfAssessmentReportProjectionSource(
            harness.Factory,
            new GetAssessmentWorkspace(new EfAssessmentWorkspaceSource(harness.Factory)),
            contentStore,
            new EfStaffAccountQueries(staffContext),
            new EfCaseAssetPreparationStore(harness.Factory),
            new ListAppliedValuations(new EfValuationStore(harness.Factory)));

        var input = await source.GetAsync(
            outcome.Identity.CaseId,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), CaseWorkSelector.Current);

        Assert.NotNull(input);
        Assert.Null(input.Signatory);
        Assert.Equal(2, input.Photos.Count);
        Assert.Equal(1, contentStore.BatchReadCount);
        Assert.Equal(0, contentStore.SingleReadCount);
        Assert.All(contentStore.Reads, read => Assert.Equal("case-root-id", read.Address.CaseRootRemoteId));
        var projected = AssessmentReportProjection.Project(
            input with { ReportDate = new DateOnly(2026, 8, 19) });
        Assert.False(projected.IsReady);
        Assert.Contains(projected.Reasons, reason => reason.Requirement == "Sign-off Engineer");

        var signOffEngineerId = await SeedSignOffEngineerAsync(
            harness.Factory,
            outcome.Identity.CaseId);
        await SeedReportReadyAssessmentAsync(harness.Factory, outcome.Identity.CaseId);
        input = await source.GetAsync(
            outcome.Identity.CaseId,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), CaseWorkSelector.Current);
        Assert.NotNull(input);
        await using (var verificationContext = await harness.Factory.CreateDbContextAsync())
        {
            Assert.Equal(
                signOffEngineerId,
                (await verificationContext.CaseWorkflows.AsNoTracking().SingleAsync(
                    item => item.CaseId == outcome.Identity.CaseId)).SignOffEngineerId);
        }
        Assert.Equal("A Engineer", input.Signatory?.PrintedName);

        var ready = AssessmentReportProjection.Project(
            input with { ReportDate = new DateOnly(2026, 8, 19) });
        Assert.True(ready.IsReady, string.Join("; ", ready.Reasons.Select(reason => reason.Requirement)));
        // The printed Case facts are read through the production projection:
        // the Assessed date is the Case's confirmed Inspection date (not the
        // intake's 2031-05-20), the claimant and Your Ref are the Case's own,
        // and instructions were received on the Case's received date (the
        // receipt's, not the instruction_date field's 2031-05-01).
        var printed = Assert.IsType<AssessmentReportSnapshot>(ready.Snapshot);
        Assert.Equal(new DateOnly(2031, 5, 6), input.Assessment.CaseOwned.InspectionDate);
        Assert.Equal(new DateOnly(2031, 5, 6), printed.Assessed);
        Assert.Equal("Mrs Jane Example", input.Assessment.CaseOwned.ClaimantName);
        Assert.Equal("Mrs Jane Example", printed.ClaimantName);
        Assert.Equal("ABC/DEF/12345/1", input.Assessment.CaseOwned.ClaimNumber);
        Assert.Equal("ABC/DEF/12345/1", printed.YourReference);
        Assert.Equal(Pegasus.Core.LondonCalendar.DateAt(StartUtc), printed.InstructionsReceived);
        var pdf = "%PDF-1.4 report-ready"u8.ToArray();
        var draft = await new GenerateAssessmentReportDraft(new TestReportRenderer(pdf))
            .ExecuteAsync(ready.Snapshot!, CaseReportArtifactKind.AssessmentReport);
        Assert.Equal(pdf, draft.Pdf);
    }

    private static async Task<Guid> SeedSignOffEngineerAsync(
        IDbContextFactory<PegasusDbContext> factory,
        Guid caseId)
    {
        await using var context = await factory.CreateDbContextAsync();
        var staffId = Guid.NewGuid();
        var engineerRole = await context.Roles.SingleOrDefaultAsync(
            role => role.NormalizedName == "ENGINEER");
        if (engineerRole is null)
        {
            engineerRole = new IdentityRole<Guid>(StaffRoleNames.Engineer)
            {
                Id = Guid.NewGuid(),
                NormalizedName = StaffRoleNames.Engineer.ToUpperInvariant()
            };
            context.Roles.Add(engineerRole);
        }

        var signature = new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };
        context.Users.Add(new PegasusIdentityUser
        {
            Id = staffId,
            UserName = "a.engineer",
            NormalizedUserName = "A.ENGINEER",
            IsEnabled = true,
            IsSignOffEngineer = true,
            SignOffPrintedName = "A Engineer",
            SignOffQualifications = "ATA VDA",
            SignOffSignature = signature,
            SignOffSignatureDigest = Convert.ToHexStringLower(SHA256.HashData(signature)),
            IsDefaultSignOffEngineer = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        });
        context.UserRoles.Add(new IdentityUserRole<Guid>
        {
            UserId = staffId,
            RoleId = engineerRole.Id
        });
        var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
        workflow.SignOffEngineerId = staffId;
        await context.SaveChangesAsync();
        return staffId;
    }

    private static async Task SeedReportReadyAssessmentAsync(
        IDbContextFactory<PegasusDbContext> factory,
        Guid caseId)
    {
        await using var context = await factory.CreateDbContextAsync();
        var recordedAt = StartUtc;
        const string engineer = "report-ready-engineer";
        var caseData = new (string Name, string Type, string Value)[]
        {
            (CaseDataFieldNames.ClaimantName, CaseDataCodes.Text, "Mrs Jane Example"),
            (CaseDataFieldNames.ClaimNumber, CaseDataCodes.Text, "ABC/DEF/12345/1"),
            (CaseDataFieldNames.VehicleRegistration, CaseDataCodes.Text, "AB12CDE"),
            (CaseDataFieldNames.VehicleMake, CaseDataCodes.Text, "Ford"),
            (CaseDataFieldNames.VehicleModel, CaseDataCodes.Text, "Focus"),
            (CaseDataFieldNames.VehicleYear, CaseDataCodes.Text, "2012"),
            (CaseDataFieldNames.VehicleMileage, CaseDataCodes.Integer, "80000"),
            (CaseDataFieldNames.VehicleMileageUnit, CaseDataCodes.Text, "miles"),
            (CaseDataFieldNames.IncidentDate, CaseDataCodes.Date, "2031-04-01"),
            (CaseDataFieldNames.InstructionDate, CaseDataCodes.Date, "2031-05-01"),
            (CaseDataFieldNames.InspectionMode, CaseDataCodes.InspectionMode,
                ProviderInspectionModePolicy.ImageBasedAssessmentCode),
            (CaseDataFieldNames.InspectionAddress, CaseDataCodes.Text, "1 Test Street, London"),
            (CaseDataFieldNames.InspectionDate, CaseDataCodes.Date, "2031-05-06")
        };
        var existingConfirmed = await context.Set<CaseDataFieldEntity>()
            .Where(field => field.WorkId == caseId
                && field.ValueKind == CaseDataCodes.Confirmed
                && caseData.Select(value => value.Name).Contains(field.FieldName))
            .ToArrayAsync();
        context.RemoveRange(existingConfirmed);
        context.Set<CaseDataFieldEntity>().AddRange(caseData.Select(field =>
            new CaseDataFieldEntity
            {
                WorkId = caseId,
                FieldName = field.Name,
                ValueKind = CaseDataCodes.Confirmed,
                ValueType = field.Type,
                Value = field.Value,
                SourceKind = CaseDataCodes.StaffCorrection,
                SourceIdentity = engineer,
                SourceLabel = "Report-ready fixture",
                PolicyKey = CaseDataPolicy.EditPolicyKey,
                PolicyVersion = CaseDataPolicy.EditPolicyVersion,
                ConfirmedByActor = engineer,
                ConfirmedAtUtc = recordedAt
            }));
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AssessmentVocabulary.VehicleType] = "car",
            [AssessmentVocabulary.VehicleMileageSource] = "owner",
            [AssessmentVocabulary.VehicleCondition] = "good",
            [AssessmentVocabulary.ImpactSeverity] = "moderate",
            [AssessmentVocabulary.ImpactLocation] = "right_rear",
            [AssessmentVocabulary.ValueRetail] = "5000.00",
            [AssessmentVocabulary.ValueTrade] = "4000.00",
            [AssessmentVocabulary.ValueEngineer] = "5000.00",
            [AssessmentVocabulary.Outcome] = "repairable",
            [AssessmentVocabulary.LegalStatus] = "roadworthy",
            [AssessmentVocabulary.HistoryCheck] = "History clear",
            [AssessmentVocabulary.EngineerName] = "A Engineer",
            [AssessmentVocabulary.EngineerQualifications] = "ATA VDA",
            [AssessmentVocabulary.EngineerSignature] = "a_engineer",
            [AssessmentVocabulary.AgreedFee] = "120.00"
        };
        context.CaseAssessmentFields.AddRange(values.Select(value =>
            new CaseAssessmentFieldEntity
            {
                WorkId = caseId,
                FieldPath = value.Key,
                Value = value.Value,
                RecordedByKind = ActorKind.Staff.ToString(),
                RecordedBy = engineer,
                RecordedAtUtc = recordedAt,
                ConfirmedBy = engineer,
                ConfirmedAtUtc = recordedAt
            }));
        var recordedBreakdown = new EstimateCalculationBreakdown(
            RepairSpecificationPolicy.PolicyVersion,
            20m,
            new EstimateRawTotals(
                Parts: 200m,
                PanelLabour: 100m,
                PaintLabour: 0m,
                Materials: 50m,
                Specialist: 0m,
                OffPattern: 0m,
                Category: 350m,
                Net: 350m,
                Taxable: 350m,
                Vat: 70m,
                Gross: 420m),
            new EstimatePrintedTotals(
                Parts: 200m,
                PanelLabour: 100m,
                PaintLabour: 0m,
                Materials: 50m,
                Specialist: 0m,
                Net: 350m,
                Vat: 70m,
                Gross: 420m));

        context.Set<CaseRepairSpecificationEntity>().Add(new()
        {
            Id = Guid.NewGuid(),
            WorkId = caseId,
            Version = 1,
            State = RepairSpecificationState.Accepted.ToString(),
            SourceRoute = RepairSpecificationSourceRoute.Manual.ToString(),
            CalculationLabour = 100m,
            CalculationParts = 200m,
            CalculationPaintMaterials = 50m,
            CalculationSpecialistOther = 0m,
            RepairerVatRegistered = true,
            CalculationVat = 70m,
            CalculationTotal = 420m,
            CalculationPolicyVersion =
                $"{RepairSpecificationPolicy.PolicyKey}/v{RepairSpecificationPolicy.PolicyVersion}",
            CalculationBreakdownJson = JsonSerializer.Serialize(
                recordedBreakdown,
                EfRepairSpecificationStore.JsonOptions),
            CreatedBy = engineer,
            CreationOperationKey = "report-ready-estimate",
            CreatedAtUtc = recordedAt,
            AcceptedBy = engineer,
            AcceptedAtUtc = recordedAt,
            Name = "Engineer's",
            LabourRate = 40m,
            OtherCosts = 0m,
            VatPercent = 20m,
            IsCurrent = true
        });
        await context.SaveChangesAsync();
    }

    private sealed class TestReportRenderer(byte[] pdf) : IAssessmentReportRenderer
    {
        public string EngineVersion => "report-ready-test";

        public Task<RenderedReportArtifact> RenderAsync(
            AssessmentReportSnapshot snapshot,
            CaseReportArtifactKind kind,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RenderedReportArtifact(
                $"{kind}.pdf",
                pdf,
                1,
                Convert.ToHexStringLower(SHA256.HashData(pdf)),
                AssessmentReportContract.TemplateVersion,
                EngineVersion));
    }

    [Theory]
    [InlineData(CaseLifecycleState.NotReady, true, false)]
    [InlineData(CaseLifecycleState.Review, true, false)]
    [InlineData(CaseLifecycleState.Held, false, true)]
    [InlineData(CaseLifecycleState.ReportPreparation, true, false)]
    [InlineData(CaseLifecycleState.PostReport, true, false)]
    [InlineData(CaseLifecycleState.PostReportComplete, true, true)]
    [InlineData(CaseLifecycleState.CreatedInError, false, true)]
    public async Task AssessmentAccessUsesNativeStateAndRetainsWorkspaceWithoutAnExport(
        CaseLifecycleState state,
        bool canOpen,
        bool isReadOnly)
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-access-review-cycle");
        var lease = await harness.AcquireLeaseAsync(
            outcome.Identity.CaseId, 0, harness.AutomationActor, "retained-assessment-lease");
        await harness.SaveAssessment.ExecuteAsync(new(
            outcome.Identity.CaseId, lease.Version, harness.AutomationActor,
            "retained-assessment-value", "Record vehicle condition.", lease.Token,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["vehicle.condition"] = "good"
            }), CancellationToken.None);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var workflow = await context.CaseWorkflows.SingleAsync(
                item => item.CaseId == outcome.Identity.CaseId);
            workflow.State = state.ToString();
            await context.SaveChangesAsync();
            Assert.False(await context.EvaFirstHandoffProxies.AnyAsync(
                item => item.CaseId == outcome.Identity.CaseId));
        }

        var access = Assert.IsType<AssessmentAccessState>(
            await new EfAssessmentAccessSource(harness.Factory).GetAsync(outcome.Identity.CaseId));
        Assert.Equal(canOpen, access.CanOpen);
        Assert.Equal(isReadOnly, access.IsReadOnly);
        var workspace = Assert.IsType<AssessmentWorkspace>(
            await new EfAssessmentWorkspaceSource(harness.Factory).GetAsync(outcome.Identity.CaseId, CaseWorkSelector.Current));
        Assert.Equal(state, workspace.Header.State);
        Assert.Equal(outcome.Identity.CaseId, workspace.Data.Identity.CaseId);
        Assert.Equal(outcome.Identity.CaseId, workspace.Assessment.CaseId);
        Assert.Equal("good", workspace.Assessment.Field("vehicle.condition")?.Value);
    }

    [Fact]
    public async Task AutomationSaveIsUnconfirmedAttributedAndParityLoggedWithAStaffSave()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-accept-1");
        var caseId = outcome.Identity.CaseId;

        // The Automation actor writes under the same lease and version
        // guards as a staff save; its values land unconfirmed.
        var automationLease = await harness.AcquireLeaseAsync(
            caseId,
            0,
            harness.AutomationActor,
            "assessment-lease-automation");
        var saved = await harness.SaveAssessment.ExecuteAsync(
            new(
                caseId,
                automationLease.Version,
                harness.AutomationActor,
                "mcp:assessment-save-1",
                "Automation recorded the assessment draft.",
                automationLease.Token,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["vehicle.condition"] = "good",
                    ["assessment.outcome"] = "total_loss",
                    ["assessment.category"] = "S",
                    ["assessment.salvage_value"] = "1500.00"
                    // The valuation values are deliberately absent: the
                    // Engineer's Value and its basis card's retail and trade
                    // are recorded only by a Case Save's adoption, and a field
                    // save that posted them is refused.
                },
                [
                    new("repair", null, "Repair nearside door", 3.5m, null, false, null, null,
                        "estimated", "judgement", "Visible panel damage"),
                    new("new_part", null, "Door skin", null, 220.40m, false, "P-1234", null,
                        "confirmed", "official", "Distorted beyond repair")
                ]),
            CancellationToken.None);

        Assert.Equal(1, saved.CaseVersion);
        Assert.All(saved.Fields, field =>
        {
            Assert.Equal(ActorKind.Automation, field.RecordedByKind);
            Assert.False(field.IsConfirmed);
        });
        Assert.Equal(2, saved.EstimateLines.Count);
        Assert.All(saved.EstimateLines, line => Assert.False(line.IsConfirmed));
        Assert.Contains(
            saved.Readiness,
            item => item.Requirement == "vehicle.condition awaits review"
                && item.Source.Contains("Automation", StringComparison.Ordinal));
        Assert.Contains(
            saved.Readiness,
            item => item.Requirement == "Estimate line 1 (repair) awaits review");

        // A staff Engineer re-saves one finding with the same value: the
        // value flips to confirmed, and both saves left exactly the same
        // shape of permanent evidence (logging parity, side by side). The
        // clock advances so the two history rows order deterministically.
        harness.Advance(TimeSpan.FromMinutes(1));
        var staffLease = await harness.AcquireLeaseAsync(
            caseId,
            saved.CaseVersion,
            harness.EngineerActor,
            "assessment-lease-staff");
        var confirmed = await harness.SaveAssessment.ExecuteAsync(
            new(
                caseId,
                staffLease.Version,
                harness.EngineerActor,
                "staff-assessment-save-1",
                "Engineer confirmed the recorded outcome.",
                staffLease.Token,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["assessment.outcome"] = "total_loss"
                }),
            CancellationToken.None);
        var confirmedOutcome = confirmed.Field("assessment.outcome");
        Assert.NotNull(confirmedOutcome);
        Assert.True(confirmedOutcome!.IsConfirmed);
        Assert.Equal(ActorKind.Staff, confirmedOutcome.RecordedByKind);

        await using var context = await harness.Factory.CreateDbContextAsync();
        var history = await context.ActionHistory.AsNoTracking()
            .Where(item => item.EventKind == "case_assessment_saved")
            .OrderBy(item => item.OccurredAtUtc)
            .ToArrayAsync();
        Assert.Equal(2, history.Length);
        Assert.Equal(nameof(ActorKind.Automation), history[0].ActorKind);
        Assert.Equal("pegasus-automation", history[0].ActorSubjectId);
        Assert.Equal(nameof(ActorKind.Staff), history[1].ActorKind);
        Assert.All(history, entry =>
        {
            Assert.Equal("case", entry.AggregateType);
            Assert.Equal(caseId.ToString("D"), entry.AggregateId);
            Assert.Equal("Succeeded", entry.Outcome);
            Assert.False(string.IsNullOrWhiteSpace(entry.BeforeJson));
            Assert.False(string.IsNullOrWhiteSpace(entry.AfterJson));
            Assert.False(string.IsNullOrWhiteSpace(entry.Reason));
            Assert.Equal("case-assessment-edit/v1", entry.PolicyVersion);
        });
        Assert.Equal(2, await context.CaseWorkflowEvents.AsNoTracking()
            .CountAsync(item => item.CaseId == caseId
                && item.EventType == "case_assessment_saved"));
        Assert.Equal(2, await context.CaseHistory.AsNoTracking()
            .CountAsync(item => item.CaseId == caseId
                && item.EventType == "case_assessment_saved"));
    }

    [Fact]
    public async Task OperationKeyReplayReturnsTheOriginalResultAndConflictsOnDifferentMaterial()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-accept-2");
        var caseId = outcome.Identity.CaseId;
        var lease = await harness.AcquireLeaseAsync(
            caseId,
            0,
            harness.AutomationActor,
            "assessment-lease-replay");
        SaveAssessmentRequest Request(string value) => new(
            caseId,
            lease.Version,
            harness.AutomationActor,
            "mcp:assessment-replay",
            "Automation recorded the assessment draft.",
            lease.Token,
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["vehicle.condition"] = value
            });

        var first = await harness.SaveAssessment.ExecuteAsync(
            Request("good"),
            CancellationToken.None);
        Assert.Equal(1, first.CaseVersion);

        var replay = await harness.SaveAssessment.ExecuteAsync(
            Request("good"),
            CancellationToken.None);
        Assert.Equal(1, replay.CaseVersion);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            Assert.Equal(1, await context.CaseWorkflowEvents.AsNoTracking()
                .CountAsync(item => item.CaseId == caseId
                    && item.EventType == "case_assessment_saved"));
        }

        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.SaveAssessment.ExecuteAsync(Request("poor"), CancellationToken.None));
    }

    [Fact]
    public async Task StaleVersionsAndMissingLeasesFailClosed()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-accept-3");
        var caseId = outcome.Identity.CaseId;

        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            harness.SaveAssessment.ExecuteAsync(
                new(
                    caseId,
                    0,
                    harness.AutomationActor,
                    "mcp:assessment-noleased",
                    "Automation recorded the assessment draft.",
                    "not-a-lease",
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["vehicle.condition"] = "good"
                    }),
                CancellationToken.None));

        var lease = await harness.AcquireLeaseAsync(
            caseId,
            0,
            harness.AutomationActor,
            "assessment-lease-stale");
        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
            harness.SaveAssessment.ExecuteAsync(
                new(
                    caseId,
                    lease.Version + 5,
                    harness.AutomationActor,
                    "mcp:assessment-stale",
                    "Automation recorded the assessment draft.",
                    lease.Token,
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["vehicle.condition"] = "good"
                    }),
                CancellationToken.None));
    }

    [Fact]
    public async Task AnUnknownWorkRequestBindingFailsClosed()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-accept-4");
        var caseId = outcome.Identity.CaseId;
        var lease = await harness.AcquireLeaseAsync(
            caseId,
            0,
            harness.AutomationActor,
            "assessment-lease-binding");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.SaveAssessment.ExecuteAsync(
                new(
                    caseId,
                    lease.Version,
                    harness.AutomationActor,
                    "mcp:assessment-binding",
                    "Automation recorded the assessment draft.",
                    lease.Token,
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["vehicle.condition"] = "good"
                    },
                    AiWorkRequestId: Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public async Task EarlierEstimateUpdateReplaysItsRecordedIdentityWithoutRevertingLaterEdits()
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = (await harness.AcceptAsync("estimate-replay-case")).Identity.CaseId;
        var actor = harness.EngineerActor;
        var jobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var save = new SaveEstimate(harness.RepairSpecifications, jobs, harness.Clock);
        var lease1 = await harness.AcquireLeaseAsync(caseId, 0, actor, "replay-lease-1");
        var create = new SaveEstimateRequest(caseId, 0, actor, "replay-K1", "Recorded an estimate.",
            lease1.Token, null, new("Repairer", 40m, null, 20m),
            [new("repair", null, "Repair door", 2m, null, false, null, null, "confirmed", "judgement", null)],
            new(RepairSpecificationSourceRoute.Manual, null, null, null));
        var first = await save.ExecuteAsync(create, default);
        var lease2 = await harness.AcquireLeaseAsync(caseId, 1, actor, "replay-lease-2");
        var update = create with
        {
            EstimateId = first.SpecificationId, ExpectedVersion = 1, EditLeaseToken = lease2.Token,
            OperationKey = "replay-K2", Details = first.Details with { Name = "Engineer revision" },
            ExistingLineIds = first.Lines.Select(line => (Guid?)line.Id).ToArray(),
            Lines = [create.Lines[0] with { WorkUnits = 3m }],
        };
        var second = await save.ExecuteAsync(update, default);
        var lease3 = await harness.AcquireLeaseAsync(caseId, 2, actor, "replay-lease-3");
        var third = await save.ExecuteAsync(update with
        {
            ExpectedVersion = 2, EditLeaseToken = lease3.Token, OperationKey = "replay-K3",
            Details = second.Details with { Name = "Final revision" },
            ExistingLineIds = second.Lines.Select(line => (Guid?)line.Id).ToArray(),
            Lines = [create.Lines[0] with { WorkUnits = 4m }],
        }, default);

        // Reconstruct the store under the real Web runtime role. The replay
        // must read its permanent action result after LastOperationKey moved.
        await using var context = await harness.Factory.CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync(
            "CREATE USER [estimate_replay_web] WITHOUT LOGIN; ALTER ROLE [pegasus_web_runtime_role] ADD MEMBER [estimate_replay_web];");
        await context.Database.ExecuteSqlRawAsync("EXECUTE AS USER = 'estimate_replay_web';");
        try
        {
            var runtimeFactory = new PooledDbContextFactory<PegasusDbContext>(
                new DbContextOptionsBuilder<PegasusDbContext>().UseSqlServer(context.Database.GetDbConnection()).Options);
            var resumed = new SaveEstimate(new EfRepairSpecificationStore(runtimeFactory, harness.Clock), jobs, harness.Clock);
            var replay = await resumed.ExecuteAsync(update, default);
            Assert.Equal(first.SpecificationId, replay.SpecificationId);
            Assert.Equal(third.Details.Name, replay.Details.Name);
            Assert.Equal(third.Lines[0].Id, replay.Lines[0].Id);
            Assert.Equal(4m, replay.Lines[0].WorkUnits);
            Assert.Equal(third.Lines[0].AmendedAtUtc, replay.Lines[0].AmendedAtUtc);
            await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
                resumed.ExecuteAsync(update with { Details = update.Details with { Name = "Changed intent" } }, default));
            await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
                resumed.ExecuteAsync(update with { OperationKey = "new-stale-operation" }, default));
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("REVERT;");
        }
        Assert.Equal(3, await context.CaseWorkflowEvents.CountAsync(item => item.CaseId == caseId && item.EventType.StartsWith("estimate_")));
        Assert.Equal(3, await context.ActionHistory.CountAsync(item => item.AggregateId == caseId.ToString("D") && item.EventKind.StartsWith("estimate_")));
        Assert.Equal(3, (await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId)).Version);
    }

    [Fact]
    public async Task NamedEstimatesUseReportsFreshnessWhenReselectingOrChangingCurrent()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("estimate-accept-case");
        var caseId = outcome.Identity.CaseId;
        var engineer = harness.UserActor;
        var jobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var save = new SaveEstimate(harness.RepairSpecifications, jobs, harness.Clock);
        var duplicate = new DuplicateEstimate(harness.RepairSpecifications);
        var discard = new DiscardEstimate(harness.RepairSpecifications);
        var setCurrent = new SetCurrentEstimate(
            harness.RepairSpecifications, jobs, new ConfirmAiJob(jobs), harness.Clock);
        var list = new ListCaseEstimates(harness.RepairSpecifications);
        long version = 0;

        async Task<CaseEditLease> LeaseAsync(ActionActor actor, string key) =>
            await harness.AcquireLeaseAsync(caseId, version, actor, key);

        // Two Engineer estimates on one case: both Drafts, staff lines confirmed.
        var leaseA = await LeaseAsync(engineer, "estimate-lease-a");
        var repairer = await save.ExecuteAsync(
            new(caseId, leaseA.Version, engineer, "estimate-save-a", "Recorded the repairer's estimate.",
                leaseA.Token, null,
                new("Repairer", 40m, 0m, 20m,
                    Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
                [
                    new("new_part", null, "Door skin", null, 220.40m, false, "P-1234", null,
                        "confirmed", "official", null, Quantity: 1, Materials: 25m),
                    new("repair", null, "Repair nearside door", 2.5m, null, false, null, null,
                        "confirmed", "judgement", null),
                    new("paint_repair", null, "Paint door", null, null, false, null, null,
                        "confirmed", "judgement", null, PaintWorkUnits: 1.5m),
                ],
                new(RepairSpecificationSourceRoute.Manual, null, null, null)),
            CancellationToken.None);
        version++;
        Assert.Equal(RepairSpecificationState.Draft, repairer.State);
        Assert.Equal("Repairer", repairer.Details.Name);
        Assert.Equal(3, repairer.Lines.Count);
        Assert.All(repairer.Lines, line => Assert.True(line.IsConfirmed));
        Assert.False(repairer.IsCurrent);

        var leaseB = await LeaseAsync(engineer, "estimate-lease-b");
        var engineers = await save.ExecuteAsync(
            new(caseId, leaseB.Version, engineer, "estimate-save-b", "Recorded the Engineer's own estimate.",
                leaseB.Token, null,
                new("Engineer's", 45m, 0m, 0m,
                    Vat: EstimateVatPolicy.For(RepairerVatStatus.NotRegistered)),
                [new("repair", null, "Repair nearside door", 2m, null, false, null, null, "confirmed", "judgement", null)],
                new(RepairSpecificationSourceRoute.Manual, null, null, null)),
            CancellationToken.None);
        version++;
        Assert.Equal(2, engineers.Version);

        // Duplicate: "<name> copy", Draft, Manual, lines cloned.
        var leaseCopy = await LeaseAsync(engineer, "estimate-lease-copy");
        var copy = await duplicate.ExecuteAsync(
            new(caseId, leaseCopy.Version, engineer, "estimate-duplicate", "Working copy.",
                leaseCopy.Token, repairer.SpecificationId),
            CancellationToken.None);
        version++;
        Assert.Equal("Repairer copy", copy.Details.Name);
        Assert.Equal(RepairSpecificationState.Draft, copy.State);
        Assert.Equal(RepairSpecificationSourceRoute.Manual, copy.Source.Route);
        Assert.Equal(3, copy.Lines.Count);
        Assert.Equal(repairer.Details.LabourRate, copy.Details.LabourRate);

        // Use estimate: the Draft is accepted with the totals owner's basis and becomes Current.
        var leaseUseA = await LeaseAsync(engineer, "estimate-lease-use-a");
        var useA = new SetCurrentEstimateRequest(
            caseId, leaseUseA.Version, engineer, "estimate-use-a", "Use the repairer's estimate.",
            leaseUseA.Token, repairer.SpecificationId);
        var currentA = await setCurrent.ExecuteAsync(useA, CancellationToken.None);
        version++;
        Assert.Equal(RepairSpecificationState.Accepted, currentA.State);
        Assert.True(currentA.IsCurrent);
        var totalsA = EstimateTotals.Compute(currentA);
        // The canonical B04 arithmetic: the one 40.00 hourly rate prices
        // panel (2.5h = 100.00) and paint (1.5h = 60.00) alike — the
        // separate paint rate is gone — and the estimate-level paint
        // materials (25.00) join Materials.
        Assert.Equal(220.40m + 100m + 60m + 25m, totalsA.Printed.Net);
        Assert.Equal(totalsA.Printed.Gross, currentA.CalculationBasis!.Total);
        Assert.Equal(totalsA.Printed.Vat, currentA.CalculationBasis.Vat);
        Assert.Equal(currentA.SpecificationId,
            (await harness.RepairSpecifications.GetCurrentAcceptedAsync(caseId, CancellationToken.None))!.SpecificationId);
        // Replay returns the same estimate without a second mutation.
        Assert.Equal(currentA.SpecificationId,
            (await setCurrent.ExecuteAsync(useA, CancellationToken.None)).SpecificationId);

        var (currentGenerationId, supersededGenerationId) = await SeedGenerationsAsync(harness, caseId);
        var leaseReselectA = await LeaseAsync(engineer, "estimate-lease-reselect-a");
        var reselectedA = await setCurrent.ExecuteAsync(
            new(caseId, leaseReselectA.Version, engineer, "estimate-reselect-a", "Keep using the repairer's estimate.",
                leaseReselectA.Token, repairer.SpecificationId),
            CancellationToken.None);
        version++;
        Assert.Equal(currentA.SpecificationId, reselectedA.SpecificationId);
        Assert.Equal("Confirmed", await harness.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentGenerationId:D}'"));
        Assert.Equal("Confirmed", await harness.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{supersededGenerationId:D}'"));
        Assert.Equal(0, await StaleRowCountAsync(harness, caseId));

        // Switching Current clears the previous in the same transaction; A stays Accepted.
        var leaseUseB = await LeaseAsync(engineer, "estimate-lease-use-b");
        var currentB = await setCurrent.ExecuteAsync(
            new(caseId, leaseUseB.Version, engineer, "estimate-use-b", "Use the Engineer's estimate.",
                leaseUseB.Token, engineers.SpecificationId),
            CancellationToken.None);
        version++;
        Assert.True(currentB.IsCurrent);
        Assert.Equal("Stale", await harness.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentGenerationId:D}'"));
        Assert.Equal("Confirmed", await harness.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{supersededGenerationId:D}'"));
        Assert.Equal(1, await StaleRowCountAsync(harness, caseId));
        Assert.Equal(CaseReportStaleReasons.EstimateChanged, await LatestStaleReasonAsync(harness, caseId));
        var listed = await list.ExecuteAsync(caseId, CaseWorkSelector.Current, CancellationToken.None);
        Assert.Equal(3, listed.Count);
        Assert.Single(listed, item => item.IsCurrent);
        Assert.Equal(RepairSpecificationState.Accepted,
            listed.Single(item => item.SpecificationId == repairer.SpecificationId).State);
        Assert.Equal(currentB.SpecificationId,
            (await harness.RepairSpecifications.GetCurrentAcceptedAsync(caseId, CancellationToken.None))!.SpecificationId);
        Assert.Equal(copy.SpecificationId,
            (await harness.RepairSpecifications.GetCurrentDraftAsync(caseId, CancellationToken.None))!.SpecificationId);

        // An accepted estimate is neither discarded nor edited; the copy is discarded with its reason.
        var leaseRefused = await LeaseAsync(engineer, "estimate-lease-refused");
        await Assert.ThrowsAsync<InvalidOperationException>(() => discard.ExecuteAsync(
            new(caseId, leaseRefused.Version, engineer, "estimate-discard-accepted", "Not wanted.",
                leaseRefused.Token, repairer.SpecificationId),
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => save.ExecuteAsync(
            new(caseId, leaseRefused.Version, engineer, "estimate-edit-accepted", "Change it.",
                leaseRefused.Token, engineers.SpecificationId, engineers.Details, [],
                new(RepairSpecificationSourceRoute.Manual, null, null, null)),
            CancellationToken.None));
        var discarded = await discard.ExecuteAsync(
            new(caseId, leaseRefused.Version, engineer, "estimate-discard-copy", "Superfluous copy.",
                leaseRefused.Token, copy.SpecificationId),
            CancellationToken.None);
        version++;
        Assert.Equal(RepairSpecificationState.Discarded, discarded.State);
        Assert.Equal("Superfluous copy.", discarded.DiscardReason);
        Assert.Null(await harness.RepairSpecifications.GetCurrentDraftAsync(caseId, CancellationToken.None));

        // AI draft: the Automation actor cites the Estimate job it holds; lines land unconfirmed;
        // the Engineer's "Use estimate" confirms the lines and completes the Draft-ready job.
        var job = await jobs.CreateAsync(
            new(AiJobKind.Estimate, AiJobSubjectKind.Case, caseId, outcome.Identity.Reference,
                "Draft an estimate at 60 % of the Engineer's Value.", 60, 12000m, engineer,
                "estimate-job-create", AiJobPolicy.DefaultExpiry),
            CancellationToken.None);
        var taken = await jobs.TransitionAsync(
            new(job.JobId, job.Version, AiJobState.Taken, harness.AutomationActor, "estimate-job-take",
                LeaseExpiresAtUtc: harness.Clock.GetUtcNow() + AiJobPolicy.LeaseDuration),
            CancellationToken.None);
        var leaseAi = await LeaseAsync(harness.AutomationActor, "estimate-lease-ai");
        var aiDraft = await save.ExecuteAsync(
            new(caseId, leaseAi.Version, harness.AutomationActor, "mcp:estimate-save-ai", "AI drafted an estimate.",
                leaseAi.Token, null,
                // The AI path records no VAT policy of its own, so this draft
                // carries the status the Engineer recorded on it before using it.
                // What happens when none is recorded is proved by
                // AnUnknownRepairerVatStatusBlocksUseAsCurrentUntilItOrTheCategoriesAreRecorded.
                new("Claude draft", 40m, 0m, 20m,
                    Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
                [new("repair", null, "Repair nearside door", 3m, null, false, null, null, "estimated", "judgement", "Visible damage")],
                new(RepairSpecificationSourceRoute.AiDraft, null, null, null),
                job.JobId),
            CancellationToken.None);
        version++;
        Assert.Equal(RepairSpecificationSourceRoute.AiDraft, aiDraft.Source.Route);
        Assert.Equal(job.JobId, aiDraft.AiJobId);
        Assert.All(aiDraft.Lines, line => Assert.False(line.IsConfirmed));
        await jobs.TransitionAsync(
            new(job.JobId, taken.Version, AiJobState.DraftReady, harness.AutomationActor, "estimate-job-ready",
                Result: new(AiJobResultKind.Estimate, aiDraft.SpecificationId.ToString("D"), null)),
            CancellationToken.None);

        var leaseUseAi = await LeaseAsync(engineer, "estimate-lease-use-ai");
        var currentAi = await setCurrent.ExecuteAsync(
            new(caseId, leaseUseAi.Version, engineer, "estimate-use-ai", "Use the AI draft.",
                leaseUseAi.Token, aiDraft.SpecificationId),
            CancellationToken.None);
        version++;
        Assert.True(currentAi.IsCurrent);
        Assert.All(currentAi.Lines, line => Assert.Equal(engineer.SubjectId, line.ConfirmedBy));
        Assert.Equal(AiJobState.Completed, (await jobs.GetAsync(job.JobId, CancellationToken.None))!.State);

        Assert.Equal(4, (await list.ExecuteAsync(caseId, CaseWorkSelector.Current, CancellationToken.None)).Count);
        Assert.Equal(version, (await harness.AcquireLeaseAsync(caseId, version, engineer, "estimate-lease-final")).Version);
    }

    [Theory]
    [InlineData(CaseLifecycleState.PostReportComplete)]
    [InlineData(CaseLifecycleState.Query)]
    public async Task CompletedAndQueryCasesRejectGuideValuationsAndOrdinaryEstimates(
        CaseLifecycleState state)
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = (await harness.AcceptAsync("assessment-read-only-case")).Identity.CaseId;
        var engineer = harness.EngineerActor;
        await using (var setup = await harness.Factory.CreateDbContextAsync())
        {
            var workflow = await setup.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
            workflow.State = state.ToString();
            await setup.SaveChangesAsync();
        }

        var lease = await harness.AcquireLeaseAsync(
            caseId, 0, engineer, "assessment-read-only-return-lease");
        var valuationRefusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new EfCaseWorkspaceStore(harness.Factory, harness.Clock).SaveAsync(
                new SaveCaseWorkspaceRequest(
                    caseId,
                    lease.Version,
                    engineer,
                    "assessment-read-only-guide-valuation",
                    null,
                    lease.Token)
                {
                    Valuation = new(
                    [
                        new(
                            ValuationSource.Glasses,
                            new DateOnly(2031, 5, 6),
                            new TimeOnly(9, 30),
                            42_000,
                            12_500m,
                            10_250m,
                            new DateOnly(2031, 5, 1))
                    ])
                },
                CancellationToken.None));
        Assert.Equal("The Case cannot be saved in its current state.", valuationRefusal.Message);

        var estimateRefusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.RepairSpecifications.SaveEstimateAsync(
                new(
                    caseId,
                    lease.Version,
                    engineer,
                    "assessment-read-only-ordinary-estimate",
                    "Attempted ordinary estimate after report completion.",
                    lease.Token,
                    null,
                    new("Read-only estimate", 40m, null, 20m),
                    [new("repair", null, "Repair door", 2m, null, false, null, null, "confirmed", "judgement", null)],
                    new(RepairSpecificationSourceRoute.Manual, null, null, null)),
                CancellationToken.None));
        Assert.Contains("read-only", estimateRefusal.Message, StringComparison.Ordinal);

        Assert.Empty(await harness.Valuations.ListForCaseAsync(caseId, CaseWorkSelector.Current, CancellationToken.None));
        Assert.Empty(await harness.RepairSpecifications.ListEstimatesAsync(caseId, CaseWorkSelector.Current, CancellationToken.None));
    }

    [Fact]
    public async Task ImportedDocumentStorePreservesAuthorityOnReplayAndRequiresEngineerAcceptance()
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = (await harness.AcceptAsync("import-store-case")).Identity.CaseId;
        var engineer = harness.EngineerActor;
        var lease = await harness.AcquireLeaseAsync(caseId, 0, engineer, "import-store-lease");
        var parsed = new Pegasus.Infrastructure.Glass.GlassEstimateXmlParser()
            .Parse(System.Text.Encoding.UTF8.GetBytes(GlassEstimateXmlParserTests.GlassExport.BuildXml()));
        var hash = Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(GlassEstimateXmlParserTests.GlassExport.BuildXml())));
        var source = new RepairSpecificationSource(RepairSpecificationSourceRoute.Glasses,
            "estimate-import:store-contract", parsed.SourceVersion, hash);
        // Chosen VAT/rate are existing Engineer header inputs, not inferred
        // from source VAT. The canonical caller separately proves Unknown VAT.
        var request = new SaveEstimateRequest(caseId, 0, engineer, "import-store-save", ImportRawEstimate.ImportReason,
            lease.Token, null, new("Glass's 1", 40m, null, 20m,
                Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)), parsed.Lines, source);
        var authority = new ImportRawEstimateRequest(engineer, caseId, 0, lease.Token,
            Guid.NewGuid(), Guid.NewGuid(), hash, request.OperationKey, request.Details.Name);
        // Not ready and Review are assessment-writable since the 17 September
        // ruling; the import is refused only where the assessment is read-only.
        foreach (var state in new[] { CaseLifecycleState.Held, CaseLifecycleState.PostReportComplete })
        {
            await using var setup = await harness.Factory.CreateDbContextAsync();
            var workflow = await setup.CaseWorkflows.SingleAsync(row => row.CaseId == caseId);
            workflow.State = state.ToString();
            await setup.SaveChangesAsync();
            var authorityRefusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.RepairSpecifications.RequireImportAuthorityAsync(authority, default));
            Assert.Contains("read-only", authorityRefusal.Message, StringComparison.Ordinal);
            var saveRefusal = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                harness.RepairSpecifications.SaveImportedEstimateAsync(request, default));
            Assert.Contains("read-only", saveRefusal.Message, StringComparison.Ordinal);
            Assert.Empty(await harness.RepairSpecifications.ListEstimatesAsync(caseId, CaseWorkSelector.Current, default));
            Assert.Equal(0, (await setup.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == caseId)).Version);
            Assert.False(await setup.ActionHistory.AnyAsync(row => row.CorrelationId == request.OperationKey));
        }
        await SetReportPreparationAsync(harness.Factory, caseId);
        await using var context = await harness.Factory.CreateDbContextAsync();
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlRawAsync(
            "CREATE USER [estimate_import_web] WITHOUT LOGIN; ALTER ROLE [pegasus_web_runtime_role] ADD MEMBER [estimate_import_web];");
        await context.Database.ExecuteSqlRawAsync("EXECUTE AS USER = 'estimate_import_web';");
        RepairSpecificationVersion imported;
        try
        {
            var runtimeFactory = new PooledDbContextFactory<PegasusDbContext>(
                new DbContextOptionsBuilder<PegasusDbContext>().UseSqlServer(context.Database.GetDbConnection()).Options);
            var store = new EfRepairSpecificationStore(runtimeFactory, harness.Clock);
            await store.RequireImportAuthorityAsync(authority, default);
            await store.RequireImportAuthorityAsync(authority, default);
            imported = await store.SaveImportedEstimateAsync(request, default);
            Assert.Equal(RepairSpecificationState.Draft, imported.State);
            Assert.False(imported.IsCurrent);
            Assert.All(imported.Lines, line => Assert.False(line.IsConfirmed));
            Assert.Null(await store.GetCurrentAcceptedAsync(caseId, default));
            await Assert.ThrowsAsync<CaseVersionConflictException>(() => store.RequireImportAuthorityAsync(authority, default));
            await Assert.ThrowsAsync<CaseVersionConflictException>(() => store.SaveImportedEstimateAsync(request, default));
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("REVERT;");
        }

        var visibleHistoryBeforeBinding = await new EfCaseQueryStore(harness.Factory, harness.Clock)
            .ListHistoryAsync(caseId, default);
        var bound = await harness.RepairSpecifications.BindSourceHashReplayAsync(
            caseId,
            "import-store-source-replay",
            hash,
            imported.SpecificationId,
            engineer,
            default);
        Assert.Equal(imported.SpecificationId, bound.EstimateId);
        var exactReplay = await harness.RepairSpecifications.ProbeSourceHashReplayAsync(
            caseId, "import-store-source-replay", hash.ToUpperInvariant(), default);
        Assert.Equal(imported.SpecificationId, exactReplay!.EstimateId);
        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.RepairSpecifications.ProbeSourceHashReplayAsync(
                caseId, "import-store-source-replay", new string('a', 64), default));
        var visibleHistoryAfterBinding = await new EfCaseQueryStore(harness.Factory, harness.Clock)
            .ListHistoryAsync(caseId, default);
        Assert.Equal(visibleHistoryBeforeBinding.Count, visibleHistoryAfterBinding.Count);
        await using (var verifyContext = await harness.Factory.CreateDbContextAsync())
        {
            Assert.Equal(
                1,
                await verifyContext.CaseWorkflows.AsNoTracking()
                    .Where(row => row.CaseId == caseId)
                    .Select(row => row.Version)
                    .SingleAsync());
            var bindingAfter = await verifyContext.ActionHistory.AsNoTracking()
                .Where(row => row.AggregateType == "case"
                    && row.AggregateId == caseId.ToString("D")
                    && row.CorrelationId == "import-store-source-replay"
                    && row.EventKind == "estimate_source_replay_bound")
                .Select(row => row.AfterJson)
                .SingleAsync();
            Assert.Contains(imported.SpecificationId.ToString("D"), bindingAfter, StringComparison.OrdinalIgnoreCase);
        }

        var live = await harness.AcquireLeaseAsync(caseId, 1, engineer, "import-store-replay-lease");
        var replay = await harness.RepairSpecifications.SaveImportedEstimateAsync(request with
        {
            ExpectedVersion = 1, EditLeaseToken = live.Token, OperationKey = "import-store-second-completion"
        }, default);
        Assert.Equal(imported.SpecificationId, replay.SpecificationId);
        await harness.RepairSpecifications.RequireImportAuthorityAsync(authority with
        {
            ExpectedVersion = 1, EditLeaseToken = live.Token
        }, default);
        var jobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var use = new SetCurrentEstimate(harness.RepairSpecifications, jobs, new ConfirmAiJob(jobs), harness.Clock);
        var useRequest = new SetCurrentEstimateRequest(caseId, 1, engineer, "import-store-use", "Use estimate.", live.Token, imported.SpecificationId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => use.ExecuteAsync(useRequest with { Actor = harness.AutomationActor }, default));
        var accepted = await use.ExecuteAsync(useRequest, default);
        Assert.True(accepted.IsCurrent);
        Assert.All(accepted.Lines, line => Assert.Equal(engineer.SubjectId, line.ConfirmedBy));
        Assert.Single(await harness.RepairSpecifications.ListEstimatesAsync(caseId, CaseWorkSelector.Current, default));
        Assert.Equal(2, (await context.CaseWorkflows.AsNoTracking().SingleAsync(row => row.CaseId == caseId)).Version);
    }

    [Fact]
    public async Task SourceHashReplayRejectsOperationKeyUsedToSetCurrentEstimate()
    {
        await using var harness = await Harness.CreateAsync();
        var caseId = (await harness.AcceptAsync("import-store-collision-case")).Identity.CaseId;
        var engineer = harness.EngineerActor;
        var lease = await harness.AcquireLeaseAsync(caseId, 0, engineer, "import-store-collision-lease");
        var xml = System.Text.Encoding.UTF8.GetBytes(GlassEstimateXmlParserTests.GlassExport.BuildXml());
        var parsed = new Pegasus.Infrastructure.Glass.GlassEstimateXmlParser().Parse(xml);
        var hash = Convert.ToHexStringLower(SHA256.HashData(xml));
        var request = new SaveEstimateRequest(
            caseId, 0, engineer, "import-store-collision-import", ImportRawEstimate.ImportReason,
            lease.Token, null,
            new("Glass's 1", 40m, null, 20m,
                Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
            parsed.Lines,
            new(RepairSpecificationSourceRoute.Glasses, "estimate-import:collision", parsed.SourceVersion, hash));
        var imported = await harness.RepairSpecifications.SaveImportedEstimateAsync(request, default);

        const string collisionKey = "import-store-collision-set-current";
        var live = await harness.AcquireLeaseAsync(caseId, 1, engineer, "import-store-collision-use-lease");
        var jobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var use = new SetCurrentEstimate(
            harness.RepairSpecifications,
            jobs,
            new ConfirmAiJob(jobs),
            harness.Clock);
        await use.ExecuteAsync(
            new(caseId, live.Version, engineer, collisionKey, "Use the imported estimate.", live.Token, imported.SpecificationId),
            default);

        var creationReplay = await harness.RepairSpecifications.BindSourceHashReplayAsync(
            caseId, request.OperationKey, hash, imported.SpecificationId, engineer, default);
        Assert.Equal(imported.SpecificationId, creationReplay.EstimateId);
        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.RepairSpecifications.BindSourceHashReplayAsync(
                caseId, collisionKey, hash, imported.SpecificationId, engineer, default));

        await using var verify = await harness.Factory.CreateDbContextAsync();
        Assert.False(await verify.ActionHistory.AnyAsync(item =>
            item.AggregateType == "case"
            && item.AggregateId == caseId.ToString("D")
            && item.CorrelationId == collisionKey
            && item.EventKind == "estimate_source_replay_bound"));
    }

    /// <summary>
    /// B04: the whole canonical estimate model survives the round trip. The
    /// header keeps its four discounts, the repairer's VAT position and the
    /// categories that position charges, and the rate card the one rate came
    /// from; every line keeps the facts the editor never shows - its
    /// materials, the values the source document stated, the document and
    /// row it came from, and its amendment attribution. The row's frozen
    /// breakdown is the one totals owner's own raw and printed figures.
    /// </summary>
    [Fact]
    public async Task AnEstimatesCanonicalHeaderLinesAndBreakdownRoundTrip()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("estimate-canonical-accept");
        var caseId = outcome.Identity.CaseId;
        var engineer = harness.EngineerActor;
        var save = new SaveEstimate(
            harness.RepairSpecifications,
            new EfAiJobStore(harness.Factory, harness.Clock),
            harness.Clock);

        var rateCardAdministrator = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);
        var rateCards = new EfLabourRateCardStore(harness.Factory, harness.Clock);
        var rateCard = await rateCards.SaveAsync(
            new(Guid.NewGuid(), "Panel and paint", 52.50m, true, 0, rateCardAdministrator,
                "Create rate", "estimate-canonical-rate-create"),
            CancellationToken.None);
        var documentVersionId = Guid.NewGuid();
        var documentSha = new string('c', 64);
        var amendedAtUtc = StartUtc.AddHours(2);
        var origin = new EstimateLineOrigin(
            "new_part", "Door skin", "P-1234", 1, null, null, 240.00m, 12.50m);
        var details = new EstimateDetails(
            "Repairer", 40m, 110m, 20m,
            new EstimateDiscounts(0.125m, 0.05m, 0.1m, 0.025m),
            new EstimateVatPolicy(
                RepairerVatStatus.NotRegistered,
                EstimateVatCategories.Parts | EstimateVatCategories.Materials,
                false));
        var lines = new[]
        {
            new EstimateLineInput("new_part", null, "Door skin", null, 220.40m, false, "P-1234", null,
                "confirmed", "official", null, Quantity: 1, Materials: 12.50m,
                Origin: origin,
                SourceDocumentIdentity: "estimate-import:estimate-canonical-save",
                SourceDocumentVersionId: documentVersionId,
                SourceDocumentSha256: documentSha,
                SourceRowIdentity: "parts:1",
                AmendedBy: engineer.SubjectId,
                AmendedAtUtc: amendedAtUtc),
            new EstimateLineInput("repair", null, "Repair nearside door", 2.5m, null, false, null, null,
                "confirmed", "judgement", null, Materials: 25m),
        };
        var source = new RepairSpecificationSource(RepairSpecificationSourceRoute.Manual, null, null, null);

        var lease = await harness.AcquireLeaseAsync(caseId, 0, engineer, "estimate-canonical-lease");
        var saved = await save.ExecuteAsync(
            new(caseId, lease.Version, engineer, "estimate-canonical-save",
                "Recorded the repairer's estimate.", lease.Token, null, details,
                lines, source)
            {
                SelectedRateCardId = rateCard.Id,
                SelectedRateCardVersion = rateCard.Version,
            },
            CancellationToken.None);

        var read = (await harness.RepairSpecifications.GetVersionAsync(
            caseId, saved.SpecificationId, CancellationToken.None))!;
        var initialRateSnapshot = new EstimateRateSnapshot(
            rateCard.Id, rateCard.Version, rateCard.HourlyRate);
        Assert.Equal(details.Discounts, read.Details.Discounts);
        Assert.Equal(details.Vat, read.Details.Vat);
        Assert.Equal(initialRateSnapshot, read.Details.Rate);
        // One rate column: the snapshot's rate is the estimate's labour rate,
        // never a second stored figure that could disagree with it.
        Assert.Equal(52.50m, read.Details.LabourRate);
        Assert.Equal(52.50m, read.Details.HourlyRate);

        var editLease = await harness.AcquireLeaseAsync(
            caseId, 1, engineer, "estimate-canonical-edit-lease");
        await Assert.ThrowsAsync<ArgumentException>(() => save.ExecuteAsync(
            new(caseId, editLease.Version, engineer, "estimate-canonical-forged-rate",
                "Attempt a forged rate snapshot.", editLease.Token, saved.SpecificationId,
                read.Details with
                {
                    Rate = new EstimateRateSnapshot(Guid.NewGuid(), 1, read.Details.LabourRate ?? 0m)
                },
                lines, source,
                ExistingLineIds: read.Lines.Select(line => (Guid?)line.Id).ToArray()),
            CancellationToken.None));

        var retiredRateCard = await rateCards.SaveAsync(
            new(rateCard.Id, rateCard.Name, 80m, false, rateCard.Version, rateCardAdministrator,
                "Retire and revise rate", "estimate-canonical-rate-retire"),
            CancellationToken.None);
        Assert.False(retiredRateCard.Enabled);
        Assert.Equal(80m, retiredRateCard.HourlyRate);

        var resaved = await save.ExecuteAsync(
            new(caseId, editLease.Version, engineer, "estimate-canonical-edit",
                "Retain the estimate's recorded rate.", editLease.Token, saved.SpecificationId,
                read.Details, lines, source,
                ExistingLineIds: read.Lines.Select(line => (Guid?)line.Id).ToArray()),
            CancellationToken.None);
        Assert.Equal(initialRateSnapshot, resaved.Details.Rate);
        Assert.Equal(52.50m, resaved.Details.LabourRate);
        Assert.Equal(52.50m, resaved.Details.HourlyRate);

        Assert.Equal(
            "NotRegistered",
            await harness.Database.ScalarAsync<string>(
                "SELECT RepairerVatStatus FROM CaseRepairSpecifications "
                + $"WHERE Id = '{saved.SpecificationId:D}'"));

        var part = read.Lines[0];
        Assert.Equal(12.50m, part.Materials);
        Assert.Equal(origin, part.Origin);
        Assert.Equal("estimate-import:estimate-canonical-save", part.SourceDocumentIdentity);
        Assert.Equal(documentVersionId, part.SourceDocumentVersionId);
        Assert.Equal(documentSha, part.SourceDocumentSha256);
        Assert.Equal("parts:1", part.SourceRowIdentity);
        Assert.Equal(engineer.SubjectId, part.AmendedBy);
        Assert.Equal(amendedAtUtc, part.AmendedAtUtc);
        var labour = read.Lines[1];
        Assert.Null(labour.Origin);
        Assert.Null(labour.SourceRowIdentity);
        Assert.Null(labour.AmendedBy);

        var totals = EstimateTotals.Compute(read);
        var breakdown = JsonSerializer.Deserialize<EstimateCalculationBreakdown>(
            await harness.Database.ScalarAsync<string>(
                "SELECT CalculationBreakdownJson FROM CaseRepairSpecifications "
                + $"WHERE Id = '{saved.SpecificationId:D}'"),
            EfRepairSpecificationStore.JsonOptions)!;
        Assert.Equal(RepairSpecificationPolicy.PolicyVersion, breakdown.CalculationPolicyVersion);
        Assert.Equal(20m, breakdown.VatPercent);
        Assert.Equal(totals.Raw, breakdown.Raw);
        Assert.Equal(totals.Printed, breakdown.Printed);
        // A repairer who is not VAT registered charges parts and materials
        // and never labour, so the printed VAT stands on that base alone.
        Assert.Equal(
            decimal.Round(
                (totals.Raw.Parts + totals.Raw.Materials) * 20m / 100m,
                2,
                MidpointRounding.AwayFromZero),
            totals.Printed.Vat);
    }

    /// <summary>
    /// An accepted estimate is a frozen report input: it is duplicated to be
    /// revised, never edited, and the copy's later acceptance leaves the
    /// original's calculation policy version, its basis and its recorded
    /// breakdown exactly as they were.
    /// </summary>
    [Fact]
    public async Task AnAcceptedEstimatesBreakdownAndPolicyVersionSurviveALaterRevision()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("estimate-frozen-accept");
        var caseId = outcome.Identity.CaseId;
        var engineer = harness.EngineerActor;
        var jobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var save = new SaveEstimate(harness.RepairSpecifications, jobs, harness.Clock);
        var duplicate = new DuplicateEstimate(harness.RepairSpecifications);
        var setCurrent = new SetCurrentEstimate(
            harness.RepairSpecifications, jobs, new ConfirmAiJob(jobs), harness.Clock);
        long version = 0;

        async Task<CaseEditLease> LeaseAsync(string key) =>
            await harness.AcquireLeaseAsync(caseId, version, engineer, key);

        var saveLease = await LeaseAsync("estimate-frozen-lease-save");
        var original = await save.ExecuteAsync(
            new(caseId, saveLease.Version, engineer, "estimate-frozen-save",
                "Recorded the repairer's estimate.", saveLease.Token, null,
                new("Repairer", 40m, 0m, 20m,
                    Vat: EstimateVatPolicy.For(RepairerVatStatus.Registered)),
                [
                    new("new_part", null, "Door skin", null, 220.40m, false, "P-1234", null,
                        "confirmed", "official", null, Quantity: 1, Materials: 25m),
                    new("repair", null, "Repair nearside door", 2.5m, null, false, null, null,
                        "confirmed", "judgement", null),
                ],
                new(RepairSpecificationSourceRoute.Manual, null, null, null)),
            CancellationToken.None);
        version++;

        var useLease = await LeaseAsync("estimate-frozen-lease-use");
        var accepted = await setCurrent.ExecuteAsync(
            new(caseId, useLease.Version, engineer, "estimate-frozen-use",
                "Use the repairer's estimate.", useLease.Token, original.SpecificationId),
            CancellationToken.None);
        version++;
        var acceptedBasis = accepted.CalculationBasis!;
        var acceptedBreakdown = await ReadBreakdownJsonAsync(harness, original.SpecificationId);
        Assert.Equal(
            $"repair-specification/v{RepairSpecificationPolicy.PolicyVersion}",
            acceptedBasis.PolicyVersion);
        Assert.Equal(EstimateTotals.Compute(accepted).Printed, acceptedBasis.Printed);

        // Revision is a duplicate with its own figures, made current in turn.
        var copyLease = await LeaseAsync("estimate-frozen-lease-copy");
        var copy = await duplicate.ExecuteAsync(
            new(caseId, copyLease.Version, engineer, "estimate-frozen-duplicate",
                "Revise the repairer's figures.", copyLease.Token, original.SpecificationId),
            CancellationToken.None);
        version++;
        var editLease = await LeaseAsync("estimate-frozen-lease-edit");
        await save.ExecuteAsync(
            new(caseId, editLease.Version, engineer, "estimate-frozen-edit",
                "Repriced at the agreed rate.", editLease.Token, copy.SpecificationId,
                copy.Details with { LabourRate = 55m },
                [
                    new("new_part", null, "Door skin", null, 310.00m, false, "P-1234", null,
                        "confirmed", "official", null, Quantity: 1),
                    new("repair", null, "Repair nearside door", 4m, null, false, null, null,
                        "confirmed", "judgement", null),
                ],
                new(RepairSpecificationSourceRoute.Manual, null, null, null)),
            CancellationToken.None);
        version++;
        // The edit replaced the copy's whole line list, and the breakdown it
        // recorded is the calculation over the replacement, not the one the
        // duplicate started from.
        var editedCopy = (await harness.RepairSpecifications.GetVersionAsync(
            caseId, copy.SpecificationId, CancellationToken.None))!;
        var editedBreakdown = JsonSerializer.Deserialize<EstimateCalculationBreakdown>(
            await ReadBreakdownJsonAsync(harness, copy.SpecificationId),
            EfRepairSpecificationStore.JsonOptions)!;
        Assert.Equal(EstimateTotals.Compute(editedCopy).Raw, editedBreakdown.Raw);
        Assert.Equal(EstimateTotals.Compute(editedCopy).Printed, editedBreakdown.Printed);

        var useCopyLease = await LeaseAsync("estimate-frozen-lease-use-copy");
        var revised = await setCurrent.ExecuteAsync(
            new(caseId, useCopyLease.Version, engineer, "estimate-frozen-use-copy",
                "Use the revised estimate.", useCopyLease.Token, copy.SpecificationId),
            CancellationToken.None);
        version++;
        Assert.NotEqual(acceptedBasis.Total, revised.CalculationBasis!.Total);

        var reread = (await harness.RepairSpecifications.GetVersionAsync(
            caseId, original.SpecificationId, CancellationToken.None))!;
        Assert.Equal(RepairSpecificationState.Accepted, reread.State);
        Assert.False(reread.IsCurrent);
        Assert.Equal(acceptedBasis, reread.CalculationBasis);
        Assert.Equal(
            acceptedBreakdown,
            await ReadBreakdownJsonAsync(harness, original.SpecificationId));
        var acceptedTotals = JsonSerializer.Deserialize<EstimateCalculationBreakdown>(
            acceptedBreakdown, EfRepairSpecificationStore.JsonOptions)!;
        Assert.Equal(acceptedTotals.Raw, reread.RecordedTotals!.Raw);
        Assert.Equal(acceptedTotals.Printed, reread.RecordedTotals.Printed);
        Assert.Equal(acceptedTotals.VatPercent, reread.RecordedTotals.VatPercent);
        Assert.Equal(
            acceptedTotals.CalculationPolicyVersion,
            reread.RecordedTotals.CalculationPolicyVersion);
        Assert.Equal(version, (await LeaseAsync("estimate-frozen-lease-final")).Version);
    }

    /// <summary>
    /// Unknown is a real recorded state, not a missing value: an estimate
    /// whose repairer VAT position was never recorded cannot be made Current
    /// at the store boundary until the position is recorded, or until the
    /// categories are selected by hand.
    /// </summary>
    [Fact]
    public async Task AnUnknownRepairerVatStatusNoLongerBlocksUseAsCurrent()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("estimate-vat-accept");
        var caseId = outcome.Identity.CaseId;
        var engineer = harness.EngineerActor;
        var jobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var save = new SaveEstimate(harness.RepairSpecifications, jobs, harness.Clock);
        var setCurrent = new SetCurrentEstimate(
            harness.RepairSpecifications, jobs, new ConfirmAiJob(jobs), harness.Clock);
        long version = 0;

        async Task<CaseEditLease> LeaseAsync(string key) =>
            await harness.AcquireLeaseAsync(caseId, version, engineer, key);

        Task<RepairSpecificationVersion> SaveAsync(
            CaseEditLease lease, string key, Guid? estimateId, string name, EstimateVatPolicy? vat) =>
            save.ExecuteAsync(
                new(caseId, lease.Version, engineer, key, "Recorded an estimate.", lease.Token,
                    estimateId,
                    new(name, 40m, 0m, 20m, null, vat, null),
                    [
                        new("new_part", null, "Door skin", null, 220.40m, false, "P-1234", null,
                            "confirmed", "official", null, Quantity: 1),
                    ],
                    new(RepairSpecificationSourceRoute.Manual, null, null, null)),
                CancellationToken.None);

        // A save that records no policy at all is Unknown too — the VAT
        // percentage never states a repairer's VAT position (B08), so the
        // row reads back Unknown and charges VAT on nothing.
        var unrecordedLease = await LeaseAsync("estimate-vat-lease-unrecorded");
        var unrecorded = await SaveAsync(
            unrecordedLease, "estimate-vat-save-unrecorded", null, "Unrecorded", vat: null);
        version++;
        var unrecordedRead = (await harness.RepairSpecifications.GetVersionAsync(
            caseId, unrecorded.SpecificationId, CancellationToken.None))!;
        Assert.Equal(RepairerVatStatus.Unknown, unrecordedRead.Details.VatPolicy.RepairerStatus);
        Assert.Equal(EstimateVatCategories.None, unrecordedRead.Details.VatPolicy.Categories);
        Assert.False(unrecordedRead.Details.VatPolicy.CategoriesOverridden);
        Assert.True(unrecordedRead.Details.VatPolicy.TreatmentPending);
        Assert.Equal(20m, unrecordedRead.Details.VatPercent);
        Assert.Equal(0m, EstimateTotals.Compute(unrecordedRead).Printed.Vat);

        var unknownLease = await LeaseAsync("estimate-vat-lease-unknown");
        var unknown = await SaveAsync(
            unknownLease, "estimate-vat-save-unknown", null, "Repairer",
            EstimateVatPolicy.For(RepairerVatStatus.Unknown));
        version++;
        Assert.Equal(RepairerVatStatus.Unknown, unknown.Details.VatPolicy.RepairerStatus);
        Assert.True(unknown.Details.VatPolicy.TreatmentPending);

        // v28 P10: the unknown status no longer refuses Use as Current; the
        // accepted basis simply carries no VAT.
        var unknownUseLease = await LeaseAsync("estimate-vat-lease-use-unknown");
        var unknownCurrent = await setCurrent.ExecuteAsync(
            new(caseId, unknownUseLease.Version, engineer, "estimate-vat-use-unknown",
                "Use the estimate.", unknownUseLease.Token, unknown.SpecificationId),
            CancellationToken.None);
        version++;
        Assert.True(unknownCurrent.IsCurrent);
        Assert.Equal(0m, unknownCurrent.CalculationBasis!.Vat);

        // Recording the status on a later edit is an ordinary header change.
        var recordedLease = await LeaseAsync("estimate-vat-lease-recorded");
        var recorded = await SaveAsync(
            recordedLease, "estimate-vat-save-recorded", null, "Repairer",
            EstimateVatPolicy.For(RepairerVatStatus.Registered));
        version++;
        Assert.Equal(EstimateVatCategories.All, recorded.Details.VatPolicy.Categories);
        var recordedUseLease = await LeaseAsync("estimate-vat-lease-use-recorded");
        var current = await setCurrent.ExecuteAsync(
            new(caseId, recordedUseLease.Version, engineer, "estimate-vat-use-recorded",
                "Use the estimate.", recordedUseLease.Token, recorded.SpecificationId),
            CancellationToken.None);
        version++;
        Assert.True(current.IsCurrent);

        // So does selecting the categories by hand while the status stays unknown.
        var overrideLease = await LeaseAsync("estimate-vat-lease-override");
        var overridden = await SaveAsync(
            overrideLease, "estimate-vat-save-override", null, "Repairer's own",
            new EstimateVatPolicy(
                RepairerVatStatus.Unknown,
                EstimateVatCategories.Parts | EstimateVatCategories.Materials,
                true));
        version++;
        var reread = (await harness.RepairSpecifications.GetVersionAsync(
            caseId, overridden.SpecificationId, CancellationToken.None))!;
        Assert.Equal(RepairerVatStatus.Unknown, reread.Details.Vat!.RepairerStatus);
        Assert.True(reread.Details.Vat.CategoriesOverridden);
        Assert.False(reread.Details.Vat.TreatmentPending);

        var overrideUseLease = await LeaseAsync("estimate-vat-lease-use-override");
        var overriddenCurrent = await setCurrent.ExecuteAsync(
            new(caseId, overrideUseLease.Version, engineer, "estimate-vat-use-override",
                "Use the Engineer's own estimate.", overrideUseLease.Token,
                overridden.SpecificationId),
            CancellationToken.None);
        version++;
        Assert.True(overriddenCurrent.IsCurrent);
        Assert.Equal(version, (await LeaseAsync("estimate-vat-lease-final")).Version);
    }

    private static Task<string> ReadBreakdownJsonAsync(Harness harness, Guid specificationId) =>
        harness.Database.ScalarAsync<string>(
            "SELECT CalculationBreakdownJson FROM CaseRepairSpecifications "
            + $"WHERE Id = '{specificationId:D}'");

    /// <summary>
    /// Stream A review (comments 5560764306/5560667174, one staleness root
    /// cause): adopting an Engineer's Value through the Case save changes
    /// frozen report inputs — the confirmed Engineer's Value field and the
    /// applied valuation — so it stales the Case's current generation inside
    /// the save's own transaction, a replay returns before staling, and a
    /// superseded generation never moves.
    /// </summary>
    [Fact]
    public async Task AdoptingAValuationThroughTheCaseSaveStalesOnlyTheCurrentGeneration()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("valuation-stale-accept");
        var caseId = outcome.Identity.CaseId;
        var engineer = harness.EngineerActor;
        var workspace = new EfCaseWorkspaceStore(harness.Factory, harness.Clock);

        // The guide card, recorded with the Case's own mileage an adopted
        // Engineer's Value carries (operator, 24 September 2026).
        var guideLease = await harness.AcquireLeaseAsync(caseId, 0, engineer, "valuation-stale-guide-lease");
        var guideSaved = await workspace.SaveAsync(
            new SaveCaseWorkspaceRequest(
                caseId,
                guideLease.Version,
                engineer,
                "valuation-stale-guide",
                null,
                guideLease.Token)
            {
                Vehicle = new(
                    null,
                    null,
                    null,
                    new(42_000, CaseOdometerUnit.Miles, CaseVehicleMileageSourcePolicy.Owner, null),
                    new Dictionary<string, string?>(StringComparer.Ordinal)),
                Valuation = new(
                [
                    new(
                        ValuationSource.Glasses,
                        new DateOnly(2031, 5, 8),
                        new TimeOnly(9, 0),
                        42000,
                        12000m,
                        10000m,
                        new DateOnly(2031, 5, 1))
                ])
            },
            CancellationToken.None);
        var guideId = Assert.Single(await harness.Valuations.ListForCaseAsync(
            caseId,
            CaseWorkSelector.Current,
            CancellationToken.None)).ValuationId;

        var (currentId, supersededId) = await SeedGenerationsAsync(harness, caseId);

        var adoptLease = await harness.AcquireLeaseAsync(
            caseId, guideSaved.Version, engineer, "valuation-stale-adopt-lease");
        var adoptRequest = AdoptRequest(
            adoptLease,
            new ValuationCalculationSelection(guideId, false, null, [], 0m),
            "valuation-stale-adopt");
        var adopted = await workspace.SaveAsync(adoptRequest, CancellationToken.None);

        Assert.Equal("Stale", await harness.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentId:D}'"));
        Assert.Equal("Confirmed", await harness.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{supersededId:D}'"));
        Assert.Equal(1, await StaleRowCountAsync(harness, caseId));
        Assert.Equal(CaseReportStaleReasons.ValuationChanged, await LatestStaleReasonAsync(harness, caseId));

        // Replay of the same operation returns before any mutation, so the
        // stale row count does not move.
        var replayed = await workspace.SaveAsync(adoptRequest, CancellationToken.None);
        Assert.True(replayed.WasReplay);
        Assert.Equal(1, await StaleRowCountAsync(harness, caseId));

        // A fresh current generation goes stale on a different adoption the
        // same way.
        await harness.Database.ExecuteAsync(
            $"UPDATE CaseReportGenerations SET State = 'Confirmed' WHERE Id = '{currentId:D}'");
        harness.Advance(TimeSpan.FromMinutes(1));
        var readoptLease = await harness.AcquireLeaseAsync(
            caseId, adopted.Version, engineer, "valuation-stale-readopt-lease");
        await workspace.SaveAsync(
            AdoptRequest(
                readoptLease,
                new ValuationCalculationSelection(guideId, false, null, [], ConditionDeduction: 100m),
                "valuation-stale-readopt"),
            CancellationToken.None);

        Assert.Equal("Stale", await harness.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentId:D}'"));
        Assert.Equal("Confirmed", await harness.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{supersededId:D}'"));
        Assert.Equal(2, await StaleRowCountAsync(harness, caseId));
        Assert.Equal(CaseReportStaleReasons.ValuationChanged, await LatestStaleReasonAsync(harness, caseId));

        SaveCaseWorkspaceRequest AdoptRequest(
            CaseEditLease lease,
            ValuationCalculationSelection selection,
            string operationKey) => new(
            caseId,
            lease.Version,
            engineer,
            operationKey,
            null,
            lease.Token)
        {
            Valuation = new([], selection)
        };
    }

    private static async Task<(Guid CurrentId, Guid SupersededId)> SeedGenerationsAsync(
        Harness harness,
        Guid caseId)
    {
        await using var context = await harness.Factory.CreateDbContextAsync();
        var currentId = Guid.NewGuid();
        var supersededId = Guid.NewGuid();
        context.AddRange(
            new CaseReportGenerationEntity
            {
                Id = supersededId,
                CaseId = caseId,
                WorkId = caseId,
                CaseVersion = 0,
                SnapshotHash = new string('1', 64),
                SnapshotJson = ReportGenerationSnapshotFixture.Json(caseId, "seed-generation-superseded"),
                TemplateVersion = "assessment-report/v1",
                RendererVersion = "renderer/v1",
                State = nameof(CaseReportGenerationState.Confirmed),
                GeneratedAtUtc = new DateTimeOffset(2031, 5, 6, 9, 0, 0, TimeSpan.Zero),
                Version = 1
            },
            new CaseReportGenerationEntity
            {
                Id = currentId,
                CaseId = caseId,
                WorkId = caseId,
                CaseVersion = 0,
                SnapshotHash = new string('2', 64),
                SnapshotJson = ReportGenerationSnapshotFixture.Json(caseId, "seed-generation-current"),
                TemplateVersion = "assessment-report/v1",
                RendererVersion = "renderer/v1",
                State = nameof(CaseReportGenerationState.Confirmed),
                GeneratedAtUtc = new DateTimeOffset(2031, 5, 6, 10, 0, 0, TimeSpan.Zero),
                Version = 1
            });
        await context.SaveChangesAsync();
        await harness.Database.ExecuteAsync(
            $"UPDATE CaseReportGenerations SET SupersededById = '{Guid.NewGuid():D}' WHERE Id = '{supersededId:D}'");
        return (currentId, supersededId);
    }

    private static Task<int> StaleRowCountAsync(Harness harness, Guid caseId) =>
        harness.Database.ScalarAsync<int>(
            $"SELECT COUNT(*) FROM ActionHistory WHERE AggregateType = 'case' AND AggregateId = '{caseId:D}' AND EventKind = 'case_report_generation_stale'");

    private static Task<string> LatestStaleReasonAsync(Harness harness, Guid caseId) =>
        harness.Database.ScalarAsync<string>(
            $"SELECT TOP (1) Reason FROM ActionHistory WHERE AggregateType = 'case' AND AggregateId = '{caseId:D}' AND EventKind = 'case_report_generation_stale' ORDER BY OccurredAtUtc DESC, Id DESC");

    [Fact]
    public async Task ValuationPortsResolveFromProductionComposition()
    {
        var artifactRoot = Path.Combine(
            Path.GetTempPath(),
            $"pegasus-market-research-composition-{Guid.NewGuid():N}");
        await using var database = await LocalDbTestDatabase.CreateAsync(
            localArtifactRootFactory: _ => artifactRoot);
        await using var scope = database.CreateAsyncScope();

        Assert.IsType<EfValuationStore>(
            scope.ServiceProvider.GetRequiredService<IValuationStore>());
        Assert.IsType<ListCaseValuations>(
            scope.ServiceProvider.GetRequiredService<IListCaseValuations>());
        Assert.IsType<EfMarketResearchAiJobCompletionStore>(
            scope.ServiceProvider.GetRequiredService<IMarketResearchAiJobCompletionStore>());
        Assert.IsType<CompleteMarketResearchAiJob>(
            scope.ServiceProvider.GetRequiredService<ICompleteMarketResearchAiJob>());
    }

    [Fact]
    public async Task MarketResearchCompletionAtomicallyRetainsEvidenceValuationAndDraftReadyJob()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("market-research-accept-case");
        var caseId = outcome.Identity.CaseId;
        await SetReportPreparationAsync(harness.Factory, caseId);
        var automation = harness.AutomationActor;
        var aiJobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var created = await aiJobs.CreateAsync(
            new(
                AiJobKind.MarketResearch,
                AiJobSubjectKind.Case,
                caseId,
                outcome.Identity.Reference,
                "Research comparable vehicles.",
                null,
                null,
                harness.EngineerActor,
                "market-research-create",
                AiJobPolicy.DefaultExpiry),
            CancellationToken.None);
        var taken = await aiJobs.TransitionAsync(
            new(
                created.JobId,
                created.Version,
                AiJobState.Taken,
                automation,
                "market-research-take",
                LeaseExpiresAtUtc: harness.Clock.GetUtcNow() + AiJobPolicy.LeaseDuration),
            CancellationToken.None);
        var lease = await harness.AcquireLeaseAsync(
            caseId,
            0,
            automation,
            "market-research-case-lease");
        var content = new MarketResearchDocumentContentStore();
        var complete = new CompleteMarketResearchAiJob(
            new EfMarketResearchAiJobCompletionStore(harness.Factory, content, harness.Clock));
        var command = new CompleteMarketResearchAiJobCommand(
            taken.JobId,
            taken.Version,
            caseId,
            lease.Version,
            lease.Token,
            automation,
            "market-research-complete",
            "market-research.pdf",
            "application/pdf",
            new byte[] { 1, 2, 3 },
            new DateOnly(2031, 5, 6),
            new TimeOnly(10, 30),
            42000,
            12000m,
            10000m);

        var completed = await complete.ExecuteAsync(command, CancellationToken.None);
        var replay = await complete.ExecuteAsync(command, CancellationToken.None);

        Assert.Equal(AiJobState.DraftReady, completed.Job.State);
        Assert.Equal(AiJobResultKind.MarketResearch, completed.Job.ResultKind);
        Assert.Equal(ValuationSource.AiMarketResearch, completed.Valuation.Details.Source);
        Assert.Equal(completed.Document.Occurrence.Id, replay.Document.Occurrence.Id);
        Assert.Equal(completed.Valuation.ValuationId, replay.Valuation.ValuationId);
        Assert.True(replay.IsReplay);
        Assert.Equal(1, content.StoreCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            complete.ExecuteAsync(command with { RetailValue = 12001m }, CancellationToken.None));

        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(1, await context.CaseValuations.CountAsync(item => item.WorkId == caseId));
        Assert.Equal(1, await context.Set<DocumentOccurrenceEntity>().CountAsync(item => item.CaseId == caseId));
        await Assert.ThrowsAnyAsync<DbException>(async () =>
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE AiJobs SET MarketResearchMileage = NULL WHERE JobId = {taken.JobId}"));
        Assert.Null(await ReadEngineersValueAsync(harness, caseId));
        Assert.Equal(ActorKind.Automation.ToString(), await context.ActionHistory
            .Where(item => item.AggregateType == "ai_job"
                && item.AggregateId == taken.JobId.ToString("D")
                && item.EventKind == "ai_job_draft_ready")
            .OrderByDescending(item => item.OccurredAtUtc)
            .Select(item => item.ActorKind)
            .FirstAsync());
    }

    [Fact]
    public async Task MarketResearchCompletionWithAStaleCaseVersionWritesNothing()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("market-research-stale-case-accept");
        var caseId = outcome.Identity.CaseId;
        await SetReportPreparationAsync(harness.Factory, caseId);
        var automation = harness.AutomationActor;
        var aiJobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var created = await aiJobs.CreateAsync(
            new(
                AiJobKind.MarketResearch,
                AiJobSubjectKind.Case,
                caseId,
                outcome.Identity.Reference,
                "Research comparable vehicles.",
                null,
                null,
                harness.EngineerActor,
                "market-research-stale-case-create",
                AiJobPolicy.DefaultExpiry),
            CancellationToken.None);
        var taken = await aiJobs.TransitionAsync(
            new(
                created.JobId,
                created.Version,
                AiJobState.Taken,
                automation,
                "market-research-stale-case-take",
                LeaseExpiresAtUtc: harness.Clock.GetUtcNow() + AiJobPolicy.LeaseDuration),
            CancellationToken.None);
        var lease = await harness.AcquireLeaseAsync(
            caseId,
            0,
            automation,
            "market-research-stale-case-lease");
        var content = new MarketResearchDocumentContentStore();
        var complete = new CompleteMarketResearchAiJob(
            new EfMarketResearchAiJobCompletionStore(harness.Factory, content, harness.Clock));
        var command = new CompleteMarketResearchAiJobCommand(
            taken.JobId,
            taken.Version,
            caseId,
            lease.Version + 1,
            lease.Token,
            automation,
            "market-research-stale-case-complete",
            "market-research.pdf",
            "application/pdf",
            new byte[] { 1, 2, 3 },
            new DateOnly(2031, 5, 6),
            new TimeOnly(10, 30),
            42000,
            12000m,
            10000m);

        await Assert.ThrowsAsync<CaseVersionConflictException>(() =>
            complete.ExecuteAsync(command, CancellationToken.None));

        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(0, content.StoreCount);
        Assert.Equal(0, await context.CaseValuations.CountAsync(item => item.WorkId == caseId));
        Assert.Equal(0, await context.Set<DocumentOccurrenceEntity>().CountAsync(item => item.CaseId == caseId));
        Assert.Equal(AiJobState.Taken.ToString(), await context.AiJobs
            .Where(item => item.JobId == taken.JobId)
            .Select(item => item.State)
            .SingleAsync());
    }

    [Fact]
    public async Task MarketResearchCompletionTreatsALapsedLeaseAsQueuedAndWritesNothing()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("market-research-lapsed-lease-accept");
        var caseId = outcome.Identity.CaseId;
        await SetReportPreparationAsync(harness.Factory, caseId);
        var automation = harness.AutomationActor;
        var aiJobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var created = await aiJobs.CreateAsync(
            new(
                AiJobKind.MarketResearch,
                AiJobSubjectKind.Case,
                caseId,
                outcome.Identity.Reference,
                "Research comparable vehicles.",
                null,
                null,
                harness.EngineerActor,
                "market-research-lapsed-lease-create",
                AiJobPolicy.DefaultExpiry),
            CancellationToken.None);
        var taken = await aiJobs.TransitionAsync(
            new(
                created.JobId,
                created.Version,
                AiJobState.Taken,
                automation,
                "market-research-lapsed-lease-take",
                LeaseExpiresAtUtc: harness.Clock.GetUtcNow() + AiJobPolicy.LeaseDuration),
            CancellationToken.None);
        harness.Advance(AiJobPolicy.LeaseDuration + TimeSpan.FromSeconds(1));

        var content = new MarketResearchDocumentContentStore();
        var complete = new CompleteMarketResearchAiJob(
            new EfMarketResearchAiJobCompletionStore(harness.Factory, content, harness.Clock));
        var command = new CompleteMarketResearchAiJobCommand(
            taken.JobId,
            taken.Version,
            caseId,
            0,
            "unused-lease-token",
            automation,
            "market-research-lapsed-lease-complete",
            "market-research.pdf",
            "application/pdf",
            new byte[] { 1, 2, 3 },
            new DateOnly(2031, 5, 6),
            new TimeOnly(10, 30),
            42000,
            12000m,
            10000m);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            complete.ExecuteAsync(command, CancellationToken.None));
        Assert.Equal("An AI job cannot move from Queued to DraftReady.", exception.Message);

        await using var context = await harness.Factory.CreateDbContextAsync();
        var unchanged = await context.AiJobs.AsNoTracking()
            .SingleAsync(item => item.JobId == taken.JobId);
        Assert.Equal(AiJobState.Taken.ToString(), unchanged.State);
        Assert.Equal(taken.Version, unchanged.Version);
        Assert.Equal(taken.TakenBy, unchanged.TakenBy);
        Assert.Equal(taken.LeaseExpiresAtUtc, unchanged.LeaseExpiresAtUtc);
        Assert.Equal(0, content.StoreCount);
        Assert.Equal(0, await context.CaseValuations.CountAsync(item => item.WorkId == caseId));
        Assert.Equal(0, await context.Set<DocumentOccurrenceEntity>().CountAsync(item => item.CaseId == caseId));
    }

    [Fact]
    public async Task MarketResearchCompletionWithAStaleJobVersionWritesNothing()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("market-research-stale-job-accept");
        var caseId = outcome.Identity.CaseId;
        await SetReportPreparationAsync(harness.Factory, caseId);
        var automation = harness.AutomationActor;
        var aiJobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var created = await aiJobs.CreateAsync(
            new(
                AiJobKind.MarketResearch,
                AiJobSubjectKind.Case,
                caseId,
                outcome.Identity.Reference,
                "Research comparable vehicles.",
                null,
                null,
                harness.EngineerActor,
                "market-research-stale-job-create",
                AiJobPolicy.DefaultExpiry),
            CancellationToken.None);
        var taken = await aiJobs.TransitionAsync(
            new(
                created.JobId,
                created.Version,
                AiJobState.Taken,
                automation,
                "market-research-stale-job-take",
                LeaseExpiresAtUtc: harness.Clock.GetUtcNow() + AiJobPolicy.LeaseDuration),
            CancellationToken.None);
        var lease = await harness.AcquireLeaseAsync(
            caseId,
            0,
            automation,
            "market-research-stale-job-lease");
        var content = new MarketResearchDocumentContentStore();
        var complete = new CompleteMarketResearchAiJob(
            new EfMarketResearchAiJobCompletionStore(harness.Factory, content, harness.Clock));
        var command = new CompleteMarketResearchAiJobCommand(
            taken.JobId,
            taken.Version + 1,
            caseId,
            lease.Version,
            lease.Token,
            automation,
            "market-research-stale-job-complete",
            "market-research.pdf",
            "application/pdf",
            new byte[] { 1, 2, 3 },
            new DateOnly(2031, 5, 6),
            new TimeOnly(10, 30),
            42000,
            12000m,
            10000m);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            complete.ExecuteAsync(command, CancellationToken.None));
        Assert.Equal("The AI job changed concurrently; reload and retry.", exception.Message);

        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(0, content.StoreCount);
        Assert.Equal(0, await context.CaseValuations.CountAsync(item => item.WorkId == caseId));
        Assert.Equal(0, await context.Set<DocumentOccurrenceEntity>().CountAsync(item => item.CaseId == caseId));
        var unchanged = await context.AiJobs.AsNoTracking()
            .SingleAsync(item => item.JobId == taken.JobId);
        Assert.Equal(AiJobState.Taken.ToString(), unchanged.State);
        Assert.Equal(taken.Version, unchanged.Version);
    }

    [Fact]
    public async Task MarketResearchCompletionRefusesAnExpiredCaseLeaseWithoutChangingTheJob()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("market-research-expired-lease-accept");
        var caseId = outcome.Identity.CaseId;
        await SetReportPreparationAsync(harness.Factory, caseId);
        var automation = harness.AutomationActor;
        var aiJobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var created = await aiJobs.CreateAsync(
            new(
                AiJobKind.MarketResearch,
                AiJobSubjectKind.Case,
                caseId,
                outcome.Identity.Reference,
                "Research comparable vehicles.",
                null,
                null,
                harness.EngineerActor,
                "market-research-expired-lease-create",
                AiJobPolicy.DefaultExpiry),
            CancellationToken.None);
        var taken = await aiJobs.TransitionAsync(
            new(
                created.JobId,
                created.Version,
                AiJobState.Taken,
                automation,
                "market-research-expired-lease-take",
                LeaseExpiresAtUtc: harness.Clock.GetUtcNow() + AiJobPolicy.LeaseDuration),
            CancellationToken.None);
        var lease = await harness.AcquireLeaseAsync(
            caseId,
            0,
            automation,
            "market-research-expired-lease-lease");
        harness.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        var content = new MarketResearchDocumentContentStore();
        var complete = new CompleteMarketResearchAiJob(
            new EfMarketResearchAiJobCompletionStore(harness.Factory, content, harness.Clock));
        var command = new CompleteMarketResearchAiJobCommand(
            taken.JobId,
            taken.Version,
            caseId,
            lease.Version,
            lease.Token,
            automation,
            "market-research-expired-lease-complete",
            "market-research.pdf",
            "application/pdf",
            new byte[] { 1, 2, 3 },
            new DateOnly(2031, 5, 6),
            new TimeOnly(10, 30),
            42000,
            12000m,
            10000m);

        await Assert.ThrowsAsync<CaseEditLeaseExpiredException>(() =>
            complete.ExecuteAsync(command, CancellationToken.None));

        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(0, content.StoreCount);
        Assert.Equal(0, await context.CaseValuations.CountAsync(item => item.WorkId == caseId));
        Assert.Equal(0, await context.Set<DocumentOccurrenceEntity>().CountAsync(item => item.CaseId == caseId));
        var unchanged = await context.AiJobs.AsNoTracking()
            .SingleAsync(item => item.JobId == taken.JobId);
        Assert.Equal(AiJobState.Taken.ToString(), unchanged.State);
        Assert.Equal(taken.Version, unchanged.Version);
    }

    [Fact]
    public async Task MarketResearchCompletionCompensatesAFailedContentWriteByRemovingTheOrphan()
    {
        var interceptor = new ThrowingCommandInterceptor("[CaseValuations]");
        await using var harness = await Harness.CreateAsync(interceptor);
        var outcome = await harness.AcceptAsync("market-research-content-compensation-accept");
        var caseId = outcome.Identity.CaseId;
        await SetReportPreparationAsync(harness.Factory, caseId);
        var automation = harness.AutomationActor;
        var aiJobs = new EfAiJobStore(harness.Factory, harness.Clock);
        var created = await aiJobs.CreateAsync(
            new(
                AiJobKind.MarketResearch,
                AiJobSubjectKind.Case,
                caseId,
                outcome.Identity.Reference,
                "Research comparable vehicles.",
                null,
                null,
                harness.EngineerActor,
                "market-research-content-compensation-create",
                AiJobPolicy.DefaultExpiry),
            CancellationToken.None);
        var taken = await aiJobs.TransitionAsync(
            new(
                created.JobId,
                created.Version,
                AiJobState.Taken,
                automation,
                "market-research-content-compensation-take",
                LeaseExpiresAtUtc: harness.Clock.GetUtcNow() + AiJobPolicy.LeaseDuration),
            CancellationToken.None);
        var lease = await harness.AcquireLeaseAsync(
            caseId,
            0,
            automation,
            "market-research-content-compensation-lease");
        var content = new MarketResearchDocumentContentStore();
        var complete = new CompleteMarketResearchAiJob(
            new EfMarketResearchAiJobCompletionStore(harness.Factory, content, harness.Clock));
        var command = new CompleteMarketResearchAiJobCommand(
            taken.JobId,
            taken.Version,
            caseId,
            lease.Version,
            lease.Token,
            automation,
            "market-research-content-compensation-complete",
            "market-research.pdf",
            "application/pdf",
            new byte[] { 1, 2, 3 },
            new DateOnly(2031, 5, 6),
            new TimeOnly(10, 30),
            42000,
            12000m,
            10000m);

        // EF Core wraps a provider/interceptor failure raised while
        // SaveChangesAsync executes its command batch in a DbUpdateException;
        // the injected InvalidOperationException survives as its inner
        // exception, so asserting on both proves the failure that triggered
        // compensation was the one this test injected.
        var thrown = await Assert.ThrowsAsync<DbUpdateException>(() =>
            complete.ExecuteAsync(command, CancellationToken.None));
        var injected = Assert.IsType<InvalidOperationException>(thrown.InnerException);
        Assert.Equal(
            "Simulated database failure for the market research content-write compensation test.",
            injected.Message);

        Assert.True(interceptor.InterceptedCount >= 1);
        Assert.Equal(1, content.StoreCount);
        Assert.Equal(1, content.DeleteCount);

        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(0, await context.CaseValuations.CountAsync(item => item.WorkId == caseId));
        Assert.Equal(0, await context.Set<DocumentOccurrenceEntity>().CountAsync(item => item.CaseId == caseId));
        var unchanged = await context.AiJobs.AsNoTracking()
            .SingleAsync(item => item.JobId == taken.JobId);
        Assert.Equal(AiJobState.Taken.ToString(), unchanged.State);
        Assert.Equal(taken.Version, unchanged.Version);
    }

    private static async Task<AssessmentFieldValue?> ReadEngineersValueAsync(
        Harness harness,
        Guid caseId)
    {
        var assessment = Assert.IsType<CaseAssessmentProjection>(
            await new GetCaseAssessment(
                    new EfCaseAssessmentStore(
                        harness.Factory,
                        harness.Clock,
                        harness.RepairSpecifications))
                .ExecuteAsync(caseId, CancellationToken.None));
        return assessment.Field(AssessmentVocabulary.ValueEngineer);
    }

    [Fact]
    public async Task TheAiWorkRequestLifecyclePersistsWithCorrelatedHistory()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-accept-5");
        var caseId = outcome.Identity.CaseId;
        var staff = harness.EngineerActor;

        var created = await harness.WorkRequests.CreateAsync(
            new(
                caseId,
                outcome.Identity.Reference,
                0,
                staff,
                "send-op-1",
                "Work the assessment.",
                TimeSpan.FromHours(24)),
            CancellationToken.None);
        Assert.Equal(AiWorkRequestState.Created, created.State);

        // Creation replays idempotently on the same operation key and
        // conflicts on different material.
        var replay = await harness.WorkRequests.CreateAsync(
            new(
                caseId,
                outcome.Identity.Reference,
                0,
                staff,
                "send-op-1",
                "Work the assessment.",
                TimeSpan.FromHours(24)),
            CancellationToken.None);
        Assert.Equal(created.RequestId, replay.RequestId);
        await Assert.ThrowsAsync<CaseOperationConflictException>(() =>
            harness.WorkRequests.CreateAsync(
                new(
                    caseId,
                    outcome.Identity.Reference,
                    0,
                    staff,
                    "send-op-1",
                    "A different instruction.",
                    TimeSpan.FromHours(24)),
                CancellationToken.None));

        var handedOff = await harness.WorkRequests.TransitionAsync(
            new(created.RequestId, created.Version, AiWorkRequestState.HandedOff, staff, "t-1"),
            CancellationToken.None);
        Assert.Equal(AiWorkRequestState.HandedOff, handedOff.State);
        Assert.NotNull(handedOff.HandedOffAtUtc);

        var completed = await harness.WorkRequests.TransitionAsync(
            new(
                created.RequestId,
                handedOff.Version,
                AiWorkRequestState.Completed,
                staff,
                "t-2",
                ReplyStatus: "done",
                ReplyMessage: "Assessment recorded."),
            CancellationToken.None);
        Assert.Equal(AiWorkRequestState.Completed, completed.State);
        Assert.Equal("Assessment recorded.", completed.ReplyMessage);

        // Completed is terminal: reopening it is an illegal transition, and
        // an exact repeat of the terminal transition replays inertly.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.WorkRequests.TransitionAsync(
                new(
                    created.RequestId,
                    completed.Version,
                    AiWorkRequestState.HandedOff,
                    staff,
                    "t-3"),
                CancellationToken.None));
        var repeat = await harness.WorkRequests.TransitionAsync(
            new(
                created.RequestId,
                completed.Version,
                AiWorkRequestState.Completed,
                staff,
                "t-2"),
            CancellationToken.None);
        Assert.Equal(completed.Version, repeat.Version);

        await using var context = await harness.Factory.CreateDbContextAsync();
        var history = await context.ActionHistory.AsNoTracking()
            .Where(item => item.AggregateType == "ai_work_request")
            .ToArrayAsync();
        Assert.Equal(3, history.Length);
        Assert.All(history, entry =>
            Assert.Equal(created.RequestId.ToString("D"), entry.CorrelationId));
        Assert.Contains(history, entry => entry.EventKind == "ai_work_request_created");
        Assert.Contains(history, entry => entry.EventKind == "ai_work_request_handedoff");
        Assert.Contains(history, entry => entry.EventKind == "ai_work_request_completed");
    }

    private sealed class Harness : IAsyncDisposable
    {
        private readonly LocalDbTestDatabase database;
        private readonly AcquireCaseEditLease acquireLease;
        private readonly AcceptIntake acceptIntake;
        private readonly CaseDataCompletenessPersistenceTests.MutableTimeProvider timeProvider;

        private Harness(
            LocalDbTestDatabase database,
            PooledDbContextFactory<PegasusDbContext> factory,
            Guid receiptId,
            AcceptIntake acceptIntake,
            AcquireCaseEditLease acquireLease,
            SaveAssessment saveAssessment,
            EfAiWorkRequestStore workRequests,
            EfRepairSpecificationStore repairSpecifications,
            EfValuationStore valuations,
            CaseDataCompletenessPersistenceTests.MutableTimeProvider timeProvider)
        {
            this.database = database;
            Factory = factory;
            ReceiptId = receiptId;
            this.acceptIntake = acceptIntake;
            this.acquireLease = acquireLease;
            SaveAssessment = saveAssessment;
            WorkRequests = workRequests;
            RepairSpecifications = repairSpecifications;
            Valuations = valuations;
            this.timeProvider = timeProvider;
        }

        public PooledDbContextFactory<PegasusDbContext> Factory { get; }
        public LocalDbTestDatabase Database => database;
        public Guid ReceiptId { get; }
        public SaveAssessment SaveAssessment { get; }
        public EfAiWorkRequestStore WorkRequests { get; }
        public EfRepairSpecificationStore RepairSpecifications { get; }
        public EfValuationStore Valuations { get; }
        public ActionActor AutomationActor { get; } = ActionActor.Automation("pegasus-automation");
        public ActionActor EngineerActor { get; } =
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

        public ActionActor UserActor { get; } =
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);

        public static async Task<Harness> CreateAsync(DbCommandInterceptor? interceptor = null)
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            try
            {
                var optionsBuilder = new DbContextOptionsBuilder<PegasusDbContext>()
                    .UseSqlServer(database.ConnectionString);
                if (interceptor is not null)
                {
                    optionsBuilder.AddInterceptors(interceptor);
                }
                var options = optionsBuilder.Options;
                var factory = new PooledDbContextFactory<PegasusDbContext>(options);
                var timeProvider = new CaseDataCompletenessPersistenceTests.MutableTimeProvider(
                    StartUtc);
                var receiptId = Guid.NewGuid();
                await SeedAsync(factory, receiptId);
                var acceptanceStore = new EfCaseAcceptanceStore(factory, timeProvider, []);
                var workflowStore = new EfCaseWorkflowStore(factory, timeProvider);
                var repairSpecifications = new EfRepairSpecificationStore(factory, timeProvider);
                var valuations = new EfValuationStore(factory);
                return new(
                    database,
                    factory,
                    receiptId,
                    new AcceptIntake(
                        acceptanceStore,
                        new FixedConfiguration(),
                        new EfProviderInspectionModeStore(factory),
                        new DiscardingCommittedWorkPublisher(),
                        new TriageCasePairing(new EfTriageStore(factory,
                            [new PrincipalCaseMatchPolicy(new QdosInstructionExtractionPolicy())], timeProvider))),
                    new AcquireCaseEditLease(workflowStore),
                    new SaveAssessment(
                        new EfCaseAssessmentStore(factory, timeProvider, repairSpecifications)),
                    new EfAiWorkRequestStore(factory, timeProvider),
                    repairSpecifications,
                    valuations,
                    timeProvider);
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public void Advance(TimeSpan interval) => timeProvider.Advance(interval);

        public TimeProvider Clock => timeProvider;

        public Task<CaseAcceptanceOutcome> AcceptAsync(string operationKey) =>
            acceptIntake.ExecuteAsync(
                new(
                    ReceiptId,
                    0,
                    ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
                    operationKey,
                    CaseType.Inspection,
                    "QDOS",
                    new(true, true),
                    AcceptedInspectionDeadline: new DateOnly(2031, 5, 20)),
                CancellationToken.None);

        public Task<CaseEditLease> AcquireLeaseAsync(
            Guid caseId,
            long version,
            ActionActor actor,
            string operationKey) => acquireLease.ExecuteAsync(
            new(caseId, version, actor, operationKey),
            CancellationToken.None);

        public async ValueTask DisposeAsync() => await database.DisposeAsync();

        private static async Task SeedAsync(
            IDbContextFactory<PegasusDbContext> factory,
            Guid receiptId)
        {
            await using var context = await factory.CreateDbContextAsync();
            var principal = await SeededPrincipals.QdosAsync(context);
            var organizationId = principal.OrganizationId;
            var lineageId = principal.SequenceLineageId;
            var principalId = principal.Id;
            var sourceHash = new string('d', 64);
            var fieldsJson =
                """{"version":1,"data":[{"name":"Claimant name","suggestedValue":"Mrs Jane Example","candidates":[{"value":"Mrs Jane Example","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Claim number","suggestedValue":"ABC/DEF/12345/1","candidates":[{"value":"ABC/DEF/12345/1","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Vehicle registration","suggestedValue":"AB12 CDE","candidates":[{"value":"AB12 CDE","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Date of incident","suggestedValue":"2031-04-01","candidates":[{"value":"2031-04-01","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection address","suggestedValue":"1 Test Street, London","candidates":[{"value":"1 Test Street, London","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false},{"name":"Inspection date","suggestedValue":"2031-05-20","candidates":[{"value":"2031-05-20","source":"pdf_content","sourceLabel":"instructions.pdf"}],"isDefaulted":false,"hasConflict":false}]}""";
            var emptyEnvelope = """{"version":1,"data":[]}""";

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeReceipts (Id, SourceFileName, MediaType, SourceLength, SourceHash, SourceChannel, ExternalReceiptToken, ReceivedAtUtc, ProcessedAtUtc, SourceReaderKey, SourceReaderVersion, ExtractionPolicyKey, ExtractionPolicyVersion, Version, Decision, DecisionReason, EvidenceJson, FieldsJson, OcrCandidatesJson) VALUES ({receiptId}, {"assessment.eml"}, {"message/rfc822"}, {100L}, {sourceHash}, {"mailbox"}, {"assessment-item-1"}, {StartUtc}, {StartUtc}, {"fixture-reader"}, {"1"}, {"qdos_instruction"}, {1}, {0L}, {"case_created"}, {"Ready fixture"}, {emptyEnvelope}, {fieldsJson}, {emptyEnvelope})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO InstructionDrafts (IntakeReceiptId, SuggestedPrincipalCode, ClaimantName, ClaimNumber, VehicleRegistration, DateOfIncident, InspectionAddress, InspectionDate) VALUES ({receiptId}, {"QDOS"}, {"Mrs Jane Example"}, {"ABC/DEF/12345/1"}, {"AB12CDE"}, {new DateOnly(2031, 4, 1)}, {"1 Test Street, London"}, {new DateOnly(2031, 5, 20)})");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO IntakeMailRouteDecisions (IntakeReceiptId, Disposition, RouteOwnerCode, RouteKind, WorkProviderCode, PredicatesJson, Reason, PolicyKey, PolicyVersion, TransportIdentitiesJson, OriginalIdentitiesJson) VALUES ({receiptId}, {"accepted"}, {"QDOS"}, {"direct_work_provider"}, {"QDOS"}, {emptyEnvelope}, {"Accepted QDOS route"}, {"qdos_mail_route"}, {3}, {emptyEnvelope}, {emptyEnvelope})");
        }
    }

    private sealed class FixedConfiguration : ICaseWorkflowConfiguration
    {
        private static readonly CaseWorkflowConfiguration Configuration = new(
            "case-workflow",
            1);

        public Task<CaseWorkflowConfiguration> GetCurrentAsync(
            CancellationToken cancellationToken) => Task.FromResult(Configuration);
    }

    /// <summary>
    /// The assessment's own opening state under D11 (FRD-11):
    /// Report preparation ("With Engineer") or later. Review no longer
    /// opens the workspace, so these cases start where it does; the
    /// export-cycle assertions the tests make are unchanged by that.
    /// </summary>
    private static async Task SetReportPreparationAsync(
        IDbContextFactory<PegasusDbContext> factory,
        Guid caseId)
    {
        await using var context = await factory.CreateDbContextAsync();
        var workflow = await context.CaseWorkflows.SingleAsync(item => item.CaseId == caseId);
        workflow.State = CaseLifecycleState.ReportPreparation.ToString();
        await context.SaveChangesAsync();
    }

    private static async Task SeedPhotosAsync(
        IDbContextFactory<PegasusDbContext> factory,
        Guid caseId,
        int count)
    {
        await using var context = await factory.CreateDbContextAsync();
        var caseEntity = await context.Cases.SingleAsync(item => item.Id == caseId);
        caseEntity.CustodyRootRemoteId = "case-root-id";
        for (var ordinal = 1; ordinal <= count; ordinal++)
        {
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var occurrenceId = Guid.NewGuid();
            context.AddRange(
                new CaseDocumentEntity
                {
                    Id = documentId,
                    CaseId = caseId,
                    Ordinal = ordinal,
                    SourceOccurrenceIdentity = $"photo:{ordinal}"
                },
                new DocumentVersionEntity
                {
                    Id = versionId,
                    DocumentId = documentId,
                    Version = 1,
                    FileName = $"photo-{ordinal}.jpg",
                    MediaType = "image/jpeg",
                    ContentLength = 1,
                    Sha256 = Convert.ToHexStringLower(
                        SHA256.HashData([(byte)ordinal])),
                    CustodyStatus = DocumentCustodyStatus.Confirmed,
                    CreatedAtUtc = StartUtc,
                    CreatedBy = "Staff:test",
                    IsCurrent = true
                },
                new DocumentOccurrenceEntity
                {
                    Id = occurrenceId,
                    CaseId = caseId,
                    DocumentId = documentId,
                    VersionId = versionId,
                    Ordinal = ordinal,
                    SemanticRole = DocumentSemanticRole.Image,
                    Source = DocumentSource.StaffUpload,
                    SourceOccurrenceIdentity = $"photo:{ordinal}",
                    RecordedAtUtc = StartUtc,
                    OperationKey = $"seed-photo:{ordinal}",
                    PreparationRole = ordinal switch
                    {
                        1 => nameof(CaseAssetReportRole.CloseUp),
                        2 => nameof(CaseAssetReportRole.Overview),
                        _ => nameof(CaseAssetReportRole.Supporting)
                    },
                    SupportingOrder = ordinal > 2 ? ordinal - 2 : null,
                    PreparationVersion = 1,
                    PreparedBy = "Staff:test",
                    PreparedAtUtc = StartUtc
                });
        }
        await context.SaveChangesAsync();
    }

    private sealed class RecordingDocumentContentStore : IDocumentContentStore
    {
        public int BatchReadCount { get; private set; }
        public int SingleReadCount { get; private set; }
        public IReadOnlyList<ManagedDocumentContentRead> Reads { get; private set; } = [];

        public Task<IReadOnlyList<ReadOnlyMemory<byte>>> ReadVersionsAsync(
            IReadOnlyList<ManagedDocumentContentRead> reads,
            CancellationToken cancellationToken)
        {
            BatchReadCount++;
            Reads = reads;
            return Task.FromResult<IReadOnlyList<ReadOnlyMemory<byte>>>(
                reads.Select((_, index) =>
                    (ReadOnlyMemory<byte>)new byte[] { checked((byte)(index + 1)) }).ToArray());
        }

        public Task<Stream> OpenReadAsync(
            Guid caseId, string caseReference, Guid versionId, string expectedSha256,
            long expectedLength, CancellationToken cancellationToken)
        {
            SingleReadCount++;
            throw new InvalidOperationException("The projection must use the batch read path.");
        }

        public Task StoreAsync(
            Guid caseId, string caseReference, Guid versionId, ReadOnlyMemory<byte> content,
            string expectedSha256, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            Stream content,
            long contentLength,
            string expectedSha256,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task DeleteAsync(
            Guid caseId, string caseReference, Guid versionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class MarketResearchDocumentContentStore : IDocumentContentStore
    {
        public int StoreCount { get; private set; }

        public int DeleteCount { get; private set; }

        public Task StoreAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            ReadOnlyMemory<byte> content,
            string expectedSha256,
            CancellationToken cancellationToken)
        {
            StoreCount++;
            return Task.CompletedTask;
        }

        public Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            Stream content,
            long contentLength,
            string expectedSha256,
            CancellationToken cancellationToken)
        {
            StoreCount++;
            return Task.FromResult(new DocumentContentWriteResult(
                DocumentContentWriteDisposition.Created,
                null));
        }

        public Task<Stream> OpenReadAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            string expectedSha256,
            long expectedLength,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task DeleteAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            CancellationToken cancellationToken)
        {
            DeleteCount++;
            return Task.CompletedTask;
        }
    }

    // EF Core's SQL Server provider executes a batched SaveChanges via
    // ExecuteReaderAsync (it reads back an affected-row count / OUTPUT per
    // statement for concurrency checking), not ExecuteNonQueryAsync — even
    // for plain inserts with no store-generated values. The command text for
    // this fixture's SaveChanges call is a single batch containing every
    // statement (ActionHistory, AiJobs, CaseDocuments, CaseHistory,
    // CaseValuations, CaseWorkflowEvents, CaseWorkflows, DocumentVersions,
    // DocumentOccurrences), so intercepting the reader path here fails the
    // whole SaveChanges round trip before any of it reaches the server. The
    // CommandSource.SaveChanges check restricts the fault to that write —
    // without it, the interceptor stays registered on the shared factory and
    // also trips the test's own post-failure LINQ verification queries
    // against CaseValuations (CommandSource.LinqQuery), which read the same
    // table name.
    private sealed class ThrowingCommandInterceptor(string commandTextContains) : DbCommandInterceptor
    {
        public int InterceptedCount { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (eventData.CommandSource == CommandSource.SaveChanges
                && command.CommandText.Contains(commandTextContains, StringComparison.Ordinal))
            {
                InterceptedCount++;
                throw new InvalidOperationException(
                    "Simulated database failure for the market research content-write compensation test.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class ReaderCommandCounter : DbCommandInterceptor
    {
        private int executedReaderCommands;

        public int ExecutedReaderCommands => Volatile.Read(ref executedReaderCommands);

        public void Reset() => Interlocked.Exchange(ref executedReaderCommands, 0);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref executedReaderCommands);
            return ValueTask.FromResult(result);
        }
    }
}

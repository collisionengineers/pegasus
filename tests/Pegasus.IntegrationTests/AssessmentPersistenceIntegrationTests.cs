using System.Data.Common;
using System.Security.Cryptography;
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
                [AssessmentVocabulary.DamageImpacts] = "[{\"zone\":\"front\",\"severity\":\"light\",\"note\":\"Bonnet\"},{\"zone\":\"right_rear\",\"severity\":\"heavy\",\"note\":\"Quarter\"}]"
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
    public async Task AssessmentWorkspaceLoadsInExactlySixReaderCommands()
    {
        var counter = new ReaderCommandCounter();
        await using var harness = await Harness.CreateAsync(counter);
        var outcome = await harness.AcceptAsync("assessment-workspace-query-count");
        await SetReportPreparationAsync(harness.Factory, outcome.Identity.CaseId);
        await SeedExportAsync(harness.Factory, outcome.Identity.CaseId, 0);
        counter.Reset();

        var workspace = await new EfAssessmentWorkspaceSource(harness.Factory)
            .GetAsync(outcome.Identity.CaseId);

        Assert.NotNull(workspace);
        Assert.Equal(6, counter.ExecutedReaderCommands);
    }

    [Fact]
    public async Task ReportDraftGenerationThroughProductionProjectionResolvesSignOffAndFailsClosedWithoutIt()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-report-photo-batch");
        await SetReportPreparationAsync(harness.Factory, outcome.Identity.CaseId);
        await SeedExportAsync(harness.Factory, outcome.Identity.CaseId, 0);
        await SeedPhotosAsync(harness.Factory, outcome.Identity.CaseId, 2);
        var contentStore = new RecordingDocumentContentStore();
        await using var staffContext = await harness.Factory.CreateDbContextAsync();
        var source = new EfAssessmentReportProjectionSource(
            harness.Factory,
            new GetAssessmentWorkspace(new EfAssessmentWorkspaceSource(harness.Factory)),
            contentStore,
            new EfStaffAccountQueries(staffContext),
            new EfCaseAssetPreparationStore(harness.Factory, TimeProvider.System),
            new ListAppliedValuations(new EfValuationStore(harness.Factory, TimeProvider.System)));

        var input = await source.GetAsync(
            outcome.Identity.CaseId,
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]));

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
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]));
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
        var pdf = "%PDF-1.4 CASE-040"u8.ToArray();
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
        const string engineer = "case-040-engineer";
        var caseData = new (string Name, string Type, string Value)[]
        {
            (CaseDataFieldNames.ClaimantName, CaseDataCodes.Text, "Mrs Jane Example"),
            (CaseDataFieldNames.ClaimNumber, CaseDataCodes.Text, "ABC/DEF/12345/1"),
            (CaseDataFieldNames.VehicleRegistration, CaseDataCodes.Text, "AB12CDE"),
            (CaseDataFieldNames.VehicleMake, CaseDataCodes.Text, "Ford"),
            (CaseDataFieldNames.VehicleModel, CaseDataCodes.Text, "Focus"),
            (CaseDataFieldNames.VehicleMileage, CaseDataCodes.Integer, "80000"),
            (CaseDataFieldNames.VehicleMileageUnit, CaseDataCodes.Text, "miles"),
            (CaseDataFieldNames.IncidentDate, CaseDataCodes.Date, "2031-04-01"),
            (CaseDataFieldNames.InstructionDate, CaseDataCodes.Date, "2031-05-01"),
            (CaseDataFieldNames.InspectionMode, CaseDataCodes.InspectionMode,
                ProviderInspectionModePolicy.ImageBasedAssessmentCode),
            (CaseDataFieldNames.InspectionAddress, CaseDataCodes.Text, "1 Test Street, London")
        };
        var existingConfirmed = await context.Set<CaseDataFieldEntity>()
            .Where(field => field.CaseId == caseId
                && field.ValueKind == CaseDataCodes.Confirmed
                && caseData.Select(value => value.Name).Contains(field.FieldName))
            .ToArrayAsync();
        context.RemoveRange(existingConfirmed);
        context.Set<CaseDataFieldEntity>().AddRange(caseData.Select(field =>
            new CaseDataFieldEntity
            {
                CaseId = caseId,
                FieldName = field.Name,
                ValueKind = CaseDataCodes.Confirmed,
                ValueType = field.Type,
                Value = field.Value,
                SourceKind = CaseDataCodes.StaffCorrection,
                SourceIdentity = engineer,
                SourceLabel = "CASE-040 report-ready fixture",
                PolicyKey = CaseDataPolicy.EditPolicyKey,
                PolicyVersion = CaseDataPolicy.EditPolicyVersion,
                ConfirmedByActor = engineer,
                ConfirmedAtUtc = recordedAt
            }));
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AssessmentVocabulary.VehicleType] = "car",
            [AssessmentVocabulary.VehicleYear] = "2012",
            [AssessmentVocabulary.VehicleMileageSource] = "online_data",
            [AssessmentVocabulary.VehicleCondition] = "good",
            [AssessmentVocabulary.IncidentAssessed] = "2031-05-06",
            [AssessmentVocabulary.ImpactSeverity] = "moderate",
            [AssessmentVocabulary.ImpactLocation] = "right_rear",
            [AssessmentVocabulary.ValueRetail] = "5000.00",
            [AssessmentVocabulary.ValueTrade] = "4000.00",
            [AssessmentVocabulary.ValueEngineer] = "5000.00",
            [AssessmentVocabulary.CostRepairerVatRegistered] = "true",
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
                CaseId = caseId,
                FieldPath = value.Key,
                Value = value.Value,
                RecordedByKind = ActorKind.Staff.ToString(),
                RecordedBy = engineer,
                RecordedAtUtc = recordedAt,
                ConfirmedBy = engineer,
                ConfirmedAtUtc = recordedAt
            }));

        context.Set<CaseRepairSpecificationEntity>().Add(new()
        {
            Id = Guid.NewGuid(),
            CaseId = caseId,
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
            CreatedBy = engineer,
            CreationOperationKey = "case-040-report-ready-estimate",
            CreatedAtUtc = recordedAt,
            AcceptedBy = engineer,
            AcceptedAtUtc = recordedAt,
            Name = "Engineer's",
            LabourRate = 40m,
            PaintMaterials = 50m,
            OtherCosts = 0m,
            VatPercent = 20m,
            IsCurrent = true
        });
        await context.SaveChangesAsync();
    }

    private sealed class TestReportRenderer(byte[] pdf) : IAssessmentReportRenderer
    {
        public string EngineVersion => "case-040-test";

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

    [Fact]
    public async Task AssessmentAccessRequiresAnExportAfterTheLatestReviewEntry()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("assessment-access-review-cycle");
        await SetReportPreparationAsync(harness.Factory, outcome.Identity.CaseId);
        var source = new EfAssessmentAccessSource(harness.Factory);

        Assert.False((await source.GetAsync(outcome.Identity.CaseId))!.CanOpen);
        Assert.Null(await new EfAssessmentWorkspaceSource(harness.Factory)
            .GetAsync(outcome.Identity.CaseId));

        await SeedExportAsync(harness.Factory, outcome.Identity.CaseId, 0);
        var exportedAccess = (await source.GetAsync(outcome.Identity.CaseId))!;
        Assert.True(exportedAccess.CanOpen);
        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            Assert.Null((await context.CaseWorkflows.AsNoTracking().SingleAsync(
                item => item.CaseId == outcome.Identity.CaseId)).AssignedEngineerId);
        }

        await using (var context = await harness.Factory.CreateDbContextAsync())
        {
            var workflow = await context.CaseWorkflows.SingleAsync(
                item => item.CaseId == outcome.Identity.CaseId);
            workflow.Version = 1;
            context.CaseWorkflowEvents.Add(new()
            {
                Id = Guid.NewGuid(),
                CaseId = outcome.Identity.CaseId,
                Workflow = workflow,
                EventType = "case_returned_to_review",
                OperationKey = "assessment-access-return",
                RequestHash = "test",
                ActorKind = ActorKind.Staff.ToString(),
                ActorSubjectId = "staff-1",
                ActorRolesJson = "[]",
                Reason = "Review the corrected case.",
                OccurredAtUtc = StartUtc,
                BeforeVersion = 0,
                AfterVersion = 1
            });
            await context.SaveChangesAsync();
        }

        Assert.False((await source.GetAsync(outcome.Identity.CaseId))!.CanOpen);
        await SeedExportAsync(harness.Factory, outcome.Identity.CaseId, 1);
        Assert.True((await source.GetAsync(outcome.Identity.CaseId))!.CanOpen);
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
                    ["assessment.salvage_value"] = "1500.00",
                    ["assessment.values.retail"] = "12000",
                    ["assessment.values.trade"] = "10500"
                    // assessment.values.engineer is deliberately absent: the
                    // Engineer's Value is adopted only by the valuation Apply
                    // command (B03/AUTO-015), and a field save that posted it
                    // is now refused rather than recorded.
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
    public async Task RepairSpecificationAcceptanceCorrectionAndExactVersionPersist()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("repair-spec-accept-case");
        var caseId = outcome.Identity.CaseId;
        var source = new RepairSpecificationSource(
            RepairSpecificationSourceRoute.Manual,
            "case://repair-spec/source-1",
            "source-v1",
            new string('a', 64));
        var basis = new RepairCalculationBasis(100m, 20m, 10m, 0m, true, 26m, 156m, "calc/v1");
        var lines = new EstimateLineInput[]
        {
            new("new_part", null, "Door skin", null, 20m, false, null, null,
                "confirmed", "case", "Engineer mapping"),
            new("repair", null, "Repair door", 2m, null, false, null, null,
                "confirmed", "judgement", "Engineer mapping"),
        };

        var draftLease = await harness.AcquireLeaseAsync(
            caseId, 0, harness.EngineerActor, "repair-spec-draft-lease");
        var draftRequest = new StartRepairSpecificationDraftRequest(
            caseId, draftLease.Version, source, harness.EngineerActor,
            "repair-spec-draft", "Create the canonical repair specification.",
            draftLease.Token, Lines: lines);
        var draft = await harness.RepairSpecifications.StartDraftAsync(draftRequest, CancellationToken.None);
        var replayedDraft = await harness.RepairSpecifications.StartDraftAsync(draftRequest, CancellationToken.None);
        Assert.Equal(draft.SpecificationId, replayedDraft.SpecificationId);

        var acceptLease = await harness.AcquireLeaseAsync(
            caseId, 1, harness.EngineerActor, "repair-spec-accept-lease");
        var accepted = await harness.RepairSpecifications.AcceptAsync(
            new(caseId, acceptLease.Version, draft.SpecificationId, draft.Version, source, basis,
                harness.EngineerActor, "repair-spec-accept", "Engineer accepted the source and mapping.",
                acceptLease.Token), CancellationToken.None);
        Assert.Equal(RepairSpecificationState.Accepted, accepted.State);
        Assert.Equal(["Door skin"], RepairSpecificationPolicy.ToDisplayLists(accepted).NewParts);

        var correctionLease = await harness.AcquireLeaseAsync(
            caseId, 2, harness.EngineerActor, "repair-spec-correct-lease");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            harness.RepairSpecifications.StartDraftAsync(
                new(caseId, correctionLease.Version, source, harness.EngineerActor,
                    "repair-spec-duplicate", "Attempt a competing canonical draft.",
                    correctionLease.Token, Lines: lines), CancellationToken.None));
        var correction = await harness.RepairSpecifications.StartDraftAsync(
            new(caseId, correctionLease.Version, source with { SourceVersion = "source-v2" },
                harness.EngineerActor, "repair-spec-correct", "Correct the accepted mapping.",
                correctionLease.Token, accepted.SpecificationId), CancellationToken.None);
        Assert.Equal(2, correction.Version);
        Assert.Equal(accepted.SpecificationId, correction.SupersedesSpecificationId);

        var correctionAcceptLease = await harness.AcquireLeaseAsync(
            caseId, 3, harness.EngineerActor, "repair-spec-correct-accept-lease");
        var corrected = await harness.RepairSpecifications.AcceptAsync(
            new(caseId, correctionAcceptLease.Version, correction.SpecificationId, correction.Version,
                source with { SourceVersion = "source-v2" }, basis, harness.EngineerActor,
                "repair-spec-correct-accept", "Engineer accepted the corrected mapping.",
                correctionAcceptLease.Token), CancellationToken.None);
        Assert.Equal(corrected.SpecificationId,
            (await harness.RepairSpecifications.GetCurrentAcceptedAsync(
                caseId, CancellationToken.None))!.SpecificationId);
        Assert.Equal(RepairSpecificationState.Superseded,
            (await harness.RepairSpecifications.GetVersionAsync(
                caseId, accepted.SpecificationId, CancellationToken.None))!.State);

        Assert.Equal(corrected.SpecificationId,
            (await harness.RepairSpecifications.GetVersionAsync(
                caseId, corrected.SpecificationId, CancellationToken.None))!.SpecificationId);
    }

    [Fact]
    public async Task NamedEstimatesSaveDuplicateDiscardSetCurrentAndListWithOneCurrentPerCase()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("estimate-accept-case");
        var caseId = outcome.Identity.CaseId;
        var engineer = harness.EngineerActor;
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
                new("Repairer", 3, 40m, 30m, 25m, 0m, 20m, "Typed from the repairer's e-mail."),
                [
                    new("new_part", null, "Door skin", null, 220.40m, false, "P-1234", null,
                        "confirmed", "official", null, Quantity: 1),
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
                new("Engineer's", 2, 45m, null, null, 0m, 0m, null),
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
        Assert.Equal(220.40m + 100m + 60m + 25m, totalsA.Subtotal);
        Assert.Equal(totalsA.Total, currentA.CalculationBasis!.Total);
        Assert.Equal(totalsA.Vat, currentA.CalculationBasis.Vat);
        Assert.Equal(currentA.SpecificationId,
            (await harness.RepairSpecifications.GetCurrentAcceptedAsync(caseId, CancellationToken.None))!.SpecificationId);
        // Replay returns the same estimate without a second mutation.
        Assert.Equal(currentA.SpecificationId,
            (await setCurrent.ExecuteAsync(useA, CancellationToken.None)).SpecificationId);

        // Switching Current clears the previous in the same transaction; A stays Accepted.
        var leaseUseB = await LeaseAsync(engineer, "estimate-lease-use-b");
        var currentB = await setCurrent.ExecuteAsync(
            new(caseId, leaseUseB.Version, engineer, "estimate-use-b", "Use the Engineer's estimate.",
                leaseUseB.Token, engineers.SpecificationId),
            CancellationToken.None);
        version++;
        Assert.True(currentB.IsCurrent);
        var listed = await list.ExecuteAsync(caseId, CancellationToken.None);
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
                new("Claude draft", 2, 40m, 30m, 20m, 0m, 20m, null),
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

        Assert.Equal(4, (await list.ExecuteAsync(caseId, CancellationToken.None)).Count);
        Assert.Equal(version, (await harness.AcquireLeaseAsync(caseId, version, engineer, "estimate-lease-final")).Version);
    }

    /// <summary>
    /// Stream A review (comments 5560764306/5560667174, one staleness root
    /// cause): a manual valuation save or edit changes frozen report inputs —
    /// guide figures, and the confirmed Engineer's Value field a manual
    /// Engineer's Value record writes — so each stales the Case's current
    /// generation inside its own transaction, replay returns before staling,
    /// and a superseded generation never moves.
    /// </summary>
    [Fact]
    public async Task SavingAndEditingAValuationStaleOnlyTheCurrentGeneration()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("valuation-stale-accept");
        var caseId = outcome.Identity.CaseId;
        var engineer = harness.EngineerActor;
        var save = new SaveValuation(harness.Valuations);
        var edit = new EditValuation(harness.Valuations);

        var (currentId, supersededId) = await SeedGenerationsAsync(harness, caseId);

        var lease = await harness.AcquireLeaseAsync(caseId, 0, engineer, "valuation-stale-save-lease");
        var saveRequest = new SaveValuationRequest(
            caseId,
            lease.Version,
            engineer,
            "valuation-stale-save",
            "Recorded the Engineer's Value.",
            lease.Token,
            new(ValuationSource.EngineersValue, new DateOnly(2031, 5, 8), new TimeOnly(9, 0), 42000, 12000m, 10000m));
        var saved = await save.ExecuteAsync(saveRequest, CancellationToken.None);

        Assert.Equal("Stale", await harness.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentId:D}'"));
        Assert.Equal("Confirmed", await harness.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{supersededId:D}'"));
        Assert.Equal(1, await StaleRowCountAsync(harness, caseId));

        // Replay of the same operation returns before any mutation, so the
        // stale row count does not move.
        Assert.Equal(saved, await save.ExecuteAsync(saveRequest, CancellationToken.None));
        Assert.Equal(1, await StaleRowCountAsync(harness, caseId));

        // A fresh current generation goes stale on the edit the same way.
        await harness.Database.ExecuteAsync(
            $"UPDATE CaseReportGenerations SET State = 'Confirmed' WHERE Id = '{currentId:D}'");
        var version = await harness.Database.ScalarAsync<long>(
            $"SELECT Version FROM CaseWorkflows WHERE CaseId = '{caseId:D}'");
        var editLease = await harness.AcquireLeaseAsync(caseId, version, engineer, "valuation-stale-edit-lease");
        await edit.ExecuteAsync(
            new EditValuationRequest(
                caseId,
                editLease.Version,
                engineer,
                "valuation-stale-edit",
                "Corrected the Engineer's Value.",
                editLease.Token,
                saved.ValuationId,
                new(ValuationSource.EngineersValue, new DateOnly(2031, 5, 9), new TimeOnly(10, 0), 42125, 12500m, 10500m)),
            CancellationToken.None);

        Assert.Equal("Stale", await harness.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{currentId:D}'"));
        Assert.Equal("Confirmed", await harness.Database.ScalarAsync<string>(
            $"SELECT State FROM CaseReportGenerations WHERE Id = '{supersededId:D}'"));
        Assert.Equal(2, await StaleRowCountAsync(harness, caseId));
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
                CaseVersion = 0,
                SnapshotHash = new string('1', 64),
                SnapshotJson = "{\"operationKey\":\"seed-generation-superseded\"}",
                TemplateVersion = "assessment-report/v1",
                RendererVersion = "playwright/v1",
                State = nameof(CaseReportGenerationState.Confirmed),
                GeneratedAtUtc = new DateTimeOffset(2031, 5, 6, 9, 0, 0, TimeSpan.Zero),
                Version = 1
            },
            new CaseReportGenerationEntity
            {
                Id = currentId,
                CaseId = caseId,
                CaseVersion = 0,
                SnapshotHash = new string('2', 64),
                SnapshotJson = "{\"operationKey\":\"seed-generation-current\"}",
                TemplateVersion = "assessment-report/v1",
                RendererVersion = "playwright/v1",
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

    [Fact]
    public async Task ValuationsSaveEditListAndOwnTheConfirmedEngineersValueField()
    {        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("valuation-accept-case");
        var caseId = outcome.Identity.CaseId;
        var engineer = harness.EngineerActor;
        var save = new SaveValuation(harness.Valuations);
        var edit = new EditValuation(harness.Valuations);
        var list = new ListCaseValuations(harness.Valuations);
        long version = 0;

        async Task<CaseEditLease> LeaseAsync(string key) =>
            await harness.AcquireLeaseAsync(caseId, version, engineer, key);

        // The same production assessment read owner ENG-028 consumes.
        async Task<AssessmentFieldValue> EngineersValueFieldAsync() =>
            Assert.IsType<AssessmentFieldValue>(
                await ReadEngineersValueAsync(harness, caseId));

        async Task<CaseValuation> SaveAsync(
            string key,
            ValuationSource source,
            DateOnly date,
            TimeOnly time,
            long mileage,
            decimal retail,
            decimal trade,
            DateOnly? guideMonth = null)
        {
            var lease = await LeaseAsync($"{key}-lease");
            var request = new SaveValuationRequest(
                caseId,
                lease.Version,
                engineer,
                key,
                "Recorded a Case valuation.",
                lease.Token,
                new(source, date, time, mileage, retail, trade, guideMonth));
            var result = await save.ExecuteAsync(request, CancellationToken.None);
            Assert.Equal(result, await save.ExecuteAsync(request, CancellationToken.None));
            version++;
            return result;
        }

        await SaveAsync(
            "valuation-save-glasses",
            ValuationSource.Glasses,
            new DateOnly(2031, 5, 8),
            new TimeOnly(9, 0),
            42000,
            12100m,
            10100m,
            new DateOnly(2031, 4, 1));
        Assert.Null(await ReadEngineersValueAsync(harness, caseId));

        var olderEngineerValue = await SaveAsync(
            "valuation-save-engineer-old",
            ValuationSource.EngineersValue,
            new DateOnly(2031, 5, 7),
            new TimeOnly(14, 30),
            41950,
            11900m,
            9900m);
        var newerEngineerValue = await SaveAsync(
            "valuation-save-engineer-new",
            ValuationSource.EngineersValue,
            new DateOnly(2031, 5, 8),
            new TimeOnly(8, 30),
            42000,
            12000m,
            10000m);

        var currentField = await EngineersValueFieldAsync();
        Assert.Equal("12000.00", currentField.Value);
        Assert.Equal(engineer.SubjectId, currentField.ConfirmedBy);
        Assert.Equal(12000m, newerEngineerValue.Details.RetailValue);

        harness.Advance(TimeSpan.FromMinutes(1));
        var editLease = await LeaseAsync("valuation-edit-lease");
        var editRequest = new EditValuationRequest(
            caseId,
            editLease.Version,
            engineer,
            "valuation-edit-engineer",
            "Corrected the Engineer valuation.",
            editLease.Token,
            olderEngineerValue.ValuationId,
            new(
                ValuationSource.EngineersValue,
                new DateOnly(2031, 5, 9),
                new TimeOnly(10, 15),
                42125,
                12345.67m,
                10345.67m,
                new DateOnly(2031, 5, 1)));
        var edited = await edit.ExecuteAsync(editRequest, CancellationToken.None);
        Assert.Equal(edited, await edit.ExecuteAsync(editRequest, CancellationToken.None));
        version++;

        // Correcting the earlier row onto the latest entered date makes it the
        // current Engineer's Value, so the owned field follows it.
        currentField = await EngineersValueFieldAsync();
        Assert.Equal("12345.67", currentField.Value);
        Assert.Equal(engineer.SubjectId, currentField.ConfirmedBy);

        // A later-recorded but earlier-dated row never demotes the field: the
        // current Engineer's Value is the latest entered one, not the last
        // one saved.
        harness.Advance(TimeSpan.FromMinutes(1));
        await SaveAsync(
            "valuation-save-engineer-backdated",
            ValuationSource.EngineersValue,
            new DateOnly(2031, 5, 6),
            new TimeOnly(7, 45),
            41800,
            9500m,
            8500m);
        currentField = await EngineersValueFieldAsync();
        Assert.Equal("12345.67", currentField.Value);
        Assert.Equal(engineer.SubjectId, currentField.ConfirmedBy);
        Assert.Equal(edited.LastEditedBy, currentField.RecordedBy);
        Assert.Equal(edited.LastEditedAtUtc!.Value, currentField.RecordedAtUtc);
        Assert.Equal(edited.LastEditedAtUtc, currentField.ConfirmedAtUtc);

        var valuations = await list.ExecuteAsync(caseId, CancellationToken.None);
        Assert.Equal(4, valuations.Count);
        Assert.Equal(edited.ValuationId, valuations[0].ValuationId);
        Assert.Equal(42125, edited.Details.Mileage);
        Assert.Equal(12345.67m, edited.Details.RetailValue);
        Assert.Equal(new DateOnly(2031, 5, 1), edited.Details.GuideMonth);
        Assert.Contains(
            valuations,
            valuation => valuation.Details.GuideMonth == new DateOnly(2031, 4, 1));
        Assert.Equal(engineer.SubjectId, edited.LastEditedBy);

        await using var context = await harness.Factory.CreateDbContextAsync();
        Assert.Equal(
            5,
            await context.ActionHistory.CountAsync(item =>
                item.AggregateType == "case_valuation"));
        Assert.Equal(
            version,
            (await harness.AcquireLeaseAsync(
                caseId,
                version,
                engineer,
                "valuation-lease-final")).Version);
    }

    [Fact]
    public async Task BackdatedEngineersValueKeepsTheSelectedRowsProvenance()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("valuation-provenance-accept-case");
        var caseId = outcome.Identity.CaseId;
        var selectedEngineer = harness.EngineerActor;
        var backdatingEngineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        long version = 0;

        async Task<CaseValuation> SaveAsync(
            string key,
            ActionActor actor,
            DateOnly date,
            decimal retail)
        {
            var lease = await harness.AcquireLeaseAsync(
                caseId,
                version,
                actor,
                $"{key}-lease");
            var result = await harness.Valuations.SaveAsync(
                new(
                    caseId,
                    lease.Version,
                    actor,
                    key,
                    "Recorded an Engineer's Value.",
                    lease.Token,
                    new(
                        ValuationSource.EngineersValue,
                        date,
                        new TimeOnly(9, 0),
                        42000,
                        retail,
                        retail - 2000m)),
                CancellationToken.None);
            version++;
            return result;
        }

        var selected = await SaveAsync(
            "valuation-provenance-selected",
            selectedEngineer,
            new DateOnly(2031, 5, 8),
            12000m);
        harness.Advance(TimeSpan.FromMinutes(5));
        await SaveAsync(
            "valuation-provenance-backdated",
            backdatingEngineer,
            new DateOnly(2031, 5, 7),
            11000m);

        var field = Assert.IsType<AssessmentFieldValue>(
            await ReadEngineersValueAsync(harness, caseId));
        Assert.Equal("12000.00", field.Value);
        Assert.Equal(ActorKind.Staff, field.RecordedByKind);
        Assert.Equal(selected.RecordedBy, field.RecordedBy);
        Assert.Equal(selected.RecordedAtUtc, field.RecordedAtUtc);
        Assert.Equal(selected.RecordedBy, field.ConfirmedBy);
        Assert.Equal(selected.RecordedAtUtc, field.ConfirmedAtUtc);
    }

    [Fact]
    public async Task EditingTheOnlyEngineersValueToAnotherSourceClearsTheAssessmentOwner()
    {
        await using var harness = await Harness.CreateAsync();
        var outcome = await harness.AcceptAsync("valuation-clear-accept-case");
        var caseId = outcome.Identity.CaseId;
        var engineer = harness.EngineerActor;
        var saveLease = await harness.AcquireLeaseAsync(
            caseId,
            0,
            engineer,
            "valuation-clear-save-lease");
        var saved = await harness.Valuations.SaveAsync(
            new(
                caseId,
                saveLease.Version,
                engineer,
                "valuation-clear-save",
                "Recorded an Engineer's Value.",
                saveLease.Token,
                new(
                    ValuationSource.EngineersValue,
                    new DateOnly(2031, 5, 8),
                    new TimeOnly(9, 0),
                    42000,
                    12000m,
                    10000m)),
            CancellationToken.None);
        Assert.NotNull(await ReadEngineersValueAsync(harness, caseId));

        harness.Advance(TimeSpan.FromMinutes(5));
        var editLease = await harness.AcquireLeaseAsync(
            caseId,
            1,
            engineer,
            "valuation-clear-edit-lease");
        await harness.Valuations.EditAsync(
            new(
                caseId,
                editLease.Version,
                engineer,
                "valuation-clear-edit",
                "Corrected the valuation source.",
                editLease.Token,
                saved.ValuationId,
                saved.Details with { Source = ValuationSource.Glasses }),
            CancellationToken.None);

        Assert.Null(await ReadEngineersValueAsync(harness, caseId));
    }

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
        Assert.IsType<SaveValuation>(
            scope.ServiceProvider.GetRequiredService<ISaveValuation>());
        Assert.IsType<EditValuation>(
            scope.ServiceProvider.GetRequiredService<IEditValuation>());
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
        Assert.Equal(1, await context.CaseValuations.CountAsync(item => item.CaseId == caseId));
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
        Assert.Equal(0, await context.CaseValuations.CountAsync(item => item.CaseId == caseId));
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
        Assert.Equal(0, await context.CaseValuations.CountAsync(item => item.CaseId == caseId));
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
        Assert.Equal(0, await context.CaseValuations.CountAsync(item => item.CaseId == caseId));
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
        Assert.Equal(0, await context.CaseValuations.CountAsync(item => item.CaseId == caseId));
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
        Assert.Equal(0, await context.CaseValuations.CountAsync(item => item.CaseId == caseId));
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
                var valuations = new EfValuationStore(factory, timeProvider);
                return new(
                    database,
                    factory,
                    receiptId,
                    new AcceptIntake(
                        acceptanceStore,
                        new FixedConfiguration(),
                        new EfProviderInspectionModeStore(factory),
                        new CommittedWorkPublisherDouble()),
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
                    "Accepted assessment fixture case",
                    CaseType.Inspection,
                    "QDOS",
                    new(true, true, false, false),
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
    /// The assessment's own opening state under D11 (FRD-11, ENG-025):
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

    private static async Task SeedExportAsync(
        IDbContextFactory<PegasusDbContext> factory,
        Guid caseId,
        long workflowVersion)
    {
        await using var context = await factory.CreateDbContextAsync();
        var proxy = await context.EvaFirstHandoffProxies
            .SingleOrDefaultAsync(item => item.CaseId == caseId);
        if (proxy is null)
        {
            context.EvaFirstHandoffProxies.Add(new()
            {
                CaseId = caseId,
                AdapterKey = "test",
                AdapterVersion = "1",
                RecordedAtUtc = StartUtc,
                LatestExportedWorkflowVersion = workflowVersion,
                ActorSubjectId = "staff-1"
            });
        }
        else
        {
            proxy.LatestExportedWorkflowVersion = workflowVersion;
        }
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

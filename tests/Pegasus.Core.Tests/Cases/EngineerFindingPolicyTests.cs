using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Lifecycle;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.Tests.Cases;

public sealed class EngineerFindingPolicyTests
{
    [Theory]
    [InlineData(StaffRole.Administrator)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public void AssignedStaffMayRecordAnInspectionAndAuditFindingDuringReportPreparation(StaffRole role)
    {
        var staffId = Guid.NewGuid();
        var actor = ActionActor.Staff(staffId, [role]);

        var actingStaffId = EngineerFindingPolicy.ValidateRequest(Request(actor));
        EngineerFindingPolicy.RequireAssignedInspectionAndAudit(
            CaseType.InspectionAndAudit,
            CaseLifecycleState.ReportPreparation,
            staffId,
            actingStaffId);

        Assert.Equal(staffId, actingStaffId);
    }

    [Theory]
    [InlineData(ActorKind.Automation)]
    [InlineData(ActorKind.SystemWorker)]
    [InlineData(ActorKind.Provider)]
    public void NonStaffActorsCannotRecordFindings(ActorKind kind)
    {
        var actor = kind switch
        {
            ActorKind.Automation => ActionActor.Automation("pegasus-automation"),
            ActorKind.SystemWorker => ActionActor.SystemWorker("worker"),
            ActorKind.Provider => ActionActor.Provider(Guid.NewGuid()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

        if (kind == ActorKind.Automation)
        {
            Assert.Throws<InvalidOperationException>(() => EngineerFindingPolicy.ValidateRequest(Request(actor)));
        }
        else
        {
            Assert.Throws<StaffAuthorizationException>(() => EngineerFindingPolicy.ValidateRequest(Request(actor)));
        }
    }

    [Fact]
    public void UnassignedOrWrongAssigneeCannotRecordAFinding()
    {
        var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]);
        var actingStaffId = EngineerFindingPolicy.ValidateRequest(Request(actor));

        Assert.Throws<InvalidOperationException>(() =>
            EngineerFindingPolicy.RequireAssignedInspectionAndAudit(
                CaseType.InspectionAndAudit,
                CaseLifecycleState.ReportPreparation,
                null,
                actingStaffId));
        Assert.Throws<InvalidOperationException>(() =>
            EngineerFindingPolicy.RequireAssignedInspectionAndAudit(
                CaseType.InspectionAndAudit,
                CaseLifecycleState.ReportPreparation,
                Guid.NewGuid(),
                actingStaffId));
    }

    [Theory]
    [InlineData(CaseLifecycleState.Review)]
    [InlineData(CaseLifecycleState.Held)]
    [InlineData(CaseLifecycleState.PostReport)]
    public void FindingsAreRefusedOutsideReportPreparation(CaseLifecycleState state)
    {
        var staffId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() =>
            EngineerFindingPolicy.RequireAssignedInspectionAndAudit(
                CaseType.InspectionAndAudit,
                state,
                staffId,
                staffId));
    }

    private static RecordEngineerFindingRequest Request(ActionActor actor) => new(
        Guid.NewGuid(),
        1,
        actor,
        "record-finding",
        "Recorded the inspection finding.",
        new string('l', CaseEditAuthority.LeaseTokenLength),
        AuditAssessment.Repairable);
}

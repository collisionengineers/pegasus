using ModelContextProtocol;
using Pegasus.Core.Assessment;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Web.Mcp;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// What a tool reads from the resolved context instead of assuming the
/// Automation Actor (R1300, R1302, ADR-0092): the actor, and the labels a
/// document, an estimate, a staged upload, a submitted source and the call's
/// own history row carry. For a member of staff signed in from Pegasus
/// Desktop the actor is the person and what is filed keeps its AI mark.
/// </summary>
public sealed class ConnectorActorContextTests
{
    private static readonly Guid StaffId = Guid.Parse("5a0f3a7e-93a5-4d0e-9d0e-2f1c1f6d7b11");

    private static readonly string[] Scopes = [AutomationMcp.CasesScope, AutomationMcp.SendScope];

    [Fact]
    public void TheAutomationActorKeepsItsLabels()
    {
        var context = AutomationActorContext.ForAutomation("pegasus-automation", "grant-7", Scopes, "trace-1");

        Assert.False(context.IsStaff);
        Assert.Equal(ActorKind.Automation, context.Actor.Kind);
        Assert.Equal("grant-7", context.Actor.SubjectId);

        // A staged upload is the client's, whichever grant staged it.
        Assert.Equal("pegasus-automation", context.UploadOwner);
        Assert.Equal(ActorKind.Automation, context.UploadActor.Kind);
        Assert.Equal("pegasus-automation", context.UploadActor.SubjectId);
        Assert.Equal("automation:pegasus-automation", context.IntakeSubmitter);
        Assert.Equal(DocumentSource.Automation, context.DocumentSource);
        Assert.Equal("automation:", context.SourceIdentityPrefix);
        Assert.Equal(RepairSpecificationSourceRoute.AiDraft, context.EstimateRoute);
        Assert.True(context.EstimateAwaitsUse);
        Assert.Equal(AutomationMcpAuditor.AutomationAggregateType, context.AuditArea);
        Assert.Equal(Scopes, context.Scopes);
    }

    [Theory]
    [InlineData(StaffRole.User)]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.Administrator)]
    public void AMemberOfStaffIsTheActorAndWhatIsFiledKeepsItsAiMarkWhateverTheirRole(StaffRole role)
    {
        var context = AutomationActorContext.ForStaff(StaffId, role, Scopes, "trace-2");

        Assert.True(context.IsStaff);
        Assert.Equal(ActorKind.Staff, context.Actor.Kind);
        Assert.Equal(StaffId.ToString("D"), context.Actor.SubjectId);
        Assert.Equal(role, Assert.Single(context.Actor.Roles));
        Assert.Same(context.Actor, context.UploadActor);
        Assert.Equal($"staff:{StaffId:D}", context.UploadOwner);
        Assert.Equal($"staff:{StaffId:D}", context.IntakeSubmitter);
        Assert.Equal(AutomationClientRegistry.DesktopAggregateType, context.AuditArea);

        // Under their name, but marked AI (R1302).
        Assert.Equal(DocumentSource.Automation, context.DocumentSource);
        Assert.Equal("automation:", context.SourceIdentityPrefix);
        Assert.Equal(RepairSpecificationSourceRoute.AiDraft, context.EstimateRoute);
        Assert.True(context.EstimateAwaitsUse);
        Assert.False(RepairSpecificationPolicy.BecomesCurrentWhenCreated(
            context.Actor, context.EstimateRoute, awaitsUse: false));
        Assert.False(RepairSpecificationPolicy.BecomesCurrentWhenCreated(
            context.Actor, RepairSpecificationSourceRoute.Json, context.EstimateAwaitsUse));
    }

    /// <summary>
    /// No tool reaches a right only an Administrator holds, so the refusal is
    /// held here at its one translation: a member of staff reads the
    /// operator's sentence (R1304), and the Automation Actor the sentence
    /// that names it.
    /// </summary>
    [Fact]
    public async Task ARefusedRightIsWordedForWhoeverTheCallIsMadeFor()
    {
        var user = AutomationActorContext.ForStaff(StaffId, StaffRole.User, Scopes, "trace-3");
        Assert.False(StaffAuthorization.IsAuthorized(user.Actor, StaffAccessRight.ManageStaffAccounts));
        Assert.True(StaffAuthorization.IsAuthorized(user.Actor, StaffAccessRight.PerformCasework));

        var staffRefusal = await Assert.ThrowsAsync<McpException>(() =>
            AutomationMcpErrors.ExecuteAsync<int>(
                user,
                () =>
                {
                    StaffAuthorization.Require(user.Actor, StaffAccessRight.ManageStaffAccounts);
                    return Task.FromResult(0);
                }));
        Assert.Equal(Pegasus.Web.Presentation.OperatorLabels.DesktopSignIn.ActionNotAllowed, staffRefusal.Message);

        var automation = AutomationActorContext.ForAutomation("pegasus-automation", "grant-7", Scopes, "trace-4");
        var automationRefusal = await Assert.ThrowsAsync<McpException>(() =>
            AutomationMcpErrors.ExecuteAsync<int>(
                automation,
                () =>
                {
                    StaffAuthorization.Require(automation.Actor, StaffAccessRight.ManageStaffAccounts);
                    return Task.FromResult(0);
                }));
        Assert.Equal("The Automation actor is not authorized for this action.", automationRefusal.Message);
    }
}

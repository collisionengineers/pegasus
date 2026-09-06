using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Identity;

public sealed class AutomationActorTests
{
    private static ActionActor Automation() => ActionActor.Automation("pegasus-automation");

    [Fact]
    public void AutomationFactoryRequiresAnActorIdentifier()
    {
        Assert.Throws<ArgumentException>(() => ActionActor.Automation(""));
        Assert.Throws<ArgumentException>(() => ActionActor.Automation("   "));
        Assert.Throws<ArgumentNullException>(() => ActionActor.Automation(null!));
    }

    [Fact]
    public void AutomationFactoryTrimsAndCarriesNoStaffRoles()
    {
        var actor = ActionActor.Automation("  pegasus-automation  ");

        Assert.Equal(ActorKind.Automation, actor.Kind);
        Assert.Equal("pegasus-automation", actor.SubjectId);
        Assert.Empty(actor.Roles);
        Assert.False(actor.IsInRole(StaffRole.Administrator));
    }

    [Fact]
    public void AutomationIsGrantedOnlyOrdinaryCasework()
    {
        Assert.True(StaffAuthorization.IsAuthorized(
            Automation(),
            StaffAccessRight.PerformCasework));
    }

    [Theory]
    [InlineData(StaffAccessRight.AccessStaffApplication)]
    [InlineData(StaffAccessRight.ManageStaffAccounts)]
    [InlineData(StaffAccessRight.AssignStaffRoles)]
    [InlineData(StaffAccessRight.ManageOrganizationsAndPrincipals)]
    [InlineData(StaffAccessRight.ManageWorkflowConfiguration)]
    [InlineData(StaffAccessRight.ManageApprovedMailboxes)]
    [InlineData(StaffAccessRight.ManageApprovedOutlookCategories)]
    [InlineData(StaffAccessRight.ManageAutomationClients)]
    [InlineData(StaffAccessRight.ExecuteSystemWork)]
    [InlineData(StaffAccessRight.SubmitRequestUpload)]
    public void AutomationIsDeniedEveryOtherRight(StaffAccessRight permission)
    {
        Assert.False(StaffAuthorization.IsAuthorized(Automation(), permission));
        var denied = Assert.Throws<StaffAuthorizationException>(() =>
            StaffAuthorization.Require(Automation(), permission));
        Assert.Equal(permission, denied.Permission);
    }

    [Fact]
    public void EveryAccessRightHasAnExplicitAutomationDecision()
    {
        // Fail-closed proof for the whole surface: exactly one right is
        // granted; anything added later defaults to denied until this test is
        // deliberately revisited.
        var granted = Enum.GetValues<StaffAccessRight>()
            .Where(permission => StaffAuthorization.IsAuthorized(Automation(), permission))
            .ToArray();

        Assert.Equal([StaffAccessRight.PerformCasework], granted);
    }

    [Fact]
    public void UnknownAccessRightFailsClosedForAutomation()
    {
        Assert.False(StaffAuthorization.IsAuthorized(Automation(), (StaffAccessRight)999));
    }

    [Fact]
    public void ManageAutomationClientsRequiresAnAdministratorStaffActor()
    {
        Assert.True(StaffAuthorization.IsAuthorized(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]),
            StaffAccessRight.ManageAutomationClients));
        Assert.False(StaffAuthorization.IsAuthorized(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]),
            StaffAccessRight.ManageAutomationClients));
        Assert.False(StaffAuthorization.IsAuthorized(
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            StaffAccessRight.ManageAutomationClients));
        Assert.False(StaffAuthorization.IsAuthorized(
            ActionActor.SystemWorker("automation-clients-test"),
            StaffAccessRight.ManageAutomationClients));
    }

    [Fact]
    public async Task AutomationActorMayExecuteTheSharedCaseSearchUseCase()
    {
        var store = new RecordingStore();
        var search = new SearchCases(store);
        var actor = Automation();

        await search.ExecuteAsync(
            new(actor, new CaseSearchFilters(Query: "QDOS")),
            default);

        var query = Assert.IsType<SearchCasesQuery>(store.Query);
        Assert.Same(actor, query.Actor);
    }

    private sealed class RecordingStore : ICaseQueryStore
    {
        public SearchCasesQuery? Query { get; private set; }

        public Task<SearchCasesResult> SearchAsync(
            SearchCasesQuery query,
            CancellationToken cancellationToken)
        {
            Query = query;
            return Task.FromResult(new SearchCasesResult([], query.Page, query.PageSize, false, false));
        }

        public Task<CaseDetails?> GetAsync(
            GetCaseQuery query,
            CancellationToken cancellationToken) => Task.FromResult<CaseDetails?>(null);
    }
}

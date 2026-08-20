using Pegasus.Web.Mcp;
using Pegasus.Web.Presentation;

namespace Pegasus.IntegrationTests;

/// <summary>
/// PLAT-011: the Automation activity view's Subject column
/// (<c>Pages/Administration/Automation/Activity.cshtml</c>, via
/// <c>ActivityModel.SubjectLabel</c>) resolves through
/// <see cref="OperatorLabels.AutomationActorLabel"/> — this is the single place
/// that decision is made, so it is covered directly rather than through HTTP.
/// </summary>
public sealed class AutomationActorLabelTests
{
    [Fact]
    public void TheConfiguredClientsSubjectResolvesToTheClientName()
    {
        var label = OperatorLabels.AutomationActorLabel("pegasus-automation", "pegasus-automation");

        Assert.Equal(AutomationMcp.ClientDisplayName, label);
    }

    [Fact]
    public void AGuidShapedSubjectThatDoesNotMatchTheConfiguredClientIsNeverShownRaw()
    {
        var subjectId = Guid.NewGuid().ToString("D");

        var label = OperatorLabels.AutomationActorLabel(subjectId, "pegasus-automation");

        Assert.Equal("Unknown automation client", label);
        Assert.DoesNotContain(subjectId, label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AGuidShapedSubjectIsNeverShownRawWhenNoClientIsConfigured()
    {
        var subjectId = Guid.NewGuid().ToString("D");

        var label = OperatorLabels.AutomationActorLabel(subjectId, configuredClientId: null);

        Assert.Equal("Unknown automation client", label);
    }

    [Fact]
    public void ANonGuidSubjectThatDoesNotMatchTheConfiguredClientPassesThroughAsIsAlreadyHonest()
    {
        var label = OperatorLabels.AutomationActorLabel("anonymous", "pegasus-automation");

        Assert.Equal("anonymous", label);
    }
}

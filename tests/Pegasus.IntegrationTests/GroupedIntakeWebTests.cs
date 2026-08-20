using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class GroupedIntakeWebTests
{
    [Fact]
    public async Task MultipleFilesCreateOneGroupAndIndependentReceipts()
    {
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var upload = await IntakeWebDriver.PostUploadManyAsync(
            client,
            form.AntiforgeryToken,
            form.ExternalReceiptToken,
            [
                ("overview.jpg", "image/jpeg", [1, 2, 3]),
                ("damage-close-up.jpg", "image/jpeg", [4, 5, 6])
            ]);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, upload.StatusCode);
        Assert.NotNull(upload.Location);
        var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());
        await IntakeWebDriver.ProcessQueuedAsync(factory, upload);

        await using var scope = factory.Services.CreateAsyncScope();
        var groups = scope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>();
        var group = await groups.GetAsync(groupId);

        Assert.NotNull(group);
        Assert.Equal(["overview.jpg", "damage-close-up.jpg"], group!.Members.Select(item => item.SourceFileName));
        Assert.Equal(2, group.Members.Select(item => item.StagedReceiptId).Distinct().Count());
    }

    [Fact]
    public async Task EveryMemberResolvesToItsGroupByItsOwnSourceIdentity()
    {
        // The test INTK-011 could not write: the ordinal-0 member carries the
        // parent token verbatim (INTK-005), and the source-identity lookup
        // must still find its group — reconciliation, replay, and the group
        // automation all resolve members this way.
        using var factory = new IntakeWebApplicationFactory();
        using var client = IntakeWebDriver.CreateClient(factory);
        var form = await IntakeWebDriver.GetUploadFormTokensAsync(client);
        var upload = await IntakeWebDriver.PostUploadManyAsync(
            client,
            form.AntiforgeryToken,
            form.ExternalReceiptToken,
            [
                ("overview.jpg", "image/jpeg", [1, 2, 3]),
                ("damage-close-up.jpg", "image/jpeg", [4, 5, 6])
            ]);
        var groupId = Guid.Parse(upload.Location!.OriginalString.Split('/').Last());

        await using var scope = factory.Services.CreateAsyncScope();
        var groups = scope.ServiceProvider.GetRequiredService<IIntakeSubmissionGroupStore>();
        var group = await groups.GetAsync(groupId);
        Assert.NotNull(group);

        foreach (var member in group!.Members)
        {
            var memberIdentity = new IntakeSourceIdentity(
                IntakeSourceChannel.ManualUpload,
                GroupedIntakeMemberToken.Create(group.SubmissionToken, member.Ordinal));
            var found = await groups.FindForMemberSourceAsync(memberIdentity);
            Assert.NotNull(found);
            Assert.Equal(group.Id, found!.Id);
        }

        var unrelated = await groups.FindForMemberSourceAsync(
            new IntakeSourceIdentity(IntakeSourceChannel.ManualUpload, Guid.NewGuid().ToString("N")));
        Assert.Null(unrelated);
    }
}

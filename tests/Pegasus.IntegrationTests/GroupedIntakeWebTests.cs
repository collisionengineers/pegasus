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
}

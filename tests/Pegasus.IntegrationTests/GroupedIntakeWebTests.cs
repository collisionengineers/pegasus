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

    /// <summary>
    /// Upload (13 September): one upload is one group with at most one decision
    /// panel; no member row links to a receipt.
    /// </summary>
    [Fact]
    public async Task TheGroupStatusShowsAtMostOneDecisionAndNoReceiptLink()
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
        await IntakeWebDriver.ProcessQueuedAsync(factory, upload);

        using var response = await client.GetAsync(upload.Location!.OriginalString);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        Assert.True(
            System.Text.RegularExpressions.Regex.Count(html, "data-upload-decision=") <= 1,
            "An upload shows one decision for the whole group, never one per file.");
        Assert.Contains("overview.jpg", html, StringComparison.Ordinal);
        Assert.Contains("damage-close-up.jpg", html, StringComparison.Ordinal);
        Assert.DoesNotMatch("href=\"/Received/[0-9a-fA-F-]{36}\"", html);
        Assert.DoesNotContain("Review this file", html, StringComparison.Ordinal);
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

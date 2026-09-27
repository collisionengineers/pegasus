using Pegasus.Core.Identity;
using Pegasus.Core.Operations;

namespace Pegasus.Core.Tests.Operations;

/// <summary>
/// E-mail templates (plan 02): each purpose's placeholders, what a saved body
/// may contain, how it renders, the built-in body at version 0, and who may
/// read and change a template.
/// </summary>
public sealed class EmailTemplatesTests
{
    private const EmailTemplatePurpose Purpose = EmailTemplatePurpose.TriageOutcomeReply;

    private static readonly ActionActor Administrator =
        ActionActor.Staff(Guid.Parse("11111111-1111-1111-1111-111111111111"), [StaffRole.Administrator]);

    [Fact]
    public void EachPlaceholderRendersItsValue()
    {
        var rendered = EmailTemplates.Render(
            "{registration}|{roadworthiness}|{repair outcome}|{reason}",
            Values(registration: "AB12CDE", roadworthiness: "Roadworthy", repairOutcome: "Repairable", reason: "Minor damage."));

        Assert.Equal("AB12CDE|Roadworthy|Repairable|Minor damage.", rendered);
        Assert.Equal(
            new[] { EmailTemplates.Registration, EmailTemplates.Roadworthiness, EmailTemplates.RepairOutcome, EmailTemplates.FindingReason },
            EmailTemplates.Placeholders(Purpose));
    }

    [Fact]
    public void AnEmptyPlaceholderLeavesItsLineOut()
    {
        var rendered = EmailTemplates.Render(
            "For {registration}.\nRoadworthiness: {roadworthiness}\nRepair outcome: {repair outcome}\n\nKind regards",
            Values(registration: "AB12CDE", roadworthiness: null, repairOutcome: "Total loss"));

        Assert.Equal("For AB12CDE.\nRepair outcome: Total loss\n\nKind regards", rendered);
    }

    [Fact]
    public void ALineWithOneFilledPlaceholderKeepsIt()
    {
        var rendered = EmailTemplates.Render(
            "Outcome: {roadworthiness}{repair outcome}",
            Values(roadworthiness: null, repairOutcome: "Total loss"));

        Assert.Equal("Outcome: Total loss", rendered);
    }

    [Fact]
    public void AnUnknownPlaceholderIsRefusedByName()
    {
        var refused = Assert.Throws<UnknownEmailTemplatePlaceholderException>(
            () => EmailTemplates.Validate(Purpose, "Dear {claimant},\nYour vehicle {registration}."));

        Assert.Equal("claimant", refused.Placeholder);
        Assert.Contains("{claimant}", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBodyIsRequiredAndBounded()
    {
        Assert.Throws<ArgumentException>(() => EmailTemplates.Validate(Purpose, "   "));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => EmailTemplates.Validate(Purpose, new string('a', EmailTemplates.MaximumBodyLength + 1)));

        var longest = new string('a', EmailTemplates.MaximumBodyLength);
        Assert.Equal(longest, EmailTemplates.Validate(Purpose, longest));
        Assert.Equal("One\nTwo", EmailTemplates.Validate(Purpose, "One\r\nTwo\r\n"));
    }

    [Fact]
    public async Task TheBuiltInBodyIsReturnedAtVersionZeroUntilOneIsSaved()
    {
        var store = new TemplateStore();

        var template = await new GetEmailTemplate(store).ExecuteAsync(Administrator, Purpose, CancellationToken.None);

        Assert.Equal(0, template.Version);
        Assert.Equal(EmailTemplates.DefaultBody(Purpose), template.Body);
        Assert.Null(template.UpdatedAtUtc);
        Assert.Null(template.UpdatedBy);
        Assert.DoesNotContain("{reason}", template.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASaveIsValidatedBeforeItReachesTheStore()
    {
        var store = new TemplateStore();
        var update = new UpdateEmailTemplate(store);

        await Assert.ThrowsAsync<UnknownEmailTemplatePlaceholderException>(() => update.ExecuteAsync(
            new(Purpose, "Hello {claimant}", 0, Administrator, "save-unknown"),
            CancellationToken.None));
        Assert.Null(store.Saved);

        var saved = await update.ExecuteAsync(
            new(Purpose, "Result for {registration}\r\n", 0, Administrator, " save-valid "),
            CancellationToken.None);

        Assert.Equal("Result for {registration}", saved.Body);
        Assert.Equal("save-valid", store.Saved!.OperationKey);
    }

    [Theory]
    [InlineData(StaffRole.Engineer)]
    [InlineData(StaffRole.User)]
    public async Task OnlyAnAdministratorReadsOrChangesATemplate(StaffRole role)
    {
        var staff = ActionActor.Staff(Guid.NewGuid(), [role]);
        var store = new TemplateStore();

        Assert.True(StaffAuthorization.IsAuthorized(Administrator, StaffAccessRight.ManageEmailTemplates));
        Assert.False(StaffAuthorization.IsAuthorized(staff, StaffAccessRight.ManageEmailTemplates));
        Assert.False(StaffAuthorization.IsAuthorized(
            ActionActor.Automation("templates-test"), StaffAccessRight.ManageEmailTemplates));
        await Assert.ThrowsAsync<StaffAuthorizationException>(
            () => new GetEmailTemplate(store).ExecuteAsync(staff, Purpose, CancellationToken.None));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => new UpdateEmailTemplate(store).ExecuteAsync(
            new(Purpose, "Result for {registration}", 0, staff, "save-refused"),
            CancellationToken.None));
        Assert.Null(store.Saved);
    }

    /// <summary>
    /// Every staff member composing a reply opens it with the saved template:
    /// reading the text to compose is casework, not template management.
    /// </summary>
    [Fact]
    public async Task StaffComposingAReplyRenderTheSavedTemplate()
    {
        var store = new TemplateStore
        {
            Current = new(Purpose, "Result for {registration}\nNote: {reason}", 3, DateTimeOffset.UnixEpoch, "admin")
        };
        var engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

        var rendered = await new RenderEmailTemplate(store).ExecuteAsync(
            engineer, Purpose, Values(registration: "AB12CDE"), CancellationToken.None);

        Assert.Equal("Result for AB12CDE", rendered);
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => new RenderEmailTemplate(store).ExecuteAsync(
            ActionActor.SystemWorker("templates-test"), Purpose, Values(), CancellationToken.None));
    }

    private static Dictionary<string, string?> Values(
        string? registration = null,
        string? roadworthiness = null,
        string? repairOutcome = null,
        string? reason = null) => new(StringComparer.Ordinal)
    {
        [EmailTemplates.Registration] = registration,
        [EmailTemplates.Roadworthiness] = roadworthiness,
        [EmailTemplates.RepairOutcome] = repairOutcome,
        [EmailTemplates.FindingReason] = reason
    };

    private sealed class TemplateStore : IEmailTemplateStore
    {
        public EmailTemplate? Current { get; init; }

        public UpdateEmailTemplateRequest? Saved { get; private set; }

        public Task<EmailTemplate?> GetAsync(EmailTemplatePurpose purpose, CancellationToken cancellationToken) =>
            Task.FromResult(Current);

        public Task<EmailTemplate> UpdateAsync(UpdateEmailTemplateRequest request, CancellationToken cancellationToken)
        {
            Saved = request;
            return Task.FromResult(new EmailTemplate(
                request.Purpose, request.Body, request.ExpectedVersion + 1, DateTimeOffset.UnixEpoch, request.Actor.SubjectId));
        }
    }
}

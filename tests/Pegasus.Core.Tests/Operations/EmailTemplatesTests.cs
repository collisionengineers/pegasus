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

    [Fact]
    public void EachPurposesBuiltInBodyIsValidForItsOwnPlaceholders()
    {
        foreach (var purpose in Enum.GetValues<EmailTemplatePurpose>())
        {
            var body = EmailTemplates.DefaultBody(purpose);
            Assert.Equal(body, EmailTemplates.Validate(purpose, body));
        }
    }

    [Fact]
    public void TheReportDeliveryTemplateHasItsOwnPlaceholders()
    {
        const EmailTemplatePurpose delivery = EmailTemplatePurpose.CaseReportDelivery;

        Assert.Equal(
            new[]
            {
                EmailTemplates.CaseReference, EmailTemplates.Registration, EmailTemplates.Outcome,
                EmailTemplates.PrincipalName, EmailTemplates.SupersededReportDate, EmailTemplates.Greeting
            },
            EmailTemplates.Placeholders(delivery));
        Assert.Equal(
            "Our reference: {case reference}",
            EmailTemplates.Validate(delivery, "Our reference: {case reference}"));
        var refused = Assert.Throws<UnknownEmailTemplatePlaceholderException>(
            () => EmailTemplates.Validate(delivery, "Roadworthiness: {roadworthiness}"));
        Assert.Equal("roadworthiness", refused.Placeholder);
        Assert.Throws<UnknownEmailTemplatePlaceholderException>(
            () => EmailTemplates.Validate(Purpose, "Our reference: {case reference}"));
    }

    [Fact]
    public void TheReportDeliveryBodyIsTheSopWordingWithTheGreeting()
    {
        var body = EmailTemplates.DefaultBody(EmailTemplatePurpose.CaseReportDelivery);

        Assert.Equal(
            "Good {greeting},\n\nPlease see attached report and fee note.\n\nAny issues let us know.\n\nKind Regards",
            body);
        Assert.Equal(
            "Good morning,\n\nPlease see attached report and fee note.\n\nAny issues let us know.\n\nKind Regards",
            EmailTemplates.Render(body, ReportValues(supersededReportDate: null)));
        Assert.Equal(body, EmailTemplates.Validate(EmailTemplatePurpose.CaseReportDelivery, body));
    }

    [Fact]
    public void ASavedBodyCanStillNameTheSupersededReportDateAndLeavesItOutOnAFirstSend()
    {
        const string body =
            "Our reference: {case reference}\nThis report supersedes our report dated {superseded report date}.";

        Assert.Contains(
            "supersedes our report dated 19 August 2026",
            EmailTemplates.Render(body, ReportValues(supersededReportDate: "19 August 2026")),
            StringComparison.Ordinal);
        var first = EmailTemplates.Render(body, ReportValues(supersededReportDate: null));
        Assert.DoesNotContain("supersedes", first, StringComparison.Ordinal);
        Assert.Contains("Our reference: QDOS26001", first, StringComparison.Ordinal);
    }

    /// <summary>
    /// Staff preparing a delivery are pre-filled from the saved report
    /// delivery template; reading it to compose is casework.
    /// </summary>
    [Fact]
    public async Task StaffPreparingAReportRenderTheSavedReportDeliveryTemplate()
    {
        const EmailTemplatePurpose delivery = EmailTemplatePurpose.CaseReportDelivery;
        var store = new TemplateStore
        {
            Current = new(delivery, "Report {case reference} for {principal name}", 2, DateTimeOffset.UnixEpoch, "admin")
        };
        var engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);

        var rendered = await new RenderEmailTemplate(store).ExecuteAsync(
            engineer, delivery, ReportValues(supersededReportDate: null), CancellationToken.None);

        Assert.Equal("Report QDOS26001 for Principal Ltd", rendered);
    }

    /// <summary>
    /// The Case chaser names the vehicle, the material and the parties, never
    /// the internal Case/PO reference (operator, 5 October 2026).
    /// </summary>
    [Fact]
    public void TheCaseChaserTemplateHasNoCaseReference()
    {
        const EmailTemplatePurpose chaser = EmailTemplatePurpose.CaseChaser;

        Assert.Equal(
            new[]
            {
                EmailTemplates.Registration, EmailTemplates.OutstandingMaterial,
                EmailTemplates.PrincipalName, EmailTemplates.Claimant
            },
            EmailTemplates.Placeholders(chaser));
        Assert.DoesNotContain(EmailTemplates.CaseReference, EmailTemplates.Placeholders(chaser));
        var refused = Assert.Throws<UnknownEmailTemplatePlaceholderException>(
            () => EmailTemplates.Validate(chaser, "Our reference: {case reference}"));
        Assert.Equal("case reference", refused.Placeholder);
    }

    [Fact]
    public void TheCaseChaserBodyAsksForTheOutstandingMaterialAndSignsOff()
    {
        var rendered = EmailTemplates.Render(
            EmailTemplates.DefaultBody(EmailTemplatePurpose.CaseChaser),
            ChaserValues(registration: "PK12TMZ", outstandingMaterial: "Images"));

        Assert.Equal(
            "Please provide the outstanding material for PK12TMZ: Images.\n"
            + "\n"
            + "Kind regards\n"
            + "Collision Engineers",
            rendered);
    }

    [Fact]
    public void TheCaseChaserBodyWithNoFactsKeepsOnlyTheSignOff()
    {
        var rendered = EmailTemplates.Render(
            EmailTemplates.DefaultBody(EmailTemplatePurpose.CaseChaser),
            ChaserValues(registration: null, outstandingMaterial: null));

        Assert.Equal("\nKind regards\nCollision Engineers", rendered);
    }

    private static Dictionary<string, string?> ChaserValues(string? registration, string? outstandingMaterial) =>
        new(StringComparer.Ordinal)
        {
            [EmailTemplates.Registration] = registration,
            [EmailTemplates.OutstandingMaterial] = outstandingMaterial,
            [EmailTemplates.PrincipalName] = "Principal Ltd",
            [EmailTemplates.Claimant] = "Jane Driver"
        };

    private static Dictionary<string, string?> ReportValues(string? supersededReportDate) => new(StringComparer.Ordinal)
    {
        [EmailTemplates.CaseReference] = "QDOS26001",
        [EmailTemplates.Registration] = "PK12TMZ",
        [EmailTemplates.Outcome] = "Total loss",
        [EmailTemplates.PrincipalName] = "Principal Ltd",
        [EmailTemplates.SupersededReportDate] = supersededReportDate,
        [EmailTemplates.Greeting] = "morning"
    };

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

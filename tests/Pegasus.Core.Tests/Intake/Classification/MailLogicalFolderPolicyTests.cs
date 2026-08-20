using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Classification;

public sealed class MailLogicalFolderPolicyTests
{
    public static TheoryData<MailCategory, MailLogicalFolderType> EverySettledCategory
    {
        get
        {
            var data = new TheoryData<MailCategory, MailLogicalFolderType>();
            foreach (var family in Enum.GetValues<ReceivedMailFamily>())
            {
                var subtypes = MailTaxonomy.ConfirmedReceivedSubtypes[family];
                if (subtypes.Length == 0)
                {
                    data.Add(MailCategory.Received(family), Expected(family, null));
                    continue;
                }

                foreach (var subtype in subtypes)
                {
                    data.Add(MailCategory.Received(family, subtype), Expected(family, subtype));
                }
            }

            foreach (var family in Enum.GetValues<SentMailFamily>())
            {
                data.Add(MailCategory.Sent(family), MailLogicalFolderType.Other);
            }

            data.Add(
                MailCategory.Other(MailDirection.Received, "supplier-newsletter", "No named category fits."),
                MailLogicalFolderType.Other);
            return data;
        }
    }

    [Fact]
    public void CatalogueHasOneStableDefinitionForEveryLogicalType()
    {
        Assert.Equal(Enum.GetValues<MailLogicalFolderType>().Length, MailLogicalFolders.All.Count);
        Assert.Equal(MailLogicalFolders.All.Count, MailLogicalFolders.All.Select(item => item.Key).Distinct().Count());
        Assert.Equal(MailLogicalFolders.All.Count, MailLogicalFolders.All.Select(item => item.Label).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(EverySettledCategory))]
    public void MapsEverySettledCategoryWithoutChangingOperationalDestination(
        MailCategory category,
        MailLogicalFolderType expected)
    {
        var classification = Classified(category);
        var operational = MailOperationalDestinationPolicy.Map(classification);

        var result = MailLogicalFolderPolicy.Map(classification);

        Assert.Equal(expected, result.FolderType);
        Assert.Equal(category, result.Classification);
        Assert.Same(category, classification.Category);
        Assert.Equal(MailLogicalFolderPolicy.Key, result.PolicyKey);
        Assert.Equal(MailLogicalFolderPolicy.Version, result.PolicyVersion);
        Assert.Equal(operational, MailOperationalDestinationPolicy.Map(classification));
    }

    [Theory]
    [MemberData(nameof(EverySettledCategory))]
    public void ReplyContextDoesNotChangeTheLogicalFolder(
        MailCategory category,
        MailLogicalFolderType expected)
    {
        var reply = category.IsOther
            ? category
            : category.Direction == MailDirection.Received
                ? MailCategory.Received(category.ReceivedFamily!.Value, category.Subtype, isReplyContext: true)
                : MailCategory.Sent(category.SentFamily!.Value, isReplyContext: true);

        Assert.Equal(expected, MailLogicalFolderPolicy.Map(Classified(reply)).FolderType);
    }

    [Theory]
    [InlineData(MailClassificationOutcome.Ambiguous)]
    [InlineData(MailClassificationOutcome.Unclassified)]
    public void AbstentionHasNoAutomaticFolder(MailClassificationOutcome outcome)
    {
        var classification = outcome == MailClassificationOutcome.Ambiguous
            ? MailClassificationResult.Ambiguous(["General", "billing"], [], "conflict", "test", 1)
            : MailClassificationResult.Unclassified([], "no match", "test", 1);

        var result = MailLogicalFolderPolicy.Map(classification);

        Assert.Null(result.FolderType);
        Assert.Null(result.Classification);
    }

    private static MailClassificationResult Classified(MailCategory category) =>
        MailClassificationResult.Classified(category, [], "staff-confirmed", "test", 1);

    private static MailLogicalFolderType Expected(ReceivedMailFamily family, string? subtype) =>
        family switch
        {
            ReceivedMailFamily.General when subtype == "general-chase" => MailLogicalFolderType.CaseQueries,
            ReceivedMailFamily.General => MailLogicalFolderType.NoAction,
            ReceivedMailFamily.Billing => MailLogicalFolderType.Billing,
            ReceivedMailFamily.NewInstructionReceived when subtype == "audit" => MailLogicalFolderType.Audits,
            ReceivedMailFamily.NewInstructionReceived when subtype == "diminution" => MailLogicalFolderType.Diminution,
            ReceivedMailFamily.NewInstructionReceived when subtype == "inspection" => MailLogicalFolderType.Instructions,
            ReceivedMailFamily.NewInstructionReceived when subtype == "new-client" => MailLogicalFolderType.NewClients,
            ReceivedMailFamily.NewInstructionReceived when subtype == "website-enquiry" => MailLogicalFolderType.Enquiries,
            ReceivedMailFamily.NonClientRelated => MailLogicalFolderType.Other,
            ReceivedMailFamily.InProgressCases when subtype == "cancellation" => MailLogicalFolderType.Cancellations,
            ReceivedMailFamily.InProgressCases => MailLogicalFolderType.CaseUpdates,
            ReceivedMailFamily.PostReportEmails => MailLogicalFolderType.CaseQueries,
            ReceivedMailFamily.PreInstructionEmails when subtype == "images-received" => MailLogicalFolderType.Images,
            ReceivedMailFamily.PreInstructionEmails => MailLogicalFolderType.PreInstructions,
            ReceivedMailFamily.InternalCc => MailLogicalFolderType.Other,
            _ => throw new ArgumentOutOfRangeException(nameof(family), family, null)
        };
}

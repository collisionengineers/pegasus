using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Classification;

public sealed class MailOperationalDestinationPolicyTests
{
    public static TheoryData<MailCategory, MailOperationalDestination> EverySettledCategory
    {
        get
        {
            var data = new TheoryData<MailCategory, MailOperationalDestination>();
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
                data.Add(MailCategory.Sent(family), MailOperationalDestination.Other);
            }

            data.Add(
                MailCategory.Other(MailDirection.Received, "supplier-newsletter", "No named category fits."),
                MailOperationalDestination.Other);
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(EverySettledCategory))]
    public void MapsSettledCategoryWithoutChangingIt(
        MailCategory category,
        MailOperationalDestination expected)
    {
        var classification = Classified(category);

        var result = MailOperationalDestinationPolicy.Map(classification);

        Assert.Equal(expected, result.Destination);
        Assert.Same(category, classification.Category);
        Assert.Equal(MailOperationalDestinationPolicy.Key, result.PolicyKey);
        Assert.Equal(MailOperationalDestinationPolicy.Version, result.PolicyVersion);
    }

    [Theory]
    [InlineData(MailClassificationOutcome.Ambiguous)]
    [InlineData(MailClassificationOutcome.Unclassified)]
    public void AbstentionFailsClosedToNeedsSorting(MailClassificationOutcome outcome)
    {
        var classification = outcome == MailClassificationOutcome.Ambiguous
            ? MailClassificationResult.Ambiguous(["General", "billing"], [], "conflict", "test", 1)
            : MailClassificationResult.Unclassified([], "no match", "test", 1);

        Assert.Equal(
            MailOperationalDestination.NeedsSorting,
            MailOperationalDestinationPolicy.Map(classification).Destination);
    }

    private static MailClassificationResult Classified(MailCategory category) =>
        MailClassificationResult.Classified(category, [], "staff-confirmed", "test", 1);

    private static MailOperationalDestination Expected(
        ReceivedMailFamily family,
        string? subtype) => family switch
        {
            ReceivedMailFamily.NewInstructionReceived => MailOperationalDestination.ReceivingWork,
            ReceivedMailFamily.PostReportEmails => MailOperationalDestination.Queries,
            ReceivedMailFamily.Billing when subtype == "billing-query" => MailOperationalDestination.Queries,
            ReceivedMailFamily.PreInstructionEmails when subtype == "triage-request" => MailOperationalDestination.Triage,
            _ => MailOperationalDestination.Other
        };
}

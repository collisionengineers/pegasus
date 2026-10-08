using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Classification;

public sealed class MailOperationalDestinationPolicyTests
{
    public static TheoryData<MailCategory, MailOperationalDestination?> EverySettledCategory
    {
        get
        {
            var data = new TheoryData<MailCategory, MailOperationalDestination?>();
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

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(EverySettledCategory))]
    public void MapsSettledCategoryWithoutChangingIt(
        MailCategory category,
        MailOperationalDestination? expected)
    {
        var classification = Classified(category);

        var result = MailOperationalDestinationPolicy.Map(classification);

        Assert.Equal(expected, result.Destination);
        Assert.Equal(category, result.Classification);
        Assert.Same(category, classification.Category);
        Assert.Equal(MailOperationalDestinationPolicy.Key, result.PolicyKey);
        Assert.Equal(MailOperationalDestinationPolicy.Version, result.PolicyVersion);
        if (expected is { } destination)
        {
            Assert.True(Matches(MailOperationalDestinationPolicy.Query(destination), category));
        }
    }

    [Fact]
    public void KnownClassificationWithoutAWorkViewHasNoDestination()
    {
        var result = MailOperationalDestinationPolicy.Map(Classified(
            MailCategory.Received(ReceivedMailFamily.General, "autoreply")));

        Assert.Null(result.Destination);
        Assert.NotNull(result.Classification);
    }

    [Fact]
    public void UnclassifiedFailsClosedToUnidentified()
    {
        var classification = MailClassificationResult.Unclassified([], "no match", "test", 1);
        var result = MailOperationalDestinationPolicy.Map(classification);

        Assert.Equal(MailOperationalDestination.Unidentified, result.Destination);
        Assert.Null(result.Classification);
        Assert.True(MailOperationalDestinationPolicy.Query(
            MailOperationalDestination.Unidentified).IncludesUnidentified);
        Assert.False(MailOperationalDestinationPolicy.Query(
            MailOperationalDestination.Triage).IncludesUnidentified);
    }

    private static MailClassificationResult Classified(MailCategory category) =>
        MailClassificationResult.Classified(category, [], "staff-confirmed", "test", 1);

    private static MailOperationalDestination? Expected(
        ReceivedMailFamily family,
        string? subtype) => family switch
        {
            ReceivedMailFamily.NewInstructionReceived => MailOperationalDestination.ReceivingWork,
            ReceivedMailFamily.PostReportEmails => MailOperationalDestination.Queries,
            ReceivedMailFamily.Billing when subtype == "billing-query" => MailOperationalDestination.Queries,
            ReceivedMailFamily.PreInstructionEmails when subtype == "triage-request" => MailOperationalDestination.Triage,
            _ => null
        };

    private static bool Matches(
        MailOperationalDestinationQuery query,
        MailCategory category) =>
        query.Families.Contains(category.ReceivedFamily)
        || (query.ExactClassification is { } exact
            && exact.ReceivedFamily == category.ReceivedFamily
            && exact.Subtype == category.Subtype);
}

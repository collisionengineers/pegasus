using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Qdos;

public sealed class QdosMailRoutePolicyTests
{
    [Fact]
    public void PolicyVersionIsFour()
    {
        Assert.Equal("qdos_mail_route", QdosMailRoutePolicy.Key);
        Assert.Equal(4, QdosMailRoutePolicy.Version);
    }

    [Fact]
    public void AcceptedSetIsExactlyTheThreeOperatorAcceptedDomains()
    {
        string[] expected = ["qdosassist.co.uk", "qdoslaw.co.uk", "qdosassists.co.uk"];
        Assert.Equal(expected, QdosMailRoutePolicy.AcceptedDirectDomains.ToArray());
    }

    [Theory]
    [InlineData("qdosassist.co.uk")]
    [InlineData("qdoslaw.co.uk")]
    [InlineData("qdosassists.co.uk")]
    public void EachAcceptedDomainIsAcceptedDirectly(string domain)
    {
        var result = new QdosMailRoutePolicy().Evaluate(
            Readable(
                transport:
                [
                    new(
                        IntakeEvidenceSource.Sender,
                        $"instructions@{domain}",
                        IntakeSenderIdentityKind.Transport,
                        "outer message")
                ]));

        Assert.Equal(MailRouteDisposition.Accepted, result.Disposition);
        var route = Assert.IsType<MailRouteSelection>(result.SelectedRoute);
        Assert.Equal(MailRouteKind.DirectProvider, route.Kind);
        Assert.Equal("QDOS", route.WorkProviderCode);
        Assert.Equal(4, result.PolicyVersion);
    }

    [Theory]
    [InlineData("qdosassist.co.uk")]
    [InlineData("qdoslaw.co.uk")]
    [InlineData("qdosassists.co.uk")]
    public void EachAcceptedDomainIsAcceptedThroughAStaffForward(string domain)
    {
        var result = new QdosMailRoutePolicy().Evaluate(
            Readable(
                transport:
                [
                    new(
                        IntakeEvidenceSource.Sender,
                        "staff@collisionengineers.co.uk",
                        IntakeSenderIdentityKind.Transport,
                        "outer message"),
                    new(
                        IntakeEvidenceSource.Sender,
                        $"instructions@{domain}",
                        IntakeSenderIdentityKind.AttachedOriginal,
                        "attached original")
                ]));

        Assert.Equal(MailRouteDisposition.Accepted, result.Disposition);
        Assert.Equal($"instructions@{domain}", result.EffectiveSender?.Address);
    }

    [Fact]
    public void MatchedPredicateReasonNamesTheExactAcceptedDomain()
    {
        var result = new QdosMailRoutePolicy().Evaluate(
            Readable(
                transport:
                [
                    new(
                        IntakeEvidenceSource.Sender,
                        "legal@qdoslaw.co.uk",
                        IntakeSenderIdentityKind.Transport,
                        "outer message")
                ]));

        var predicate = Assert.Single(
            result.Predicates,
            item => item.Key == "direct.qdos-domain");
        Assert.True(predicate.Matched);
        Assert.Contains("'qdoslaw.co.uk'", predicate.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("mail.qdosassist.co.uk")]
    [InlineData("qdosassist.co.uk.example.com")]
    public void SubdomainAndSuffixWideningsAreRejected(string domain)
    {
        var result = new QdosMailRoutePolicy().Evaluate(
            Readable(
                transport:
                [
                    new(
                        IntakeEvidenceSource.Sender,
                        $"instructions@{domain}",
                        IntakeSenderIdentityKind.Transport,
                        "outer message")
                ]));

        Assert.Equal(MailRouteDisposition.NoMatch, result.Disposition);
        Assert.Null(result.SelectedRoute);
    }

    [Theory]
    [InlineData("pch-ltd.com")]
    [InlineData("connexus.co.uk")]
    [InlineData("ensurance-claims.co.uk")]
    [InlineData("ax-uk.com")]
    [InlineData("oakwoodsolicitors.co.uk")]
    [InlineData("oakwoodscotland.co.uk")]
    [InlineData("knightsbridgesolicitors.co.uk")]
    [InlineData("robertjameslaw.co.uk")]
    [InlineData("blackstone-legal.co.uk")]
    [InlineData("dfd-solicitors.co.uk")]
    [InlineData("qc-law.co.uk")]
    [InlineData("fairwaylegal.co.uk")]
    [InlineData("montrealprestige.co.uk")]
    public void NoNonQdosProviderDomainFromTheInventoryIsAccepted(string domain)
    {
        var result = new QdosMailRoutePolicy().Evaluate(
            Readable(
                transport:
                [
                    new(
                        IntakeEvidenceSource.Sender,
                        $"claims@{domain}",
                        IntakeSenderIdentityKind.Transport,
                        "outer message")
                ]));

        Assert.Equal(MailRouteDisposition.NoMatch, result.Disposition);
        Assert.Null(result.SelectedRoute);
    }

    [Fact]
    public void StaffForwardUsesUnambiguousAttachedOriginalAndRetainsTransportIdentity()
    {
        var result = new QdosMailRoutePolicy().Evaluate(
            Readable(
                transport:
                [
                    new(
                        IntakeEvidenceSource.Sender,
                        "staff@collisionengineers.co.uk",
                        IntakeSenderIdentityKind.Transport,
                        "outer message"),
                    new(
                        IntakeEvidenceSource.Sender,
                        "instructions@qdosassist.co.uk",
                        IntakeSenderIdentityKind.AttachedOriginal,
                        "attached original")
                ]));

        Assert.Equal(MailRouteDisposition.Accepted, result.Disposition);
        var route = Assert.IsType<MailRouteSelection>(result.SelectedRoute);
        Assert.Equal(MailRouteKind.DirectProvider, route.Kind);
        Assert.Equal("QDOS", route.RouteOwnerCode);
        Assert.Equal("QDOS", route.WorkProviderCode);
        Assert.Equal("staff@collisionengineers.co.uk", Assert.Single(result.TransportIdentities).Address);
        Assert.Equal("instructions@qdosassist.co.uk", Assert.Single(result.OriginalIdentities).Address);
        Assert.Equal("instructions@qdosassist.co.uk", result.EffectiveSender?.Address);
        Assert.Equal(QdosMailRoutePolicy.Version, result.PolicyVersion);
    }

    [Fact]
    public void StaffForwardUsesUnambiguousInlineOriginalAndRetainsTransportIdentity()
    {
        var result = new QdosMailRoutePolicy().Evaluate(
            Readable(
                transport:
                [
                    new(
                        IntakeEvidenceSource.Sender,
                        "staff@collisionengineers.co.uk",
                        IntakeSenderIdentityKind.Transport,
                        "outer message"),
                    new(
                        IntakeEvidenceSource.Sender,
                        "instructions@qdosassist.co.uk",
                        IntakeSenderIdentityKind.InlineForwardedOriginal,
                        "inline forwarded-message header")
                ]));

        Assert.Equal(MailRouteDisposition.Accepted, result.Disposition);
        Assert.Equal("staff@collisionengineers.co.uk", Assert.Single(result.TransportIdentities).Address);
        var original = Assert.Single(result.OriginalIdentities);
        Assert.Equal("instructions@qdosassist.co.uk", original.Address);
        Assert.Equal("inline forwarded-message header", original.SourceLabel);
        Assert.Equal("instructions@qdosassist.co.uk", result.EffectiveSender?.Address);
        Assert.Equal(4, result.PolicyVersion);
    }

    [Fact]
    public void StaffForwardWithConflictingAttachedAndInlineOriginalsFailsClosed()
    {
        var result = new QdosMailRoutePolicy().Evaluate(
            Readable(
                transport:
                [
                    new(
                        IntakeEvidenceSource.Sender,
                        "staff@collisionengineers.co.uk",
                        IntakeSenderIdentityKind.Transport,
                        "outer message"),
                    new(
                        IntakeEvidenceSource.Sender,
                        "first@qdosassist.co.uk",
                        IntakeSenderIdentityKind.AttachedOriginal,
                        "attached original"),
                    new(
                        IntakeEvidenceSource.Sender,
                        "second@qdosassist.co.uk",
                        IntakeSenderIdentityKind.InlineForwardedOriginal,
                        "inline forwarded-message header")
                ]));

        Assert.Equal(MailRouteDisposition.NeedsSorting, result.Disposition);
        Assert.Null(result.EffectiveSender);
        Assert.Equal(2, result.OriginalIdentities.Count);
    }

    [Fact]
    public void StaffForwardWithConflictingAttachedOriginalsFailsClosed()
    {
        var result = new QdosMailRoutePolicy().Evaluate(
            Readable(
                transport:
                [
                    new(
                        IntakeEvidenceSource.Sender,
                        "staff@collisionengineers.co.uk",
                        IntakeSenderIdentityKind.Transport,
                        "outer message"),
                    new(
                        IntakeEvidenceSource.Sender,
                        "first@qdosassist.co.uk",
                        IntakeSenderIdentityKind.AttachedOriginal,
                        "attached original one"),
                    new(
                        IntakeEvidenceSource.Sender,
                        "second@qdosassist.co.uk",
                        IntakeSenderIdentityKind.AttachedOriginal,
                        "attached original two")
                ]));

        Assert.Equal(MailRouteDisposition.NeedsSorting, result.Disposition);
        Assert.Null(result.SelectedRoute);
        Assert.Null(result.EffectiveSender);
        Assert.Equal(2, result.OriginalIdentities.Count);
        Assert.Contains(
            result.Predicates,
            predicate => predicate.Key == "forward.original-exactly-one" && !predicate.Matched);
    }

    private static IntakeSourceReadResult Readable(
        IReadOnlyList<IntakeTransportEvidence>? transport = null) =>
        new(
            IntakeSourceReadStatus.Readable,
            [],
            transport ?? [],
            [],
            false);
}

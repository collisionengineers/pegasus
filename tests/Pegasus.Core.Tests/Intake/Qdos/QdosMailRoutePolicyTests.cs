using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.Qdos;

public sealed class PrincipalMailRoutePolicyTests
{
    [Theory]
    [InlineData("ALS", "kalan@autologistic.co.uk")]
    [InlineData("AX", "instructions@ax-uk.com")]
    [InlineData("BC", "N.Ellahi@bakercoleman.co.uk")]
    [InlineData("BLACK", "instructions@blackstone-legal.co.uk")]
    [InlineData("DFD", "instructions@dfd-solicitors.co.uk")]
    [InlineData("FW", "instructions@fairwaylegal.co.uk")]
    [InlineData("KBS", "instructions@knightsbridgesolicitors.co.uk")]
    [InlineData("MP", "instructions@montrealprestige.co.uk")]
    [InlineData("OAK", "instructions@oakwoodscotland.co.uk")]
    [InlineData("OAK", "instructions@oakwoodsolicitors.co.uk")]
    [InlineData("PCH", "instructions@pch-ltd.com")]
    [InlineData("QCL", "instructions@qc-law.co.uk")]
    [InlineData("QDOS", "instructions@qdosassist.co.uk")]
    [InlineData("RJS", "instructions@robertjameslaw.co.uk")]
    [InlineData("SBL", "Claims@smartbusinesslink.com")]
    [InlineData("YML", "networkhduk@gmail.com")]
    public void EvidencedIdentityHasTheSamePrincipalDirectAndStaffForwarded(string principal, string address)
    {
        // Structural identity probes, not invented instruction emails.
        foreach (var forwarded in new[] { false, true })
        {
            IntakeTransportEvidence[] transport = forwarded
                ? [new(IntakeEvidenceSource.Sender, "digital@collisionengineers.co.uk", IntakeSenderIdentityKind.Transport, "outer"),
                   new(IntakeEvidenceSource.Sender, address, IntakeSenderIdentityKind.AttachedOriginal, "original")]
                : [new(IntakeEvidenceSource.Sender, address, IntakeSenderIdentityKind.Transport, "outer")];
            var result = new PrincipalMailRoutePolicy().Evaluate(Readable(transport: transport));
            Assert.Equal(MailRouteDisposition.Accepted, result.Disposition);
            Assert.Equal(principal, result.SelectedRoute?.WorkProviderCode);
            Assert.Equal(MailRouteKind.DirectProvider, result.SelectedRoute?.Kind);
            Assert.Equal(address.ToLowerInvariant(), result.EffectiveSender?.Address.ToLowerInvariant());
        }
    }

    [Theory]
    [InlineData("another@gmail.com")]
    [InlineData("networkhduk@gmail.com.invalid")]
    [InlineData("Claims@sbgl.co.uk")]
    [InlineData("kalan@mail.autologistic.co.uk")]
    [InlineData("kalan@autologistic.co.uk.invalid")]
    public void SharedMailboxOrUnprovedDomainDoesNotEstablishAPrincipal(string address)
    {
        var result = new PrincipalMailRoutePolicy().Evaluate(Readable(transport:
            [new(IntakeEvidenceSource.Sender, address, IntakeSenderIdentityKind.Transport, "outer")]));
        Assert.NotEqual(MailRouteDisposition.Accepted, result.Disposition);
        Assert.Null(result.SelectedRoute);
    }

    [Theory]
    [InlineData("connexus.co.uk")]
    [InlineData("ensurance-claims.co.uk")]
    public void AnIntermediaryRequiresOneAgreeingPchInstruction(string domain)
    {
        var read = Readable(transport:
            [new(IntakeEvidenceSource.Sender, $"instructions@{domain}", IntakeSenderIdentityKind.Transport, "outer")]);
        var sut = new PrincipalMailRoutePolicy();
        Assert.NotEqual(MailRouteDisposition.Accepted, sut.Evaluate(read).Disposition);
        var selection = InstructionPolicySelection.Selected(new PchInstructionExtractionPolicy());
        var accepted = sut.Evaluate(read, selection);
        Assert.Equal(MailRouteDisposition.Accepted, accepted.Disposition);
        Assert.Equal(MailRouteKind.Intermediary, accepted.SelectedRoute?.Kind);
        Assert.Equal("PCH", accepted.SelectedRoute?.WorkProviderCode);
        Assert.NotEqual(MailRouteDisposition.Accepted,
            sut.Evaluate(read, selection with { Policy = new QdosInstructionExtractionPolicy() }).Disposition);
    }

    [Fact]
    public void GeneralizedPolicyHasItsOwnVersionedIdentity()
    {
        Assert.Equal("principal_mail_route", PrincipalMailRoutePolicy.Key);
        Assert.Equal(1, PrincipalMailRoutePolicy.Version);
    }

    [Fact]
    public void AcceptedSetIsExactlyTheThreeOperatorAcceptedDomains()
    {
        string[] expected = ["qdosassist.co.uk", "qdoslaw.co.uk", "qdosassists.co.uk"];
        Assert.Equal(expected, PrincipalMailRoutePolicy.AcceptedIdentities["QDOS"].ToArray());
    }

    [Theory]
    [InlineData("qdosassist.co.uk")]
    [InlineData("qdoslaw.co.uk")]
    [InlineData("qdosassists.co.uk")]
    public void EachAcceptedDomainIsAcceptedDirectly(string domain)
    {
        var result = new PrincipalMailRoutePolicy().Evaluate(
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
        Assert.Equal(1, result.PolicyVersion);
    }

    [Theory]
    [InlineData("qdosassist.co.uk")]
    [InlineData("qdoslaw.co.uk")]
    [InlineData("qdosassists.co.uk")]
    public void EachAcceptedDomainIsAcceptedThroughAStaffForward(string domain)
    {
        var result = new PrincipalMailRoutePolicy().Evaluate(
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
        var result = new PrincipalMailRoutePolicy().Evaluate(
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
            item => item.Key == "direct.principal-identity");
        Assert.True(predicate.Matched);
        Assert.Contains("'qdoslaw.co.uk'", predicate.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("mail.qdosassist.co.uk")]
    [InlineData("qdosassist.co.uk.example.com")]
    public void SubdomainAndSuffixWideningsAreRejected(string domain)
    {
        var result = new PrincipalMailRoutePolicy().Evaluate(
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
    [InlineData("pch-ltd.com", "PCH")]
    [InlineData("connexus.co.uk", null)]
    [InlineData("ensurance-claims.co.uk", null)]
    [InlineData("ax-uk.com", "AX")]
    [InlineData("oakwoodsolicitors.co.uk", "OAK")]
    [InlineData("oakwoodscotland.co.uk", "OAK")]
    [InlineData("knightsbridgesolicitors.co.uk", "KBS")]
    [InlineData("robertjameslaw.co.uk", "RJS")]
    [InlineData("blackstone-legal.co.uk", "BLACK")]
    [InlineData("dfd-solicitors.co.uk", "DFD")]
    [InlineData("qc-law.co.uk", "QCL")]
    [InlineData("fairwaylegal.co.uk", "FW")]
    [InlineData("montrealprestige.co.uk", "MP")]
    public void EvidencedInventoryDomainsResolveTheirPrincipalButIntermediariesNeedAProfile(string domain, string? principal)
    {
        var result = new PrincipalMailRoutePolicy().Evaluate(
            Readable(
                transport:
                [
                    new(
                        IntakeEvidenceSource.Sender,
                        $"claims@{domain}",
                        IntakeSenderIdentityKind.Transport,
                        "outer message")
                ]));

        Assert.Equal(principal is null ? MailRouteDisposition.NoMatch : MailRouteDisposition.Accepted, result.Disposition);
        Assert.Equal(principal, result.SelectedRoute?.WorkProviderCode);
        if (principal is not null)
        {
            Assert.Equal(MailRouteKind.DirectProvider, result.SelectedRoute?.Kind);
            Assert.Equal($"claims@{domain}", result.EffectiveSender?.Address);
            Assert.Contains(result.Predicates, predicate => predicate.Key == "direct.principal-identity" && predicate.Matched);
        }
    }

    [Fact]
    public void StaffForwardUsesUnambiguousAttachedOriginalAndRetainsTransportIdentity()
    {
        var result = new PrincipalMailRoutePolicy().Evaluate(
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
        Assert.Equal(PrincipalMailRoutePolicy.Version, result.PolicyVersion);
    }

    [Fact]
    public void StaffForwardUsesUnambiguousInlineOriginalAndRetainsTransportIdentity()
    {
        var result = new PrincipalMailRoutePolicy().Evaluate(
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
        Assert.Equal(PrincipalMailRoutePolicy.Version, result.PolicyVersion);
    }

    [Fact]
    public void StaffForwardWithConflictingAttachedAndInlineOriginalsFailsClosed()
    {
        var result = new PrincipalMailRoutePolicy().Evaluate(
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
        var result = new PrincipalMailRoutePolicy().Evaluate(
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

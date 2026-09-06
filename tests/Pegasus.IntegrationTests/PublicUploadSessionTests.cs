using Pegasus.Core.Documents;
using Pegasus.Core.Intake;

namespace Pegasus.IntegrationTests;

/// <summary>
/// The one fixed submission session a public upload link may have, and the
/// typed refusal a link that outlived its limits version returns.
/// </summary>
/// <remarks>
/// <para>
/// The window and the finalization rule are pure policy over a fixed clock, so
/// they are proved directly rather than through a host: a real clock would make
/// a fifteen-minute boundary a waiting game rather than a fact.
/// </para>
/// <para>
/// This suite therefore needs no host and no database, and by that measure
/// belongs in <c>Pegasus.Core.Tests</c>. It sits here deliberately: the plan
/// names this path and the runner filter reads it from this project, and
/// <c>tests/Pegasus.Core.Tests/Documents/</c> is outside the slice's file
/// scope. It moves to the Core suite when the accept path is wired (A04) and
/// the filter moves with it.
/// </para>
/// </remarks>
public sealed class PublicUploadSessionTests
{
    private static readonly DateTimeOffset Start = new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void TheFirstConfirmedFileOpensAFifteenMinuteWindow()
    {
        var session = PublicUploadSessionPolicy.Start(NewSession(), Confirmed(), Start);

        Assert.Equal(Start, session.StartedAtUtc);
        Assert.Equal(Start.AddMinutes(15), session.ExpiresAtUtc);
        Assert.Equal(PublicUploadSessionState.Open, PublicUploadSessionPolicy.Evaluate(session, Start));
        Assert.Equal(
            PublicUploadSessionState.Open,
            PublicUploadSessionPolicy.Evaluate(session, Start.AddMinutes(14).AddSeconds(59)));
        Assert.Equal(
            PublicUploadSessionState.Expired,
            PublicUploadSessionPolicy.Evaluate(session, Start.AddMinutes(15)));
    }

    [Theory]
    [InlineData(IncomingArtifactCustodyState.Pending)]
    [InlineData(IncomingArtifactCustodyState.Failed)]
    [InlineData(IncomingArtifactCustodyState.Unknown)]
    public void AnAttemptThatIsNotConfirmedNeverStartsTheWindow(IncomingArtifactCustodyState state)
    {
        var occurrence = Confirmed() with { CustodyState = state };
        var session = NewSession();

        // Nothing that is not confirmed counts, so it cannot open a window and
        // cannot burn the sender's fifteen minutes before anything lands.
        Assert.False(occurrence.CountsTowardsTheSession);
        Assert.Throws<ArgumentException>(
            () => PublicUploadSessionPolicy.Start(session, occurrence, Start));
        Assert.Equal(
            PublicUploadSessionState.NotStarted,
            PublicUploadSessionPolicy.Evaluate(session, Start.AddHours(1)));
        // ... and the session still accepts, because that is how it starts.
        Assert.True(PublicUploadSessionPolicy.AcceptsBytes(session, Start.AddHours(1)));
    }

    [Fact]
    public void TheWindowIsFixedSoALaterSuccessNeverExtendsIt()
    {
        var opened = PublicUploadSessionPolicy.Start(NewSession(), Confirmed(), Start);
        var afterASecondFile = PublicUploadSessionPolicy.Start(
            opened,
            Confirmed() with { Id = Guid.NewGuid(), OperationKey = "second" },
            Start.AddMinutes(10));

        Assert.Equal(opened, afterASecondFile);
        Assert.Equal(Start.AddMinutes(15), afterASecondFile.ExpiresAtUtc);
    }

    [Fact]
    public void FinalizationIsReplaySafeAndThenRefusesMoreBytes()
    {
        var opened = PublicUploadSessionPolicy.Start(NewSession(), Confirmed(), Start);
        var finalized = PublicUploadSessionPolicy.Finalize(opened, Start.AddMinutes(5));
        var replay = PublicUploadSessionPolicy.Finalize(finalized, Start.AddMinutes(6));

        Assert.Equal(Start.AddMinutes(5), finalized.FinalizedAtUtc);
        // The same finalization again is the same session, not a second one.
        Assert.Equal(finalized, replay);
        Assert.Equal(
            PublicUploadSessionState.Finalized,
            PublicUploadSessionPolicy.Evaluate(finalized, Start.AddMinutes(6)));
        Assert.False(PublicUploadSessionPolicy.AcceptsBytes(finalized, Start.AddMinutes(6)));
    }

    [Fact]
    public void AnExpiredOrUnstartedSessionRefusesBytesAndCannotBeFinalized()
    {
        var expired = PublicUploadSessionPolicy.Start(NewSession(), Confirmed(), Start);
        var afterTheWindow = Start.AddMinutes(15);

        Assert.False(PublicUploadSessionPolicy.AcceptsBytes(expired, afterTheWindow));
        Assert.Throws<InvalidOperationException>(
            () => PublicUploadSessionPolicy.Finalize(expired, afterTheWindow));
        Assert.Throws<InvalidOperationException>(
            () => PublicUploadSessionPolicy.Finalize(NewSession(), Start));
    }

    [Fact]
    public void ALinkFromAnotherLimitsVersionIsATypedRefusalThatMayBeReissued()
    {
        var issue = RequestUploadToken.Create();
        var policy = new RequestUploadPolicy(
            Limits("current-v2"),
            new FixedTimeProvider(Start));
        var authorization = policy.Authorize(
            Link(issue, "accepted-v1"),
            new(issue.Secret.Token, File("estimate.pdf"), 0));

        Assert.Equal(RequestUploadDecision.LimitsVersionMismatch, authorization.Decision);
        Assert.True(authorization.MayReissue);
        // Nothing about the file, and nothing about the Case, is disclosed.
        Assert.False(authorization.MayEnterCustody);
        Assert.Null(authorization.ContentHash);
        Assert.Null(authorization.SafeFileName);
    }

    [Fact]
    public void AMatchingLimitsVersionStillAcceptsAndNeverOffersReissue()
    {
        var issue = RequestUploadToken.Create();
        var policy = new RequestUploadPolicy(
            Limits("accepted-v1"),
            new FixedTimeProvider(Start));
        var authorization = policy.Authorize(
            Link(issue, "accepted-v1"),
            new(issue.Secret.Token, File("estimate.pdf"), 0));

        Assert.Equal(RequestUploadDecision.Accepted, authorization.Decision);
        Assert.False(authorization.MayReissue);
        Assert.True(authorization.MayEnterCustody);
    }

    private static PublicUploadSession NewSession() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "accepted-v1",
        StartedAtUtc: null,
        FinalizedAtUtc: null,
        ExpiresAtUtc: null,
        Version: 0);

    private static PublicUploadOccurrence Confirmed() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "first",
        "estimate.pdf",
        "application/pdf",
        1024,
        new string('a', 64),
        IncomingArtifactCustodyState.Confirmed);

    private static RequestUploadLimits Limits(string version) => new(
        version,
        TimeSpan.FromHours(1),
        5,
        1024 * 1024,
        5 * 1024 * 1024,
        ["application/pdf"],
        10,
        TimeSpan.FromMinutes(1));

    private static RequestUploadLink Link(RequestUploadTokenIssue issue, string limitsVersion) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        issue.TokenDigest,
        RequestUploadStatus.Active,
        Start,
        Start.AddHours(1),
        RevokedAtUtc: null,
        AcceptedFileCount: 0,
        AcceptedByteCount: 0,
        limitsVersion,
        Version: 0);

    private static RequestUploadFile File(string name) =>
        new(name, "application/pdf", "document"u8.ToArray(), "upload-1");

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

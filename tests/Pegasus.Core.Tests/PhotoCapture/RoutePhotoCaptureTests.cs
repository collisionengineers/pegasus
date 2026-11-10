using Pegasus.Core.ImageIntake;
using Pegasus.Core.PhotoCapture;

namespace Pegasus.Core.Tests.PhotoCapture;

public sealed class RoutePhotoCaptureTests
{
    private static readonly Guid PrincipalA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid PrincipalB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static ImageIntakeCaseCandidate Candidate(
        string registration = "RY65OFG",
        Guid? principalId = null,
        bool isTriage = false,
        DateOnly? incidentDate = null) =>
        new(
            Guid.NewGuid(),
            "QDOS26001",
            CaseVersion: 3,
            registration,
            new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero),
            principalId ?? PrincipalA,
            "QDOS",
            isTriage,
            incidentDate);

    [Fact]
    public void OneEligibleCaseFilesThere()
    {
        var candidate = Candidate();

        var decision = RoutePhotoCapture.Decide([candidate], "RY65OFG", null, null);

        Assert.Equal(PhotoCaptureDestination.Case, decision.Destination);
        Assert.Equal(candidate.CaseId, decision.CaseId);
        Assert.Equal(RoutePhotoCapture.SingleEligibleCase, decision.Reason);
    }

    [Fact]
    public void NoCaseBecomesAVehicleImagesRecord()
    {
        var decision = RoutePhotoCapture.Decide([], "RY65OFG", null, null);

        Assert.Equal(PhotoCaptureDestination.VehicleImages, decision.Destination);
        Assert.Null(decision.CaseId);
        Assert.Equal(RoutePhotoCapture.NoEligibleCase, decision.Reason);
    }

    [Fact]
    public void SeveralCasesLeaveTheChoiceToStaff()
    {
        var decision = RoutePhotoCapture.Decide([Candidate(), Candidate()], "RY65OFG", null, null);

        Assert.Equal(PhotoCaptureDestination.VehicleImages, decision.Destination);
        Assert.Equal(RoutePhotoCapture.CasesAmbiguous, decision.Reason);
    }

    [Fact]
    public void ADifferentIncidentDateExcludesACaseAndAMissingOneDoesNot()
    {
        var typed = new DateOnly(2026, 10, 28);
        var other = Candidate(incidentDate: new DateOnly(2026, 8, 14));
        var same = Candidate(incidentDate: typed);
        var undated = Candidate();

        Assert.Equal(PhotoCaptureDestination.VehicleImages, RoutePhotoCapture.Decide([other], "RY65OFG", typed, null).Destination);
        Assert.Equal(same.CaseId, RoutePhotoCapture.Decide([other, same], "RY65OFG", typed, null).CaseId);
        Assert.Equal(undated.CaseId, RoutePhotoCapture.Decide([other, undated], "RY65OFG", typed, null).CaseId);
    }

    /// <summary>
    /// The routing, the settle's match and the record's later pairing read
    /// one date rule (FRD-22; operator, 9 November 2026, R791).
    /// </summary>
    [Theory]
    [InlineData(28, true)]
    [InlineData(14, false)]
    [InlineData(null, true)]
    public void TheRoutingExcludesByTheDateRuleTheRecordsPairingUses(int? caseDay, bool admitted)
    {
        var typed = new DateOnly(2026, 10, 28);
        var candidate = Candidate(incidentDate: caseDay is { } day ? new DateOnly(2026, 10, day) : null);

        Assert.Equal(admitted, IncidentDateExclusion.Admits(candidate, typed));
        Assert.Equal(
            admitted,
            RoutePhotoCapture.Decide([candidate], "RY65OFG", typed, null).Destination == PhotoCaptureDestination.Case);
    }

    [Fact]
    public void AKnownPrincipalScopesTheCandidatesToItsOwnCases()
    {
        var mine = Candidate(principalId: PrincipalA);
        var theirs = Candidate(principalId: PrincipalB);

        Assert.Equal(mine.CaseId, RoutePhotoCapture.Decide([mine, theirs], "RY65OFG", null, PrincipalA).CaseId);
        Assert.Equal(PhotoCaptureDestination.VehicleImages, RoutePhotoCapture.Decide([theirs], "RY65OFG", null, PrincipalA).Destination);
    }

    [Fact]
    public void ATriageCaseAloneIsNotADestination()
    {
        var decision = RoutePhotoCapture.Decide([Candidate(isTriage: true)], "RY65OFG", null, null);

        Assert.Equal(PhotoCaptureDestination.VehicleImages, decision.Destination);
    }
}

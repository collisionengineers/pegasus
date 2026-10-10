using Pegasus.Core.Assessment;
using Pegasus.Core.Identity;

namespace Pegasus.Core.Tests.Assessment;

public sealed class GlassRepairEstimateSessionPolicyTests
{
    private static readonly Guid EngineerId = Guid.NewGuid();
    private static readonly ActionActor Engineer = ActionActor.Staff(EngineerId, [StaffRole.Engineer]);

    [Theory]
    [InlineData(GlassRepairEstimateSessionState.Prepared, true)]
    [InlineData(GlassRepairEstimateSessionState.Launching, true)]
    [InlineData(GlassRepairEstimateSessionState.Active, true)]
    [InlineData(GlassRepairEstimateSessionState.AwaitingImport, true)]
    [InlineData(GlassRepairEstimateSessionState.Unknown, true)]
    [InlineData(GlassRepairEstimateSessionState.Importing, false)]
    [InlineData(GlassRepairEstimateSessionState.Completed, false)]
    [InlineData(GlassRepairEstimateSessionState.Failed, false)]
    [InlineData(GlassRepairEstimateSessionState.Expired, false)]
    [InlineData(GlassRepairEstimateSessionState.Cancelled, false)]
    public void EverySessionThatHoldsTheAccountCanBeClosedExceptOneMidImport(
        GlassRepairEstimateSessionState state, bool closable)
    {
        Assert.Equal(closable, GlassRepairEstimateSessionPolicy.CanClose(state));

        var closure = () => GlassRepairEstimateSessionPolicy.ValidateClosure(
            new(Engineer, Guid.NewGuid(), 3, ExternalSessionClosed: true, "Closed in Glass's."),
            Session(state));
        if (closable)
        {
            closure();
        }
        else
        {
            Assert.Throws<GlassRepairEstimateRefusalException>(closure);
        }
    }

    [Theory]
    [InlineData(GlassRepairEstimateSessionState.Prepared, true)]
    [InlineData(GlassRepairEstimateSessionState.Launching, true)]
    [InlineData(GlassRepairEstimateSessionState.Importing, true)]
    [InlineData(GlassRepairEstimateSessionState.Active, false)]
    [InlineData(GlassRepairEstimateSessionState.AwaitingImport, false)]
    [InlineData(GlassRepairEstimateSessionState.Unknown, false)]
    [InlineData(GlassRepairEstimateSessionState.Completed, false)]
    [InlineData(GlassRepairEstimateSessionState.Failed, false)]
    [InlineData(GlassRepairEstimateSessionState.Expired, false)]
    [InlineData(GlassRepairEstimateSessionState.Cancelled, false)]
    public void OnlyTheStatesRunningProviderWorkHoldsAreSettledWhenNothingRuns(
        GlassRepairEstimateSessionState state, bool awaits)
    {
        Assert.Equal(awaits, GlassRepairEstimateSessionPolicy.AwaitsProviderWork(state));
    }

    [Fact]
    public void ClosureStillNeedsTheConfirmationAndAReasonFromTheOwner()
    {
        Assert.Throws<GlassRepairEstimateRefusalException>(() =>
            GlassRepairEstimateSessionPolicy.ValidateClosure(
                new(Engineer, Guid.NewGuid(), 3, ExternalSessionClosed: false, "Closed in Glass's."),
                Session(GlassRepairEstimateSessionState.Active)));
        Assert.Throws<GlassRepairEstimateRefusalException>(() =>
            GlassRepairEstimateSessionPolicy.ValidateClosure(
                new(Engineer, Guid.NewGuid(), 3, ExternalSessionClosed: true, " "),
                Session(GlassRepairEstimateSessionState.Active)));
        Assert.Throws<GlassRepairEstimateRefusalException>(() =>
            GlassRepairEstimateSessionPolicy.ValidateClosure(
                new(ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]), Guid.NewGuid(), 3, true, "Closed."),
                Session(GlassRepairEstimateSessionState.Active)));
    }

    [Fact]
    public void AUserOwnerMayCloseItsSessionWhileOtherStaffAndNonHumanActorsAreRefused()
    {
        var userOwner = ActionActor.Staff(EngineerId, [StaffRole.User]);
        var session = Session(GlassRepairEstimateSessionState.Active);

        GlassRepairEstimateSessionPolicy.ValidateClosure(
            new(userOwner, Guid.NewGuid(), 3, true, "Closed in Glass's."),
            session);

        foreach (var actor in new[]
        {
            ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
            ActionActor.Automation("pegasus-automation"),
            ActionActor.SystemWorker("worker"),
            ActionActor.Principal(Guid.NewGuid())
        })
        {
            Assert.Throws<GlassRepairEstimateRefusalException>(() =>
                GlassRepairEstimateSessionPolicy.ValidateClosure(
                    new(actor, Guid.NewGuid(), 3, true, "Closed in Glass's."),
                    session));
        }
    }

    [Theory]
    [InlineData(GlassRepairEstimateSessionState.Failed, "glass.export.unreadable", true)]
    [InlineData(GlassRepairEstimateSessionState.Failed, "glass.export.request", true)]
    [InlineData(GlassRepairEstimateSessionState.Failed, "glass.export.ambiguous", true)]
    [InlineData(GlassRepairEstimateSessionState.Failed, "glass.export.off_origin", true)]
    [InlineData(GlassRepairEstimateSessionState.Failed, "glass.download.request", true)]
    [InlineData(GlassRepairEstimateSessionState.Failed, "glass.download.oversize", true)]
    [InlineData(GlassRepairEstimateSessionState.Unknown, "glass.export.request", false)]
    [InlineData(GlassRepairEstimateSessionState.Failed, "glass.export.empty", false)]
    [InlineData(GlassRepairEstimateSessionState.Failed, "glass.identity.registration", false)]
    [InlineData(GlassRepairEstimateSessionState.Failed, null, false)]
    [InlineData(GlassRepairEstimateSessionState.Unknown, "glass.export.unreadable", false)]
    [InlineData(GlassRepairEstimateSessionState.Completed, "glass.export.unreadable", false)]
    public void OnlyAFailedSessionWhoseExportCouldNotBeReadOrFetchedFetchesItAgain(
        GlassRepairEstimateSessionState state, string? failureCode, bool expected) =>
        Assert.Equal(expected, GlassRepairEstimateSessionPolicy.CanRefetchExport(state, failureCode));

    /// <summary>
    /// Only a changed registration on a real stock vehicle refuses a reopen or
    /// a resume, in the operator's words (9 October 2026, issue 1070). A
    /// corrected mileage continues on the registration and mileage the vehicle
    /// was made with; a placeholder holds neither at Glass's and continues on
    /// what the Case records now.
    /// </summary>
    [Theory]
    [InlineData("XY99 ZZZ", 33000L, false, null, 0L)]
    [InlineData("ab12cde", 33001L, false, "AB12 CDE", 33000L)]
    [InlineData("XY99 ZZZ", 33001L, true, "XY99 ZZZ", 33001L)]
    public void OnlyAChangedRegistrationOnARealVehicleRefusesAReopenOrAResume(
        string registration, long mileage, bool placeholder, string? continuesOn, long continuesAt)
    {
        var link = new GlassEstimateLink("33576604", "1954488", "123456789", placeholder, "AB12 CDE", 33000);
        var reopen = () => GlassRepairEstimateSessionPolicy.RequireUnchangedEstimateVehicle(
            link, registration, mileage);
        var resume = () => GlassRepairEstimateSessionPolicy.RequireUnchangedVehicle(
            "AB12 CDE", 33000, placeholder, vehicleRecorded: true, registration, mileage);

        if (continuesOn is null)
        {
            Assert.Equal(
                "The Case registration has changed since this Glass's estimate was started. "
                + "Restore the original vehicle details to reopen it.",
                Assert.Throws<GlassRepairEstimateRefusalException>(() => reopen()).Message);
            Assert.Equal(
                "The Case registration has changed since this Glass's session started. "
                + "The session still holds the account. Restore the original vehicle details to resume, "
                + "or close the external session and confirm its closure before launching again.",
                Assert.Throws<GlassRepairEstimateRefusalException>(() => resume()).Message);
        }
        else
        {
            Assert.Equal((continuesOn, continuesAt), reopen());
            Assert.Equal((continuesOn, continuesAt), resume());
        }
    }

    /// <summary>
    /// A session that has recorded no stock vehicle yet holds nothing at
    /// Glass's, so a corrected mileage is the one it goes on to use.
    /// </summary>
    [Fact]
    public void ASessionWithNoVehicleYetFollowsTheCaseMileage() =>
        Assert.Equal(
            ("ab12cde", 33001L),
            GlassRepairEstimateSessionPolicy.RequireUnchangedVehicle(
                "AB12 CDE", 33000, placeholder: false, vehicleRecorded: false, "ab12cde", 33001));

    private static GlassRepairEstimateSession Session(GlassRepairEstimateSessionState state) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            EngineerId,
            CredentialGeneration: 1,
            "account-key",
            state,
            Version: 3,
            OperationKey: "op",
            DateTimeOffset.UnixEpoch,
            ProviderVehicleId: null,
            ProviderEstimateId: null,
            FailureCode: null);
}

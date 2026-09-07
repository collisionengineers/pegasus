using Pegasus.Core.Cases;
using Pegasus.Core.Custody;
using Pegasus.Core.Identity;
using Pegasus.Core.Vehicle;

namespace Pegasus.Core.Tests.Vehicle;

public sealed class VehicleWorkflowTests
{
    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
    private static readonly ActionActor Staff =
        ActionActor.Staff(Guid.Parse("11111111-1111-1111-1111-111111111111"), [StaffRole.User]);

    [Fact]
    public async Task RequestRequiresAnAvailableProfileAndAuthorizedStaffActor()
    {
        var command = RequestCommand();
        var unavailable = new RequestVehicleLookup(
            new RecordingRequestStore(),
            VehicleLookupAvailability.Unavailable,
            new CommittedWorkPublisherDouble());
        await Assert.ThrowsAsync<VehicleLookupUnavailableException>(() =>
            unavailable.ExecuteAsync(command, CancellationToken.None));

        var available = new RequestVehicleLookup(
            new RecordingRequestStore(),
            VehicleLookupAvailability.DevelopmentOfflineReplay,
            new CommittedWorkPublisherDouble());
        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            available.ExecuteAsync(
                command with { Actor = ActionActor.SystemWorker("vehicle-test") },
                CancellationToken.None));

        // ProductionLive permits requests; the Web's production profile composes it
        // (the composition itself lives in Program.cs and is not observable from Core).
        Assert.True(VehicleLookupAvailability.ProductionLive.RequestsEnabled);
    }

    [Fact]
    public async Task ExactRequestIsNormalizedAndDelegatedOnce()
    {
        var store = new RecordingRequestStore();
        var publisher = new CommittedWorkPublisherDouble();
        var useCase = new RequestVehicleLookup(
            store,
            VehicleLookupAvailability.DevelopmentOfflineReplay,
            publisher);

        var result = await useCase.ExecuteAsync(RequestCommand(), CancellationToken.None);

        Assert.Equal("AB12CDE", result.Registration);
        var recorded = Assert.Single(store.Commands);
        Assert.Equal("AB12CDE", recorded.Registration);
        Assert.Equal("vehicle-request", recorded.OperationKey);
        Assert.Equal([result.WorkItemId], publisher.ExternalWorkIds);
    }

    [Fact]
    public async Task AcceptanceRequiresAnExplicitReasonAndCorrectionShape()
    {
        var useCase = new AcceptVehicleSuggestion(new RecordingAcceptStore());
        var command = AcceptCommand();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(command with { Reason = " " }, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(
                command with
                {
                    Decision = VehicleSuggestionDecision.Correct,
                    Correction = null
                },
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            useCase.ExecuteAsync(
                command with
                {
                    Correction = new("AB12CDE", "Make", "Model", 12000, VehicleMileageUnit.Miles)
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task ExplicitCorrectionIsNormalizedAndDelegatedWithReason()
    {
        var store = new RecordingAcceptStore();
        var useCase = new AcceptVehicleSuggestion(store);
        var command = AcceptCommand() with
        {
            Decision = VehicleSuggestionDecision.Correct,
            Correction = new("AB12CDE", " Example ", " Model ", 12000, VehicleMileageUnit.Miles),
            Reason = " Corrected against the retained source image. "
        };

        var result = await useCase.ExecuteAsync(command, CancellationToken.None);

        var recorded = Assert.Single(store.Commands);
        Assert.Equal("Example", recorded.Correction?.Make);
        Assert.Equal("Model", recorded.Correction?.Model);
        Assert.Equal("Corrected against the retained source image.", recorded.Reason);
        Assert.Equal(recorded.Correction, result.Values);
    }

    [Fact]
    public void AKilometreOdometerIsConvertedToMiles()
    {
        // ENG-010, from DP07EFB's real MOT history: an imported vehicle
        // records in kilometres. The derived Case value is always miles, so
        // consumers that ask for miles stop silently ignoring it.
        // 113,068 km / 1.609344 = 70,257.4 -> 70,257 miles.
        MotTestObservation[] observations =
        [
            new(new DateOnly(2026, 5, 14), "PASSED", null, 113068, VehicleMileageUnit.Kilometres),
            new(new DateOnly(2025, 5, 14), "PASSED", null, 102742, VehicleMileageUnit.Kilometres)
        ];

        var calculation = Assert.IsType<VehicleMileageCalculation>(
            VehicleMileagePolicy.Calculate(observations));

        Assert.Equal(70_257, calculation.Value);
        Assert.Equal(VehicleMileageUnit.Miles, calculation.Unit);
        Assert.Equal(new DateOnly(2026, 5, 14), calculation.ObservedOn);
    }

    [Fact]
    public void TheSameReadingInBothUnitsIsNotAConflict()
    {
        // Comparing before converting made one reading recorded twice, in
        // different units, look like two disagreeing readings and abstain.
        MotTestObservation[] observations =
        [
            new(new DateOnly(2026, 5, 14), "PASSED", null, 100000, VehicleMileageUnit.Kilometres),
            new(new DateOnly(2026, 5, 14), "PASSED", null, 62137, VehicleMileageUnit.Miles)
        ];

        var calculation = Assert.IsType<VehicleMileageCalculation>(
            VehicleMileagePolicy.Calculate(observations));

        Assert.Equal(62_137, calculation.Value);
        Assert.Equal(2, calculation.SupportingObservationCount);
    }

    [Fact]
    public void MileageUsesTheLatestExactObservationAndRejectsLatestDateConflicts()
    {
        MotTestObservation[] observations =
        [
            new(new DateOnly(2030, 4, 1), "passed", null, 22000, VehicleMileageUnit.Miles),
            new(new DateOnly(2029, 4, 1), "passed", null, 15000, VehicleMileageUnit.Miles),
            new(new DateOnly(2030, 4, 1), "passed", null, 22000, VehicleMileageUnit.Miles)
        ];

        var calculation = Assert.IsType<VehicleMileageCalculation>(
            VehicleMileagePolicy.Calculate(observations.Reverse().ToArray()));
        Assert.Equal(22000, calculation.Value);
        Assert.Equal(new DateOnly(2030, 4, 1), calculation.ObservedOn);
        Assert.Equal(2, calculation.SupportingObservationCount);
        Assert.Equal(VehicleMileagePolicy.MethodKey, calculation.MethodKey);

        Assert.Null(VehicleMileagePolicy.Calculate(
        [
            observations[0],
            observations[0] with { Mileage = 22001 }
        ]));
    }

    [Fact]
    public void LookupSourcedMileageClassifiesAsEstimatedAndNeverAsSupplied()
    {
        // Accepting a vehicle suggestion stores the derived MOT calculation, so a
        // lookup-sourced case mileage is the derived estimate; the operator rule is
        // that a derived estimate is never relabelled as supplied mileage.
        var classification = VehicleMileageEvidenceClassification.Classify(
            CaseDataSourceKind.VehicleLookup);

        Assert.Equal(VehicleMileageEvidenceClass.Estimated, classification);
        Assert.NotEqual(VehicleMileageEvidenceClass.Supplied, classification);
    }

    [Theory]
    [InlineData(CaseDataSourceKind.IntakeEvidence)]
    [InlineData(CaseDataSourceKind.MailRoute)]
    [InlineData(CaseDataSourceKind.CaseAcceptance)]
    [InlineData(CaseDataSourceKind.StaffCorrection)]
    [InlineData(CaseDataSourceKind.ProviderSetting)]
    public void DirectlyAttributedMileageClassifiesAsSupplied(CaseDataSourceKind sourceKind) =>
        Assert.Equal(
            VehicleMileageEvidenceClass.Supplied,
            VehicleMileageEvidenceClassification.Classify(sourceKind));

    [Fact]
    public void AcceptedSuggestionProposesTheDerivedMileageCalculation()
    {
        MotTestObservation[] motTests =
        [
            new(new DateOnly(2030, 4, 1), "passed", new DateOnly(2031, 3, 31), 22000, VehicleMileageUnit.Miles),
            new(new DateOnly(2029, 4, 1), "passed", new DateOnly(2030, 3, 31), 15000, VehicleMileageUnit.Miles)
        ];
        var calculation = Assert.IsType<VehicleMileageCalculation>(
            VehicleMileagePolicy.Calculate(motTests));
        var observation = new VehicleLookupObservation(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            1,
            VehicleLookupOutcome.Current,
            "AB12CDE",
            new("offline-replay", "fixture-v1", "response-Current", FixedUtcNow, null, FixedUtcNow.AddDays(-1)),
            new("Example", "Model", 2020, 1600, "petrol"),
            motTests,
            calculation,
            null,
            FixedUtcNow);

        var values = VehicleSuggestionAcceptancePolicy.Resolve(
            observation,
            VehicleSuggestionDecision.Accept,
            correction: null);

        // The proposed mileage is exactly the derived calculation, not a raw or
        // invented figure, so the confirmed value's lookup source classifies it
        // as an estimate.
        Assert.Equal(calculation.Value, values.Mileage);
        Assert.Equal(calculation.Unit, values.MileageUnit);
    }

    [Theory]
    [MemberData(nameof(QueueOutcomes))]
    public async Task VehicleProcessorPersistsEveryTypedOutcome(
        VehicleLookupResult result,
        int attemptNumber,
        VehicleLookupWorkState expectedState)
    {
        var workId = Guid.NewGuid();
        var store = new RecordingWorkStore(new(
            workId,
            Guid.NewGuid(),
            "AB12CDE",
            "vehicle-request",
            VehicleLookupWorkState.Processing,
            attemptNumber,
            FixedUtcNow,
            "lease-token",
            FixedUtcNow.AddMinutes(5)));
        var processor = new ProcessQueuedVehicleLookup(
            store,
            new StubLookupAdapter(result),
            new FixedTimeProvider(FixedUtcNow));

        await processor.ExecuteAsync(workId, CancellationToken.None);

        var recorded = Assert.Single(store.Recorded);
        Assert.Equal(expectedState, recorded.State);
        Assert.Equal(result.Outcome, recorded.Outcome.Result.Outcome);
        if (expectedState == VehicleLookupWorkState.RetryScheduled)
        {
            Assert.True(recorded.DueAtUtc > FixedUtcNow);
        }
        else
        {
            Assert.Null(recorded.DueAtUtc);
        }
    }

    [Fact]
    public async Task TypedDispatcherInvokesExactlyOneHandlerAndDeniesUnknownKinds()
    {
        var workId = Guid.NewGuid();
        var custody = new RecordingCustodyProcessor();
        var vehicle = new RecordingVehicleProcessor();
        var eva = new RecordingEvaProcessor();
        var ocr = new RecordingOcrProcessor();
        var reader = new MutableExternalWorkReader(new(workId, ExternalWorkKinds.VehicleLookup));
        var dispatcher = new ProcessQueuedExternalWork(reader, custody, vehicle, eva, ocr);

        await dispatcher.ExecuteAsync(workId, CancellationToken.None);
        Assert.Empty(custody.ProcessedIds);
        Assert.Empty(eva.ProcessedIds);
        Assert.Equal([workId], vehicle.ProcessedIds);

        // EXT-04's arm routes to its own handler and to no other.
        reader.Work = new(workId, ExternalWorkKinds.SubmitCaseToEva);
        await dispatcher.ExecuteAsync(workId, CancellationToken.None);
        Assert.Empty(custody.ProcessedIds);
        Assert.Equal([workId], vehicle.ProcessedIds);
        Assert.Equal([workId], eva.ProcessedIds);

        reader.Work = new(workId, ExternalWorkKinds.IntakeOcr);
        await dispatcher.ExecuteAsync(workId, CancellationToken.None);
        Assert.Empty(custody.ProcessedIds);
        Assert.Equal([workId], vehicle.ProcessedIds);
        Assert.Equal([workId], eva.ProcessedIds);
        Assert.Equal([workId], ocr.ProcessedIds);

        reader.Work = new(workId, "not_registered");
        await Assert.ThrowsAsync<UnknownExternalWorkKindException>(() =>
            dispatcher.ExecuteAsync(workId, CancellationToken.None));
        Assert.Empty(custody.ProcessedIds);
        Assert.Equal([workId], vehicle.ProcessedIds);
        Assert.Equal([workId], eva.ProcessedIds);
        Assert.Equal([workId], ocr.ProcessedIds);

        var dispatcherWithoutOcr = new ProcessQueuedExternalWork(reader, custody, vehicle, eva);
        reader.Work = new(workId, ExternalWorkKinds.IntakeOcr);
        await Assert.ThrowsAsync<UnknownExternalWorkKindException>(() =>
            dispatcherWithoutOcr.ExecuteAsync(workId, CancellationToken.None));
    }

    public static TheoryData<VehicleLookupResult, int, VehicleLookupWorkState> QueueOutcomes()
    {
        return new()
        {
            {
                Result(
                    VehicleLookupOutcome.Current,
                    vehicle: new("Example", "Model", 2020, 1600, "Petrol"),
                    motTests:
                    [
                        new(
                            new DateOnly(2030, 1, 2),
                            "passed",
                            new DateOnly(2031, 1, 1),
                            12000,
                            VehicleMileageUnit.Miles)
                    ]),
                1,
                VehicleLookupWorkState.Completed
            },
            {
                Result(
                    VehicleLookupOutcome.Stale,
                    vehicle: new("Example", "Model", 2020, 1600, "Petrol")),
                1,
                VehicleLookupWorkState.Completed
            },
            {
                Result(
                    VehicleLookupOutcome.Partial,
                    vehicle: new("Example", null, null, null, null)),
                1,
                VehicleLookupWorkState.Completed
            },
            { Result(VehicleLookupOutcome.NotFound), 1, VehicleLookupWorkState.Completed },
            {
                Result(
                    VehicleLookupOutcome.Throttled,
                    failure: new("throttled", true, TimeSpan.FromMinutes(3))),
                1,
                VehicleLookupWorkState.RetryScheduled
            },
            {
                Result(VehicleLookupOutcome.Failed, failure: new("provider_error", false)),
                1,
                VehicleLookupWorkState.Failed
            },
            {
                Result(VehicleLookupOutcome.Failed, failure: new("provider_error", true)),
                5,
                VehicleLookupWorkState.Failed
            },
            {
                Result(VehicleLookupOutcome.Unavailable, failure: new("fixture_unavailable", false)),
                1,
                VehicleLookupWorkState.Completed
            }
        };
    }

    private static VehicleLookupResult Result(
        VehicleLookupOutcome outcome,
        VehicleDetails? vehicle = null,
        IReadOnlyList<MotTestObservation>? motTests = null,
        VehicleLookupFailure? failure = null) =>
        new(
            "AB12CDE",
            outcome,
            "offline-replay",
            "fixture-v1",
            $"response-{outcome}",
            FixedUtcNow,
            EffectiveAtUtc: null,
            SourceObservedAtUtc: vehicle is null && (motTests is null || motTests.Count == 0)
                ? null
                : FixedUtcNow.AddDays(-1),
            vehicle,
            motTests ?? [],
            failure);

    private static RequestVehicleLookupCommand RequestCommand() =>
        new(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            3,
            "AB12CDE",
            Staff,
            " vehicle-request ",
            "lease-token");

    private static AcceptVehicleSuggestionCommand AcceptCommand() =>
        new(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            4,
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            VehicleSuggestionDecision.Accept,
            null,
            Staff,
            "vehicle-accept",
            "Accepted after checking the retained lookup evidence.",
            "lease-token");

    private sealed class RecordingRequestStore : IRequestVehicleLookupStore
    {
        public List<RequestVehicleLookupCommand> Commands { get; } = [];

        public Task<RequestedVehicleLookup> RequestAsync(
            RequestVehicleLookupCommand command,
            CancellationToken cancellationToken)
        {
            Commands.Add(command);
            return Task.FromResult(new RequestedVehicleLookup(
                Guid.NewGuid(),
                command.CaseId,
                command.Registration,
                VehicleLookupWorkState.Pending,
                command.ExpectedCaseVersion + 1,
                false));
        }
    }

    private sealed class RecordingAcceptStore : IAcceptVehicleSuggestionStore
    {
        public List<AcceptVehicleSuggestionCommand> Commands { get; } = [];

        public Task<AcceptedVehicleSuggestion> AcceptAsync(
            AcceptVehicleSuggestionCommand command,
            CancellationToken cancellationToken)
        {
            Commands.Add(command);
            var values = command.Correction
                ?? new VehicleConfirmationValues("AB12CDE", "Example", "Model", 12000, VehicleMileageUnit.Miles);
            return Task.FromResult(new AcceptedVehicleSuggestion(
                Guid.NewGuid(),
                command.CaseId,
                command.LookupObservationId,
                command.Decision,
                values,
                new(
                    "offline-replay",
                    "fixture-v1",
                    "response-current",
                    FixedUtcNow,
                    null,
                    FixedUtcNow.AddDays(-1)),
                command.ExpectedCaseVersion + 1,
                false));
        }
    }

    private sealed class StubLookupAdapter(VehicleLookupResult result) : IVehicleLookupAdapter
    {
        public Task<VehicleLookupResult> LookupAsync(
            VehicleLookupRequest request,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class RecordingWorkStore(VehicleLookupWorkItem work) : IVehicleLookupWorkStore
    {
        public List<(
            VehicleLookupProcessedOutcome Outcome,
            VehicleLookupWorkState State,
            DateTimeOffset? DueAtUtc)> Recorded { get; } = [];

        public Task<VehicleLookupWorkItem?> ClaimProcessingAsync(
            Guid workItemId,
            DateTimeOffset nowUtc,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken) =>
            Task.FromResult<VehicleLookupWorkItem?>(work);

        public Task RecordOutcomeAsync(
            Guid workItemId,
            string leaseToken,
            VehicleLookupProcessedOutcome outcome,
            VehicleLookupWorkState state,
            DateTimeOffset? dueAtUtc,
            DateTimeOffset recordedAtUtc,
            CancellationToken cancellationToken)
        {
            Recorded.Add((outcome, state, dueAtUtc));
            return Task.CompletedTask;
        }
    }

    private sealed class MutableExternalWorkReader(QueuedExternalWork work)
        : IQueuedExternalWorkReader
    {
        public QueuedExternalWork Work { get; set; } = work;

        public Task<QueuedExternalWork?> GetAsync(
            Guid workItemId,
            CancellationToken cancellationToken) =>
            Task.FromResult<QueuedExternalWork?>(Work);
    }

    private sealed class RecordingCustodyProcessor : IProcessQueuedCustody
    {
        public List<Guid> ProcessedIds { get; } = [];

        public Task ExecuteAsync(Guid workId, CancellationToken cancellationToken)
        {
            ProcessedIds.Add(workId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingVehicleProcessor : IProcessQueuedVehicleLookup
    {
        public List<Guid> ProcessedIds { get; } = [];

        public Task ExecuteAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            ProcessedIds.Add(workItemId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEvaProcessor : IProcessQueuedEvaSubmission
    {
        public List<Guid> ProcessedIds { get; } = [];

        public Task ExecuteAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            ProcessedIds.Add(workItemId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOcrProcessor : IProcessIntakeOcr
    {
        public List<Guid> ProcessedIds { get; } = [];

        public Task ExecuteAsync(Guid workItemId, CancellationToken cancellationToken)
        {
            ProcessedIds.Add(workItemId);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

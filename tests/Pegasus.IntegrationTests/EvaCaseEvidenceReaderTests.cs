using Pegasus.Core.Cases;
using Pegasus.Core.Eva;
using Pegasus.Core.Intake;
using Pegasus.Core.Vehicle;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

public sealed class EvaCaseEvidenceReaderTests
{
    [Fact]
    public void ConfirmedMakeAndAcceptedFactModelComposeIndependently()
    {
        var evidence = EvaCaseEvidenceReader.Build(
            CaseData(make: Fact("Ford"), model: Fact("Focus")),
            VehicleEvidence(make: ConfirmedVehicleField("Ford")));

        Assert.Equal("Ford Focus", evidence.VehicleModel.Value);
        Assert.Equal(EvaEvidenceStatus.Accepted, evidence.VehicleModel.Status);
        Assert.Contains("vehicle:", evidence.VehicleModel.Source, StringComparison.Ordinal);
        Assert.Contains("case-data:IntakeEvidence", evidence.VehicleModel.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptedFactMakeAndConfirmedModelComposeIndependently()
    {
        var evidence = EvaCaseEvidenceReader.Build(
            CaseData(make: Fact("Ford"), model: Fact("Focus")),
            VehicleEvidence(model: ConfirmedVehicleField("Focus")));

        Assert.Equal("Ford Focus", evidence.VehicleModel.Value);
        Assert.Equal(EvaEvidenceStatus.Accepted, evidence.VehicleModel.Status);
        Assert.Contains("case-data:IntakeEvidence", evidence.VehicleModel.Source, StringComparison.Ordinal);
        Assert.Contains("vehicle:", evidence.VehicleModel.Source, StringComparison.Ordinal);
    }

    private static CaseDataProjection CaseData(CaseField<string> make, CaseField<string> model)
    {
        var caseId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var source = new CaseDataSource(
            CaseDataSourceKind.IntakeEvidence,
            "instruction",
            "retained instruction",
            "qdos_instruction",
            1);
        CaseField<T> Empty<T>() where T : notnull => new(null, null, null);
        CaseField<T> Accepted<T>(T value) where T : notnull =>
            new(new(value, CaseDataValueKind.Fact, source), null, null);
        return new(
            new(caseId, "QDOS", 2031, 42, "QDOS3100042"),
            new(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                IntakeSourceChannel.Mailbox,
                "receipt-token",
                "source-hash",
                new DateTimeOffset(2031, 5, 1, 9, 0, 0, TimeSpan.Zero),
                "reader",
                "1",
                "qdos_instruction",
                1),
            new DateTimeOffset(2031, 5, 1, 9, 0, 0, TimeSpan.Zero),
            7,
            CaseLifecycleState.Review,
            new(new(true, true), new(true, "case-completeness", 1)),
            new(Accepted("QDOS")),
            new(Empty<string>(), Empty<string>(), Empty<string>()),
            new(Empty<string>()),
            new(Accepted("AB12CDE"), make, model, Empty<long>(), Empty<string>()),
            new(Empty<DateOnly>(), Empty<string>()),
            new(Empty<string>(), Empty<string>(), Empty<string>()),
            new(Empty<DateOnly>(), Empty<string>()),
            new(Empty<DateOnly>(), Empty<DateOnly>(), Empty<string>(), Empty<CaseInspectionMode>()));
    }

    private static CaseVehicleEvidence VehicleEvidence(
        ConfirmedVehicleField<string>? make = null,
        ConfirmedVehicleField<string>? model = null) =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new(null, make, model, null, null),
            null,
            [],
            []);

    private static ConfirmedVehicleField<string> ConfirmedVehicleField(string value) => new(
        value,
        "vehicle_lookup",
        "lookup-observation",
        "DVLA and MOT",
        "vehicle-lookup",
        1,
        "engineer-1",
        new DateTimeOffset(2031, 5, 2, 9, 0, 0, TimeSpan.Zero),
        null);

    private static CaseField<string> Fact(string value) => new(
        new(
            value,
            CaseDataValueKind.Fact,
            new(
                CaseDataSourceKind.IntakeEvidence,
                "instruction",
                "retained instruction",
                "qdos_instruction",
                1)),
        null,
        null);
}

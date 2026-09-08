using Microsoft.Extensions.Logging;
using Pegasus.Core.ImageIntake;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;
using Pegasus.Core.ProviderApi;
using Pegasus.Core.Triage;
using Pegasus.Core.Vehicle;
using Pegasus.Infrastructure.Custody;
using Pegasus.Worker;

namespace Pegasus.ArchitectureTests;

public sealed class StagedArtifactReconciliationFunctionTests
{
    [Fact]
    public void FunctionDependsOnTheCanonicalStagedArtifactReconciler()
    {
        var constructor = Assert.Single(
            typeof(StagedArtifactReconciliationFunction).GetConstructors());

        Assert.Equal(
            [
                typeof(ReconcileStagedArtifacts),
                typeof(IDocumentContentCacheCleanup),
                typeof(ReconcilePendingArtifactCustody),
                typeof(ReconcileGroupedImageIntake),
                typeof(IImageIntakeCasePairing),
                typeof(ITriageCasePairing),
                typeof(ReconcileUnidentifiedDestinations),
                typeof(ReconcileAutomaticVehicleLookups),
                typeof(ReconcileProviderSubmissions),
                typeof(ILogger<StagedArtifactReconciliationFunction>)
            ],
            constructor.GetParameters().Select(parameter => parameter.ParameterType));
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Tasks;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Worker;

namespace Pegasus.ArchitectureTests;

public sealed class DueWorkSweepFunctionTests
{
    [Fact]
    public void InfrastructureRegistersOneScopedDueChaserStoreAndCoreUseCase()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPegasusInfrastructure(
            (_, options) => options.UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=Pegasus_DueChaserActivationOnly;" +
                "Integrated Security=true;Encrypt=false"));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var scopedServices = scope.ServiceProvider;
        var store = scopedServices.GetRequiredService<EfCaseDueChaserStore>();

        Assert.Same(store, scopedServices.GetRequiredService<ICaseDueChaserQueries>());
        Assert.Same(store, scopedServices.GetRequiredService<ICaseDueChaserStore>());
        Assert.NotNull(scopedServices.GetRequiredService<RunDueChasers>());
        Assert.NotNull(ActivatorUtilities.CreateInstance<DueWorkSweepFunction>(scopedServices));
    }

    [Fact]
    public void FunctionMetadataBindsOnlyApprovedTimerScheduleAndCoreUseCase()
    {
        var constructor = Assert.Single(typeof(DueWorkSweepFunction).GetConstructors());
        Assert.Equal(
            [typeof(RunDueChasers), typeof(ILogger<DueWorkSweepFunction>)],
            constructor.GetParameters().Select(parameter => parameter.ParameterType));

        var method = typeof(DueWorkSweepFunction).GetMethod(nameof(DueWorkSweepFunction.RunAsync));
        Assert.NotNull(method);
        var functionAttribute = Assert.Single(
            method.CustomAttributes,
            attribute => attribute.AttributeType.Name == "FunctionAttribute");
        Assert.Equal(
            nameof(DueWorkSweepFunction),
            Assert.IsType<string>(Assert.Single(functionAttribute.ConstructorArguments).Value));

        var parameters = method.GetParameters();
        Assert.Equal(2, parameters.Length);
        var timerAttribute = Assert.Single(
            parameters[0].CustomAttributes,
            attribute => attribute.AttributeType.Name == "TimerTriggerAttribute");
        Assert.Equal(
            "%DueWorkSweepSchedule%",
            Assert.IsType<string>(Assert.Single(timerAttribute.ConstructorArguments).Value));
        Assert.False(Assert.IsType<bool>(Assert.Single(
            timerAttribute.NamedArguments,
            argument => argument.MemberName == "RunOnStartup").TypedValue.Value));
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
    }

    [Fact]
    public async Task TimerCallsRunDueChasersWithoutAnyOutboundAdapter()
    {
        var queries = new EmptyQueries();
        var useCase = new RunDueChasers(
            queries,
            new RejectingStore(),
            new FixedTimeProvider(new DateTimeOffset(2026, 7, 30, 9, 0, 0, TimeSpan.Zero)));
        using var loggerProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var function = new DueWorkSweepFunction(
            useCase,
            loggerProvider.GetRequiredService<ILogger<DueWorkSweepFunction>>());

        await function.RunAsync(null!, default);

        Assert.Equal(1, queries.CallCount);
        Assert.Equal(50, queries.MaximumResults);
    }

    private sealed class EmptyQueries : ICaseDueChaserQueries
    {
        public int CallCount { get; private set; }
        public int MaximumResults { get; private set; }

        public Task<IReadOnlyList<DueCaseChaser>> GetDueAsync(
            DateTimeOffset asOfUtc,
            int maximumResults,
            CancellationToken cancellationToken)
        {
            CallCount++;
            MaximumResults = maximumResults;
            return Task.FromResult<IReadOnlyList<DueCaseChaser>>([]);
        }

        public Task<GeneratedCaseChaser?> GetLatestAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<GeneratedCaseChaser?>(null);
    }

    private sealed class RejectingStore : ICaseDueChaserStore
    {
        public Task<DueChaserClaimResult> TryClaimAndRecordAsync(
            DueChaserTransition transition,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An empty due query must not reach the store.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Triage;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Worker;

namespace Pegasus.ArchitectureTests;

public sealed class EmailEvidenceFunctionActivationTests
{
    [Fact]
    public void InfrastructureRegistrationsActivateActualSentEvidencePollFunction()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationManager());
        services.AddPegasusInfrastructure(
            (_, options) => options.UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=Pegasus_ActivationOnly;" +
                "Integrated Security=true;Encrypt=false"));
        services.AddLocalApprovedSent(_ => new(
            LocalApprovedSentOptions.RequiredRuntimeProfile,
            "instructions",
            "instructions@example.test",
            "sent-items",
            Path.Combine(Path.GetTempPath(), $"pegasus-sent-activation-{Guid.NewGuid():N}")));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var scopedServices = scope.ServiceProvider;

        Assert.NotNull(ActivatorUtilities.CreateInstance<SentEvidencePollFunction>(scopedServices));
        Assert.NotNull(scopedServices.GetRequiredService<PollSentEvidence>());

        var store = scopedServices.GetRequiredService<EfEmailEvidenceStore>();
        Assert.Same(store, scopedServices.GetRequiredService<IRecordSentEmailEvidence>());
        Assert.Same(store, scopedServices.GetRequiredService<IRecordEmailResponseEvidence>());
        Assert.Same(store, scopedServices.GetRequiredService<IExactEmailResponseEvidenceQueries>());
        Assert.NotNull(scopedServices.GetRequiredService<ISentEvidencePollOutcomeQueries>());
        Assert.NotNull(scopedServices.GetRequiredService<IApprovedSentSource>());
        Assert.NotNull(scopedServices.GetRequiredService<ISentEvidencePollStore>());
        Assert.NotNull(scopedServices.GetRequiredService<IRetainApprovedMailboxReportSentEvidence>());
        Assert.NotNull(scopedServices.GetRequiredService<IAutoLinkReportEvidence>());
    }

    [Fact]
    public void SentPollFunctionHasExactTimerBindingMetadata()
    {
        var constructor = Assert.Single(typeof(SentEvidencePollFunction).GetConstructors());
        Assert.Equal(
            [typeof(PollSentEvidence), typeof(ILogger<SentEvidencePollFunction>)],
            constructor.GetParameters().Select(parameter => parameter.ParameterType));

        var runMethod = typeof(SentEvidencePollFunction).GetMethod(nameof(SentEvidencePollFunction.RunAsync));
        Assert.NotNull(runMethod);
        var functionAttribute = Assert.Single(
            runMethod.CustomAttributes,
            attribute => attribute.AttributeType.Name == "FunctionAttribute");
        Assert.Equal(
            nameof(SentEvidencePollFunction),
            Assert.IsType<string>(Assert.Single(functionAttribute.ConstructorArguments).Value));

        var parameters = runMethod.GetParameters();
        Assert.Equal(2, parameters.Length);
        var timerAttribute = Assert.Single(
            parameters[0].CustomAttributes,
            attribute => attribute.AttributeType.Name == "TimerTriggerAttribute");
        Assert.Equal(
            "%SentEvidencePollSchedule%",
            Assert.IsType<string>(Assert.Single(timerAttribute.ConstructorArguments).Value));
        Assert.False(Assert.IsType<bool>(Assert.Single(
            timerAttribute.NamedArguments,
            argument => argument.MemberName == "RunOnStartup").TypedValue.Value));
        Assert.Equal(typeof(CancellationToken), parameters[1].ParameterType);
    }
}

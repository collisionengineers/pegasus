using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Identity;
using Pegasus.Core.Triage;

namespace Pegasus.Worker;

public sealed class SentEmailEvidenceReplayFunction(
    ReplaySentEmailEvidence replaySentEmailEvidence,
    IConfiguration configuration)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Function(nameof(SentEmailEvidenceReplayFunction))]
    public async Task RunAsync(
        [QueueTrigger("email-evidence-replay", Connection = "AzureWebJobsStorage")] string message,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(configuration["EmailEvidence:ReplayEnabled"], "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Sent email evidence replay is disabled until EmailEvidence:ReplayEnabled is explicitly set to true.");
        }

        SentEmailEvidenceReplay? replay;
        try
        {
            replay = JsonSerializer.Deserialize<SentEmailEvidenceReplay>(message, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The sent email evidence replay message is invalid JSON.", exception);
        }

        if (replay is null)
        {
            throw new InvalidDataException("The sent email evidence replay message is empty.");
        }

        await replaySentEmailEvidence.ExecuteAsync(
            replay,
            ActionActor.SystemWorker("email-evidence-replay"),
            cancellationToken);
    }
}

public sealed partial class EmailEvidenceChaseProjectionFunction(
    IEmailEvidenceChaseReadModel chaseReadModel,
    TimeProvider timeProvider,
    ILogger<EmailEvidenceChaseProjectionFunction> logger)
{
    [Function(nameof(EmailEvidenceChaseProjectionFunction))]
    public async Task RunAsync(
        [TimerTrigger("0 */5 * * * *", RunOnStartup = false)] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        var dueChases = await chaseReadModel.GetDueAsync(
            timeProvider.GetUtcNow(),
            maximumResults: 50,
            cancellationToken);
        LogDueChases(logger, dueChases.Count);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Projected {ChaseCount} due email-evidence chases; no external email was sent.")]
    private static partial void LogDueChases(ILogger logger, int chaseCount);
}
